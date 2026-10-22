using System.Windows;
using System.Windows.Controls;
using PowerOptionsManager.Models;
using PowerOptionsManager.Views;

namespace PowerOptionsManager.Tests;

public class TrayMenuTests
{
    private static List<PowerPlan> Plans() =>
    [
        new() { Name = "Balanced", Guid = TestPlans.Balanced, IsActive = true },
        new() { Name = "High performance", Guid = TestPlans.HighPerformance },
    ];

    [Fact]
    public void UpdatePlans_ListsEachPlanOnce_WithActiveChecked() => Sta.Run(() =>
    {
        var menu = new TrayMenu();

        menu.UpdatePlans(Plans());

        Assert.Equal(2, menu.PlanItems.Count);
        Assert.Equal("Balanced", menu.PlanItems[0].Header);
        Assert.True(menu.PlanItems[0].IsChecked);
        Assert.False(menu.PlanItems[1].IsChecked);
        // Each plan appears exactly once anywhere in the menu (no duplicate submenu).
        Assert.Single(menu.Items.OfType<MenuItem>(), i => Equals(i.Header, "Balanced"));
        Assert.All(menu.Items.OfType<MenuItem>(), i => Assert.Empty(i.Items));
    });

    [Fact]
    public void UpdatePlans_CalledAgain_ReplacesItems() => Sta.Run(() =>
    {
        var menu = new TrayMenu();
        menu.UpdatePlans(Plans());

        menu.UpdatePlans([new PowerPlan { Name = "Only", Guid = TestPlans.Custom, IsActive = true }]);

        Assert.Equal("Only", Assert.Single(menu.PlanItems).Header);
        Assert.Single(menu.Items.OfType<MenuItem>(), i => Equals(i.Header, "Only"));
    });

    [Fact]
    public void ClickingPlan_RaisesPlanSelected_WithoutTogglingCheck() => Sta.Run(() =>
    {
        var menu = new TrayMenu();
        menu.UpdatePlans(Plans());
        PowerPlan? selected = null;
        menu.PlanSelected += p => selected = p;

        var item = menu.PlanItems[1];
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        Assert.Equal(TestPlans.HighPerformance, selected?.Guid);
        Assert.False(item.IsCheckable);
        Assert.False(item.IsChecked); // moves only after the switch succeeds and the list reloads
    });

    [Fact]
    public void NoPlans_HidesExtraSeparator() => Sta.Run(() =>
    {
        var menu = new TrayMenu();

        menu.UpdatePlans([]);

        Assert.Empty(menu.PlanItems);
        Assert.Single(menu.Items.OfType<Separator>(), s => s.Visibility == Visibility.Visible);
    });
}
