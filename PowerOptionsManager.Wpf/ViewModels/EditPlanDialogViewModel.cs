using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PowerOptionsManager.Models;
using PowerOptionsManager.Services;

namespace PowerOptionsManager.ViewModels;

public enum SettingKind
{
    /// <summary>Stored in seconds, edited in whole minutes; 0 = never.</summary>
    Minutes,
    Percent,
    Toggle,
}

/// <summary>One editable setting row with a "plugged in" and an "on battery" value.</summary>
public partial class SettingRowViewModel : ObservableObject
{
    public const uint MaxMinutes = uint.MaxValue / 60;

    private readonly Loc _loc = Loc.Instance;
    private AcDc _original;

    public SettingRowViewModel(PowerSetting setting, string labelKey, SettingKind kind, AcDc original)
    {
        Setting = setting;
        LabelKey = labelKey;
        Kind = kind;
        Load(original);
    }

    public PowerSetting Setting { get; }
    public string LabelKey { get; }
    public SettingKind Kind { get; }

    public bool IsToggle => Kind == SettingKind.Toggle;
    public bool IsNumeric => Kind != SettingKind.Toggle;

    public string Label => _loc[LabelKey];
    public string AcAutomationName => $"{Label} – {_loc["Edit_PluggedIn"]}";
    public string DcAutomationName => $"{Label} – {_loc["Edit_OnBattery"]}";

    [ObservableProperty] private string _acText = string.Empty;
    [ObservableProperty] private string _dcText = string.Empty;
    [ObservableProperty] private bool _acOn;
    [ObservableProperty] private bool _dcOn;

    public void Load(AcDc value)
    {
        _original = value;
        AcText = ToText(value.Ac);
        DcText = ToText(value.Dc);
        AcOn = value.Ac != 0;
        DcOn = value.Dc != 0;
    }

    public void Revert() => Load(_original);

    /// <summary>
    /// Parses the edited values. Returns null for <paramref name="value"/> when nothing
    /// changed, so untouched settings (including odd values such as 90 s) are never rewritten.
    /// </summary>
    public bool TryGetChange(out AcDc? value, out string? error)
    {
        value = null;
        error = null;

        uint ac, dc;
        if (Kind == SettingKind.Toggle)
        {
            ac = AcOn ? 1u : 0u;
            dc = DcOn ? 1u : 0u;
        }
        else if (!TryParse(AcText, out ac) || !TryParse(DcText, out dc))
        {
            error = Kind == SettingKind.Minutes
                ? _loc.Format("Val_Minutes", Label, MaxMinutes)
                : _loc.Format("Val_Percent", Label);
            return false;
        }

        // Compare in edit units so an unchanged minute value never rewrites e.g. 90 s as 60 s.
        var acChanged = Kind == SettingKind.Toggle ? ac != Normalize(_original.Ac) : AcText.Trim() != ToText(_original.Ac);
        var dcChanged = Kind == SettingKind.Toggle ? dc != Normalize(_original.Dc) : DcText.Trim() != ToText(_original.Dc);
        if (!acChanged && !dcChanged) return true;

        value = new AcDc(acChanged ? ToStored(ac) : _original.Ac, dcChanged ? ToStored(dc) : _original.Dc);
        return true;
    }

    private static uint Normalize(uint flag) => flag == 0 ? 0u : 1u;

    private string ToText(uint stored) => Kind switch
    {
        SettingKind.Minutes => (stored / 60).ToString(CultureInfo.InvariantCulture),
        _ => stored.ToString(CultureInfo.InvariantCulture),
    };

    private uint ToStored(uint edited) => Kind == SettingKind.Minutes ? edited * 60 : edited;

    private bool TryParse(string text, out uint value)
    {
        if (!uint.TryParse(text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value)) return false;
        return Kind switch
        {
            SettingKind.Minutes => value <= MaxMinutes,
            SettingKind.Percent => value <= 100,
            _ => true,
        };
    }
}

public partial class EditPlanDialogViewModel : ObservableObject
{
    private readonly Loc _loc = Loc.Instance;
    private readonly PlanSettings _current;

    public EditPlanDialogViewModel(PowerPlan plan, PlanSettings current)
    {
        Plan = plan;
        _current = current;

        // Only settings this machine supports get a row.
        Rows = new[]
        {
            Row(PowerSetting.DisplayOff, "Edit_DisplayOff", SettingKind.Minutes),
            Row(PowerSetting.Sleep, "Edit_SleepAfter", SettingKind.Minutes),
            Row(PowerSetting.Hibernate, "Edit_HibernateAfter", SettingKind.Minutes),
            Row(PowerSetting.HybridSleep, "Edit_HybridSleep", SettingKind.Toggle),
            Row(PowerSetting.MinProcessor, "Edit_MinProcessor", SettingKind.Percent),
            Row(PowerSetting.MaxProcessor, "Edit_MaxProcessor", SettingKind.Percent),
            Row(PowerSetting.Brightness, "Edit_Brightness", SettingKind.Percent),
        }.OfType<SettingRowViewModel>().ToList();
    }

    public event Action<bool>? RequestClose;

    public PowerPlan Plan { get; }
    public string Title => _loc.Format("Edit_Title", Plan.Name);
    public IReadOnlyList<SettingRowViewModel> Rows { get; }
    public bool HasRows => Rows.Count > 0;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>The changed settings (unchanged ones are null), set when the dialog closes with Save.</summary>
    public PlanSettings? Result { get; private set; }

    [RelayCommand]
    private void Save()
    {
        var result = new PlanSettings();
        foreach (var row in Rows)
        {
            if (!row.TryGetChange(out var value, out var error))
            {
                ErrorMessage = error!;
                return;
            }
            result = PowerSettingAliases.With(result, row.Setting, value);
        }

        var min = Effective(result.MinProcessorPercent, _current.MinProcessorPercent);
        var max = Effective(result.MaxProcessorPercent, _current.MaxProcessorPercent);
        if (min is { } mn && max is { } mx && (mn.Ac > mx.Ac || mn.Dc > mx.Dc))
        {
            ErrorMessage = _loc["Val_MinAboveMax"];
            return;
        }

        ErrorMessage = string.Empty;
        Result = result;
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Revert()
    {
        foreach (var row in Rows) row.Revert();
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(false);

    private SettingRowViewModel? Row(PowerSetting setting, string labelKey, SettingKind kind) =>
        PowerSettingAliases.Read(_current, setting) is { } value ? new SettingRowViewModel(setting, labelKey, kind, value) : null;

    private static AcDc? Effective(AcDc? changed, AcDc? original) => changed ?? original;
}
