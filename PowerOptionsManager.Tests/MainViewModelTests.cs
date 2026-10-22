using PowerOptionsManager.Models;
using PowerOptionsManager.Services;
using PowerOptionsManager.ViewModels;

namespace PowerOptionsManager.Tests;

public class MainViewModelTests
{
    private readonly FakePowerService _power = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeSettingsService _settings = new();
    private readonly FakeThemeService _theme = new();

    public MainViewModelTests() => Loc.Instance.SetLanguage("en-US");

    private async Task<MainViewModel> CreateAsync()
    {
        var vm = new MainViewModel(_power, _dialogs, _settings, _theme);
        await vm.RefreshAsync();
        return vm;
    }

    private static PowerPlan Select(MainViewModel vm, string guid) =>
        vm.SelectedPlan = vm.Plans.Single(p => p.Guid == guid);

    [Fact]
    public async Task Refresh_LoadsPlans()
    {
        var vm = await CreateAsync();

        Assert.Equal(2, vm.Plans.Count);
        Assert.Equal(TestPlans.Custom, vm.Plans.Single(p => p.IsActive).Guid);
    }

    [Fact]
    public async Task NoSelection_DisablesPlanCommands()
    {
        var vm = await CreateAsync();

        Assert.False(vm.SetActiveCommand.CanExecute(null));
        Assert.False(vm.EditCommand.CanExecute(null));
        Assert.False(vm.DeleteCommand.CanExecute(null));
        Assert.False(vm.ExportCommand.CanExecute(null));
        Assert.False(vm.ResetPlanCommand.CanExecute(null));
        Assert.True(vm.CreateCommand.CanExecute(null));
        Assert.True(vm.ImportCommand.CanExecute(null));
    }

    [Fact]
    public async Task SelectingInactiveBuiltInPlan_EnablesCommands_AndRaisesCanExecuteChanged()
    {
        var vm = await CreateAsync();
        var raised = 0;
        vm.EditCommand.CanExecuteChanged += (_, _) => raised++;

        Select(vm, TestPlans.Balanced);

        Assert.True(raised > 0);
        Assert.True(vm.SetActiveCommand.CanExecute(null));
        Assert.True(vm.EditCommand.CanExecute(null));
        Assert.True(vm.DeleteCommand.CanExecute(null));
        Assert.True(vm.ExportCommand.CanExecute(null));
        Assert.True(vm.ResetPlanCommand.CanExecute(null));
    }

    [Fact]
    public async Task ActiveCustomPlan_CannotBeDeletedResetOrReactivated()
    {
        var vm = await CreateAsync();

        Select(vm, TestPlans.Custom);

        Assert.False(vm.DeleteCommand.CanExecute(null));
        Assert.False(vm.ResetPlanCommand.CanExecute(null));
        Assert.False(vm.SetActiveCommand.CanExecute(null));
        Assert.True(vm.EditCommand.CanExecute(null));
    }

    [Fact]
    public async Task SetActive_CallsServiceOnce_RefreshesAndKeepsSelection()
    {
        var vm = await CreateAsync();
        Select(vm, TestPlans.Balanced);
        var loadsBefore = _power.GetPlansCount;

        await vm.SetActiveCommand.ExecuteAsync(null);

        Assert.Equal([$"setactive {TestPlans.Balanced}"], _power.Calls);
        Assert.True(_power.GetPlansCount > loadsBefore);
        Assert.Equal(TestPlans.Balanced, vm.SelectedPlan?.Guid);
        Assert.True(vm.SelectedPlan!.IsActive);
        Assert.Contains("Balanced", vm.StatusMessage);
        Assert.Empty(_dialogs.Errors);
    }

    [Fact]
    public async Task Refresh_RaisesPlansChanged()
    {
        var vm = await CreateAsync();
        var raised = false;
        vm.PlansChanged += (_, _) => raised = true;

        await vm.RefreshAsync();

        Assert.True(raised);
    }

    [Fact]
    public async Task ServiceFailure_ShowsError_AndStillRefreshes()
    {
        var vm = await CreateAsync();
        Select(vm, TestPlans.Balanced);
        _power.FailWith = new PowercfgException(PowercfgErrorKind.Failed, "boom");
        var loadsBefore = _power.GetPlansCount;

        await vm.SetActiveCommand.ExecuteAsync(null);

        Assert.Contains("boom", Assert.Single(_dialogs.Errors));
        Assert.True(_power.GetPlansCount > loadsBefore);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task UacCancelled_ShowsStatus_NotErrorDialog()
    {
        var vm = await CreateAsync();
        Select(vm, TestPlans.Balanced);
        _power.FailWith = new PowercfgException(PowercfgErrorKind.ElevationCancelled, "cancelled");

        await vm.SetActiveCommand.ExecuteAsync(null);

        Assert.Empty(_dialogs.Errors);
        Assert.Equal(Loc.Instance["Msg_ElevationCancelled"], vm.StatusMessage);
    }

    [Fact]
    public async Task Delete_NotConfirmed_DoesNothing()
    {
        var vm = await CreateAsync();
        Select(vm, TestPlans.Balanced);
        _dialogs.ConfirmAnswer = false;

        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Single(_dialogs.Confirmations);
        Assert.Empty(_power.Calls);
    }

    [Fact]
    public async Task Delete_Confirmed_DeletesAndRemovesFromList()
    {
        var vm = await CreateAsync();
        Select(vm, TestPlans.Balanced);

        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Equal([$"delete {TestPlans.Balanced}"], _power.Calls);
        Assert.DoesNotContain(vm.Plans, p => p.Guid == TestPlans.Balanced);
        Assert.Null(vm.SelectedPlan);
    }

    [Fact]
    public async Task Create_UsesDialogResult_AndSelectsNewPlan()
    {
        var vm = await CreateAsync();
        _dialogs.CreateResult = new CreatePlanRequest("QA Temp", TestPlans.Balanced);

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Equal([$"create QA Temp {TestPlans.Balanced}"], _power.Calls);
        Assert.Equal(TestPlans.Created, vm.SelectedPlan?.Guid);
    }

    [Fact]
    public async Task Create_Cancelled_DoesNothing()
    {
        var vm = await CreateAsync();

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Empty(_power.Calls);
    }

    [Fact]
    public async Task Edit_LoadsRealSettings_AndSavesDialogResult()
    {
        var vm = await CreateAsync();
        Select(vm, TestPlans.Balanced);
        _power.Settings = new PlanSettings { DisplayOffSeconds = new AcDc(300, 180) };
        var edited = new PlanSettings { DisplayOffSeconds = new AcDc(600, 180) };
        _dialogs.EditResult = _ => edited;

        await vm.EditCommand.ExecuteAsync(null);

        Assert.Equal(new AcDc(300, 180), _dialogs.EditShownWith?.DisplayOffSeconds);
        Assert.Equal([$"getsettings {TestPlans.Balanced}", $"update {TestPlans.Balanced}"], _power.Calls);
        Assert.Same(edited, _power.Settings);
    }

    [Fact]
    public async Task Edit_Cancelled_DoesNotSave()
    {
        var vm = await CreateAsync();
        Select(vm, TestPlans.Balanced);

        await vm.EditCommand.ExecuteAsync(null);

        Assert.Equal([$"getsettings {TestPlans.Balanced}"], _power.Calls);
    }

    [Fact]
    public async Task Export_SuggestsSafeFileName()
    {
        _power.Plans[0].Name = "A/B: test";
        var vm = await CreateAsync();
        Select(vm, TestPlans.Balanced);
        _dialogs.ExportPath = @"C:\out\a.pow";

        await vm.ExportCommand.ExecuteAsync(null);

        Assert.Equal("A_B_ test.pow", _dialogs.SuggestedExportName);
        Assert.Equal([$@"export {TestPlans.Balanced} C:\out\a.pow"], _power.Calls);
    }

    [Fact]
    public async Task Import_SelectsImportedPlan()
    {
        var vm = await CreateAsync();
        _dialogs.ImportPath = @"C:\in\a.pow";

        await vm.ImportCommand.ExecuteAsync(null);

        Assert.Equal(TestPlans.Created, vm.SelectedPlan?.Guid);
    }

    [Fact]
    public async Task Reset_BuiltInConfirmed_ResetsThatPlan()
    {
        var vm = await CreateAsync();
        Select(vm, TestPlans.Balanced);

        await vm.ResetPlanCommand.ExecuteAsync(null);

        Assert.Equal([$"reset {TestPlans.Balanced}"], _power.Calls);
    }

    [Fact]
    public async Task ChangingLanguage_SwitchesStringsAndSaves()
    {
        var vm = await CreateAsync();
        var darkOption = vm.ThemeOptions.Single(t => t.Value == AppTheme.Dark);

        vm.SelectedLanguage = "id-ID";

        // Same item instance with a new label, so the combo box keeps its selection.
        Assert.Same(darkOption, vm.ThemeOptions.Single(t => t.Value == AppTheme.Dark));
        Assert.Equal("Gelap", darkOption.ToString());
        Assert.Equal("id-ID", _settings.Current.Language);
        Assert.Equal("Versi " + MainViewModel.AppVersion, vm.VersionText);
        Assert.Equal("Gelap", vm.ThemeOptions.Single(t => t.Value == AppTheme.Dark).DisplayName);

        vm.SelectedLanguage = "en-US";
        Assert.Equal("Version " + MainViewModel.AppVersion, vm.VersionText);
    }

    [Fact]
    public async Task ChangingTheme_AppliesAndSaves()
    {
        var vm = await CreateAsync();

        vm.SelectedTheme = AppTheme.Dark;

        Assert.Equal([AppTheme.Dark], _theme.Applied);
        Assert.Equal(AppTheme.Dark, _settings.Current.Theme);
    }

    [Fact]
    public void Constructor_LoadsSavedPreferences_WithoutReapplyingOrSaving()
    {
        _settings.Save(new AppSettings { Language = "id-ID", Theme = AppTheme.Light });
        var saves = _settings.SaveCount;

        var vm = new MainViewModel(_power, _dialogs, _settings, _theme);

        Assert.Equal("id-ID", vm.SelectedLanguage);
        Assert.Equal(AppTheme.Light, vm.SelectedTheme);
        Assert.Equal(saves, _settings.SaveCount);
        Assert.Empty(_theme.Applied);
    }
}
