using System.IO;

namespace Hangline.Shell;

/// <summary>
/// A small append-only log next to the settings. A tray app has no console, so
/// this is the only way to see why a screenshot did not hang.
/// </summary>
public static class Log
{
    private static readonly object Gate = new();
    private const long MaxBytes = 300_000;
    private static string? _path;

    public static string FilePath => _path ??= Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Hangline", "hangline.log");

    public static void Write(string message)
    {
        lock (Gate)
        {
            try
            {
                string dir = Path.GetDirectoryName(FilePath)!;
                Directory.CreateDirectory(dir);
                File.AppendAllText(FilePath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
                var info = new FileInfo(FilePath);
                if (info.Length > MaxBytes) Trim(info);
            }
            catch (IOException)
            {
                // Logging must never be the reason the app stops.
            }
        }
    }

    private static void Trim(FileInfo info)
    {
        try
        {
            string[] lines = File.ReadAllLines(FilePath);
            int keep = Math.Max(200, lines.Length / 2);
            File.WriteAllLines(FilePath, lines[^(Math.Min(keep, lines.Length))..]);
        }
        catch (IOException) { }
    }

    public static void Error(string where, Exception ex) => Write($"ERROR {where}: {ex}");
}
