namespace Hangline.Interop;

/// <summary>
/// File operations the shell owns: the Recycle Bin, and moves that do not
/// overwrite. Deleting through the shell rather than <c>File.Delete</c> is the
/// point — a discarded screenshot should come back from where it went.
/// </summary>
public static class FileOps
{
    /// Sends a file to the Recycle Bin with its normal restore information.
    /// <para>
    /// This goes through <c>Microsoft.VisualBasic.FileIO</c> rather than
    /// <c>IFileOperation</c> on purpose: it is the same shell path, it honours
    /// "confirm deletion" and undo, and it does not need a COM apartment dance
    /// in an app that already has enough moving parts. The assembly ships with
    /// the .NET runtime, so it costs nothing at install time.
    /// </para>
    public static bool Recycle(string path)
    {
        try
        {
            if (!System.IO.File.Exists(path)) return false;
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                path,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            return true;
        }
        catch (Exception ex)
        {
            Shell.Log.Write($"recycle failed {path}: {ex.Message}");
            return false;
        }
    }

    /// Move into a folder without clobbering: "name.png" becomes "name 2.png".
    public static string? MoveTo(string path, string folder)
    {
        try
        {
            string name = System.IO.Path.GetFileName(path);
            string target = Core.Naming.UniquePath(folder, name);
            System.IO.File.Move(path, target);
            return target;
        }
        catch (Exception ex)
        {
            Shell.Log.Write($"move failed {path}: {ex.Message}");
            return null;
        }
    }

    /// Write bytes in place, atomically, so an editor saving over a hanging
    /// photo never leaves the line looking at a half-written file.
    public static bool WriteAtomic(string path, byte[] bytes)
    {
        string temp = path + ".tmp";
        try
        {
            System.IO.File.WriteAllBytes(temp, bytes);
            System.IO.File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            Shell.Log.Write($"write failed {path}: {ex.Message}");
            try { if (System.IO.File.Exists(temp)) System.IO.File.Delete(temp); }
            catch (IOException) { }
            return false;
        }
    }
}
