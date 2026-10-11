namespace Hangline.Core;

/// <summary>File-kind rules shared by the watchers, the line and the shell.</summary>
public static class FileKind
{
    public static readonly string[] ImageExtensions =
        ["png", "jpg", "jpeg", "heic", "heif", "tif", "tiff", "gif", "webp", "bmp"];

    public static readonly string[] RecordingExtensions = ["mp4", "mov", "mkv", "wmv"];

    public static string ExtensionOf(string path) =>
        System.IO.Path.GetExtension(path).TrimStart('.').ToLowerInvariant();

    public static bool IsImage(string path) => Array.IndexOf(ImageExtensions, ExtensionOf(path)) >= 0;

    /// A screen recording rather than a screenshot.
    public static bool IsRecording(string path) => Array.IndexOf(RecordingExtensions, ExtensionOf(path)) >= 0;

    public static bool IsCandidate(string path, bool anyImage, bool acceptsRecordings)
    {
        if (IsRecording(path)) return acceptsRecordings;
        return anyImage || IsImage(path);
    }

    public static bool SamePath(string? a, string? b)
    {
        if (a is null || b is null) return a == b;
        return string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string path)
    {
        try { return System.IO.Path.GetFullPath(path).TrimEnd(System.IO.Path.DirectorySeparatorChar); }
        catch (ArgumentException) { return path; }
    }

    public static bool IsInside(string path, string folder)
    {
        string full = Normalize(path);
        string dir = Normalize(folder) + System.IO.Path.DirectorySeparatorChar;
        return full.StartsWith(dir, StringComparison.OrdinalIgnoreCase);
    }
}
