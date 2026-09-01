using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PowerOptionsManager.Models;
using PowerOptionsManager.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace PowerOptionsManager.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IPowerService _powerService;

    [ObservableProperty]
    private ObservableCollection<PowerPlan> _plans = new();

    [ObservableProperty]
    private PowerPlan? _selectedPlan;

    public MainViewModel(IPowerService powerService)
    {
        _powerService = powerService;
        RefreshPlans();
    }

    [RelayCommand]
    private void RefreshPlans()
    {
        Plans.Clear();
        var plans = _powerService.EnumeratePlans();
        foreach (var plan in plans)
        {
            Plans.Add(plan);
        }
    }

    [RelayCommand]
    private async Task SetActiveAsync()
    {
        if (SelectedPlan is null) return;
        await _powerService.SetActivePlanAsync(SelectedPlan.Guid);
        RefreshPlans();
        MessageBox.Show($"Power plan '{SelectedPlan.Name}' activated successfully.");
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        var dialog = new CreatePlanDialog();
        if (dialog.ShowDialog() == true)
        {
            var newPlan = await _powerService.CreatePlanAsync(dialog.PlanName, dialog.BasePlanGuid);
            RefreshPlans();
            MessageBox.Show($"Power plan '{newPlan.Name}' created successfully.");
        }
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        if (SelectedPlan is null) return;
        var dialog = new EditPlanDialog(SelectedPlan);
        if (dialog.ShowDialog() == true)
        {
            await _powerService.EditPlanAsync(SelectedPlan.Guid, dialog.ViewModel.Settings);
            RefreshPlans();
            MessageBox.Show($"Power plan '{SelectedPlan.Name}' updated successfully.");
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedPlan is null) return;
        var result = MessageBox.Show(
            $"Are you sure you want to delete '{SelectedPlan.Name}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning
        );
        if (result == MessageBoxResult.Yes)
        {
            await _powerService.DeletePlanAsync(SelectedPlan.Guid);
            RefreshPlans();
            MessageBox.Show($"Power plan '{SelectedPlan.Name}' deleted successfully.");
        }
    }

    public bool HasSelectedPlan => SelectedPlan is not null;
}
