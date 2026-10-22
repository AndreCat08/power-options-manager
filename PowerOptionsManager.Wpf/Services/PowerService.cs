using System.Globalization;
using System.IO;
using PowerOptionsManager.Models;

namespace PowerOptionsManager.Services;

/// <summary>
/// Manages power plans by shelling out to powercfg.exe. Read operations never
/// elevate; write operations retry through a UAC prompt when powercfg reports
/// a missing privilege. Elevated runs return no output, so results that would
/// be parsed from stdout (new plan GUIDs) are recovered by diffing the plan list.
/// </summary>
public sealed class PowerService : IPowerService
{
    private static readonly PowerSetting[] AllSettings = Enum.GetValues<PowerSetting>();

    private readonly IPowercfgRunner _runner;

    public PowerService() : this(new PowercfgRunner()) { }

    public PowerService(IPowercfgRunner runner) => _runner = runner ?? throw new ArgumentNullException(nameof(runner));

    public async Task<IReadOnlyList<PowerPlan>> GetPlansAsync(CancellationToken ct = default) =>
        PowercfgParser.ParseList(await ReadAsync(["/list"], ct).ConfigureAwait(false));

    public async Task<PowerPlan?> GetActivePlanAsync(CancellationToken ct = default)
    {
        var plans = await GetPlansAsync(ct).ConfigureAwait(false);
        return plans.FirstOrDefault(p => p.IsActive);
    }

    public Task SetActivePlanAsync(string planGuid, CancellationToken ct = default) =>
        WriteAsync(["/setactive", RequireGuid(planGuid)], ct);

    public async Task<PowerPlan> CreatePlanAsync(string name, string basePlanGuid, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A plan name is required.", nameof(name));
        RequireGuid(basePlanGuid);

        var newGuid = await WriteAndFindNewPlanAsync(["/duplicatescheme", basePlanGuid], ct).ConfigureAwait(false);
        await WriteAsync(["/changename", newGuid, name.Trim()], ct).ConfigureAwait(false);

        return new PowerPlan { Guid = newGuid, Name = name.Trim() };
    }

    public async Task<PlanSettings> GetSettingsAsync(string planGuid, CancellationToken ct = default)
    {
        RequireGuid(planGuid);
        await RequirePlanAsync(planGuid, ct).ConfigureAwait(false);

        var settings = new PlanSettings();
        foreach (var setting in AllSettings)
        {
            var (subgroup, alias) = PowerSettingAliases.Get(setting);
            var result = await _runner.RunAsync(["/query", planGuid, subgroup, alias], ct).ConfigureAwait(false);
            // A failed query means the setting does not exist on this machine (e.g. no
            // brightness control on a desktop); it stays null and the UI hides it.
            var value = result.Succeeded ? PowercfgParser.ParseQuery(result.StdOut) : null;
            settings = PowerSettingAliases.With(settings, setting, value);
        }
        return settings;
    }

    public async Task UpdateSettingsAsync(string planGuid, PlanSettings settings, CancellationToken ct = default)
    {
        RequireGuid(planGuid);
        ArgumentNullException.ThrowIfNull(settings);

        foreach (var setting in AllSettings)
        {
            if (PowerSettingAliases.Read(settings, setting) is not { } value) continue;

            var (subgroup, alias) = PowerSettingAliases.Get(setting);
            await WriteAsync(["/setacvalueindex", planGuid, subgroup, alias, Format(value.Ac)], ct).ConfigureAwait(false);
            await WriteAsync(["/setdcvalueindex", planGuid, subgroup, alias, Format(value.Dc)], ct).ConfigureAwait(false);
        }

        // Changes to the active scheme only take effect once it is applied again.
        var active = await GetActivePlanAsync(ct).ConfigureAwait(false);
        if (active is not null && string.Equals(active.Guid, planGuid, StringComparison.OrdinalIgnoreCase))
        {
            await WriteAsync(["/setactive", planGuid], ct).ConfigureAwait(false);
        }
    }

    public async Task DeletePlanAsync(string planGuid, CancellationToken ct = default)
    {
        RequireGuid(planGuid);
        var plan = await RequirePlanAsync(planGuid, ct).ConfigureAwait(false);
        if (plan.IsActive) throw new InvalidOperationException("The active power plan cannot be deleted.");

        await WriteAsync(["/delete", planGuid], ct).ConfigureAwait(false);
    }

    public async Task<PowerPlan> ImportPlanAsync(string filePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("A file path is required.", nameof(filePath));

        var newGuid = await WriteAndFindNewPlanAsync(["/import", Path.GetFullPath(filePath)], ct).ConfigureAwait(false);
        var plans = await GetPlansAsync(ct).ConfigureAwait(false);
        return plans.FirstOrDefault(p => p.Guid == newGuid)
               ?? new PowerPlan { Guid = newGuid, Name = Path.GetFileNameWithoutExtension(filePath) };
    }

    public Task ExportPlanAsync(string planGuid, string filePath, CancellationToken ct = default)
    {
        RequireGuid(planGuid);
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("A file path is required.", nameof(filePath));

        return WriteAsync(["/export", Path.GetFullPath(filePath), planGuid], ct);
    }

    public Task ResetPlanToDefaultAsync(string planGuid, CancellationToken ct = default)
    {
        RequireGuid(planGuid);
        if (!PowerPlan.BuiltInSchemes.Contains(planGuid))
        {
            throw new InvalidOperationException("Only built-in Windows power plans can be restored to their defaults.");
        }

        // Never /restoredefaultschemes: it deletes every custom plan.
        return WriteAsync(["/restoreindividualdefaultscheme", planGuid], ct);
    }

    private async Task<PowerPlan> RequirePlanAsync(string planGuid, CancellationToken ct)
    {
        var plans = await GetPlansAsync(ct).ConfigureAwait(false);
        return plans.FirstOrDefault(p => string.Equals(p.Guid, planGuid, StringComparison.OrdinalIgnoreCase))
               ?? throw new PowercfgException(PowercfgErrorKind.Failed, $"Power plan {planGuid} no longer exists.");
    }

    /// <summary>Runs a command that must not need elevation and returns its output.</summary>
    private async Task<string> ReadAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        var result = await _runner.RunAsync(args, ct).ConfigureAwait(false);
        if (!result.Succeeded) throw Failure(result);
        return result.StdOut;
    }

    /// <summary>
    /// Runs a write command, retrying elevated on a privilege error.
    /// Returns stdout, or null when the command ran elevated (no output available).
    /// </summary>
    private async Task<string?> WriteAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        var result = await _runner.RunAsync(args, ct).ConfigureAwait(false);
        if (result.Succeeded) return result.StdOut;
        if (!result.IsAccessDenied) throw Failure(result);

        var exitCode = await _runner.RunElevatedAsync(args, ct).ConfigureAwait(false);
        if (exitCode != 0)
        {
            throw new PowercfgException(PowercfgErrorKind.Failed,
                $"powercfg {args[0]} failed with exit code {exitCode} even with administrator rights.");
        }
        return null;
    }

    /// <summary>Runs a command that creates a plan and returns the new plan's GUID.</summary>
    private async Task<string> WriteAndFindNewPlanAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        var before = (await GetPlansAsync(ct).ConfigureAwait(false)).Select(p => p.Guid).ToHashSet();

        var output = await WriteAsync(args, ct).ConfigureAwait(false);
        var newGuid = output is null ? null : PowercfgParser.ParseLastGuid(output);
        if (newGuid is not null && !before.Contains(newGuid)) return newGuid;

        // Elevated run (no output) or unexpected output: find the plan that appeared.
        var after = await GetPlansAsync(ct).ConfigureAwait(false);
        return after.Select(p => p.Guid).FirstOrDefault(g => !before.Contains(g))
               ?? throw new PowercfgException(PowercfgErrorKind.Failed, $"powercfg {args[0]} did not create a new power plan.");
    }

    private static PowercfgException Failure(PowercfgResult result) =>
        new(PowercfgErrorKind.Failed, string.IsNullOrEmpty(result.ErrorText)
            ? $"powercfg failed with exit code {result.ExitCode}."
            : result.ErrorText);

    private static string RequireGuid(string guid) =>
        System.Guid.TryParse(guid, out _) ? guid : throw new ArgumentException($"'{guid}' is not a valid plan GUID.", nameof(guid));

    private static string Format(uint value) => value.ToString(CultureInfo.InvariantCulture);
}
