using System.IO;
using System.Text.Json;

namespace PowerOptionsManager.Services;

public enum AppTheme
{
    System,
    Light,
    Dark,
}

public sealed record AppSettings
{
    /// <summary>"" follows Windows; otherwise "en-US" or "id-ID".</summary>
    public string Language { get; init; } = string.Empty;

    public AppTheme Theme { get; init; } = AppTheme.System;
}

public interface IAppSettingsService
{
    AppSettings Current { get; }
    void Save(AppSettings settings);
}

/// <summary>Persists <see cref="AppSettings"/> as JSON under %LOCALAPPDATA%\PowerOptionsManager.</summary>
public sealed class AppSettingsService : IAppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private readonly string _path;

    public AppSettingsService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PowerOptionsManager", "settings.json")) { }

    public AppSettingsService(string path)
    {
        _path = path;
        Current = Load();
    }

    public AppSettings Current { get; private set; }

    public void Save(AppSettings settings)
    {
        Current = settings;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        // Preferences are a convenience; failing to persist them must not break the app.
    }

    private AppSettings Load()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), JsonOptions) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }
}
