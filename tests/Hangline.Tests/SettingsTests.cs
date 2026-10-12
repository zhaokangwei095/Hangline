using System.IO;
using Hangline.Core;
using Hangline.Shell;
using Xunit;

namespace Hangline.Tests;

public class SettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "hangline-tests-" + Guid.NewGuid().ToString("N"));

    public SettingsTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    [Fact]
    public void Settings_survive_a_round_trip()
    {
        var store = new SettingsStore(_dir);
        var settings = store.Load();
        settings.LineSize = "large";
        settings.KeepOnLine = 50;
        settings.ClipboardEnabled = true;
        settings.SoundOff = true;
        settings.ExtraFolders.Add(@"D:\shots");
        settings.Pegged.Add(@"C:\s\a.png");
        settings.SetShortcut(new Shortcut(0x42, HotKeyModifiers.Control | HotKeyModifiers.Alt, "B"));
        store.Save(settings);

        Settings again = store.Load();
        Assert.Equal("large", again.LineSize);
        Assert.Equal(50, again.KeepOnLine);
        Assert.True(again.ClipboardEnabled);
        Assert.True(again.SoundOff);
        Assert.Equal(new[] { @"D:\shots" }, again.ExtraFolders);
        Assert.Equal(new[] { @"C:\s\a.png" }, again.Pegged);
        Assert.Equal("Ctrl+Alt+B", again.ToShortcut().Display);
    }

    [Fact]
    public void Capture_shortcuts_round_trip_and_a_file_without_them_still_loads()
    {
        var store = new SettingsStore(_dir);
        Settings s = store.Load();
        s.RegionShortcut = Settings.ToSettings(
            new Shortcut(0x41, HotKeyModifiers.Control | HotKeyModifiers.Alt, "A"));
        s.WindowShortcut = Settings.ToSettings(Shortcut.None);   // the user turned it off
        store.Save(s);

        Settings again = store.Load();
        Assert.Equal("Ctrl+Alt+A", new Shortcut(
            again.RegionShortcut!.VirtualKey, (HotKeyModifiers)again.RegionShortcut.Modifiers,
            again.RegionShortcut.Key).Display);
        Assert.True(again.WindowShortcut!.IsEmpty);
        Assert.Null(again.FullscreenShortcut);   // never written → the built-in default applies
    }

    [Fact]
    public void Normalize_drops_a_shortcut_that_could_never_fire_but_keeps_a_deliberate_off()
    {
        var s = new Settings
        {
            // A bare letter with no modifier would swallow normal typing.
            RegionShortcut = new ShortcutSettings { VirtualKey = 0x41, Key = "A" },
            WindowShortcut = new ShortcutSettings(),
        };
        s.Normalize();

        Assert.Null(s.RegionShortcut);
        Assert.NotNull(s.WindowShortcut);
        Assert.True(s.WindowShortcut.IsEmpty);
    }

    [Fact]
    public void A_corrupt_settings_file_falls_back_to_defaults_instead_of_failing_to_start()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), "{ this is not json");
        Settings loaded = new SettingsStore(_dir).Load();
        Assert.Equal("medium", loaded.LineSize);
        Assert.False(loaded.ClipboardEnabled);
    }

    [Fact]
    public void Bad_stored_values_are_corrected_on_load()
    {
        var store = new SettingsStore(_dir);
        store.Save(new Settings { LineSize = "gigantic", KeepOnLine = 7, Language = "klingon" });

        Settings loaded = store.Load();
        Assert.Equal("medium", loaded.LineSize);
        Assert.Null(loaded.KeepChoice);
        Assert.Equal("auto", loaded.Language);
    }

    [Fact]
    public void Only_the_three_offered_counts_are_honoured()
    {
        foreach (int n in new[] { 25, 50, 100 })
            Assert.Equal(n, new Settings { KeepOnLine = n }.KeepChoice);

        Assert.Null(new Settings { KeepOnLine = 0 }.KeepChoice);
        Assert.Null(new Settings { KeepOnLine = 33 }.KeepChoice);
    }

    [Fact]
    public void Extra_folders_are_capped_at_three()
    {
        var settings = new Settings();
        settings.ExtraFolders.AddRange([@"C:\1", @"C:\2", @"C:\3", @"C:\4", @"C:\5"]);
        settings.Normalize();
        Assert.Equal(3, settings.ExtraFolders.Count);
    }

    [Fact]
    public void An_absent_shortcut_entry_still_means_control_alt_t() =>
        Assert.Equal("Ctrl+Alt+T", new Settings().ToShortcut().Display);

    [Fact]
    public void Saving_never_leaves_a_half_written_file()
    {
        var store = new SettingsStore(_dir);
        store.Save(new Settings { LineSize = "small" });
        Assert.False(File.Exists(Path.Combine(_dir, "settings.json.tmp")));
        Assert.Equal("small", store.Load().LineSize);
    }
}

public class DebouncerTests
{
    private sealed class FakeScheduler : IScheduler
    {
        private readonly List<Token> _tokens = [];

        public IDisposable Schedule(TimeSpan delay, Action action)
        {
            var token = new Token(action);
            _tokens.Add(token);
            return token;
        }

        /// Fire everything still pending, as a clock reaching their deadlines would.
        public void RunDue()
        {
            var due = _tokens.Where(t => !t.Cancelled).ToList();
            _tokens.Clear();
            foreach (Token token in due) token.Run();
        }

        public int Pending => _tokens.Count(t => !t.Cancelled);

        private sealed class Token(Action action) : IDisposable
        {
            public bool Cancelled { get; private set; }

            public void Run()
            {
                if (Cancelled) return;
                Cancelled = true;
                action();
            }

            public void Dispose() => Cancelled = true;
        }
    }

    [Fact]
    public void A_burst_of_events_rescans_once()
    {
        var scheduler = new FakeScheduler();
        int runs = 0;
        using var debouncer = new Debouncer(TimeSpan.FromMilliseconds(200), () => runs++, scheduler);

        for (int i = 0; i < 5; i++) debouncer.Trigger();
        Assert.Equal(1, scheduler.Pending);

        scheduler.RunDue();
        Assert.Equal(1, runs);
    }

    [Fact]
    public void Events_after_the_deadline_start_a_new_countdown()
    {
        var scheduler = new FakeScheduler();
        int runs = 0;
        using var debouncer = new Debouncer(TimeSpan.FromMilliseconds(200), () => runs++, scheduler);

        debouncer.Trigger();
        scheduler.RunDue();
        debouncer.Trigger();
        scheduler.RunDue();

        Assert.Equal(2, runs);
    }

    [Fact]
    public void Disposing_cancels_what_has_not_fired()
    {
        var scheduler = new FakeScheduler();
        int runs = 0;
        var debouncer = new Debouncer(TimeSpan.FromMilliseconds(200), () => runs++, scheduler);
        debouncer.Trigger();
        debouncer.Dispose();
        scheduler.RunDue();

        Assert.Equal(0, runs);
    }

}