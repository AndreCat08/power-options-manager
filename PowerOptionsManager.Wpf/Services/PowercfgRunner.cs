using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace PowerOptionsManager.Services;

public sealed record PowercfgResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Succeeded => ExitCode == 0;

    /// <summary>The error message powercfg printed (stderr, or stdout when stderr is empty).</summary>
    public string ErrorText => (string.IsNullOrWhiteSpace(StdErr) ? StdOut : StdErr).Trim();

    /// <summary>
    /// True when the failure is a missing-privilege error that an elevated retry can fix:
    /// ERROR_ACCESS_DENIED (0x5) or ERROR_PRIVILEGE_NOT_HELD (0x522). The hex codes are
    /// locale-independent; the English/Indonesian phrases are a fallback.
    /// </summary>
    public bool IsAccessDenied =>
        !Succeeded && (
            ErrorText.Contains("(0x522)", StringComparison.OrdinalIgnoreCase) ||
            ErrorText.Contains("(0x5)", StringComparison.OrdinalIgnoreCase) ||
            ErrorText.Contains("access is denied", StringComparison.OrdinalIgnoreCase) ||
            ErrorText.Contains("privilege", StringComparison.OrdinalIgnoreCase) ||
            ErrorText.Contains("akses ditolak", StringComparison.OrdinalIgnoreCase));
}

public enum PowercfgErrorKind
{
    Failed,
    ElevationCancelled,
}

public sealed class PowercfgException : Exception
{
    public PowercfgErrorKind Kind { get; }

    public PowercfgException(PowercfgErrorKind kind, string message, Exception? inner = null)
        : base(message, inner) => Kind = kind;
}

public interface IPowercfgRunner
{
    /// <summary>Runs powercfg unelevated and captures its output.</summary>
    Task<PowercfgResult> RunAsync(IReadOnlyList<string> args, CancellationToken ct = default);

    /// <summary>
    /// Runs powercfg through a UAC prompt. Output cannot be captured, so only the
    /// exit code is returned. Throws <see cref="PowercfgException"/> with
    /// <see cref="PowercfgErrorKind.ElevationCancelled"/> when the user declines.
    /// </summary>
    Task<int> RunElevatedAsync(IReadOnlyList<string> args, CancellationToken ct = default);
}

public sealed partial class PowercfgRunner : IPowercfgRunner
{
    private const string PowercfgExe = "powercfg.exe";
    private const int ErrorCancelled = 1223;

    // powercfg writes in the console's OEM code page, not UTF-8.
    private static readonly Encoding OutputEncoding = CreateOemEncoding();

    public async Task<PowercfgResult> RunAsync(IReadOnlyList<string> args, CancellationToken ct = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = PowercfgExe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = OutputEncoding,
            StandardErrorEncoding = OutputEncoding,
        };
        foreach (var arg in args) startInfo.ArgumentList.Add(arg);

        using var process = Process.Start(startInfo)
            ?? throw new PowercfgException(PowercfgErrorKind.Failed, "powercfg.exe could not be started.");

        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        return new PowercfgResult(process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
    }

    public async Task<int> RunElevatedAsync(IReadOnlyList<string> args, CancellationToken ct = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = PowercfgExe,
            Arguments = string.Join(' ', args.Select(QuoteArgument)),
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new PowercfgException(PowercfgErrorKind.Failed, "powercfg.exe could not be started.");
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            return process.ExitCode;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            throw new PowercfgException(PowercfgErrorKind.ElevationCancelled, "Administrator permission was not granted.", ex);
        }
    }

    /// <summary>Quotes one argument using the Windows command-line rules (CommandLineToArgvW).</summary>
    public static string QuoteArgument(string arg)
    {
        if (arg.Length > 0 && arg.IndexOfAny([' ', '\t', '"']) < 0) return arg;

        var sb = new StringBuilder("\"");
        var backslashes = 0;
        foreach (var c in arg)
        {
            if (c == '\\') { backslashes++; continue; }
            if (c == '"') sb.Append('\\', backslashes * 2 + 1).Append('"');
            else sb.Append('\\', backslashes).Append(c);
            backslashes = 0;
        }
        sb.Append('\\', backslashes * 2).Append('"');
        return sb.ToString();
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetOEMCP();

    private static Encoding CreateOemEncoding()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding((int)GetOEMCP());
        }
        catch (Exception)
        {
            return Encoding.UTF8;
        }
    }
}
