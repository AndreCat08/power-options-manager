using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PowerOptionsManager.Models;
using PowerOptionsManager.Services;

namespace PowerOptionsManager.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IPowerService _powerService;
    private readonly IDialogService _dialogs;
    private readonly IAppSettingsService _settings;
    private readonly IThemeService _theme;
    private readonly Loc _loc = Loc.Instance;
    private bool _applyingPreferences;

    public MainViewModel(IPowerService powerService, IDialogService dialogs, IAppSettingsService settings, IThemeService theme)
    {
        _powerService = powerService ?? throw new ArgumentNullException(nameof(powerService));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));

        _applyingPreferences = true;
        _selectedLanguage = _settings.Current.Language;
        _selectedTheme = _settings.Current.Theme;
        _applyingPreferences = false;

        _loc.LanguageChanged += (_, _) => OnPropertyChanged(nameof(ThemeOptions));
    }

    /// <summary>Raised after the plan list was reloaded (the tray mirrors it).</summary>
    public event EventHandler? PlansChanged;

    public ObservableCollection<PowerPlan> Plans { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetActiveCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetPlanCommand))]
    private PowerPlan? _selectedPlan;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _selectedLanguage;

    [ObservableProperty]
    private AppTheme _selectedTheme;

    public string VersionText => _loc.Format("Main_Version", AppVersion);

    public static string AppVersion =>
        (Assembly.GetEntryAssembly() ?? typeof(MainViewModel).Assembly).GetName().Version?.ToString(3) ?? "1.0.0";

    public IReadOnlyList<LanguageOption> Languages => _loc.Languages;

    public IReadOnlyList<KeyValuePair<AppTheme, string>> ThemeOptions =>
    [
        new(AppTheme.System, _loc["Theme_System"]),
        new(AppTheme.Light, _loc["Theme_Light"]),
        new(AppTheme.Dark, _loc["Theme_Dark"]),
    ];

    partial void OnSelectedLanguageChanged(string value)
    {
        if (_applyingPreferences) return;
        _loc.SetLanguage(value);
        _settings.Save(_settings.Current with { Language = value });
        OnPropertyChanged(nameof(VersionText));
    }

    partial void OnSelectedThemeChanged(AppTheme value)
    {
        if (_applyingPreferences) return;
        _theme.Apply(value);
        _settings.Save(_settings.Current with { Theme = value });
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        try
        {
            var selectedGuid = SelectedPlan?.Guid;
            var plans = await _powerService.GetPlansAsync();

            Plans.Clear();
            foreach (var plan in plans) Plans.Add(plan);

            // Keep the selection across reloads; plan objects are recreated on every refresh.
            SelectedPlan = Plans.FirstOrDefault(p => p.Guid == selectedGuid);
            PlansChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusMessage = _loc.Format("Err_LoadFailed", ex.Message);
        }
    }

    private bool CanSetActive() => SelectedPlan is { IsActive: false } && !IsBusy;
    private bool HasSelection() => SelectedPlan is not null && !IsBusy;
    private bool CanDelete() => SelectedPlan is { IsActive: false } && !IsBusy;
    private bool CanReset() => SelectedPlan is { IsBuiltIn: true } && !IsBusy;

    partial void OnIsBusyChanged(bool value)
    {
        SetActiveCommand.NotifyCanExecuteChanged();
        EditCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
        ResetPlanCommand.NotifyCanExecuteChanged();
        CreateCommand.NotifyCanExecuteChanged();
        ImportCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSetActive))]
    private Task SetActiveAsync() => ActivateAsync(SelectedPlan!);

    /// <summary>Activates a plan (used by the main window and the tray).</summary>
    public Task ActivateAsync(PowerPlan plan) => RunAsync(async () =>
    {
        await _powerService.SetActivePlanAsync(plan.Guid);
        StatusMessage = _loc.Format("Msg_Activated", plan.Name);
    });

    private bool NotBusy() => !IsBusy;

    [RelayCommand(CanExecute = nameof(NotBusy))]
    private Task CreateAsync()
    {
        var request = _dialogs.ShowCreatePlan(Plans.ToList());
        if (request is null) return Task.CompletedTask;

        return RunAsync(async () =>
        {
            var created = await _powerService.CreatePlanAsync(request.Name, request.BasePlanGuid);
            await RefreshAsync();
            SelectedPlan = Plans.FirstOrDefault(p => p.Guid == created.Guid) ?? SelectedPlan;
            StatusMessage = _loc.Format("Msg_Created", created.Name);
        }, refresh: false);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task EditAsync()
    {
        var plan = SelectedPlan!;
        return RunAsync(async () =>
        {
            var current = await _powerService.GetSettingsAsync(plan.Guid);
            var updated = _dialogs.ShowEditPlan(plan, current);
            if (updated is null) return;

            await _powerService.UpdateSettingsAsync(plan.Guid, updated);
            StatusMessage = _loc.Format("Msg_Updated", plan.Name);
        });
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private Task DeleteAsync()
    {
        var plan = SelectedPlan!;
        if (!_dialogs.Confirm(_loc.Format("Confirm_Delete", plan.Name))) return Task.CompletedTask;

        return RunAsync(async () =>
        {
            await _powerService.DeletePlanAsync(plan.Guid);
            StatusMessage = _loc.Format("Msg_Deleted", plan.Name);
        });
    }

    [RelayCommand(CanExecute = nameof(NotBusy))]
    private Task ImportAsync()
    {
        var path = _dialogs.PickImportFile();
        if (path is null) return Task.CompletedTask;

        return RunAsync(async () =>
        {
            var imported = await _powerService.ImportPlanAsync(path);
            await RefreshAsync();
            SelectedPlan = Plans.FirstOrDefault(p => p.Guid == imported.Guid) ?? SelectedPlan;
            StatusMessage = _loc.Format("Msg_Imported", imported.Name);
        }, refresh: false);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task ExportAsync()
    {
        var plan = SelectedPlan!;
        var path = _dialogs.PickExportFile(SafeFileName(plan.Name) + ".pow");
        if (path is null) return Task.CompletedTask;

        return RunAsync(async () =>
        {
            await _powerService.ExportPlanAsync(plan.Guid, path);
            StatusMessage = _loc.Format("Msg_Exported", plan.Name, path);
        }, refresh: false);
    }

    [RelayCommand(CanExecute = nameof(CanReset))]
    private Task ResetPlanAsync()
    {
        var plan = SelectedPlan!;
        if (!_dialogs.Confirm(_loc.Format("Confirm_Reset", plan.Name))) return Task.CompletedTask;

        return RunAsync(async () =>
        {
            await _powerService.ResetPlanToDefaultAsync(plan.Guid);
            StatusMessage = _loc.Format("Msg_Reset", plan.Name);
        });
    }

    /// <summary>Runs one operation: busy flag, error reporting, then (by default) a refresh.</summary>
    private async Task RunAsync(Func<Task> operation, bool refresh = true)
    {
        IsBusy = true;
        try
        {
            await operation();
        }
        catch (PowercfgException ex) when (ex.Kind == PowercfgErrorKind.ElevationCancelled)
        {
            StatusMessage = _loc["Msg_ElevationCancelled"];
        }
        catch (Exception ex)
        {
            StatusMessage = string.Empty;
            _dialogs.ShowError(_loc.Format("Err_Operation", ex.Message));
        }
        finally
        {
            IsBusy = false;
        }

        if (refresh) await RefreshAsync();
    }

    private static string SafeFileName(string name)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return cleaned.Length == 0 ? "power-plan" : cleaned;
    }
}
