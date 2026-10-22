using System.Globalization;
using System.Text.RegularExpressions;
using PowerOptionsManager.Models;

namespace PowerOptionsManager.Services;

/// <summary>
/// Parses powercfg output without relying on its (localized) labels: it keys
/// on GUIDs, the "(name)" after a GUID, a trailing "*" for the active scheme,
/// and hex values for setting indexes.
/// </summary>
public static partial class PowercfgParser
{
    [GeneratedRegex(@"[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}")]
    private static partial Regex GuidRegex();

    [GeneratedRegex(@":\s*0x([0-9a-fA-F]+)\s*$")]
    private static partial Regex HexValueRegex();

    /// <summary>Parses <c>powercfg /list</c>.</summary>
    public static IReadOnlyList<PowerPlan> ParseList(string output)
    {
        var plans = new List<PowerPlan>();
        foreach (var raw in SplitLines(output))
        {
            var match = GuidRegex().Match(raw);
            if (!match.Success) continue;

            var line = raw.TrimEnd();
            var rest = line[(match.Index + match.Length)..];
            var isActive = rest.EndsWith('*');
            if (isActive) rest = rest[..^1].TrimEnd();

            var open = rest.IndexOf('(');
            var close = rest.LastIndexOf(')');
            var name = open >= 0 && close > open ? rest[(open + 1)..close].Trim() : string.Empty;

            plans.Add(new PowerPlan
            {
                Guid = match.Value.ToLowerInvariant(),
                Name = name,
                IsActive = isActive,
            });
        }
        return plans;
    }

    /// <summary>First GUID in the output (e.g. <c>/getactivescheme</c>), or null.</summary>
    public static string? ParseFirstGuid(string output)
    {
        var match = GuidRegex().Match(output);
        return match.Success ? match.Value.ToLowerInvariant() : null;
    }

    /// <summary>Last GUID in the output (e.g. the new scheme from <c>/duplicatescheme</c> or <c>/import</c>).</summary>
    public static string? ParseLastGuid(string output)
    {
        var matches = GuidRegex().Matches(output);
        return matches.Count > 0 ? matches[^1].Value.ToLowerInvariant() : null;
    }

    /// <summary>
    /// Parses <c>powercfg /query SCHEME SUB SETTING</c>. The current AC and DC
    /// indexes are always the last two hex-valued lines of the block.
    /// </summary>
    public static AcDc? ParseQuery(string output)
    {
        var values = new List<uint>();
        foreach (var line in SplitLines(output))
        {
            var match = HexValueRegex().Match(line);
            if (match.Success && uint.TryParse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            {
                values.Add(v);
            }
        }
        return values.Count >= 2 ? new AcDc(values[^2], values[^1]) : null;
    }

    private static IEnumerable<string> SplitLines(string output) =>
        output.Split('\n').Select(l => l.TrimEnd('\r'));
}
