using PowerOptionsManager.Models;
using PowerOptionsManager.Services;
using PowerOptionsManager.ViewModels;
using System.Windows;

namespace PowerOptionsManager;

public partial class CreatePlanDialog : Window
{
    public string PlanName => ((CreatePlanDialogViewModel)DataContext).PlanName;
    public string BasePlanGuid => ((CreatePlanDialogViewModel)DataContext).BasePlanGuid;

    public CreatePlanDialog()
    {
        InitializeComponent();
        var vm = new CreatePlanDialogViewModel(new PowerService());
        DataContext = vm;
    }
}
