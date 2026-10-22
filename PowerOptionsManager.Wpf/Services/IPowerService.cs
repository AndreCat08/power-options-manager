using PowerOptionsManager.Models;

namespace PowerOptionsManager.Services;

public interface IPowerService
{
    Task<IReadOnlyList<PowerPlan>> GetPlansAsync(CancellationToken ct = default);
    Task<PowerPlan?> GetActivePlanAsync(CancellationToken ct = default);
    Task SetActivePlanAsync(string planGuid, CancellationToken ct = default);

    /// <summary>Duplicates <paramref name="basePlanGuid"/> and names the copy <paramref name="name"/>.</summary>
    Task<PowerPlan> CreatePlanAsync(string name, string basePlanGuid, CancellationToken ct = default);

    Task<PlanSettings> GetSettingsAsync(string planGuid, CancellationToken ct = default);

    /// <summary>Writes every non-null setting; re-applies the plan when it is the active one.</summary>
    Task UpdateSettingsAsync(string planGuid, PlanSettings settings, CancellationToken ct = default);

    /// <summary>Deletes a plan. Throws <see cref="InvalidOperationException"/> for the active plan.</summary>
    Task DeletePlanAsync(string planGuid, CancellationToken ct = default);

    Task<PowerPlan> ImportPlanAsync(string filePath, CancellationToken ct = default);
    Task ExportPlanAsync(string planGuid, string filePath, CancellationToken ct = default);

    /// <summary>
    /// Restores a built-in plan to its Windows defaults. Throws
    /// <see cref="InvalidOperationException"/> for custom plans, which have no default.
    /// </summary>
    Task ResetPlanToDefaultAsync(string planGuid, CancellationToken ct = default);
}
