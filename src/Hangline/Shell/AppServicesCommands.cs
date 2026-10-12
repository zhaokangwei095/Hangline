using System.Diagnostics;
using Hangline.Capture;

namespace Hangline.Shell;

/// <summary>
/// Everything the tray menu can ask for, in one place, so the menu builder stays
/// declarative and the actions stay testable by hand.
/// </summary>
public sealed partial class AppServices
{
    public string ShortcutDisplay => ToggleKey.Current.Display;

    public void ToggleLine()
    {
        if (Line is null) return;
        Line.Toggle();
        Tray.RebuildMenu();
    }

    public void SetInboxEnabled(bool on)
    {
        Settings.InboxEnabled = on;
        Save();
        Watchers?.Apply();
    }

    /// The three "when to bring the line down" switches all land here.
    public void ApplyRevealToggles()
    {
        Save();
        ApplyRevealSettings();
    }

    public void SetClipboardEnabled(bool on)
    {
        Settings.ClipboardEnabled = on;
        Clipboards.IsEnabled = on;
        Save();
        Tray.RefreshIcon();
    }

    public void SetSounds(bool on)
    {
        Settings.SoundOff = !on;
        Save();
    }

    public void SetKeepOnLine(int n)
    {
        if (Settings.KeepOnLine == n) return;
        Settings.KeepOnLine = n;
        Model.KeepOnLine = Settings.KeepChoice;
        Save();
        Line?.ApplyLayoutChanges();
        Tray.RebuildMenu();
    }

    public void SetLineSize(string size)
    {
        if (Settings.LineSize == size) return;
        Settings.LineSize = size;
        Save();
        SetLineSizeCore(size);
        Line?.ApplyLayoutChanges();
    }

    public void OpenInboxFolder()
    {
        Inbox.Ensure();
        OpenInExplorer(Inbox.Folder);
    }

    public void OpenInExplorer(string path)
    {
        if (!Directory.Exists(path) && !File.Exists(path)) return;
        Start(new ProcessStartInfo("explorer.exe", EscapeArg(path)) { UseShellExecute = true });
    }

    public void RevealInFolder(string filePath) =>
        Start(new ProcessStartInfo("explorer.exe", $"/select,{EscapeArg(filePath)}") { UseShellExecute = true });

    /// explorer.exe parses its own command line, so quoting rather than ArgumentList.
    private static string EscapeArg(string path) => $"\"{path}\"";

    private static void Start(ProcessStartInfo info)
    {
        try { Process.Start(info); }
        catch (Exception ex) { Log.Error("open", ex); }
    }

    public void AddExtraFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = Localization.T("Add Folder…"),
            Multiselect = true,
        };
        if (dialog.ShowDialog() != true) return;

        foreach (string folder in dialog.FolderNames)
        {
            if (Settings.ExtraFolders.Count >= 3) break;
            if (!Directory.Exists(folder)) continue;
            if (!Settings.ExtraFolders.Any(f => string.Equals(f, folder, StringComparison.OrdinalIgnoreCase)))
                Settings.ExtraFolders.Add(folder);
        }
        Save();
        Watchers?.Apply();
        Tray.RebuildMenu();
    }

    public void ClearExtraFolders()
    {
        Settings.ExtraFolders.Clear();
        Save();
        Watchers?.Apply();
        Tray.RebuildMenu();
    }

    public bool AutostartIsOn => Autostart.IsEnabled();

    public void SetAutostart(bool on)
    {
        Autostart.Set(on);
        Tray.RebuildMenu();
    }

    public void Quit() => System.Windows.Application.Current.Shutdown();
}
