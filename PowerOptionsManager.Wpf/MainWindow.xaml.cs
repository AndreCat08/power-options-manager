using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using PowerOptionsManager.ViewModels;

namespace PowerOptionsManager;

public partial class MainWindow : Window
{
    private const int WM_POWERBROADCAST = 0x0218;
    private const int PBT_POWERSETTINGCHANGE = 0x8013;
    private const int DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;

    // Fires on every active-scheme change. (GUID_POWERSCHEME_PERSONALITY only fires when the
    // plan's base type changes, so switching between two Balanced-based plans was missed.)
    private static readonly Guid GUID_ACTIVE_POWERSCHEME = new("31f9f286-5084-42fe-b720-2b0264993763");

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid PowerSettingGuid, uint Flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterPowerSettingNotification(IntPtr Handle);

    private IntPtr _powerNotifyHandle;

    public MainWindow()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(HwndHook);

        // Notified whenever the active scheme changes, including from Control Panel or powercfg.
        var guid = GUID_ACTIVE_POWERSCHEME;
        _powerNotifyHandle = RegisterPowerSettingNotification(handle, ref guid, DEVICE_NOTIFY_WINDOW_HANDLE);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_powerNotifyHandle != IntPtr.Zero)
        {
            UnregisterPowerSettingNotification(_powerNotifyHandle);
            _powerNotifyHandle = IntPtr.Zero;
        }
        base.OnClosed(e);
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_POWERBROADCAST && (int)wParam == PBT_POWERSETTINGCHANGE)
        {
            ViewModel?.RefreshCommand.Execute(null);
        }
        return IntPtr.Zero;
    }

    private void PlanRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel?.SetActiveCommand is { } command && command.CanExecute(null))
        {
            command.Execute(null);
            e.Handled = true;
        }
    }

    private void PlansGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel is not { } vm) return;

        // Enter activates the selected plan, Delete deletes it (instead of DataGrid's defaults).
        var command = e.Key switch
        {
            Key.Enter => vm.SetActiveCommand,
            Key.Delete => vm.DeleteCommand,
            _ => null,
        };
        if (command is null) return;

        if (command.CanExecute(null)) command.Execute(null);
        e.Handled = true;
    }
}
