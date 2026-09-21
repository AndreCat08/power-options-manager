using System.Windows;
using System.Windows.Controls;
using PowerOptionsManager.Models;

namespace PowerOptionsManager.Views;

public partial class TrayMenu : ContextMenu
{
    public MenuItem SetActiveItem => SetActiveMenuItem;
    public MenuItem OpenWindowItem => OpenWindowMenuItem;
    public MenuItem ExitItem => ExitMenuItem;

    private readonly List<Control> _dynamicPlanItems = new();

    public event Action? OpenWindowRequested;
    public event Action? ExitRequested;
    public event Action<PowerPlan>? PlanSelected;

    public TrayMenu()
    {
        InitializeComponent();

        OpenWindowMenuItem.Click += (s, e) => OpenWindowRequested?.Invoke();
        ExitMenuItem.Click += (s, e) => ExitRequested?.Invoke();
    }

    public void UpdatePlans(IReadOnlyList<PowerPlan> plans)
    {
        // Remove previous dynamic plan items from top-level menu
        foreach (var item in _dynamicPlanItems)
        {
            Items.Remove(item);
        }
        _dynamicPlanItems.Clear();

        SetActiveMenuItem.Items.Clear();

        int insertIndex = Items.IndexOf(TopSeparator) + 1;

        foreach (var plan in plans)
        {
            // Top-level plan menu item
            var planItem = new MenuItem
            {
                Header = plan.Name,
                IsCheckable = true,
                IsChecked = plan.IsActive
            };
            var capturedPlan = plan;
            planItem.Click += (s, e) => PlanSelected?.Invoke(capturedPlan);

            Items.Insert(insertIndex++, planItem);
            _dynamicPlanItems.Add(planItem);

            // Item in "Set Active" submenu
            var subMenuItem = new MenuItem
            {
                Header = plan.Name,
                IsCheckable = true,
                IsChecked = plan.IsActive
            };
            subMenuItem.Click += (s, e) => PlanSelected?.Invoke(capturedPlan);
            SetActiveMenuItem.Items.Add(subMenuItem);
        }
    }
}
