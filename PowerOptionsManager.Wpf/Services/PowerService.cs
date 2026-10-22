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

    public async Task<PowerPlan> ImportPlanAsync(string filePath, string? targetGuid = null)
    {
        var args = string.IsNullOrEmpty(targetGuid)
            ? $"/import \"{filePath}\""
            : $"/import \"{filePath}\" {targetGuid}";
        
        var output = RunPowercfgCommand(args);
        var guidMatch = System.Text.RegularExpressions.Regex.Match(output, @"([a-f0-9-]{36})");
        var importedGuid = guidMatch.Success ? guidMatch.Value : (targetGuid ?? string.Empty);

        var plans = EnumeratePlans();
        return plans.FirstOrDefault(p => string.Equals(p.Guid, importedGuid, StringComparison.OrdinalIgnoreCase))
               ?? new PowerPlan { Guid = importedGuid, Name = System.IO.Path.GetFileNameWithoutExtension(filePath) };
    }

    public async Task ExportPlanAsync(string planGuid, string filePath)
    {
        RunPowercfgCommand($"/export \"{filePath}\" {planGuid}");
    }

    public async Task ResetPlanToDefaultAsync(string planGuid)
    {
        RunPowercfgCommand($"/restoredefaultschemes");
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
            if (IsAccessDenied(error))
            {
                return RunPowercfgCommandElevated(args);
            }

            throw new InvalidOperationException($"Powercfg error: {error}");
        }

        return output;
    }

    private static bool IsAccessDenied(string error) =>
        error.Contains("access is denied", StringComparison.OrdinalIgnoreCase) ||
        error.Contains("akses ditolak", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Retries a powercfg command elevated via UAC (Verb=runas). Used when the
    /// unelevated call fails with access denied (create/edit/delete/set-active
    /// all route through here). No output is captured back from the elevated
    /// process since UseShellExecute is required for the UAC prompt.
    /// </summary>
    private string RunPowercfgCommandElevated(string args)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = PowercfgPath,
                Arguments = args,
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit();

            if (process != null && process.ExitCode != 0)
            {
                throw new InvalidOperationException("Powercfg failed even with elevation.");
            }

            return string.Empty;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            // ERROR_CANCELLED: user declined the UAC prompt
            throw new InvalidOperationException("Administrator permission is required for this action and was not granted.", ex);
        }
    }
}
