using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Windows.Data;
using System.Windows.Markup;

namespace PowerOptionsManager.Services;

/// <summary>
/// UI strings from Resources/Strings.resx (English) and Strings.id-ID.resx (Indonesian).
/// The indexer raises PropertyChanged("Item[]") on a language switch, so every
/// {loc:Tr Key} binding updates live.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
    public static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");

    public static Loc Instance { get; } = new();

    private static readonly ResourceManager Resources =
        new("PowerOptionsManager.Resources.Strings", typeof(Loc).Assembly);

    private readonly CultureInfo _systemCulture = CultureInfo.CurrentUICulture;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;

    private Loc() => Culture = Detect(_systemCulture);

    public CultureInfo Culture { get; private set; }

    /// <summary>The language code the user picked ("" = follow Windows).</summary>
    public string SelectedCode { get; private set; } = string.Empty;

    public string this[string key] => Resources.GetString(key, Culture) ?? $"[{key}]";

    public string Format(string key, params object?[] args) => string.Format(Culture, this[key], args);

    public void SetLanguage(string? code)
    {
        SelectedCode = code ?? string.Empty;
        Culture = SelectedCode switch
        {
            "id-ID" => Indonesian,
            "en-US" => English,
            _ => Detect(_systemCulture),
        };
        CultureInfo.CurrentUICulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Indonesian when Windows uses Indonesian, otherwise English.</summary>
    public static CultureInfo Detect(CultureInfo systemCulture) =>
        systemCulture.TwoLetterISOLanguageName == "id" ? Indonesian : English;

    /// <summary>All keys in the neutral (English) resource set, for tests.</summary>
    public static IReadOnlyDictionary<string, string> ReadAll(CultureInfo culture)
    {
        var set = Resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false)
                  ?? throw new InvalidOperationException($"No resources for {culture.Name}.");
        return set.Cast<System.Collections.DictionaryEntry>()
                  .ToDictionary(e => (string)e.Key, e => e.Value as string ?? string.Empty);
    }
}

/// <summary>XAML shorthand for a live-updating localized string: <c>{loc:Tr App_Title}</c>.</summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension() { }

    public TrExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
