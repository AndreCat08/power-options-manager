using PowerOptionsManager.Models;
using PowerOptionsManager.Services;

namespace PowerOptionsManager.Tests;

/// <summary>Scripted powercfg: records every call and answers from a handler.</summary>
internal sealed class FakePowercfgRunner : IPowercfgRunner
{
    public List<string> Calls { get; } = [];
    public List<string> ElevatedCalls { get; } = [];
    public Func<IReadOnlyList<string>, PowercfgResult> Handler { get; set; } = _ => new PowercfgResult(0, string.Empty, string.Empty);
    public Func<IReadOnlyList<string>, int> ElevatedHandler { get; set; } = _ => 0;

    public Task<PowercfgResult> RunAsync(IReadOnlyList<string> args, CancellationToken ct = default)
    {
        Calls.Add(string.Join(' ', args));
        return Task.FromResult(Handler(args));
    }

    public Task<int> RunElevatedAsync(IReadOnlyList<string> args, CancellationToken ct = default)
    {
        ElevatedCalls.Add(string.Join(' ', args));
        return Task.FromResult(ElevatedHandler(args));
    }

    public static PowercfgResult Ok(string stdout = "") => new(0, stdout, string.Empty);
    public static PowercfgResult Fail(string stderr) => new(1, string.Empty, stderr);
    public const string Denied = "Unable to perform operation.  An unexpected error (0x522) has occurred:  A required privilege is not held by the client.";
}

public class PowerServiceTests
{
    private const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";
    private const string Custom = "788abe11-f08c-451e-8375-82eede699673";
    private const string NewGuid = "d3a77d17-95a3-4c68-8984-d2b6a826158a";

    private static string List(bool customActive = true, bool withNew = false) =>
        "Existing Power Schemes (* Active)\r\n-----\r\n" +
        $"Power Scheme GUID: {Balanced}  (Balanced){(customActive ? "" : " *")}\r\n" +
        $"Power Scheme GUID: {Custom}  (Never Sleep){(customActive ? " *" : "")}\r\n" +
        (withNew ? $"Power Scheme GUID: {NewGuid}  (Balanced)\r\n" : "");

    private static string Query(uint ac, uint dc) =>
        $"      Minimum Possible Setting: 0x00000000\r\n      Maximum Possible Setting: 0xffffffff\r\n" +
        $"    Current AC Power Setting Index: 0x{ac:x8}\r\n    Current DC Power Setting Index: 0x{dc:x8}\r\n";

    [Fact]
    public async Task GetPlans_ParsesList()
    {
        var runner = new FakePowercfgRunner { Handler = _ => FakePowercfgRunner.Ok(List()) };

        var plans = await new PowerService(runner).GetPlansAsync();

        Assert.Equal(["/list"], runner.Calls);
        Assert.Equal(2, plans.Count);
        Assert.Equal(Custom, Assert.Single(plans, p => p.IsActive).Guid);
    }

    [Fact]
    public async Task GetPlans_NonZeroExit_Throws()
    {
        var runner = new FakePowercfgRunner { Handler = _ => FakePowercfgRunner.Fail("boom") };

        var ex = await Assert.ThrowsAsync<PowercfgException>(() => new PowerService(runner).GetPlansAsync());
        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public async Task SetActive_IssuesSetActive()
    {
        var runner = new FakePowercfgRunner();

        await new PowerService(runner).SetActivePlanAsync(Balanced);

        Assert.Equal([$"/setactive {Balanced}"], runner.Calls);
        Assert.Empty(runner.ElevatedCalls);
    }

    [Fact]
    public async Task Create_DuplicatesBaseThenRenames()
    {
        var runner = new FakePowercfgRunner
        {
            Handler = a => a[0] switch
            {
                "/list" => FakePowercfgRunner.Ok(List()),
                "/duplicatescheme" => FakePowercfgRunner.Ok($"Power Scheme GUID: {NewGuid}  (Balanced)\r\n"),
                _ => FakePowercfgRunner.Ok(),
            },
        };

        var plan = await new PowerService(runner).CreatePlanAsync("  QA Temp ", Balanced);

        Assert.Equal(NewGuid, plan.Guid);
        Assert.Equal("QA Temp", plan.Name);
        Assert.Equal(["/list", $"/duplicatescheme {Balanced}", $"/changename {NewGuid} QA Temp"], runner.Calls);
    }

    [Fact]
    public async Task Create_ElevatedDuplicate_FindsNewPlanByDiffingList()
    {
        var duplicated = false;
        var runner = new FakePowercfgRunner
        {
            Handler = a => a[0] switch
            {
                "/list" => FakePowercfgRunner.Ok(List(withNew: duplicated)),
                "/duplicatescheme" => FakePowercfgRunner.Fail(FakePowercfgRunner.Denied),
                _ => FakePowercfgRunner.Ok(),
            },
            ElevatedHandler = a => { if (a[0] == "/duplicatescheme") duplicated = true; return 0; },
        };

        var plan = await new PowerService(runner).CreatePlanAsync("QA Temp", Balanced);

        Assert.Equal(NewGuid, plan.Guid);
        Assert.Equal([$"/duplicatescheme {Balanced}"], runner.ElevatedCalls);
        Assert.Contains($"/changename {NewGuid} QA Temp", runner.Calls);
    }

    [Fact]
    public async Task Write_AccessDenied_RetriesElevated()
    {
        var runner = new FakePowercfgRunner { Handler = _ => FakePowercfgRunner.Fail(FakePowercfgRunner.Denied) };

        await new PowerService(runner).SetActivePlanAsync(Balanced);

        Assert.Equal([$"/setactive {Balanced}"], runner.ElevatedCalls);
    }

    [Fact]
    public async Task Write_ElevatedNonZeroExit_Throws()
    {
        var runner = new FakePowercfgRunner
        {
            Handler = _ => FakePowercfgRunner.Fail(FakePowercfgRunner.Denied),
            ElevatedHandler = _ => 1,
        };

        await Assert.ThrowsAsync<PowercfgException>(() => new PowerService(runner).SetActivePlanAsync(Balanced));
    }

    [Fact]
    public async Task Write_OtherFailure_DoesNotElevate()
    {
        var runner = new FakePowercfgRunner { Handler = _ => FakePowercfgRunner.Fail("Invalid Parameters -- try \"/?\" for help") };

        await Assert.ThrowsAsync<PowercfgException>(() => new PowerService(runner).SetActivePlanAsync(Balanced));
        Assert.Empty(runner.ElevatedCalls);
    }

    [Fact]
    public async Task Write_UacCancelled_PropagatesCancellation()
    {
        var runner = new FakePowercfgRunner
        {
            Handler = _ => FakePowercfgRunner.Fail(FakePowercfgRunner.Denied),
            ElevatedHandler = _ => throw new PowercfgException(PowercfgErrorKind.ElevationCancelled, "cancelled"),
        };

        var ex = await Assert.ThrowsAsync<PowercfgException>(() => new PowerService(runner).SetActivePlanAsync(Balanced));
        Assert.Equal(PowercfgErrorKind.ElevationCancelled, ex.Kind);
    }

    [Fact]
    public async Task GetSettings_QueriesRealAliases_AndMarksMissingAsUnsupported()
    {
        var runner = new FakePowercfgRunner
        {
            Handler = a => a[0] switch
            {
                "/list" => FakePowercfgRunner.Ok(List()),
                "/query" when a[3] == "VIDEONORMALLEVEL" => FakePowercfgRunner.Fail("The power scheme, subgroup or setting specified does not exist."),
                "/query" when a[3] == "VIDEOIDLE" => FakePowercfgRunner.Ok(Query(300, 180)),
                "/query" => FakePowercfgRunner.Ok(Query(5, 5)),
                _ => FakePowercfgRunner.Ok(),
            },
        };

        var settings = await new PowerService(runner).GetSettingsAsync(Balanced);

        Assert.Equal(new AcDc(300, 180), settings.DisplayOffSeconds);
        Assert.Equal(new AcDc(5, 5), settings.MinProcessorPercent);
        Assert.Null(settings.BrightnessPercent);
        Assert.Contains($"/query {Balanced} SUB_VIDEO VIDEOIDLE", runner.Calls);
        Assert.Contains($"/query {Balanced} SUB_SLEEP STANDBYIDLE", runner.Calls);
        Assert.Contains($"/query {Balanced} SUB_SLEEP HIBERNATEIDLE", runner.Calls);
        Assert.Contains($"/query {Balanced} SUB_SLEEP HYBRIDSLEEP", runner.Calls);
        Assert.Contains($"/query {Balanced} SUB_PROCESSOR PROCTHROTTLEMIN", runner.Calls);
        Assert.Contains($"/query {Balanced} SUB_PROCESSOR PROCTHROTTLEMAX", runner.Calls);
    }

    [Fact]
    public async Task UpdateSettings_WritesAcAndDcSeparately_SkipsUnsupported_NoReapplyWhenInactive()
    {
        var runner = new FakePowercfgRunner { Handler = a => a[0] == "/list" ? FakePowercfgRunner.Ok(List()) : FakePowercfgRunner.Ok() };

        await new PowerService(runner).UpdateSettingsAsync(Balanced, new PlanSettings
        {
            DisplayOffSeconds = new AcDc(300, 120),
            HybridSleep = new AcDc(1, 0),
        });

        Assert.Equal(
        [
            $"/setacvalueindex {Balanced} SUB_VIDEO VIDEOIDLE 300",
            $"/setdcvalueindex {Balanced} SUB_VIDEO VIDEOIDLE 120",
            $"/setacvalueindex {Balanced} SUB_SLEEP HYBRIDSLEEP 1",
            $"/setdcvalueindex {Balanced} SUB_SLEEP HYBRIDSLEEP 0",
            "/list",
        ], runner.Calls);
    }

    [Fact]
    public async Task UpdateSettings_ActivePlan_IsReapplied()
    {
        var runner = new FakePowercfgRunner { Handler = a => a[0] == "/list" ? FakePowercfgRunner.Ok(List()) : FakePowercfgRunner.Ok() };

        await new PowerService(runner).UpdateSettingsAsync(Custom, new PlanSettings { SleepSeconds = new AcDc(0, 900) });

        Assert.Equal($"/setactive {Custom}", runner.Calls[^1]);
    }

    [Fact]
    public async Task Delete_InactivePlan_Deletes()
    {
        var runner = new FakePowercfgRunner { Handler = a => a[0] == "/list" ? FakePowercfgRunner.Ok(List()) : FakePowercfgRunner.Ok() };

        await new PowerService(runner).DeletePlanAsync(Balanced);

        Assert.Equal($"/delete {Balanced}", runner.Calls[^1]);
    }

    [Fact]
    public async Task Delete_ActivePlan_IsRefused()
    {
        var runner = new FakePowercfgRunner { Handler = a => a[0] == "/list" ? FakePowercfgRunner.Ok(List()) : FakePowercfgRunner.Ok() };

        await Assert.ThrowsAsync<InvalidOperationException>(() => new PowerService(runner).DeletePlanAsync(Custom));
        Assert.DoesNotContain(runner.Calls, c => c.StartsWith("/delete", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Reset_BuiltInPlan_RestoresOnlyThatScheme()
    {
        var runner = new FakePowercfgRunner();

        await new PowerService(runner).ResetPlanToDefaultAsync(Balanced);

        Assert.Equal([$"/restoreindividualdefaultscheme {Balanced}"], runner.Calls);
    }

    [Fact]
    public async Task Reset_CustomPlan_IsRefused_AndNeverRestoresAllSchemes()
    {
        var runner = new FakePowercfgRunner();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new PowerService(runner).ResetPlanToDefaultAsync(Custom));
        Assert.Empty(runner.Calls);
        Assert.DoesNotContain(runner.ElevatedCalls, c => c.Contains("restoredefaultschemes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Export_UsesFullPathAndPlan()
    {
        var runner = new FakePowercfgRunner();

        await new PowerService(runner).ExportPlanAsync(Balanced, @"C:\plans\b.pow");

        Assert.Equal([$@"/export C:\plans\b.pow {Balanced}"], runner.Calls);
    }

    [Fact]
    public async Task Import_ReturnsImportedPlan()
    {
        var imported = false;
        var runner = new FakePowercfgRunner
        {
            Handler = a => a[0] switch
            {
                "/list" => FakePowercfgRunner.Ok(List(withNew: imported)),
                "/import" => Run(() => imported = true, $"Imported Power Scheme Successfully. GUID: {NewGuid}\r\n"),
                _ => FakePowercfgRunner.Ok(),
            },
        };

        var plan = await new PowerService(runner).ImportPlanAsync(@"C:\plans\b.pow");

        Assert.Equal(NewGuid, plan.Guid);
        Assert.Contains(@"/import C:\plans\b.pow", runner.Calls);
    }

    [Fact]
    public async Task InvalidGuid_IsRejectedBeforeRunningPowercfg()
    {
        var runner = new FakePowercfgRunner();

        await Assert.ThrowsAsync<ArgumentException>(() => new PowerService(runner).SetActivePlanAsync("not-a-guid & del"));
        Assert.Empty(runner.Calls);
    }

    private static PowercfgResult Run(Action sideEffect, string stdout)
    {
        sideEffect();
        return FakePowercfgRunner.Ok(stdout);
    }
}
