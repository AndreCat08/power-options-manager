using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using PowerOptionsManager.Services;
using PowerOptionsManager.ViewModels;

namespace PowerOptionsManager.Views;

/// <summary>
/// Small borderless window showing the active power plan with one-click switching.
/// Visibility follows <see cref="WidgetViewModel.IsVisible"/>; dragging moves it and the drop
/// position is remembered.
/// </summary>
public partial class WidgetWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private readonly WidgetViewModel _viewModel;
    private bool _placed;
    private bool _exiting;

    public WidgetWindow(WidgetViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = _viewModel;

        // Off-screen until the first layout, so the widget never flashes at (0,0).
        Left = _viewModel.SavedLeft ?? -10000;
        Top = _viewModel.SavedTop ?? -10000;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        SizeChanged += (_, _) => Place();
    }

    /// <summary>Shows or hides the window to match the view model.</summary>
    public void SyncVisibility()
    {
        if (_viewModel.IsVisible)
        {
            Show();
            Place();
        }
        else
        {
            Hide();
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Tool window: kept out of Alt+Tab and the taskbar.
        var handle = new WindowInteropHelper(this).Handle;
        var style = GetWindowLongPtr(handle, GWL_EXSTYLE).ToInt64();
        style = (style | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;
        SetWindowLongPtr(handle, GWL_EXSTYLE, new IntPtr(style));
    }

    /// <summary>Really closes the window (on app exit); the saved "show widget" setting is left as is.</summary>
    public void CloseForExit()
    {
        _exiting = true;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Alt+F4 hides the widget instead of destroying it.
        if (!_exiting)
        {
            e.Cancel = true;
            _viewModel.IsVisible = false;
        }
        base.OnClosing(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WidgetViewModel.IsVisible)) SyncVisibility();
    }

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;

        DragMove(); // returns when the button is released
        var clamped = Clamp(new Point(Left, Top));
        Left = clamped.X;
        Top = clamped.Y;
        _viewModel.SavePosition(Left, Top);
        e.Handled = true;
    }

    /// <summary>First time: saved or default position. Later (size changes): keep it on screen.</summary>
    private void Place()
    {
        if (!IsVisible || ActualWidth <= 0 || ActualHeight <= 0) return;

        var size = new Size(ActualWidth, ActualHeight);
        var target = _placed
            ? Clamp(new Point(Left, Top))
            : WidgetPlacement.Resolve(_viewModel.SavedLeft, _viewModel.SavedTop, size, SystemParameters.WorkArea, VirtualScreen());
        if (!_placed)
        {
            _placed = true;
            Left = target.X;
            Top = target.Y;
            target = Clamp(target); // now that it is on its monitor, keep it off that monitor's taskbar
        }

        if (Left != target.X) Left = target.X;
        if (Top != target.Y) Top = target.Y;
    }

    /// <summary>Keeps the widget inside the work area (excluding the taskbar) of the monitor it is on.</summary>
    private Point Clamp(Point position) =>
        WidgetPlacement.Clamp(position, new Size(ActualWidth, ActualHeight), CurrentWorkArea() ?? VirtualScreen());

    private Rect? CurrentWorkArea()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero || PresentationSource.FromVisual(this)?.CompositionTarget is not { } target) return null;

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(MonitorFromWindow(handle, MONITOR_DEFAULTTONEAREST), ref info)) return null;

        // Physical pixels -> DIPs at this window's DPI.
        var fromDevice = target.TransformFromDevice;
        var topLeft = fromDevice.Transform(new Point(info.rcWork.Left, info.rcWork.Top));
        var bottomRight = fromDevice.Transform(new Point(info.rcWork.Right, info.rcWork.Bottom));
        return new Rect(topLeft, bottomRight);
    }

    private static Rect VirtualScreen() => new(
        SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
        SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
}
