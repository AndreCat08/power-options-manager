using PowerOptionsManager.Services;

namespace PowerOptionsManager.Tests;

public class PowercfgParserTests
{
    // Captured from `powercfg /list` on Windows 11 (en-US), CRLF line endings.
    private const string ListEn =
        "\r\nExisting Power Schemes (* Active)\r\n" +
        "-----------------------------------\r\n" +
        "Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)\r\n" +
        "Power Scheme GUID: 788abe11-f08c-451e-8375-82eede699673  (Never Sleep) *\r\n";

    // Synthetic Indonesian-UI variant: labels differ, structure is the same.
    private const string ListId =
        "Skema Daya yang Ada (* Aktif)\r\n" +
        "-----------------------------------\r\n" +
        "GUID Skema Daya: 381B4222-F694-41F0-9685-FF5BB260DF2E  (Seimbang) *\r\n" +
        "GUID Skema Daya: d3a77d17-95a3-4c68-8984-d2b6a826158a  (Hemat (Kantor))\r\n";

    // Captured from `powercfg /query <guid> SUB_VIDEO VIDEOIDLE`.
    private const string QueryVideoIdle =
        "Power Scheme GUID: d3a77d17-95a3-4c68-8984-d2b6a826158a  (QA Temp)\r\n" +
        "  Subgroup GUID: 7516b95f-f776-4464-8c53-06167f40cc99  (Display)\r\n" +
        "    GUID Alias: SUB_VIDEO\r\n" +
        "    Power Setting GUID: 3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e  (Turn off display after)\r\n" +
        "      GUID Alias: VIDEOIDLE\r\n" +
        "      Minimum Possible Setting: 0x00000000\r\n" +
        "      Maximum Possible Setting: 0xffffffff\r\n" +
        "      Possible Settings increment: 0x00000001\r\n" +
        "      Possible Settings units: Seconds\r\n" +
        "    Current AC Power Setting Index: 0x0000012c\r\n" +
        "    Current DC Power Setting Index: 0x000000b4\r\n\r\n";

    // Captured from `powercfg /query <guid> SUB_SLEEP HYBRIDSLEEP` (an index-type setting).
    private const string QueryHybridSleep =
        "Power Scheme GUID: d3a77d17-95a3-4c68-8984-d2b6a826158a  (QA Temp)\r\n" +
        "  Subgroup GUID: 238c9fa8-0aad-41ed-83f4-97be242c8f20  (Sleep)\r\n" +
        "    GUID Alias: SUB_SLEEP\r\n" +
        "    Power Setting GUID: 94ac6d29-73ce-41a6-809f-6363ba21b47e  (Allow hybrid sleep)\r\n" +
        "      GUID Alias: HYBRIDSLEEP\r\n" +
        "      Possible Setting Index: 000\r\n" +
        "      Possible Setting Friendly Name: Off\r\n" +
        "      Possible Setting Index: 001\r\n" +
        "      Possible Setting Friendly Name: On\r\n" +
        "    Current AC Power Setting Index: 0x00000001\r\n" +
        "    Current DC Power Setting Index: 0x00000000\r\n\r\n";

    [Fact]
    public void ParseList_RealEnglishOutput_ReturnsPlansAndActiveMarker()
    {
        var plans = PowercfgParser.ParseList(ListEn);

        Assert.Equal(2, plans.Count);
        Assert.Equal("381b4222-f694-41f0-9685-ff5bb260df2e", plans[0].Guid);
        Assert.Equal("Balanced", plans[0].Name);
        Assert.False(plans[0].IsActive);
        Assert.Equal("Never Sleep", plans[1].Name);
        Assert.True(plans[1].IsActive);
    }

    [Fact]
    public void ParseList_IndonesianLabels_DoesNotDependOnLabelText()
    {
        var plans = PowercfgParser.ParseList(ListId);

        Assert.Equal(2, plans.Count);
        Assert.Equal("381b4222-f694-41f0-9685-ff5bb260df2e", plans[0].Guid); // normalized to lower case
        Assert.Equal("Seimbang", plans[0].Name);
        Assert.True(plans[0].IsActive);
        Assert.Equal("Hemat (Kantor)", plans[1].Name); // nested parentheses kept
        Assert.False(plans[1].IsActive);
    }

    [Fact]
    public void ParseList_NoSchemes_ReturnsEmpty()
    {
        Assert.Empty(PowercfgParser.ParseList("Existing Power Schemes (* Active)\r\n---\r\n"));
    }

    [Fact]
    public void ParseQuery_SecondsSetting_ReturnsAcThenDc()
    {
        var value = PowercfgParser.ParseQuery(QueryVideoIdle);

        Assert.NotNull(value);
        Assert.Equal(300u, value.Value.Ac);
        Assert.Equal(180u, value.Value.Dc);
    }

    [Fact]
    public void ParseQuery_IndexSetting_IgnoresNonHexPossibleValues()
    {
        var value = PowercfgParser.ParseQuery(QueryHybridSleep);

        Assert.NotNull(value);
        Assert.Equal(1u, value.Value.Ac);
        Assert.Equal(0u, value.Value.Dc);
    }

    [Fact]
    public void ParseQuery_SettingDoesNotExist_ReturnsNull()
    {
        Assert.Null(PowercfgParser.ParseQuery("The power scheme, subgroup or setting specified does not exist.\r\n"));
    }

    [Fact]
    public void ParseLastGuid_DuplicateOutput_ReturnsNewSchemeGuid()
    {
        var guid = PowercfgParser.ParseLastGuid("Power Scheme GUID: D3A77D17-95A3-4C68-8984-D2B6A826158A  (Balanced)\r\n");
        Assert.Equal("d3a77d17-95a3-4c68-8984-d2b6a826158a", guid);
    }

    [Theory]
    [InlineData("Unable to perform operation.  An unexpected error (0x522) has occurred:  A required privilege is not held by the client.", true)]
    [InlineData("Access is denied.", true)]
    [InlineData("Akses ditolak.", true)]
    [InlineData("The power scheme, subgroup or setting specified does not exist.", false)]
    public void PowercfgResult_IsAccessDenied_DetectsPrivilegeErrors(string stderr, bool expected)
    {
        Assert.Equal(expected, new PowercfgResult(1, string.Empty, stderr).IsAccessDenied);
    }

    [Fact]
    public void PowercfgResult_Success_IsNeverAccessDenied()
    {
        Assert.False(new PowercfgResult(0, "Access is denied", string.Empty).IsAccessDenied);
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("QA Temp", "\"QA Temp\"")]
    [InlineData(@"C:\My Plans\a.pow", "\"C:\\My Plans\\a.pow\"")]
    [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [InlineData(@"C:\dir\", @"C:\dir\")]
    [InlineData(@"C:\my dir\", "\"C:\\my dir\\\\\"")]
    [InlineData("", "\"\"")]
    public void QuoteArgument_FollowsWindowsRules(string input, string expected)
    {
        Assert.Equal(expected, PowercfgRunner.QuoteArgument(input));
    }
}
