using System.Windows;
using System.Windows.Controls;
using PowerOptionsManager.Models;

namespace PowerOptionsManager.Views;

public partial class TrayMenu : ContextMenu
{
    private readonly List<MenuItem> _planItems = [];

    public event Action? OpenWindowRequested;
    public event Action? ExitRequested;
    public event Action<PowerPlan>? PlanSelected;

    public TrayMenu()
    {
        InitializeComponent();

        OpenWindowMenuItem.Click += (_, _) => OpenWindowRequested?.Invoke();
        ExitMenuItem.Click += (_, _) => ExitRequested?.Invoke();
    }

    public IReadOnlyList<MenuItem> PlanItems => _planItems;

    /// <summary>Lists each plan once, with a check mark on the active plan.</summary>
    public void UpdatePlans(IEnumerable<PowerPlan> plans)
    {
        foreach (var item in _planItems) Items.Remove(item);
        _planItems.Clear();

        var insertIndex = Items.IndexOf(TopSeparator) + 1;
        foreach (var plan in plans)
        {
            // Not checkable: the check reflects Windows' state and moves only after a successful switch.
            var item = new MenuItem { Header = plan.Name, IsCheckable = false, IsChecked = plan.IsActive };
            var captured = plan;
            item.Click += (_, _) => PlanSelected?.Invoke(captured);

            Items.Insert(insertIndex++, item);
            _planItems.Add(item);
        }

        BottomSeparator.Visibility = _planItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
