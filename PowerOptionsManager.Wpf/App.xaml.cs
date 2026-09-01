using PowerOptionsManager.Models;
using PowerOptionsManager.Services;
using PowerOptionsManager.ViewModels;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace PowerOptionsManager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        var window = new MainWindow();
        var service = new PowerService();
        var viewModel = new MainViewModel(service);
        window.DataContext = viewModel;
        window.Show();
    }
}
