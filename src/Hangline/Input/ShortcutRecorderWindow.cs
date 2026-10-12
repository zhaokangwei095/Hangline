using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Hangline.Core;
using Localization = Hangline.Shell.Localization;

namespace Hangline.Input;

/// <summary>
/// Captures one key combination and tries to claim it.
/// <para>
/// A combination that another app already owns is refused and the window stays
/// open, because the alternative — accepting it and leaving a shortcut that
/// silently does nothing — is the failure mode users cannot diagnose.
/// </summary>
public sealed class ShortcutRecorderWindow : Window
{
    private readonly TextBlock _prompt = new()
    {
        FontSize = 14,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextAlignment = TextAlignment.Center,
        Margin = new Thickness(0, 0, 0, 0),
    };

    private readonly TextBlock _hint = new()
    {
        FontSize = 11,
        Foreground = new SolidColorBrush(Color.FromRgb(150, 152, 156)),
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(0, 10, 0, 0),
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
    };

    private readonly Func<Shortcut, bool> _tryApply;
    private readonly Action _done;

    public ShortcutRecorderWindow(Shortcut current, Func<Shortcut, bool> tryApply, Action done)
    {
        _tryApply = tryApply;
        _done = done;

        Title = Localization.T("Shortcut");
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Width = 360;
        Height = 150;
        Background = new SolidColorBrush(Color.FromRgb(246, 246, 248));

        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18) };
        panel.Children.Add(_prompt);
        panel.Children.Add(_hint);
        Content = panel;

        _prompt.Text = Localization.T("Press the new shortcut");
        _hint.Text = current.IsEmpty
            ? Localization.T("Backspace removes it.")
            : string.Format(CultureInfo.CurrentUICulture, "{0}    " + Localization.T("Backspace removes it."), current.Display);

        KeyDown += OnKey;
        // No close-on-deactivate: at startup the window loses activation the
        // instant it appears, and mid-recording an Alt-Tab would throw the
        // user's keystrokes away. Escape is the way out.
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape) { Close(); return; }
        if (key is Key.Back or Key.Delete)
        {
            // OnClosed runs the callback, so the menu rebuilds either way.
            _tryApply(Shortcut.None);
            Close();
            return;
        }

        // Modifier keys on their own are not a shortcut yet.
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        int vk = KeyInterop.VirtualKeyFromKey(key);
        HotKeyModifiers modifiers = HotKeyModifiers.None;
        if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= HotKeyModifiers.Control;
        if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= HotKeyModifiers.Alt;
        if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= HotKeyModifiers.Shift;

        var candidate = new Shortcut(vk, modifiers, Shortcut.LabelFor(vk));
        if (!candidate.IsValid)
        {
            _prompt.Text = Localization.T("Hold Ctrl, Alt or Shift with it");
            return;
        }

        if (_tryApply(candidate))
        {
            Close();
            return;
        }

        _prompt.Text = string.Format(CultureInfo.CurrentUICulture,
            Localization.T("{0} is already used by another app"), candidate.Display);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _done();
    }
}
