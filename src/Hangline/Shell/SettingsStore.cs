using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hangline.Shell;

/// <summary>
/// User settings. Field names follow the original app's stored keys where they
/// still say something true on Windows; where the original named a macOS
/// concept that has no Windows twin, the comment records the rename.
/// </summary>
public sealed class Settings
{
    /// Paths still hanging when Hangline last quit.
    public List<string> Pegged { get; set; } = [];

    /// 0 = as many as fit across the screen; otherwise one of 25 / 50 / 100.
    public int KeepOnLine { get; set; }

    /// "small" | "medium" | "large".
    public string LineSize { get; set; } = "medium";

    public bool SoundOff { get; set; }

    /// Screenshots Hangline takes itself land in its inbox folder instead of the desktop.
    public bool InboxEnabled { get; set; } = true;

    /// Images put on the clipboard are hung too.
    public bool ClipboardEnabled { get; set; }

    /// Up to three extra folders to watch.
    public List<string> ExtraFolders { get; set; } = [];

    public bool WatchPicturesFolder { get; set; } = true;
    public bool WatchDesktop { get; set; } = true;
    public bool WatchCaptures { get; set; } = true;

    /// Was "menuBarRevealOff": on Windows the trigger band is the top edge of the screen.
    public bool RevealOnEdgeOff { get; set; }

    /// The line slides down by itself when something new hangs.
    public bool RevealOnNewHang { get; set; } = true;

    public bool ShowOverFullScreen { get; set; }

    /// No sound when a photo hangs (still sounds when it falls).
    public bool QuietHang { get; set; }

    public ShortcutSettings? Shortcut { get; set; }

    /// The three capture shortcuts. An absent entry means the built-in default;
    /// an explicit empty one means the user turned that capture off.
    public ShortcutSettings? RegionShortcut { get; set; }
    public ShortcutSettings? FullscreenShortcut { get; set; }
    public ShortcutSettings? WindowShortcut { get; set; }

    /// "auto" follows Windows; "zh-Hans" and "en" force one.
    public string Language { get; set; } = "auto";

    /// 0 = as many as fit across the screen; otherwise one of 25 / 50 / 100.
    public int? KeepChoice => KeepOnLine is 25 or 50 or 100 ? KeepOnLine : null;

    /// An absent shortcut still means Ctrl+Alt+T; an explicit empty one means the
    /// user deleted it in the recorder, and that has to mean "off". Falling back
    /// to the default there left people holding a key that could not be
    /// registered at all, which looks exactly like a dead feature.
    public Core.Shortcut ToShortcut() => Shortcut switch
    {
        null => Core.Shortcut.Standard,
        { IsEmpty: true } => Core.Shortcut.None,
        var s => new Core.Shortcut(s.VirtualKey, (Core.HotKeyModifiers)s.Modifiers, s.Key),
    };

    public void SetShortcut(Core.Shortcut shortcut) =>
        Shortcut = ToSettings(shortcut);

    public static ShortcutSettings ToSettings(Core.Shortcut shortcut) => new()
    {
        VirtualKey = shortcut.VirtualKey,
        Modifiers = (int)shortcut.Modifiers,
        Key = shortcut.Label,
    };

    /// An empty slot is the user's "off", not a broken value, so it is left
    /// alone; anything else that could not be registered is dropped back to
    /// the default rather than kept as a shortcut that does nothing.
    private static ShortcutSettings? Kept(ShortcutSettings? stored) =>
        stored is { IsEmpty: false } s &&
        !new Core.Shortcut(s.VirtualKey, (Core.HotKeyModifiers)s.Modifiers, s.Key).IsValid ? null : stored;

    public void Normalize()
    {
        if (LineSize is not ("small" or "medium" or "large")) LineSize = "medium";
        if (KeepOnLine is not (0 or 25 or 50 or 100)) KeepOnLine = 0;
        if (Language is not ("auto" or "zh-Hans" or "en")) Language = "auto";
        ExtraFolders.RemoveAll(string.IsNullOrWhiteSpace);
        if (ExtraFolders.Count > 3) ExtraFolders.RemoveRange(3, ExtraFolders.Count - 3);
        Pegged.RemoveAll(string.IsNullOrWhiteSpace);
        RegionShortcut = Kept(RegionShortcut);
        FullscreenShortcut = Kept(FullscreenShortcut);
        WindowShortcut = Kept(WindowShortcut);
    }
}

public sealed class ShortcutSettings
{
    [JsonPropertyName("vk")] public int VirtualKey { get; set; }
    [JsonPropertyName("modifiers")] public int Modifiers { get; set; }
    [JsonPropertyName("key")] public string? Key { get; set; }

    /// An empty entry means "no shortcut", matching the original.
    public bool IsEmpty => VirtualKey == 0;
}

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Folder { get; }
    public string FilePath { get; }

    public SettingsStore(string? folder = null)
    {
        Folder = folder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Hangline");
        FilePath = Path.Combine(Folder, "settings.json");
    }

    public Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string text = File.ReadAllText(FilePath);
                Settings? loaded = JsonSerializer.Deserialize<Settings>(text, Json);
                if (loaded is not null)
                {
                    loaded.Normalize();
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            // A broken settings file must never stop the line from hanging.
            Log.Write($"settings load failed: {ex.Message}");
        }
        return new Settings();
    }

    /// Atomic: write a temporary file, then move it over the target.
    public void Save(Settings settings)
    {
        settings.Normalize();
        string temp = FilePath + ".tmp";
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, Json));
            if (File.Exists(FilePath)) File.Move(temp, FilePath, true);
            else File.Move(temp, FilePath);
        }
        catch (Exception ex)
        {
            Log.Write($"settings save failed: {ex.Message}");
            try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { }
        }
    }
}
