using Microsoft.Win32;
using PowerOptionsManager.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace PowerOptionsManager.Services;

public class PowerService : IPowerService
{
    private const string PowercfgPath = "powercfg";

    public IReadOnlyList<PowerPlan> EnumeratePlans()
    {
        var plans = new List<PowerPlan>();
        var output = RunPowercfgCommand("/list");

        var lines = output.Split('\n');
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var match = System.Text.RegularExpressions.Regex.Match(line, @"\s*GUID:\s*([a-f0-9-]+)\s*\)\s*(.*)");
            if (match.Success)
            {
                var guid = match.Groups[1].Value.Trim();
                var name = match.Groups[2].Value.Trim().Trim('"');
                
                plans.Add(new PowerPlan
                {
                    Guid = guid,
                    Name = name,
                    IsActive = line.Contains("*") || line.Contains("Aktif")
                });
            }
        }

        return plans;
    }

    public async Task<PowerPlan> CreatePlanAsync(string name, string? basePlanGuid = null)
    {
        var baseArg = string.IsNullOrEmpty(basePlanGuid) ? "/create" : $"/duplicate {basePlanGuid}";
        var args = $"{baseArg} \"{name}\"";
        
        var output = RunPowercfgCommand(args);
        
        var guidMatch = System.Text.RegularExpressions.Regex.Match(output, @"([a-f0-9-]{36})");
        var newGuid = guidMatch.Success ? guidMatch.Value : string.Empty;

        return new PowerPlan
        {
            Guid = newGuid,
            Name = name,
            IsActive = false
        };
    }

    public async Task EditPlanAsync(string planGuid, Dictionary<string, object> settings)
    {
        foreach (var setting in settings)
        {
            var (category, value) = ParseSettingKey(setting.Key);
            var args = $"/setacvalueindex {planGuid} {category} {value} {setting.Value}";
            RunPowercfgCommand(args);
            
            args = $"/setdcvalueindex {planGuid} {category} {value} {setting.Value}";
            RunPowercfgCommand(args);
        }
        
        ApplyChanges(planGuid);
    }

    public async Task DeletePlanAsync(string planGuid)
    {
        RunPowercfgCommand($"/delete {planGuid}");
    }

    public async Task SetActivePlanAsync(string planGuid)
    {
        RunPowercfgCommand($"/setactive {planGuid}");
    }

    public async Task<PowerPlan> GetActivePlanAsync()
    {
        var output = RunPowercfgCommand("/getactive");
        var match = System.Text.RegularExpressions.Regex.Match(output, @"([a-f0-9-]{36})");
        var guid = match.Success ? match.Value : string.Empty;

        var plans = EnumeratePlans();
        return plans.FirstOrDefault(p => p.Guid == guid) ?? new PowerPlan();
    }

    private (string Category, string Value) ParseSettingKey(string key)
    {
        return key switch
        {
            "DisplayTimeout" => ("SUB_DISPLAY", "CONSOLELOCKDISPLAYAC"),
            "SleepTimeout" => ("SUB_SLEEP", "STANDBYIDLEAC"),
            "ProcessorPower" => ("SUB_PROCESSOR", "PROCTHROTTLEIDLEAC"),
            "Brightness" => ("SUB_VIDEO", "DISPBRIGHTNESSAC"),
            _ => ("SUB_PROCESSOR", "PROCTHROTTLEIDLEAC")
        };
    }

    private void ApplyChanges(string planGuid)
    {
        RunPowercfgCommand($"/setacvalueindex {planGuid} SUB_VIDEO DISPBRIGHTNESSAC 100");
        RunPowercfgCommand($"/setdcvalueindex {planGuid} SUB_VIDEO DISPBRIGHTNESSAC 100");
    }

    private string RunPowercfgCommand(string args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = PowercfgPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = Process.Start(startInfo);
        var output = process?.StandardOutput.ReadToEnd() ?? string.Empty;
        var error = process?.StandardError.ReadToEnd() ?? string.Empty;

        if (!string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException($"Powercfg error: {error}");
        }

        return output;
    }
}
