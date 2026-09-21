using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using PowerOptionsManager.Services;
using PowerOptionsManager.ViewModels;

namespace PowerOptionsManager;

public partial class MainWindow : Window
{
    private const int WM_POWERBROADCAST = 0x0218;
    private const int PBT_POWERSETTINGCHANGE = 0x8013;
    private const int DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;

    private static readonly Guid GUID_POWERSCHEME_PERSONALITY = new("245d8541-3942-4eb7-ae01-4d7d90680a64");

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid PowerSettingGuid, uint Flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterPowerSettingNotification(IntPtr Handle);

    private IntPtr _powerNotifyHandle;
    private TrayService? _trayService;

    public MainWindow()
    {
        InitializeComponent();
    }

    public void AttachTrayService(TrayService trayService)
    {
        _trayService = trayService;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(handle);
        source?.AddHook(HwndHook);

        var guid = GUID_POWERSCHEME_PERSONALITY;
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
            _trayService?.RefreshMenu();
            if (DataContext is MainViewModel vm)
            {
                vm.RefreshCommand.Execute(null);
            }
        }
        return IntPtr.Zero;
    }
}
