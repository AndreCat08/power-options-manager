using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PowerOptionsManager.Models;
using System.Collections.ObjectModel;
using System.Windows;

namespace PowerOptionsManager.ViewModels;

public partial class EditPlanDialogViewModel : ObservableObject
{
    private readonly PowerPlan _originalPlan;
    private readonly Dictionary<string, object> _settings = new();

    [ObservableProperty]
    private ObservableCollection<int> _timeoutOptions = new() { 0, 1, 2, 3, 5, 10, 15, 20, 30, 45, 60 };

    public EditPlanDialogViewModel(PowerPlan plan)
    {
        _originalPlan = plan;
        InitializeSettings();
    }

    private void InitializeSettings()
    {
        DisplayTimeout = 10;
        SleepTimeout = 15;
        MinProcessorStateAc = 5;
        MinProcessorStateDc = 5;
        BrightnessAc = 100;
        BrightnessDc = 50;
    }

    [ObservableProperty]
    private int _displayTimeout;

    [ObservableProperty]
    private int _sleepTimeout;

    [ObservableProperty]
    private bool _hybridSleep;

    [ObservableProperty]
    private bool _hibernateEnabled;

    [ObservableProperty]
    private int _hibernateTimeout;

    [ObservableProperty]
    private int _minProcessorStateAc;

    [ObservableProperty]
    private int _minProcessorStateDc;

    [ObservableProperty]
    private int _brightnessAc;

    [ObservableProperty]
    private int _brightnessDc;

    public Dictionary<string, object> Settings => _settings;

    [RelayCommand]
    private void ResetToDefault()
    {
        InitializeSettings();
    }

    [RelayCommand]
    private void Ok()
    {
        _settings["DisplayTimeout"] = DisplayTimeout;
        _settings["SleepTimeout"] = SleepTimeout;
        _settings["MinProcessorStateAc"] = MinProcessorStateAc;
        _settings["MinProcessorStateDc"] = MinProcessorStateDc;
        _settings["BrightnessAc"] = BrightnessAc;
        _settings["BrightnessDc"] = BrightnessDc;

        (Application.Current.MainWindow as Window)?.Close();
    }
}
