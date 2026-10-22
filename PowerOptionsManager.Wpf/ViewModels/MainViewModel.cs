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
        _ = RefreshAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        var plans = await _powerService.GetPlansAsync();
        Plans.Clear();
        foreach (var plan in plans)
        {
            Plans.Add(plan);
        }
    }

    public void RefreshPlans() => _ = RefreshAsync();

    [RelayCommand]
    private async Task SetActiveAsync()
    {
        if (SelectedPlan is null) return;
        try
        {
            await _powerService.SetActivePlanAsync(SelectedPlan.Guid);
            RefreshPlans();
            MessageBox.Show($"Power plan '{SelectedPlan.Name}' activated successfully.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to activate power plan: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        var dialog = new CreatePlanDialog();
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var newPlan = await _powerService.CreatePlanAsync(dialog.PlanName, dialog.BasePlanGuid);
                RefreshPlans();
                MessageBox.Show($"Power plan '{newPlan.Name}' created successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create power plan: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        if (SelectedPlan is null) return;
        var dialog = new EditPlanDialog(SelectedPlan);
        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _powerService.UpdateSettingsAsync(SelectedPlan.Guid, dialog.ViewModel.ToSettings());
                RefreshPlans();
                MessageBox.Show($"Power plan '{SelectedPlan.Name}' updated successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update power plan: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            try
            {
                await _powerService.DeletePlanAsync(SelectedPlan.Guid);
                RefreshPlans();
                MessageBox.Show($"Power plan '{SelectedPlan.Name}' deleted successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete power plan: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Power Plan (*.pow)|*.pow|All Files (*.*)|*.*",
            Title = "Import Power Plan"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var imported = await _powerService.ImportPlanAsync(dialog.FileName);
                RefreshPlans();
                MessageBox.Show($"Power plan '{imported.Name}' imported successfully.", "Import Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import power plan: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (SelectedPlan is null) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Power Plan (*.pow)|*.pow",
            Title = "Export Power Plan",
            FileName = $"{SelectedPlan.Name}.pow"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _powerService.ExportPlanAsync(SelectedPlan.Guid, dialog.FileName);
                MessageBox.Show($"Power plan '{SelectedPlan.Name}' exported successfully.", "Export Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export power plan: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private async Task ResetPlanAsync()
    {
        if (SelectedPlan is null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to restore power plans to default settings?",
            "Confirm Reset to Default",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning
        );

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _powerService.ResetPlanToDefaultAsync(SelectedPlan.Guid);
                RefreshPlans();
                MessageBox.Show("Power plan settings restored to default.", "Reset Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to reset power plans: {ex.Message}", "Reset Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public bool HasSelectedPlan => SelectedPlan is not null;
}
