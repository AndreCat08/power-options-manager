using System.Windows;
using PowerOptionsManager.ViewModels;

namespace PowerOptionsManager;

public partial class CreatePlanDialog : Window
{
    public CreatePlanDialog(CreatePlanDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += result => DialogResult = result;
    }
}
