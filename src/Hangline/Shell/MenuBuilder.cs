using Forms = System.Windows.Forms;


namespace Hangline.Shell;

/// <summary>
/// Builds the tray menu. <see cref="Populate"/> refills the same instance and
/// the tray calls it on every right-click, so a checkmark can never go stale —
/// the original does the same with <c>menuNeedsUpdate</c>. (NotifyIcon never
/// raises the menu's own Opening event, hence rebuilding on the press.)
/// Items whose behaviour needs a part of the app that is not wired yet are
/// left out rather than shown disabled, so nothing in here lies.
/// </summary>
public static class MenuBuilder
{
    public static Forms.ContextMenuStrip Build(AppServices app)
    {
        // The check mark is drawn in the image margin, so that margin has to
        // stay for a tick to be visible at all; ShowCheckMargin keeps it narrow.
        var menu = new Forms.ContextMenuStrip { ShowImageMargin = false, ShowCheckMargin = true };
        Populate(menu, app);
        return menu;
    }

    public static void Populate(Forms.ContextMenuStrip menu, AppServices app)
    {
        menu.Items.Clear();
        var s = app.Settings;
        string T(string english) => Localization.T(english);

        Check(menu.Items, T("Line is down"), app.Line is { Revealed: true },
            T("Show or hide the line; the shortcut does the same"),
            _ => app.ToggleLine(), enabled: app.Line is not null);

        Add(menu.Items, T("Take everything down"),
            enabled: app.Line is not null && app.Line.LiveCount > 0,
            onClick: () => app.Line?.TakeEverythingDown());

        Check(menu.Items, T("Handle screenshots"), s.InboxEnabled,
            T("Screenshots hang instantly and skip the Desktop"),
            on => app.SetInboxEnabled(on));

        Check(menu.Items, T("Hang copied images"), s.ClipboardEnabled,
            T("Images you copy hang on the line too"),
            on => app.SetClipboardEnabled(on));

        // These three are independent switches, not a choice: the original
        // shows them as three checkmarks too. Radio buttons here would lie.
        var bring = Submenu(menu.Items, T("Bring the line down"));
        Check(bring.DropDownItems, T("When the pointer rests at the top edge"), !s.RevealOnEdgeOff, null,
            on => { s.RevealOnEdgeOff = !on; app.ApplyRevealToggles(); });
        Check(bring.DropDownItems, T("When something new hangs"), s.RevealOnNewHang, null,
            on => { s.RevealOnNewHang = on; app.ApplyRevealToggles(); });
        Check(bring.DropDownItems, T("Over full screen apps"), s.ShowOverFullScreen, null,
            on => { s.ShowOverFullScreen = on; app.ApplyRevealToggles(); });

        var watch = Submenu(menu.Items, T("Also watch folders"));
        foreach (string folder in s.ExtraFolders)
        {
            string captured = folder;
            string name = Path.GetFileName(captured.TrimEnd('\\')) is { Length: > 0 } n ? n : captured;
            var item = Add(watch.DropDownItems, name, onClick: () => app.OpenInExplorer(captured));
            item.ToolTipText = captured;
        }
        if (s.ExtraFolders.Count > 0) watch.DropDownItems.Add(new Forms.ToolStripSeparator());
        // Three is what the original allows; a line that could only be clicked
        // to fail is left out instead of shown disabled.
        if (s.ExtraFolders.Count < 3)
            Add(watch.DropDownItems, T("Add Folder…"), onClick: app.AddExtraFolder);
        if (s.ExtraFolders.Count > 0)
        {
            var stop = Submenu(watch.DropDownItems, T("Stop Watching"));
            foreach (string folder in s.ExtraFolders)
            {
                string captured = folder;
                string name = Path.GetFileName(captured.TrimEnd('\\')) is { Length: > 0 } n ? n : captured;
                Add(stop.DropDownItems, name, onClick: () => app.RemoveExtraFolder(captured));
            }
        }

        var keep = Submenu(menu.Items, T("Keep on line"));
        Radio(keep, T("As many as fit"), s.KeepChoice is null, on => app.SetKeepOnLine(0));
        foreach (int n in new[] { 25, 50, 100 })
        {
            int captured = n;
            Radio(keep, Localization.TFormat("{0} photos", n), s.KeepChoice == n,
                  on => app.SetKeepOnLine(captured));
        }

        var size = Submenu(menu.Items, T("Size"));
        Radio(size, T("Small"), s.LineSize == "small", on => app.SetLineSize("small"));
        Radio(size, T("Medium"), s.LineSize == "medium", on => app.SetLineSize("medium"));
        Radio(size, T("Large"), s.LineSize == "large", on => app.SetLineSize("large"));

        // Submenu() adds itself to the menu, so this lands after "Size".
        var shortcut = Submenu(menu.Items, T("Shortcut"));
        shortcut.DropDownItems.Add(new Forms.ToolStripMenuItem(app.ShortcutDisplay) { Enabled = false });
        Add(shortcut.DropDownItems, T("Change Shortcut…"), () => app.RecordShortcut());

        menu.Items.Add(new Forms.ToolStripSeparator());
        Add(menu.Items, T("Open screenshots folder"), app.OpenInboxFolder);
        Check(menu.Items, T("Sounds"), !s.SoundOff, null, on => app.SetSounds(on));
        Check(menu.Items, T("Open at login"), app.AutostartIsOn, null, on => app.SetAutostart(on));
        menu.Items.Add(new Forms.ToolStripSeparator());
        Add(menu.Items, T("Settings…"), () => app.ShowSettings());
        Add(menu.Items, T("Quit"), app.Quit);
    }

    // MARK: - helpers

    private static Forms.ToolStripMenuItem Add(Forms.ToolStripItemCollection items, string text,
        Action onClick, bool enabled = true, string? tip = null)
    {
        var item = new Forms.ToolStripMenuItem(text)
        {
            Enabled = enabled,
            ToolTipText = tip ?? string.Empty,
        };
        item.Click += (_, _) =>
        {
            try { onClick(); }
            catch (Exception ex) { Log.Error($"menu “{text}”", ex); }
        };
        items.Add(item);
        return item;
    }

    private static void Check(Forms.ToolStripItemCollection items, string text, bool isChecked,
        string? tip, Action<bool> onChange, bool enabled = true)
    {
        var item = new Forms.ToolStripMenuItem(text)
        {
            Checked = isChecked,
            Enabled = enabled,
            ToolTipText = tip ?? string.Empty,
        };
        item.Click += (_, _) =>
        {
            bool next = !item.Checked;
            try
            {
                onChange(next);
                item.Checked = next;
            }
            catch (Exception ex) { Log.Error($"menu “{text}”", ex); }
        };
        items.Add(item);
    }

    private static Forms.ToolStripMenuItem Submenu(Forms.ToolStripItemCollection items, string text)
    {
        var item = new Forms.ToolStripMenuItem(text);
        items.Add(item);
        return item;
    }

    /// Radios share one parent: choosing one unchecks the rest, and the action
    /// is saved by the callback, not by the widget.
    private static void Radio(Forms.ToolStripMenuItem parent, string text, bool isChecked, Action<bool> onChange)
    {
        var item = new Forms.ToolStripMenuItem(text) { Checked = isChecked };
        item.Click += (_, _) =>
        {
            foreach (Forms.ToolStripItem sibling in parent.DropDownItems)
            {
                if (sibling is Forms.ToolStripMenuItem other) other.Checked = ReferenceEquals(other, item);
            }
            try { onChange(true); }
            catch (Exception ex) { Log.Error($"menu “{text}”", ex); }
        };
        parent.DropDownItems.Add(item);
    }
}
