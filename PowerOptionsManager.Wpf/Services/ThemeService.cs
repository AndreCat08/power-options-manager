using System.Windows;

namespace PowerOptionsManager.Services;

public interface IThemeService
{
    void Apply(AppTheme theme);
}

/// <summary>
/// Applies the WPF Fluent theme. ThemeMode is still marked experimental (WPF0001)
/// in .NET 10, so the suppression is confined to this class.
/// </summary>
public sealed class ThemeService : IThemeService
{
    public void Apply(AppTheme theme)
    {
#pragma warning disable WPF0001
        Application.Current.ThemeMode = theme switch
        {
            AppTheme.Light => ThemeMode.Light,
            AppTheme.Dark => ThemeMode.Dark,
            _ => ThemeMode.System,
        };
#pragma warning restore WPF0001
    }
}
