using PowerOptionsManager.Models;
using PowerOptionsManager.Services;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace PowerOptionsManager.Tests;

internal static class TestPlans
{
    public const string Balanced = PowerPlan.BalancedGuid;
    public const string HighPerformance = PowerPlan.HighPerformanceGuid;
    public const string Custom = "788abe11-f08c-451e-8375-82eede699673";
    public const string Created = "d3a77d17-95a3-4c68-8984-d2b6a826158a";

    public static List<PowerPlan> Default() =>
    [
        new() { Guid = Balanced, Name = "Balanced" },
        new() { Guid = Custom, Name = "Never Sleep", IsActive = true },
    ];
}

/// <summary>In-memory power service that records calls.</summary>
internal sealed class FakePowerService : IPowerService
{
    public List<PowerPlan> Plans { get; set; } = TestPlans.Default();
    public PlanSettings Settings { get; set; } = new();
    public List<string> Calls { get; } = [];
    public Exception? FailWith { get; set; }
    public int GetPlansCount { get; private set; }

    public Task<IReadOnlyList<PowerPlan>> GetPlansAsync(CancellationToken ct = default)
    {
        GetPlansCount++;
        // Fresh objects on every call, like the real service.
        IReadOnlyList<PowerPlan> copy = Plans.Select(p => new PowerPlan { Guid = p.Guid, Name = p.Name, IsActive = p.IsActive }).ToList();
        return Task.FromResult(copy);
    }

    public Task<PowerPlan?> GetActivePlanAsync(CancellationToken ct = default) =>
        Task.FromResult(Plans.FirstOrDefault(p => p.IsActive));

    public Task SetActivePlanAsync(string planGuid, CancellationToken ct = default) => Record($"setactive {planGuid}", () =>
    {
        foreach (var p in Plans) p.IsActive = p.Guid == planGuid;
    });

    public async Task<PowerPlan> CreatePlanAsync(string name, string basePlanGuid, CancellationToken ct = default)
    {
        await Record($"create {name} {basePlanGuid}", () => Plans.Add(new PowerPlan { Guid = TestPlans.Created, Name = name }));
        return new PowerPlan { Guid = TestPlans.Created, Name = name };
    }

    public async Task<PlanSettings> GetSettingsAsync(string planGuid, CancellationToken ct = default)
    {
        await Record($"getsettings {planGuid}", () => { });
        return Settings;
    }

    public Task UpdateSettingsAsync(string planGuid, PlanSettings settings, CancellationToken ct = default) =>
        Record($"update {planGuid}", () => Settings = settings);

    public Task DeletePlanAsync(string planGuid, CancellationToken ct = default) =>
        Record($"delete {planGuid}", () => Plans.RemoveAll(p => p.Guid == planGuid));

    public async Task<PowerPlan> ImportPlanAsync(string filePath, CancellationToken ct = default)
    {
        await Record($"import {filePath}", () => Plans.Add(new PowerPlan { Guid = TestPlans.Created, Name = "Imported" }));
        return new PowerPlan { Guid = TestPlans.Created, Name = "Imported" };
    }

    public Task ExportPlanAsync(string planGuid, string filePath, CancellationToken ct = default) =>
        Record($"export {planGuid} {filePath}", () => { });

    public Task ResetPlanToDefaultAsync(string planGuid, CancellationToken ct = default) =>
        Record($"reset {planGuid}", () => { });

    private Task Record(string call, Action effect)
    {
        Calls.Add(call);
        if (FailWith is not null) return Task.FromException(FailWith);
        effect();
        return Task.CompletedTask;
    }
}

internal sealed class FakeDialogService : IDialogService
{
    public List<string> Errors { get; } = [];
    public List<string> Confirmations { get; } = [];
    public bool ConfirmAnswer { get; set; } = true;
    public string? ImportPath { get; set; }
    public string? ExportPath { get; set; }
    public string? SuggestedExportName { get; private set; }
    public CreatePlanRequest? CreateResult { get; set; }
    public Func<PlanSettings, PlanSettings?> EditResult { get; set; } = _ => null;
    public PlanSettings? EditShownWith { get; private set; }

    public void ShowError(string message) => Errors.Add(message);

    public bool Confirm(string message)
    {
        Confirmations.Add(message);
        return ConfirmAnswer;
    }

    public string? PickImportFile() => ImportPath;

    public string? PickExportFile(string suggestedFileName)
    {
        SuggestedExportName = suggestedFileName;
        return ExportPath;
    }

    public CreatePlanRequest? ShowCreatePlan(IReadOnlyList<PowerPlan> existingPlans) => CreateResult;

    public PlanSettings? ShowEditPlan(PowerPlan plan, PlanSettings current)
    {
        EditShownWith = current;
        return EditResult(current);
    }
}

internal sealed class FakeSettingsService : IAppSettingsService
{
    public AppSettings Current { get; private set; } = new();
    public int SaveCount { get; private set; }

    public void Save(AppSettings settings)
    {
        Current = settings;
        SaveCount++;
    }
}

internal sealed class FakeThemeService : IThemeService
{
    public List<AppTheme> Applied { get; } = [];
    public void Apply(AppTheme theme) => Applied.Add(theme);
}

internal static class Sta
{
    /// <summary>Runs WPF control code on an STA thread and rethrows assertion failures on the test thread.</summary>
    public static void Run(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
