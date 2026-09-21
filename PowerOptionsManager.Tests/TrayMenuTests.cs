using PowerOptionsManager.Models;
using PowerOptionsManager.Services;
using PowerOptionsManager.Views;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Xunit;

namespace PowerOptionsManager.Tests;

public class TrayMenuTests
{
    [Fact]
    public void UpdatePlans_PopulatesMenuItemsAndSetActiveSubmenu()
    {
        var thread = new System.Threading.Thread(() =>
        {
            var menu = new TrayMenu();
            var plans = new List<PowerPlan>
            {
                new() { Name = "Balanced", Guid = "381b4222-f694-41f0-9685-ff5bb260df2e", IsActive = true },
                new() { Name = "High Performance", Guid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", IsActive = false }
            };

            menu.UpdatePlans(plans);

            // Verify "Set Active" submenu contains the plans
            Assert.Equal(2, menu.SetActiveItem.Items.Count);

            var subItem1 = menu.SetActiveItem.Items[0] as MenuItem;
            var subItem2 = menu.SetActiveItem.Items[1] as MenuItem;

            Assert.NotNull(subItem1);
            Assert.NotNull(subItem2);
            Assert.Equal("Balanced", subItem1.Header);
            Assert.True(subItem1.IsChecked);
            Assert.Equal("High Performance", subItem2.Header);
            Assert.False(subItem2.IsChecked);

            // Verify top-level items contains the plans
            var planItems = menu.Items.OfType<MenuItem>().Where(i => i.Header?.ToString() == "Balanced" || i.Header?.ToString() == "High Performance").ToList();
            Assert.Equal(2, planItems.Count);
        });

        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void UpdatePlans_FiresPlanSelectedEvent_OnClick()
    {
        var thread = new System.Threading.Thread(() =>
        {
            var menu = new TrayMenu();
            var plan = new PowerPlan { Name = "Balanced", Guid = "test-guid", IsActive = false };
            menu.UpdatePlans(new List<PowerPlan> { plan });

            PowerPlan? selectedPlan = null;
            menu.PlanSelected += p => selectedPlan = p;

            var subItem = menu.SetActiveItem.Items[0] as MenuItem;
            Assert.NotNull(subItem);
            subItem.RaiseEvent(new System.Windows.RoutedEventArgs(MenuItem.ClickEvent));

            Assert.NotNull(selectedPlan);
            Assert.Equal("test-guid", selectedPlan.Guid);
        });

        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
}
