using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PowerOptionsManager.Models;
using PowerOptionsManager.Services;

namespace PowerOptionsManager.ViewModels;

/// <summary>
/// State of the floating desktop widget: the active plan and one-click switching, backed by
/// <see cref="MainViewModel"/> so the window, tray and widget always show the same list.
/// </summary>
public partial class WidgetViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private readonly IAppSettingsService _settings;
    private readonly Loc _loc = Loc.Instance;

    public WidgetViewModel(MainViewModel main, IAppSettingsService settings)
    {
        _main = main ?? throw new ArgumentNullException(nameof(main));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        _isVisible = _settings.Current.ShowWidget;
        _isTopmost = _settings.Current.WidgetTopmost;

        _main.PlansChanged += (_, _) => RaiseActivePlanChanged();
        _main.PropertyChanged += OnMainPropertyChanged;
        _loc.LanguageChanged += (_, _) => RaiseActivePlanChanged();
    }

    /// <summary>Raised when the user asks the widget to open the main window.</summary>
    public event EventHandler? OpenMainWindowRequested;

    public ObservableCollection<PowerPlan> Plans => _main.Plans;

    public PowerPlan? ActivePlan => _main.Plans.FirstOrDefault(p => p.IsActive);

    public string ActivePlanName => ActivePlan?.Name ?? _loc["Widget_NoActivePlan"];

    public string ActivePlanA11y => _loc.Format("A11y_WidgetActivePlan", ActivePlanName);

    public bool IsBusy => _main.IsBusy;

    public double? SavedLeft => _settings.Current.WidgetLeft;

    public double? SavedTop => _settings.Current.WidgetTop;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private bool _isTopmost;

    partial void OnIsVisibleChanged(bool value) => _settings.Save(_settings.Current with { ShowWidget = value });

    partial void OnIsTopmostChanged(bool value) => _settings.Save(_settings.Current with { WidgetTopmost = value });

    private bool CanActivatePlan(PowerPlan? plan) => plan is { IsActive: false } && !_main.IsBusy;

    [RelayCommand(CanExecute = nameof(CanActivatePlan))]
    private Task ActivatePlanAsync(PowerPlan? plan) => _main.ActivateAsync(plan!);

    [RelayCommand]
    private void Hide() => IsVisible = false;

    [RelayCommand]
    private void ToggleTopmost() => IsTopmost = !IsTopmost;

    [RelayCommand]
    private void OpenMainWindow() => OpenMainWindowRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Remembers where the user dropped the widget.</summary>
    public void SavePosition(double left, double top)
    {
        if (double.IsNaN(left) || double.IsNaN(top)) return;
        if (SavedLeft == left && SavedTop == top) return;
        _settings.Save(_settings.Current with { WidgetLeft = left, WidgetTop = top });
    }

    private void OnMainPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.IsBusy)) return;
        OnPropertyChanged(nameof(IsBusy));
        ActivatePlanCommand.NotifyCanExecuteChanged();
    }

    private void RaiseActivePlanChanged()
    {
        OnPropertyChanged(nameof(ActivePlan));
        OnPropertyChanged(nameof(ActivePlanName));
        OnPropertyChanged(nameof(ActivePlanA11y));
        ActivatePlanCommand.NotifyCanExecuteChanged();
    }
}
