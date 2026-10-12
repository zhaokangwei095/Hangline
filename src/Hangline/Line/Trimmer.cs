using System.Diagnostics;
using System.IO;
using Hangline.Shell;

namespace Hangline.Line;

/// <summary>
/// Cutting a screen recording down, the way the original trims with
/// <c>AVAssetExportSession(Passthrough)</c>: no re-encode, no quality loss.
/// <para>
/// Windows' own <c>VideoEditingService</c> is the same idea, but it is not in the
/// WinRT projection a .NET 8 WPF app can reach — the namespace simply is not
/// there, at 19041 or at 22621. So this uses ffmpeg when the machine already has
/// one, and never bundles or downloads it: without a copy on the box,
/// <see cref="Find"/> returns null and the caller falls back to opening the clip
/// where the user can trim it themselves.
/// </para>
/// </summary>
public static class Trimmer
{
    private static string? _probed;
    private static bool _probedOnce;

    /// The ffmpeg executable, if this machine has one. Cached: the search runs
    /// once per session, not once per clip.
    public static string? Find()
    {
        if (_probedOnce) return _probed;
        _probedOnce = true;

        foreach (string name in new[] { "ffmpeg.exe", "ffmpeg" })
        {
            foreach (string? dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                try
                {
                    string candidate = Path.Combine(dir.Trim(), name);
                    if (File.Exists(candidate)) { _probed = candidate; return _probed; }
                }
                catch (ArgumentException) { }
            }
        }

        _probed = null;
        return null;
    }

    public static bool IsAvailable => Find() is not null;

    /// <returns>true when the file was replaced by the trimmed copy.</returns>
    public static async Task<bool> TryTrim(string path, TimeSpan from, TimeSpan to)
    {
        string? ffmpeg = Find();
        if (ffmpeg is null) return false;

        string temp = path + ".trimming" + Path.GetExtension(path);

        try
        {
            // -ss before -i seeks by keyframes and copies the rest untouched,
            // which is what keeps this instant and lossless.
            var info = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = $"-y -hide_banner -loglevel error -ss {Seconds(from)} -to {Seconds(to)} -i \"{path}\" -c copy \"{temp}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(info);
            if (process is null) return false;

            // A long recording should not block the line for minutes.
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await process.WaitForExitAsync(timeout.Token);

            if (process.ExitCode != 0 || !File.Exists(temp) || new FileInfo(temp).Length == 0)
            {
                Log.Write($"trim exit {process.ExitCode} for {Path.GetFileName(path)}");
                return false;
            }

            File.Delete(path);
            File.Move(temp, path);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("trim", ex);
            return false;
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch (IOException) { }
        }
    }

    private static string Seconds(TimeSpan t) => t.TotalSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}
