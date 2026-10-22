using PowerOptionsManager.Models;
using PowerOptionsManager.Services;
using PowerOptionsManager.ViewModels;

namespace PowerOptionsManager.Tests;

public class CreatePlanDialogViewModelTests
{
    public CreatePlanDialogViewModelTests() => Loc.Instance.SetLanguage("en-US");

    [Fact]
    public void DefaultBase_IsHighPerformance_WhenInstalled()
    {
        var plans = TestPlans.Default();
        plans.Add(new PowerPlan { Guid = TestPlans.HighPerformance, Name = "High performance" });

        var vm = new CreatePlanDialogViewModel(plans);

        Assert.Equal(TestPlans.HighPerformance, vm.SelectedBasePlan?.Guid);
    }

    [Fact]
    public void DefaultBase_FallsBackToActivePlan()
    {
        var vm = new CreatePlanDialogViewModel(TestPlans.Default());

        Assert.Equal(TestPlans.Custom, vm.SelectedBasePlan?.Guid);
    }

    [Theory]
    [InlineData("", "Val_NameRequired")]
    [InlineData("   ", "Val_NameRequired")]
    [InlineData("balanced", "Val_NameDuplicate")]
    public void InvalidName_ShowsError_AndDoesNotClose(string name, string errorKey)
    {
        var vm = new CreatePlanDialogViewModel(TestPlans.Default()) { PlanName = name };
        bool? closed = null;
        vm.RequestClose += r => closed = r;

        vm.OkCommand.Execute(null);

        Assert.Null(closed);
        Assert.Null(vm.Result);
        Assert.Equal(Loc.Instance[errorKey], vm.ErrorMessage);
    }

    [Fact]
    public void TooLongName_IsRejected()
    {
        var vm = new CreatePlanDialogViewModel(TestPlans.Default()) { PlanName = new string('x', CreatePlanDialogViewModel.MaxNameLength + 1) };

        vm.OkCommand.Execute(null);

        Assert.Null(vm.Result);
        Assert.Contains("100", vm.ErrorMessage);
    }

    [Fact]
    public void ValidName_ClosesWithTrue_AndReturnsTrimmedRequest()
    {
        var vm = new CreatePlanDialogViewModel(TestPlans.Default()) { PlanName = "  QA Temp  " };
        vm.SelectedBasePlan = vm.BasePlans.Single(p => p.Guid == TestPlans.Balanced);
        bool? closed = null;
        vm.RequestClose += r => closed = r;

        vm.OkCommand.Execute(null);

        Assert.True(closed);
        Assert.Equal(new CreatePlanRequest("QA Temp", TestPlans.Balanced), vm.Result);
    }

    [Fact]
    public void Cancel_ClosesWithFalse()
    {
        var vm = new CreatePlanDialogViewModel(TestPlans.Default());
        bool? closed = null;
        vm.RequestClose += r => closed = r;

        vm.CancelCommand.Execute(null);

        Assert.False(closed);
        Assert.Null(vm.Result);
    }
}

public class EditPlanDialogViewModelTests
{
    private static readonly PowerPlan Plan = new() { Guid = TestPlans.Balanced, Name = "Balanced" };

    private static readonly PlanSettings Current = new()
    {
        DisplayOffSeconds = new AcDc(300, 180),
        SleepSeconds = new AcDc(1800, 90), // 90 s is not a whole minute
        HybridSleep = new AcDc(1, 0),
        MinProcessorPercent = new AcDc(5, 5),
        MaxProcessorPercent = new AcDc(100, 80),
        BrightnessPercent = null, // unsupported (e.g. desktop monitor)
    };

    public EditPlanDialogViewModelTests() => Loc.Instance.SetLanguage("en-US");

    private static SettingRowViewModel RowOf(EditPlanDialogViewModel vm, PowerSetting s) => vm.Rows.Single(r => r.Setting == s);

    [Fact]
    public void UnsupportedSettings_GetNoRow()
    {
        var vm = new EditPlanDialogViewModel(Plan, Current);

        Assert.DoesNotContain(vm.Rows, r => r.Setting == PowerSetting.Brightness);
        Assert.DoesNotContain(vm.Rows, r => r.Setting == PowerSetting.Hibernate);
        Assert.Equal(5, vm.Rows.Count);
    }

    [Fact]
    public void Timeouts_AreShownInMinutes()
    {
        var vm = new EditPlanDialogViewModel(Plan, Current);

        Assert.Equal("5", RowOf(vm, PowerSetting.DisplayOff).AcText);
        Assert.Equal("3", RowOf(vm, PowerSetting.DisplayOff).DcText);
        Assert.True(RowOf(vm, PowerSetting.HybridSleep).AcOn);
        Assert.False(RowOf(vm, PowerSetting.HybridSleep).DcOn);
    }

    [Fact]
    public void Save_Unchanged_ReturnsNoChanges_AndKeepsOddSeconds()
    {
        var vm = new EditPlanDialogViewModel(Plan, Current);
        bool? closed = null;
        vm.RequestClose += r => closed = r;

        vm.SaveCommand.Execute(null);

        Assert.True(closed);
        Assert.Equal(new PlanSettings(), vm.Result);
    }

    [Fact]
    public void Save_ChangedMinutes_ConvertsToSeconds_OnlyForThatSide()
    {
        var vm = new EditPlanDialogViewModel(Plan, Current);
        RowOf(vm, PowerSetting.DisplayOff).AcText = "10";
        RowOf(vm, PowerSetting.Sleep).AcText = "0";

        vm.SaveCommand.Execute(null);

        Assert.Equal(new AcDc(600, 180), vm.Result?.DisplayOffSeconds);
        Assert.Equal(new AcDc(0, 90), vm.Result?.SleepSeconds); // DC 90 s untouched
        Assert.Null(vm.Result?.MinProcessorPercent);
    }

    [Fact]
    public void Save_Toggle_WritesZeroOrOne()
    {
        var vm = new EditPlanDialogViewModel(Plan, Current);
        RowOf(vm, PowerSetting.HybridSleep).DcOn = true;

        vm.SaveCommand.Execute(null);

        Assert.Equal(new AcDc(1, 1), vm.Result?.HybridSleep);
    }

    [Theory]
    [InlineData(PowerSetting.MinProcessor, "101")]
    [InlineData(PowerSetting.MinProcessor, "-1")]
    [InlineData(PowerSetting.DisplayOff, "abc")]
    [InlineData(PowerSetting.DisplayOff, "1.5")]
    [InlineData(PowerSetting.DisplayOff, "99999999999")]
    public void Save_InvalidValue_ShowsError_AndDoesNotClose(PowerSetting setting, string text)
    {
        var vm = new EditPlanDialogViewModel(Plan, Current);
        RowOf(vm, setting).AcText = text;
        bool? closed = null;
        vm.RequestClose += r => closed = r;

        vm.SaveCommand.Execute(null);

        Assert.Null(closed);
        Assert.Null(vm.Result);
        Assert.Contains(RowOf(vm, setting).Label, vm.ErrorMessage);
    }

    [Fact]
    public void Save_MinAboveMax_IsRejected()
    {
        var vm = new EditPlanDialogViewModel(Plan, Current);
        RowOf(vm, PowerSetting.MinProcessor).DcText = "90"; // DC max is 80

        vm.SaveCommand.Execute(null);

        Assert.Null(vm.Result);
        Assert.Equal(Loc.Instance["Val_MinAboveMax"], vm.ErrorMessage);
    }

    [Fact]
    public void Revert_RestoresLoadedValues()
    {
        var vm = new EditPlanDialogViewModel(Plan, Current);
        RowOf(vm, PowerSetting.DisplayOff).AcText = "42";
        RowOf(vm, PowerSetting.HybridSleep).AcOn = false;

        vm.RevertCommand.Execute(null);

        Assert.Equal("5", RowOf(vm, PowerSetting.DisplayOff).AcText);
        Assert.True(RowOf(vm, PowerSetting.HybridSleep).AcOn);
    }
}
