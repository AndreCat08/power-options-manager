using PowerOptionsManager.Models;
using System.Collections.Generic;

namespace PowerOptionsManager.Services;

public interface IPowerService
{
    IReadOnlyList<PowerPlan> EnumeratePlans();
    Task<PowerPlan> CreatePlanAsync(string name, string? basePlanGuid = null);
    Task EditPlanAsync(string planGuid, Dictionary<string, object> settings);
    Task DeletePlanAsync(string planGuid);
    Task SetActivePlanAsync(string planGuid);
    Task<PowerPlan> GetActivePlanAsync();
    Task<PowerPlan> ImportPlanAsync(string filePath, string? targetGuid = null);
    Task ExportPlanAsync(string planGuid, string filePath);
    Task ResetPlanToDefaultAsync(string planGuid);
}
