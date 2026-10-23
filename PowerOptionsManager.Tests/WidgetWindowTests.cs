using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using PowerOptionsManager.Services;
using PowerOptionsManager.ViewModels;
using PowerOptionsManager.Views;

namespace PowerOptionsManager.Tests;

public class WidgetWindowTests
{
    private static T FindChild<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) return match;
            if (FindChild<T>(child) is { } nested) return nested;
        }
        return null!;
    }

    [Fact]
    public void PinButton_TogglesTopmostBothWays() => Sta.Run(() =>
    {
        Loc.Instance.SetLanguage("en-US");
        var settings = new FakeSettingsService();
        var main = new MainViewModel(new FakePowerService(), new FakeDialogService(), settings, new FakeThemeService());
        var vm = new WidgetViewModel(main, settings);
        var window = new WidgetWindow(vm);
        window.Show();
        try
        {
            var pin = FindChild<ToggleButton>(window);
            var toggle = (IToggleProvider)UIElementAutomationPeer.CreatePeerForElement(pin).GetPattern(PatternInterface.Toggle);

            toggle.Toggle();
            Assert.False(vm.IsTopmost);
            Assert.False(window.Topmost);

            toggle.Toggle();
            Assert.True(vm.IsTopmost);
            Assert.True(window.Topmost);
        }
        finally
        {
            window.Hide();
        }
    });

    [Fact]
    public void AltF4_HidesWidget_ExitReallyCloses_KeepingSetting() => Sta.Run(() =>
    {
        Loc.Instance.SetLanguage("en-US");
        var settings = new FakeSettingsService();
        settings.Save(new AppSettings { ShowWidget = true });
        var main = new MainViewModel(new FakePowerService(), new FakeDialogService(), settings, new FakeThemeService());
        var vm = new WidgetViewModel(main, settings);
        var window = new WidgetWindow(vm);
        var closed = false;
        window.Closed += (_, _) => closed = true;
        window.SyncVisibility();

        window.Close(); // what Alt+F4 does
        Assert.False(closed);
        Assert.False(window.IsVisible);
        Assert.False(settings.Current.ShowWidget);

        vm.IsVisible = true;
        Assert.True(window.IsVisible);

        window.CloseForExit();
        Assert.True(closed);
        Assert.True(settings.Current.ShowWidget); // shown again on next start
    });
}
