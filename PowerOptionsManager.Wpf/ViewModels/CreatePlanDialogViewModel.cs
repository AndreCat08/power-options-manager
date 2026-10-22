using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PowerOptionsManager.Models;
using PowerOptionsManager.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace PowerOptionsManager.ViewModels;

public partial class CreatePlanDialogViewModel : ObservableObject
{
    private readonly IPowerService _powerService;

    [ObservableProperty]
    private string _planName = "New Plan";

    [ObservableProperty]
    private string _basePlanGuid = string.Empty;

    [ObservableProperty]
    private ObservableCollection<PowerPlan> _basePlans = new();

    public CreatePlanDialogViewModel(IPowerService powerService)
    {
        _powerService = powerService;
        _ = LoadBasePlansAsync();
    }

    [RelayCommand]
    private async Task LoadBasePlansAsync()
    {
        BasePlans.Clear();
        var plans = await _powerService.GetPlansAsync();
        foreach (var plan in plans)
        {
            BasePlans.Add(plan);
        }
        if (BasePlans.Count > 0)
        {
            BasePlanGuid = BasePlans[0].Guid;
        }
    }

    [RelayCommand]
    private void Ok()
    {
        if (string.IsNullOrWhiteSpace(PlanName))
        {
            MessageBox.Show("Please enter a plan name.");
            return;
        }
        (Application.Current.MainWindow as Window)?.Close();
    }
}
