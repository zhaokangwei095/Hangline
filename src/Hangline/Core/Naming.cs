using System.Globalization;

namespace Hangline.Core;

/// <summary>
/// File names for what the line takes in. Kept as data rather than looked up
/// from the UI resources, because a file name written today must still read
/// the same way in a language the user switches to tomorrow.
/// </summary>
public readonly record struct NameStyle(string Prefix, bool Meridiem, string Morning, string Afternoon)
{
    public static NameStyle Chinese { get; } = new("截屏", true, "上午", "下午");
    public static NameStyle English { get; } = new("Screenshot", false, "", "");

    public static NameStyle For(bool zhHans) => zhHans ? Chinese : English;
}

public static class Naming
{
    public const string Png = "png";

    public static string ScreenshotName(DateTime local, NameStyle style) =>
        $"{style.Prefix} {local:yyyy-MM-dd}{Stamp(local, style)}.png";

    public static string ClipboardName(DateTime local, NameStyle style) =>
        style.Meridiem
            ? $"剪贴板 {local:yyyy-MM-dd} {Meridiem(local, style)}{StampRaw(local, style)}.png"
            : $"Clipboard {local:yyyy-MM-dd} at {local:HH.mm:ss}.png".Replace(":", ".");

    public static string RecordingName(DateTime local, NameStyle style) =>
        $"{(style.Meridiem ? "录屏" : "Recording")} {local:yyyy-MM-dd}{Stamp(local, style)}.mp4";

    /// Chinese style reads "下午3.45.06" — no leading zero on the hour, and
    /// midnight/noon are both 12, not 0.
    private static string Stamp(DateTime local, NameStyle style) =>
        style.Meridiem ? $" {Meridiem(local, style)}{StampRaw(local, style)}" : $" at {local:HH.mm.ss}";

    private static string StampRaw(DateTime local, NameStyle style)
    {
        if (!style.Meridiem) return local.ToString("HH.mm.ss", CultureInfo.InvariantCulture);
        int h12 = local.Hour % 12;
        if (h12 == 0) h12 = 12;
        return $"{h12}.{local:mm.ss}";
    }

    private static string Meridiem(DateTime local, NameStyle style) =>
        local.Hour < 12 ? style.Morning : style.Afternoon;

    /// macOS-style " 2" suffix when the name is taken.
    public static string AlternateName(string fileName, int n)
    {
        string stem = System.IO.Path.GetFileNameWithoutExtension(fileName);
        string ext = System.IO.Path.GetExtension(fileName);
        return $"{stem} {n}{ext}";
    }

    /// Pure half of de-duplication: the sequence of candidate names.
    public static IEnumerable<string> Candidates(string fileName, int limit = 1000)
    {
        yield return fileName;
        for (int n = 2; n <= limit; n++) yield return AlternateName(fileName, n);
    }

    public static string UniquePath(string folder, string fileName, Func<string, bool> exists)
    {
        foreach (string candidate in Candidates(fileName))
        {
            string path = System.IO.Path.Combine(folder, candidate);
            if (!exists(path)) return path;
        }
        return System.IO.Path.Combine(folder, $"{Guid.NewGuid():N}.png");
    }

    public static string UniquePath(string folder, string fileName) =>
        UniquePath(folder, fileName, System.IO.File.Exists);
}
