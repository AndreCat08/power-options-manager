using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using PowerOptionsManager.Models;
using PowerOptionsManager.ViewModels;
using PowerOptionsManager.Views;

namespace PowerOptionsManager.Services;

public class TrayService : IDisposable
{
    private readonly IPowerService _powerService;
    private readonly MainWindow _mainWindow;
    private readonly MainViewModel _mainViewModel;
    private readonly TaskbarIcon _taskbarIcon;
    private readonly TrayMenu _trayMenu;
    private bool _isExiting;
    private bool _disposed;

    public TrayService(IPowerService powerService, MainWindow mainWindow, MainViewModel mainViewModel)
    {
        _powerService = powerService ?? throw new ArgumentNullException(nameof(powerService));
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));

        _trayMenu = new TrayMenu();
        _trayMenu.OpenWindowRequested += ShowMainWindow;
        _trayMenu.ExitRequested += ExitApplication;
        _trayMenu.PlanSelected += async plan => await SwitchPlanAsync(plan);
        _trayMenu.Opened += (s, e) => RefreshMenu();

        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = "Power Options Manager",
            IconSource = new GeneratedIconSource
            {
                Text = "⚡",
                Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212))
            },
            ContextMenu = _trayMenu,
            MenuActivation = PopupActivationMode.RightClick
        };

        _taskbarIcon.TrayLeftMouseDoubleClick += (s, e) => ShowMainWindow();

        _mainWindow.Closing += (s, e) =>
        {
            if (!_isExiting)
            {
                e.Cancel = true;
                _mainWindow.Hide();
            }
        };

        RefreshMenu();
    }

    public void ShowMainWindow()
    {
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }
        _mainWindow.Show();
        _mainWindow.Activate();
    }

    public void RefreshMenu()
    {
        try
        {
            var plans = _powerService.EnumeratePlans();
            _trayMenu.UpdatePlans(plans);

            var active = plans.FirstOrDefault(p => p.IsActive);
            if (active != null)
            {
                _taskbarIcon.ToolTipText = $"Power Options Manager - Active: {active.Name}";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error refreshing tray menu: {ex.Message}");
        }
    }

    public async Task SwitchPlanAsync(PowerPlan plan)
    {
        if (plan == null) return;

        try
        {
            await _powerService.SetActivePlanAsync(plan.Guid);
        }
        catch (Exception)
        {
            // Fallback with UAC elevation if required
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = $"/setactive {plan.Guid}",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true
                };
                var process = Process.Start(psi);
                process?.WaitForExit();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to activate power plan '{plan.Name}': {ex.Message}",
                    "Power Options Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }
        }

        RefreshMenu();
        _mainViewModel.RefreshCommand.Execute(null);
    }

    public void ExitApplication()
    {
        _isExiting = true;
        Dispose();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _taskbarIcon.Dispose();
        GC.SuppressFinalize(this);
    }
}
