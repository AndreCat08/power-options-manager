using System.Windows;
using System.Windows.Media;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using PowerOptionsManager.ViewModels;
using PowerOptionsManager.Views;

namespace PowerOptionsManager.Services;

/// <summary>Tray icon whose menu mirrors <see cref="MainViewModel.Plans"/> and switches plans through it.</summary>
public sealed class TrayService : IDisposable
{
    private readonly MainWindow _mainWindow;
    private readonly MainViewModel _viewModel;
    private readonly TaskbarIcon _taskbarIcon;
    private readonly TrayMenu _trayMenu;
    private readonly Loc _loc = Loc.Instance;
    private bool _isExiting;
    private bool _disposed;

    public TrayService(MainWindow mainWindow, MainViewModel viewModel)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        _trayMenu = new TrayMenu();
        _trayMenu.OpenWindowRequested += ShowMainWindow;
        _trayMenu.ExitRequested += ExitApplication;
        _trayMenu.PlanSelected += plan => _ = _viewModel.ActivateAsync(plan);
        _trayMenu.Opened += (_, _) => _viewModel.RefreshCommand.Execute(null);

        _taskbarIcon = new TaskbarIcon
        {
            IconSource = new GeneratedIconSource
            {
                Text = "⚡",
                Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
            },
            ContextMenu = _trayMenu,
            MenuActivation = PopupActivationMode.RightClick,
        };
        _taskbarIcon.TrayLeftMouseDoubleClick += (_, _) => ShowMainWindow();
        _taskbarIcon.ForceCreate();

        _viewModel.PlansChanged += (_, _) => UpdateFromViewModel();
        _loc.LanguageChanged += (_, _) => UpdateFromViewModel();

        // Closing the window hides it to the tray; Exit in the tray menu quits.
        _mainWindow.Closing += (_, e) =>
        {
            if (_isExiting) return;
            e.Cancel = true;
            _mainWindow.Hide();
        };

        UpdateFromViewModel();
    }

    public void ShowMainWindow()
    {
        if (_mainWindow.WindowState == WindowState.Minimized) _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Show();
        _mainWindow.Activate();
    }

    public void ExitApplication()
    {
        _isExiting = true;
        _mainWindow.Close();
        Dispose();
        Application.Current.Shutdown();
    }

    private void UpdateFromViewModel()
    {
        _trayMenu.UpdatePlans(_viewModel.Plans);

        var active = _viewModel.Plans.FirstOrDefault(p => p.IsActive);
        _taskbarIcon.ToolTipText = active is null ? _loc["App_Title"] : _loc.Format("Tray_TooltipActive", active.Name);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _taskbarIcon.Dispose();
    }
}
