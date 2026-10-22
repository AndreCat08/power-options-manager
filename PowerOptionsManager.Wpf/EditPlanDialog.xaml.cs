using System.Windows;
using PowerOptionsManager.ViewModels;

namespace PowerOptionsManager;

public partial class EditPlanDialog : Window
{
    public EditPlanDialog(EditPlanDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += result => DialogResult = result;
    }
}
