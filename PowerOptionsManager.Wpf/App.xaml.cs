using System.Threading;
using System.Windows;
using PowerOptionsManager.Services;
using PowerOptionsManager.ViewModels;

namespace PowerOptionsManager;

public partial class App : Application
{
    private const string MutexName = @"Local\PowerOptionsManager.SingleInstance";
    private const string ShowEventName = @"Local\PowerOptionsManager.Show";

    private Mutex? _instanceMutex;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _showWait;
    private TrayService? _trayService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceMutex = new Mutex(initiallyOwned: true, MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            // Another instance is running: ask it to show its window, then exit.
            if (EventWaitHandle.TryOpenExisting(ShowEventName, out var existing))
            {
                using (existing) existing.Set();
            }
            _instanceMutex.Dispose();
            _instanceMutex = null;
            Shutdown();
            return;
        }

        var settings = new AppSettingsService();
        var theme = new ThemeService();
        Loc.Instance.SetLanguage(settings.Current.Language);
        theme.Apply(settings.Current.Theme);

        var window = new MainWindow();
        MainWindow = window;
        var viewModel = new MainViewModel(new PowerService(), new WpfDialogService(), settings, theme);
        window.DataContext = viewModel;

        _trayService = new TrayService(window, viewModel);

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _showWait = ThreadPool.RegisterWaitForSingleObject(_showEvent,
            (_, _) => Dispatcher.BeginInvoke(() => _trayService?.ShowMainWindow()),
            null, Timeout.Infinite, executeOnlyOnce: false);

        window.Show();
        _ = viewModel.RefreshAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showWait?.Unregister(null);
        _showEvent?.Dispose();
        _trayService?.Dispose();
        if (_instanceMutex is not null)
        {
            _instanceMutex.ReleaseMutex();
            _instanceMutex.Dispose();
        }
        base.OnExit(e);
    }
}
