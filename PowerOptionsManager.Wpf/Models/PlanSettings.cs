namespace PowerOptionsManager.Models;

/// <summary>A setting value for "plugged in" (AC) and "on battery" (DC).</summary>
public readonly record struct AcDc(uint Ac, uint Dc);

/// <summary>
/// The power settings the editor exposes. A null value means the setting is
/// not supported on this machine (powercfg reported it does not exist).
/// Timeouts are in seconds (0 = never), the rest are percentages or 0/1 flags.
/// </summary>
public sealed record PlanSettings
{
    public AcDc? DisplayOffSeconds { get; init; }
    public AcDc? SleepSeconds { get; init; }
    public AcDc? HibernateSeconds { get; init; }
    public AcDc? HybridSleep { get; init; }
    public AcDc? MinProcessorPercent { get; init; }
    public AcDc? MaxProcessorPercent { get; init; }
    public AcDc? BrightnessPercent { get; init; }
}

/// <summary>powercfg subgroup/setting aliases for each <see cref="PlanSettings"/> member.</summary>
public enum PowerSetting
{
    DisplayOff,
    Sleep,
    Hibernate,
    HybridSleep,
    MinProcessor,
    MaxProcessor,
    Brightness,
}

public static class PowerSettingAliases
{
    public static (string Subgroup, string Setting) Get(PowerSetting setting) => setting switch
    {
        PowerSetting.DisplayOff => ("SUB_VIDEO", "VIDEOIDLE"),
        PowerSetting.Sleep => ("SUB_SLEEP", "STANDBYIDLE"),
        PowerSetting.Hibernate => ("SUB_SLEEP", "HIBERNATEIDLE"),
        PowerSetting.HybridSleep => ("SUB_SLEEP", "HYBRIDSLEEP"),
        PowerSetting.MinProcessor => ("SUB_PROCESSOR", "PROCTHROTTLEMIN"),
        PowerSetting.MaxProcessor => ("SUB_PROCESSOR", "PROCTHROTTLEMAX"),
        PowerSetting.Brightness => ("SUB_VIDEO", "VIDEONORMALLEVEL"),
        _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, null),
    };

    public static AcDc? Read(PlanSettings s, PowerSetting setting) => setting switch
    {
        PowerSetting.DisplayOff => s.DisplayOffSeconds,
        PowerSetting.Sleep => s.SleepSeconds,
        PowerSetting.Hibernate => s.HibernateSeconds,
        PowerSetting.HybridSleep => s.HybridSleep,
        PowerSetting.MinProcessor => s.MinProcessorPercent,
        PowerSetting.MaxProcessor => s.MaxProcessorPercent,
        PowerSetting.Brightness => s.BrightnessPercent,
        _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, null),
    };

    public static PlanSettings With(PlanSettings s, PowerSetting setting, AcDc? value) => setting switch
    {
        PowerSetting.DisplayOff => s with { DisplayOffSeconds = value },
        PowerSetting.Sleep => s with { SleepSeconds = value },
        PowerSetting.Hibernate => s with { HibernateSeconds = value },
        PowerSetting.HybridSleep => s with { HybridSleep = value },
        PowerSetting.MinProcessor => s with { MinProcessorPercent = value },
        PowerSetting.MaxProcessor => s with { MaxProcessorPercent = value },
        PowerSetting.Brightness => s with { BrightnessPercent = value },
        _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, null),
    };
}
