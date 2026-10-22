using System.Windows;
using Microsoft.Win32;
using PowerOptionsManager.Models;
using PowerOptionsManager.ViewModels;

namespace PowerOptionsManager.Services;

public sealed class WpfDialogService : IDialogService
{
    private readonly Loc _loc = Loc.Instance;

    private static Window? Owner =>
        Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        ?? (Application.Current?.MainWindow is { IsVisible: true } main ? main : null);

    public void ShowError(string message) =>
        Show(message, MessageBoxButton.OK, MessageBoxImage.Error);

    public bool Confirm(string message) =>
        Show(message, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;

    public string? PickImportFile()
    {
        var dialog = new OpenFileDialog { Filter = _loc["File_Filter"], Title = _loc["File_ImportTitle"] };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public string? PickExportFile(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = _loc["File_Filter"],
            Title = _loc["File_ExportTitle"],
            FileName = suggestedFileName,
            DefaultExt = ".pow",
            AddExtension = true,
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public CreatePlanRequest? ShowCreatePlan(IReadOnlyList<PowerPlan> existingPlans)
    {
        var vm = new CreatePlanDialogViewModel(existingPlans);
        var dialog = new CreatePlanDialog(vm) { Owner = Owner };
        return dialog.ShowDialog() == true ? vm.Result : null;
    }

    public PlanSettings? ShowEditPlan(PowerPlan plan, PlanSettings current)
    {
        var vm = new EditPlanDialogViewModel(plan, current);
        var dialog = new EditPlanDialog(vm) { Owner = Owner };
        return dialog.ShowDialog() == true ? vm.Result : null;
    }

    private MessageBoxResult Show(string message, MessageBoxButton buttons, MessageBoxImage image,
        MessageBoxResult defaultResult = MessageBoxResult.None)
    {
        var title = _loc["App_Title"];
        return Owner is { } owner
            ? MessageBox.Show(owner, message, title, buttons, image, defaultResult)
            : MessageBox.Show(message, title, buttons, image, defaultResult);
    }
}
