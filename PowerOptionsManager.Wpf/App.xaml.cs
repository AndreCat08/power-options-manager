using System.Windows;
using PowerOptionsManager.Services;
using PowerOptionsManager.ViewModels;

namespace PowerOptionsManager;

public partial class App : Application
{
    private TrayService? _trayService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var window = new MainWindow();
        var service = new PowerService();
        var viewModel = new MainViewModel(service);
        window.DataContext = viewModel;

        _trayService = new TrayService(service, window, viewModel);
        window.AttachTrayService(_trayService);

        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayService?.Dispose();
        base.OnExit(e);
    }
}
