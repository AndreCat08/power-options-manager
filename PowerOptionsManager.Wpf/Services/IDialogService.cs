using PowerOptionsManager.Models;

namespace PowerOptionsManager.Services;

public sealed record CreatePlanRequest(string Name, string BasePlanGuid);

/// <summary>Everything the view models need from windows and message boxes, so they stay testable.</summary>
public interface IDialogService
{
    void ShowError(string message);
    bool Confirm(string message);
    string? PickImportFile();
    string? PickExportFile(string suggestedFileName);
    CreatePlanRequest? ShowCreatePlan(IReadOnlyList<PowerPlan> existingPlans);
    PlanSettings? ShowEditPlan(PowerPlan plan, PlanSettings current);
}
