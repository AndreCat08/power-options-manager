using System.Windows;
using PowerOptionsManager.Models;
using PowerOptionsManager.Services;
using PowerOptionsManager.ViewModels;

namespace PowerOptionsManager.Tests;

public class WidgetViewModelTests
{
    private readonly FakePowerService _power = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeSettingsService _settings = new();

    public WidgetViewModelTests() => Loc.Instance.SetLanguage("en-US");

    private async Task<(MainViewModel Main, WidgetViewModel Widget)> CreateAsync()
    {
        var main = new MainViewModel(_power, _dialogs, _settings, new FakeThemeService());
        var widget = new WidgetViewModel(main, _settings);
        await main.RefreshAsync();
        return (main, widget);
    }

    [Fact]
    public async Task ShowsActivePlanName_AndUpdatesAfterRefresh()
    {
        var (main, widget) = await CreateAsync();
        Assert.Equal("Never Sleep", widget.ActivePlanName);

        var raised = new List<string?>();
        widget.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        foreach (var p in _power.Plans) p.IsActive = p.Guid == TestPlans.Balanced;
        await main.RefreshAsync();

        Assert.Equal("Balanced", widget.ActivePlanName);
        Assert.Contains(nameof(WidgetViewModel.ActivePlanName), raised);
    }

    [Fact]
    public async Task NoActivePlan_ShowsPlaceholder()
    {
        foreach (var p in _power.Plans) p.IsActive = false;
        var (_, widget) = await CreateAsync();

        Assert.Equal("No active plan", widget.ActivePlanName);
        Assert.Null(widget.ActivePlan);
    }

    [Fact]
    public async Task ActivatePlan_SwitchesThroughPowerService_AndRefreshes()
    {
        var (_, widget) = await CreateAsync();
        var balanced = widget.Plans.Single(p => p.Guid == TestPlans.Balanced);

        Assert.True(widget.ActivatePlanCommand.CanExecute(balanced));
        await widget.ActivatePlanCommand.ExecuteAsync(balanced);

        Assert.Contains($"setactive {TestPlans.Balanced}", _power.Calls);
        Assert.Equal("Balanced", widget.ActivePlanName);
    }

    [Fact]
    public async Task ActivatePlan_DisabledForActivePlanAndNull()
    {
        var (_, widget) = await CreateAsync();

        Assert.False(widget.ActivatePlanCommand.CanExecute(widget.ActivePlan));
        Assert.False(widget.ActivatePlanCommand.CanExecute(null));
    }

    [Fact]
    public async Task VisibilityAndTopmost_ArePersisted()
    {
        var (_, widget) = await CreateAsync();
        Assert.False(widget.IsVisible);
        Assert.True(widget.IsTopmost);

        widget.IsVisible = true;
        widget.ToggleTopmostCommand.Execute(null);

        Assert.True(_settings.Current.ShowWidget);
        Assert.False(_settings.Current.WidgetTopmost);

        widget.HideCommand.Execute(null);
        Assert.False(_settings.Current.ShowWidget);
    }

    [Fact]
    public async Task StartsFromSavedSettings_AndSavesPosition()
    {
        _settings.Save(new AppSettings { ShowWidget = true, WidgetTopmost = false, WidgetLeft = 10, WidgetTop = 20 });
        var (_, widget) = await CreateAsync();

        Assert.True(widget.IsVisible);
        Assert.False(widget.IsTopmost);
        Assert.Equal(10, widget.SavedLeft);

        var saves = _settings.SaveCount;
        widget.SavePosition(10, 20); // unchanged: no write
        Assert.Equal(saves, _settings.SaveCount);

        widget.SavePosition(300, 400);
        Assert.Equal(300, _settings.Current.WidgetLeft);
        Assert.Equal(400, _settings.Current.WidgetTop);
        Assert.True(_settings.Current.ShowWidget); // other settings kept
    }

    [Fact]
    public async Task OpenMainWindow_RaisesEvent()
    {
        var (_, widget) = await CreateAsync();
        var raised = false;
        widget.OpenMainWindowRequested += (_, _) => raised = true;

        widget.OpenMainWindowCommand.Execute(null);

        Assert.True(raised);
    }
}

public class WidgetPlacementTests
{
    private static readonly Rect Work = new(0, 0, 1920, 1032);
    private static readonly Rect Virtual = new(0, 0, 1920, 1080);
    private static readonly Size Widget = new(250, 200);

    [Fact]
    public void NoSavedPosition_UsesBottomRightOfWorkArea()
    {
        var p = WidgetPlacement.Resolve(null, null, Widget, Work, Virtual);

        Assert.Equal(1920 - 250 - WidgetPlacement.EdgeMargin, p.X);
        Assert.Equal(1032 - 200 - WidgetPlacement.EdgeMargin, p.Y);
    }

    [Fact]
    public void SavedPositionOnScreen_IsKept()
    {
        var p = WidgetPlacement.Resolve(100, 50, Widget, Work, Virtual);

        Assert.Equal(new Point(100, 50), p);
    }

    [Fact]
    public void SavedPositionPartlyOffScreen_IsClampedInside()
    {
        var p = WidgetPlacement.Resolve(1800, 1000, Widget, Work, Virtual);

        Assert.Equal(new Point(1920 - 250, 1080 - 200), p);
    }

    [Fact]
    public void SavedPositionOnUnpluggedMonitor_FallsBackToDefault()
    {
        // e.g. saved on a second monitor at x=2500 that is no longer connected
        var p = WidgetPlacement.Resolve(2500, 100, Widget, Work, Virtual);

        Assert.Equal(WidgetPlacement.DefaultPosition(Widget, Work), p);
    }

    [Fact]
    public void Clamp_SecondaryMonitorLeftOfPrimary_AllowsNegativeCoordinates()
    {
        var virtualScreen = new Rect(-1280, 0, 3200, 1080);

        var p = WidgetPlacement.Resolve(-1000, 100, Widget, Work, virtualScreen);

        Assert.Equal(new Point(-1000, 100), p);
    }
}

public class TrayMenuWidgetTests
{
    [Fact]
    public void WidgetItem_RaisesToggle_AndMirrorsVisibility() => Sta.Run(() =>
    {
        var menu = new Views.TrayMenu();
        var toggles = 0;
        menu.WidgetToggleRequested += () => toggles++;

        menu.WidgetItem.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));
        menu.SetWidgetVisible(true);

        Assert.Equal(1, toggles);
        Assert.True(menu.WidgetItem.IsChecked);
        Assert.False(menu.WidgetItem.IsCheckable); // state comes from the view model, not the click
    });
}
