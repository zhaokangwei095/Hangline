using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Hangline.Core;
using Hangline.Interop;
using Hangline.Line;
using Hangline.Shell;
using Localization = Hangline.Shell.Localization;

namespace Hangline.Overlay;

/// <summary>
/// Holding a recording trims it here: watch it, drag the two ends, save.
/// <para>
/// The cut itself is <see cref="Trimmer.TryTrim"/> — a stream copy, so a long
/// recording stays long-quality and the operation takes under a second. When
/// this machine has no ffmpeg to trim with, the window says so plainly and
/// offers to open the clip elsewhere instead of pretending it saved something.
/// </para>
/// </summary>
public sealed class TrimWindow : Window
{
    private readonly MediaElement _player = new() { LoadedBehavior = MediaState.Manual, Stretch = Stretch.Uniform };
    private readonly Slider _from = Slider();
    private readonly Slider _to = Slider();
    private readonly TextBlock _readout = new()
    {
        Foreground = Brushes.White,
        FontSize = 13,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private double _duration;
    private bool _playing;

    public TrimWindow(string path)
    {
        Path = path;

        Title = "Hangline";
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Background = new SolidColorBrush(Color.FromRgb(24, 24, 26));
        Width = 720;
        Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Grid stage = new();
        stage.Children.Add(_player);
        stage.Background = Brushes.Black;
        stage.Children.Add(_readout);
        Grid.SetRow(stage, 0);
        root.Children.Add(stage);

        root.Children.Add(Track(Localization.T("From"), _from, 1));
        root.Children.Add(Track(Localization.T("To"), _to, 2));

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
        };
        buttons.Children.Add(Button(Localization.T("Cancel"), () => Close()));
        buttons.Children.Add(Button(Trimmer.IsAvailable ? Localization.T("Save") : Localization.T("Open"), Save));
        Grid.SetRow(buttons, 3);
        root.Children.Add(buttons);

        Content = root;

        _player.MediaOpened += (_, _) =>
        {
            _duration = _player.NaturalDuration.HasTimeSpan ? _player.NaturalDuration.TimeSpan.TotalSeconds : 0;
            if (_duration <= 0) return;
            _from.Maximum = _duration;
            _to.Maximum = _duration;
            _to.Value = _duration;
            Update();

            // A black stage reads as "failed to load"; the clip starts playing so
            // the two ends can be dragged against something moving.
            _player.Play();
            _playing = true;
        };
        _player.MediaEnded += (_, _) => { _playing = false; };
        _from.ValueChanged += (_, _) => { ClampPair(); Update(); SeekTo(_from.Value); };
        _to.ValueChanged += (_, _) => { ClampPair(); Update(); };
        stage.MouseLeftButtonDown += (_, _) => TogglePlay();
        KeyDown += OnKey;

        _player.Source = new Uri(path);
        Closed += (_, _) => _player.Close();
    }

    public string Path { get; }
    public bool Saved { get; private set; }

    private static Slider Slider() => new() { Minimum = 0, IsMoveToPointEnabled = true, TickFrequency = 0.1 };

    private static FrameworkElement Track(string label, Slider slider, int row)
    {
        var panel = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });

        var name = new TextBlock { Text = label, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
        var value = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(190, 192, 196)),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        slider.ValueChanged += (_, _) => value.Text = TimeSpan.FromSeconds(slider.Value).ToString(@"hh\:mm\:ss\.f");
        value.Text = "0:00.0";

        Grid.SetColumn(name, 0);
        Grid.SetColumn(slider, 1);
        Grid.SetColumn(value, 2);
        panel.Children.Add(name);
        panel.Children.Add(slider);
        panel.Children.Add(value);
        Grid.SetRow(panel, row);
        return panel;
    }

    private static Button Button(string text, Action onClick)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(14, 6, 14, 6),
            Margin = new Thickness(6, 0, 0, 0),
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    private void ClampPair()
    {
        if (_from.Value > _to.Value - 0.2) _from.Value = Math.Max(0, _to.Value - 0.2);
    }

    private void Update()
    {
        _readout.Text = Trimmer.IsAvailable
            ? $"{Format(_from.Value)} → {Format(_to.Value)}"
            : Localization.T("No ffmpeg on this PC, so the clip cannot be cut here.");
        _readout.Visibility = _playing ? Visibility.Hidden : Visibility.Visible;
    }

    private static string Format(double seconds) => TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss\.f");

    private void TogglePlay()
    {
        if (_playing) { _player.Pause(); _playing = false; }
        else { _player.Play(); _playing = true; }
        Update();
    }

    private void SeekTo(double seconds)
    {
        if (!_playing) _player.Position = TimeSpan.FromSeconds(seconds);
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                break;
            case Key.Space:
                TogglePlay();
                break;
            case Key.Enter:
                Save();
                break;
        }
    }

    private async void Save()
    {
        if (!Trimmer.IsAvailable)
        {
            Actions.OpenPath(Path);
            return;
        }

        _player.Stop();
        bool ok = await Trimmer.TryTrim(Path, TimeSpan.FromSeconds(_from.Value), TimeSpan.FromSeconds(_to.Value));
        if (!ok)
        {
            _readout.Text = Localization.T("The trim failed; the file is unchanged.");
            return;
        }

        Saved = true;
        Close();
    }
}
