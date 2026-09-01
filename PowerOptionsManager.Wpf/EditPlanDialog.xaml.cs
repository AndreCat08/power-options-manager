using PowerOptionsManager.Models;
using PowerOptionsManager.ViewModels;
using System.Windows;

namespace PowerOptionsManager;

public partial class EditPlanDialog : Window
{
    public EditPlanDialogViewModel ViewModel => (EditPlanDialogViewModel)DataContext;

    public EditPlanDialog(PowerPlan plan)
    {
        InitializeComponent();
        var vm = new EditPlanDialogViewModel(plan);
        DataContext = vm;
    }
}
