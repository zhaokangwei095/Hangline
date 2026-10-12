using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Hangline.Core;

namespace Hangline.Shell;

/// <summary>
/// The tray menu's items as a window, plus the shortcuts page.
/// <para>
/// Everything applies the moment it is touched and goes through the same
/// setters the tray calls, so the two surfaces cannot drift and there is no
/// "Save" button to forget. Controls read their state once, on open; the tray
/// rebuilds itself on every right-click, and this window is rebuilt the same
/// way by closing and reopening it.
/// </para>
/// </summary>
public sealed class SettingsWindow : Window
{
    private static readonly Brush Dim = new SolidColorBrush(Color.FromRgb(110, 116, 126));
    private static readonly Brush Rule = new SolidColorBrush(Color.FromRgb(223, 226, 231));
    private static readonly FontFamily Face = new("Microsoft YaHei UI, Segoe UI");

    private readonly AppServices _app;

    /// <param name="tab">Which page to open on; the dev switch passes one so a
    /// script can screenshot the second page without clicking.</param>
    public SettingsWindow(AppServices app, int tab = 0)
    {
        _app = app;

        Title = T("Hangline Settings");
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.CanResize;
        ShowInTaskbar = true;
        Width = 500;
        Height = 720;
        MinWidth = 440;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = Face;
        Background = new SolidColorBrush(Color.FromRgb(246, 246, 248));

        var tabs = new TabControl { Margin = new Thickness(12), SelectedIndex = Math.Max(0, tab) };
        tabs.Items.Add(new TabItem { Header = T("General"), Content = Scrolled(General()) });
        tabs.Items.Add(new TabItem { Header = T("Shortcuts"), Content = Scrolled(ShortcutPage()) });
        Content = tabs;
    }

    private static string T(string english) => Localization.T(english);

    private static FrameworkElement Scrolled(StackPanel body) => new ScrollViewer
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        Padding = new Thickness(16, 14, 16, 14),
        Content = body,
    };

    // MARK: - 常规

    private StackPanel General()
    {
        var s = _app.Settings;
        var body = new StackPanel();

        body.Children.Add(Header(T("The line")));
        body.Children.Add(Row(T("Line is down"), T("Show or hide the line; the shortcut does the same"),
            Check(_app.Line is { Revealed: true }, on => _app.ToggleLine(), enabled: _app.Line is not null)));
        body.Children.Add(Row(T("Take everything down"), T("Photos in Hangline's folder go to the Recycle Bin; others are only taken off the line"),
            Push(T("Take down now"), () => _app.Line?.TakeEverythingDown(),
                enabled: _app.Line is not null && _app.Line.LiveCount > 0)));

        body.Children.Add(Header(T("Where screenshots come from")));
        body.Children.Add(Row(T("Handle screenshots"),
            T("Takes over the Print Screen key and reads the folders Windows saves screenshots to"),
            Check(s.InboxEnabled, _app.SetInboxEnabled)));
        body.Children.Add(Row(T("Hang copied images"), T("Images you copy hang on the line too"),
            Check(s.ClipboardEnabled, _app.SetClipboardEnabled)));

        body.Children.Add(Header(T("Bring the line down")));
        body.Children.Add(Row(T("When the pointer rests at the top edge"), null,
            Check(!s.RevealOnEdgeOff, on => { s.RevealOnEdgeOff = !on; _app.ApplyRevealToggles(); })));
        body.Children.Add(Row(T("When something new hangs"), null,
            Check(s.RevealOnNewHang, on => { s.RevealOnNewHang = on; _app.ApplyRevealToggles(); })));
        body.Children.Add(Row(T("Over full screen apps"), null,
            Check(s.ShowOverFullScreen, on => { s.ShowOverFullScreen = on; _app.ApplyRevealToggles(); })));

        body.Children.Add(Header(T("How much hangs")));
        body.Children.Add(Row(T("Keep on line"), T("More than fit are reached with the mouse wheel"),
            Radio("keep", new[] { T("As many as fit"), Localization.TFormat("{0} photos", 25), Localization.TFormat("{0} photos", 50), Localization.TFormat("{0} photos", 100) },
                new[] { 0, 25, 50, 100 }, s.KeepOnLine, _app.SetKeepOnLine)));
        body.Children.Add(Row(T("Size"), T("Only the photos and the room they take change"),
            Radio("size", new[] { T("Small"), T("Medium"), T("Large") },
                new[] { 0, 1, 2 }, s.LineSize switch { "small" => 0, "large" => 2, _ => 1 },
                index => _app.SetLineSize(index == 0 ? "small" : index == 2 ? "large" : "medium"))));

        body.Children.Add(Header(T("The app")));
        body.Children.Add(Row(T("Sounds"), null, Check(!s.SoundOff, _app.SetSounds)));
        body.Children.Add(Row(T("Open at login"), null, Check(_app.AutostartIsOn, _app.SetAutostart)));
        body.Children.Add(Row(T("Open screenshots folder"), T("Where Hangline's own captures land"),
            Push(T("Open"), () => _app.OpenInboxFolder())));

        return body;
    }

    // MARK: - 快捷键

    private StackPanel ShortcutPage()
    {
        var body = new StackPanel();

        body.Children.Add(Note(T("Press a combination in the recorder. PrtSc and the F keys may stand alone; anything else needs Ctrl, Alt or Shift, or it would swallow your typing.")));

        body.Children.Add(Header(T("The line")));
        body.Children.Add(ShortcutRow(AppServices.Toggle, T("Show or hide the line")));

        body.Children.Add(Header(T("Screenshots Hangline takes")));
        body.Children.Add(ShortcutRow(AppServices.Region, T("Region capture")));
        body.Children.Add(ShortcutRow(AppServices.Fullscreen, T("Full-screen capture")));
        body.Children.Add(ShortcutRow(AppServices.Window, T("Active window")));

        body.Children.Add(Header(T("Print Screen")));
        body.Children.Add(Row(T("Handle screenshots"), T("While this is on, Hangline registers the Print Screen key and a press starts a region capture"),
            Check(_app.Settings.InboxEnabled, _app.SetInboxEnabled)));
        body.Children.Add(Note(_app.SnapshotStatus()));

        return body;
    }

    private FrameworkElement ShortcutRow(string name, string label) => Row(
        label,
        null,
        new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                new TextBlock
                {
                    Text = _app.ShortcutDisplayFor(name),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0),
                    FontFamily = new FontFamily("Consolas, Microsoft YaHei UI"),
                },
                Push(T("Change…"), () => _app.RecordShortcut(name)),
                Push(T("Back to default"), () => _app.Rebind(name, _app.DefaultFor(name))),
            },
        });

    // MARK: - pieces

    private static FrameworkElement Header(string text) => new Border
    {
        Margin = new Thickness(0, 14, 0, 2),
        BorderBrush = Rule,
        BorderThickness = new Thickness(0, 1, 0, 0),
        Padding = new Thickness(0, 10, 0, 0),
        Child = new TextBlock
        {
            Text = text,
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = Dim,
        },
    };

    private static FrameworkElement Note(string text) => new TextBlock
    {
        Text = text,
        FontSize = 11.5,
        Foreground = Dim,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 6, 0, 6),
    };

    private static FrameworkElement Row(string title, string? note, FrameworkElement control)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0),
        };
        text.Children.Add(new TextBlock { Text = title, FontSize = 13, TextWrapping = TextWrapping.Wrap });
        if (note is not null)
        {
            text.Children.Add(new TextBlock
            {
                Text = note,
                FontSize = 11,
                Foreground = Dim,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 1, 0, 0),
            });
        }

        Grid.SetColumn(text, 0);
        Grid.SetColumn(control, 1);
        grid.Children.Add(text);
        grid.Children.Add(control);
        return grid;
    }

    private static FrameworkElement Check(bool isChecked, Action<bool> onChange, bool enabled = true)
    {
        var box = new CheckBox { IsChecked = isChecked, IsEnabled = enabled, VerticalAlignment = VerticalAlignment.Center };
        box.Click += (_, _) => onChange(box.IsChecked == true);
        return box;
    }

    private static FrameworkElement Radio(string group, string[] labels, int[] values, int current, Action<int> onChoose)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        for (int i = 0; i < labels.Length; i++)
        {
            int value = values[i];
            var radio = new RadioButton
            {
                Content = labels[i],
                GroupName = group,
                IsChecked = value == current,
                Margin = new Thickness(0, 0, 12, 0),
            };
            radio.Checked += (_, _) => { if (value != current) onChoose(value); };
            panel.Children.Add(radio);
        }
        return panel;
    }

    private static FrameworkElement Push(string text, Action onClick, bool enabled = true)
    {
        var button = new Button
        {
            Content = text,
            IsEnabled = enabled,
            Padding = new Thickness(10, 3, 10, 3),
            Margin = new Thickness(0, 0, 6, 0),
        };
        button.Click += (_, _) => onClick();
        return button;
    }
}
