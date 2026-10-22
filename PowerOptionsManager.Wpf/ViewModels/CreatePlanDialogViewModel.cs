using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PowerOptionsManager.Models;
using PowerOptionsManager.Services;

namespace PowerOptionsManager.ViewModels;

public partial class CreatePlanDialogViewModel : ObservableObject
{
    public const int MaxNameLength = 100;

    private readonly Loc _loc = Loc.Instance;

    public CreatePlanDialogViewModel(IReadOnlyList<PowerPlan> existingPlans)
    {
        BasePlans = new ObservableCollection<PowerPlan>(existingPlans);

        // Default base: High performance when installed, otherwise the active plan.
        SelectedBasePlan = BasePlans.FirstOrDefault(p => p.Guid == PowerPlan.HighPerformanceGuid)
                           ?? BasePlans.FirstOrDefault(p => p.IsActive)
                           ?? BasePlans.FirstOrDefault();
        _planName = _loc["Create_DefaultName"];
    }

    /// <summary>Raised with true when the dialog should close and save, false to cancel.</summary>
    public event Action<bool>? RequestClose;

    public ObservableCollection<PowerPlan> BasePlans { get; }

    [ObservableProperty]
    private string _planName;

    [ObservableProperty]
    private PowerPlan? _selectedBasePlan;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public CreatePlanRequest? Result { get; private set; }

    partial void OnPlanNameChanged(string value) => ErrorMessage = string.Empty;

    [RelayCommand]
    private void Ok()
    {
        var name = PlanName?.Trim() ?? string.Empty;
        ErrorMessage = Validate(name) ?? string.Empty;
        if (ErrorMessage.Length > 0) return;

        Result = new CreatePlanRequest(name, SelectedBasePlan!.Guid);
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(false);

    private string? Validate(string name)
    {
        if (name.Length == 0) return _loc["Val_NameRequired"];
        if (name.Length > MaxNameLength) return _loc.Format("Val_NameTooLong", MaxNameLength);
        if (BasePlans.Any(p => string.Equals(p.Name, name, StringComparison.CurrentCultureIgnoreCase))) return _loc["Val_NameDuplicate"];
        if (SelectedBasePlan is null) return _loc["Val_BaseRequired"];
        return null;
    }
}
