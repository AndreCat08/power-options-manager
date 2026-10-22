using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using PowerOptionsManager.Services;

namespace PowerOptionsManager.Tests;

public partial class LocalizationTests
{
    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex Placeholder();

    // Any key-shaped token (Prefix_Name) in a string literal or {loc:Tr ...} markup.
    [GeneratedRegex(@"(?:""|loc:Tr )((?:App|Main|Lbl|Lang|Theme|A11y|Col|Status|Type|Btn|Tip|Err|Msg|Confirm|File|Create|Val|Edit|Tray)_[A-Za-z0-9]+)")]
    private static partial Regex KeyToken();

    [Fact]
    public void EnglishAndIndonesian_HaveTheSameKeys_AndNoEmptyValues()
    {
        var en = Loc.ReadAll(CultureInfo.InvariantCulture);
        var id = Loc.ReadAll(Loc.Indonesian);

        Assert.NotEmpty(en);
        Assert.Equal(en.Keys.Order(), id.Keys.Order());
        Assert.All(en, kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value), $"en '{kv.Key}' is empty"));
        Assert.All(id, kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value), $"id '{kv.Key}' is empty"));
    }

    [Fact]
    public void Translations_KeepTheSamePlaceholders()
    {
        var en = Loc.ReadAll(CultureInfo.InvariantCulture);
        var id = Loc.ReadAll(Loc.Indonesian);

        foreach (var (key, value) in en)
        {
            var expected = Placeholder().Matches(value).Select(m => m.Value).Order().ToList();
            var actual = Placeholder().Matches(id[key]).Select(m => m.Value).Order().ToList();
            Assert.True(expected.SequenceEqual(actual), $"'{key}' placeholders differ: [{string.Join(",", expected)}] vs [{string.Join(",", actual)}]");
        }
    }

    [Fact]
    public void EveryKeyUsedInCodeAndXaml_Exists()
    {
        var keys = Loc.ReadAll(CultureInfo.InvariantCulture).Keys.ToHashSet();
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "PowerOptionsManager.Wpf"));
        var used = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".xaml") || f.EndsWith(".cs")) && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(f => KeyToken().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .ToHashSet();

        Assert.NotEmpty(used);
        Assert.All(used, k => Assert.Contains(k, keys));
    }

    [Theory]
    [InlineData("id-ID", "id-ID")]
    [InlineData("id", "id-ID")]
    [InlineData("en-US", "en-US")]
    [InlineData("ja-JP", "en-US")]
    [InlineData("", "en-US")]
    public void Detect_UsesIndonesianOnlyForIndonesianWindows(string system, string expected)
    {
        Assert.Equal(expected, Loc.Detect(CultureInfo.GetCultureInfo(system)).Name);
    }

    [Fact]
    public void SetLanguage_SwitchesStrings_AndNotifiesBindings()
    {
        var loc = Loc.Instance;
        var changed = new List<string?>();
        PropertyChangedEventHandler handler = (_, e) => changed.Add(e.PropertyName);
        loc.PropertyChanged += handler;
        try
        {
            loc.SetLanguage("id-ID");
            Assert.Equal("Batal", loc["Btn_Cancel"]);
            Assert.Contains("Item[]", changed);

            loc.SetLanguage("en-US");
            Assert.Equal("Cancel", loc["Btn_Cancel"]);
        }
        finally
        {
            loc.PropertyChanged -= handler;
            loc.SetLanguage("en-US");
        }
    }

    [Fact]
    public void MissingKey_IsVisibleInsteadOfThrowing()
    {
        Assert.Equal("[No_Such_Key]", Loc.Instance["No_Such_Key"]);
    }
}

public class AppSettingsServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pom-tests-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "settings.json");

    [Fact]
    public void MissingFile_GivesDefaults()
    {
        var service = new AppSettingsService(FilePath);

        Assert.Equal(new AppSettings(), service.Current);
    }

    [Fact]
    public void Save_RoundTrips()
    {
        new AppSettingsService(FilePath).Save(new AppSettings { Language = "id-ID", Theme = AppTheme.Dark });

        var reloaded = new AppSettingsService(FilePath);

        Assert.Equal("id-ID", reloaded.Current.Language);
        Assert.Equal(AppTheme.Dark, reloaded.Current.Theme);
        Assert.Contains("\"Dark\"", File.ReadAllText(FilePath));
    }

    [Fact]
    public void CorruptFile_GivesDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ not json");

        Assert.Equal(new AppSettings(), new AppSettingsService(FilePath).Current);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
