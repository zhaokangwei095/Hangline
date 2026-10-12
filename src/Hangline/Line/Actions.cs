using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Hangline.Capture;
using Hangline.Core;
using Hangline.Interop;
using Hangline.Shell;

namespace Hangline.Line;

/// <summary>
/// The things a card can be asked to do. Kept apart from the gesture recognition
/// so the same actions can be reached from the context menu, the tray and a
/// future keyboard path without duplicating any of it.
/// </summary>
public static class Actions
{
    /// A screenshot is copied as both pixels and a file, because some apps want
    /// to paste an image and some want to paste an attachment.
    public static bool Copy(PeggedItem item)
    {
        try
        {
            // WPF's clipboard understands BitmapSource for DataFormats.Bitmap;
            // WinForms' does not, and silently puts nothing there.
            var data = new System.Windows.DataObject();
            if (!item.IsRecording)
            {
                BitmapSource image = Decode(item.Path);
                // WPF turns a BitmapSource on the clipboard into the DIB that
                // paint programs and Office both paste as an image.
                data.SetData(DataFormats.Bitmap, image);
            }
            data.SetData(DataFormats.FileDrop, new[] { item.Path });

            System.Windows.Clipboard.SetDataObject(data, true);
            return true;
        }
        catch (Exception ex)
        {
            // The clipboard is frequently held by another process; a failed copy
            // should be quiet, not a dialog.
            Log.Write($"copy: {ex.Message}");
            return false;
        }
    }

    public static void Open(PeggedItem item) => Start(item.Path, null);

    public static void Reveal(PeggedItem item) =>
        Start("explorer.exe", $"/select,\"{item.Path}\"");

    /// Quick Look: look at the photo without opening anything. Windows has no
    /// force click, so the line also answers a middle click with it.
    public static void QuickLook(LineModel model, PeggedItem item)
    {
        List<PeggedItem> live = model.Live().ToList();
        int at = live.FindIndex(p => p.Id == item.Id);
        Overlay.PreviewHost.Show(live, Math.Max(0, at));
    }

    /// Hold a photo: mark it up here, and the card swaps its picture in place
    /// when the edit is saved.
    public static void Annotate(PeggedItem item, Action<string> onSaved)
    {
        if (item.IsRecording)
        {
            Open(item);
            return;
        }

        try
        {
            var editor = new Overlay.AnnotatorWindow(item.Path);
            editor.Closed += (_, _) =>
            {
                if (editor.Saved) onSaved(item.Path);
            };
            editor.Show();
        }
        catch (Exception ex)
        {
            // A broken editor must not swallow the gesture: fall back to Paint.
            Log.Error("annotate", ex);
            EditExternally(item);
        }
    }

    /// The Windows stand-in for macOS's Markup until the in-app editor lands.
    public static void EditExternally(PeggedItem item) => Start("mspaint.exe", $"\"{item.Path}\"");

    public static bool SaveTo(PeggedItem item, string folder)
    {
        string? moved = FileOps.MoveTo(item.Path, folder);
        if (moved is null) return false;
        // The old path is gone; the line notices through the watcher, but say so
        // now so the card falls immediately rather than after a rescan.
        return true;
    }

    private static BitmapSource Decode(string path)
    {
        using var ms = new MemoryStream(File.ReadAllBytes(path));
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = ms;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static void Start(string fileName, string? arguments)
    {
        try
        {
            Process.Start(new ProcessStartInfo(fileName, arguments ?? "") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Write($"open {fileName}: {ex.Message}");
        }
    }
}
