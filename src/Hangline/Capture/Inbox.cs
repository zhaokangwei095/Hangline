using Hangline.Core;

namespace Hangline.Capture;

/// <summary>
/// Where Hangline's own captures and clipboard images land. Screenshots saved
/// here are the ones the corner cross sends to the Recycle Bin — a file from
/// somewhere else is only taken off the line, never deleted.
/// </summary>
public static class Inbox
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Hangline");

    public static string Folder { get; } = Path.Combine(Root, "Screenshots");

    public static void Ensure() => Directory.CreateDirectory(Folder);

    public static bool IsInInbox(string path) => FileKind.IsInside(path, Folder);

    /// The name shown in the menu and the folder tip.
    public static string DisplayName => "Hangline";

    /// Moving a file in without clobbering an existing name.
    public static string Accept(string sourcePath, string fileName)
    {
        Ensure();
        string target = Naming.UniquePath(Folder, fileName);
        File.Move(sourcePath, target);
        return target;
    }

    public static string NewPath(string fileName)
    {
        Ensure();
        return Naming.UniquePath(Folder, fileName);
    }
}
