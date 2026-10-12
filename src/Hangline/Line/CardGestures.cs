using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Hangline.Core;
using Hangline.Shell;

namespace Hangline.Line;

/// <summary>
/// What a click, a hold, a drag or a right-click on one photo means.
/// <para>
/// The order matters and mirrors the original: a press starts the long-press
/// clock, four points of movement hands over to the drag, and only a release
/// that did neither counts as "copy this".
/// </para>
/// </summary>
public sealed class CardGestures
{
    private const double DragSlop = 4;
    private const double HoldSeconds = 0.45;

    private readonly CardView _view;
    private readonly LineWindow _window;
    private readonly LineModel _model;
    private readonly MotionController _motion;
    private readonly DispatcherTimer _hold;

    private Point _down;
    private bool _armed;
    private bool _held;
    private bool _dragging;

    public CardGestures(CardView view, LineWindow window)
    {
        _view = view;
        _window = window;
        _model = window.Model;
        _motion = window.Motion;

        _hold = new DispatcherTimer { Interval = TimeSpan.FromSeconds(HoldSeconds) };
        _hold.Tick += (_, _) =>
        {
            _hold.Stop();
            if (!_armed || _dragging) return;
            _held = true;
            Release();
            OnLongPress();
        };

        view.MouseLeftButtonDown += OnDown;
        view.MouseMove += OnMove;
        view.MouseLeftButtonUp += OnUp;
        view.MouseRightButtonUp += (_, _) => ShowMenu();
        // The closest Windows has to a force click: WPF has no middle-button
        // event on UIElement, so the tunneling press is filtered by button.
        view.PreviewMouseDown += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.Middle) Actions.QuickLook(_model, Item);
        };
        view.MouseWheel += OnWheel;
        view.MouseLeave += (_, _) => { _hold.Stop(); if (_armed) Release(); };
    }

    private PeggedItem Item => _view.Item;

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        // The corner cross is checked before anything else, so a discard is never
        // mistaken for a copy.
        if (HitCross(e))
        {
            _model.Discard(Item.Id);
            _window.RefreshAfterGesture();
            return;
        }

        _down = e.GetPosition(_view);
        _armed = true;
        _held = false;
        _dragging = false;
        _model.PressedId = Item.Id;
        _motion.Press(Item.Id, true);
        _window.StartMotion();

        if (e.ClickCount == 2)
        {
            Release();
            Actions.Open(Item);
            return;
        }

        _hold.Start();
        e.Handled = true;
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (!_armed || _dragging || e.LeftButton != MouseButtonState.Pressed) return;
        Point now = e.GetPosition(_view);
        if (Math.Sqrt((now.X - _down.X) * (now.X - _down.X) + (now.Y - _down.Y) * (now.Y - _down.Y)) < DragSlop) return;

        _dragging = true;
        _hold.Stop();
        Release();
        DragOut();
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (!_armed) return;
        bool wasHold = _held;
        bool wasDrag = _dragging;
        Release();
        if (wasHold || wasDrag) return;

        if (Actions.Copy(Item))
        {
            _model.CopiedId = Item.Id;
            _motion.Nudge(Item.Id, 3);
            _window.StartMotion();
            _model.ClearCopiedFlag();
        }
    }

    private void Release()
    {
        _armed = false;
        _hold.Stop();
        if (_model.PressedId == Item.Id) _model.PressedId = null;
        _motion.Press(Item.Id, false);
        _window.StartMotion();
    }

    /// Long press: the original hands the photo to the system Markup editor,
    /// which has no Windows twin. Until the in-app editor is done this opens
    /// Paint, and the card refreshes itself when the file changes on disk.
    private void OnLongPress()
    {
        if (Item.IsRecording)
        {
            Actions.Open(Item);
            return;
        }
        Actions.EditExternally(Item);
        _window.WatchForEdit(Item);
    }

    /// Dragging out is done by the shell: dropping on a folder moves the file,
    /// dropping on another app copies it, dropping on the Recycle Bin deletes it.
    /// Afterwards the line simply notices what is no longer where it was.
    private void DragOut()
    {
        var data = new DataObject();
        data.SetData(DataFormats.FileDrop, new[] { Item.Path });
        _model.DraggingId = Item.Id;
        _view.InvalidateVisual();

        try
        {
            DragDrop.DoDragDrop(_view, data, DragDropEffects.Copy | DragDropEffects.Move);
        }
        catch (Exception ex)
        {
            Log.Write($"drag: {ex.Message}");
        }
        finally
        {
            _model.DraggingId = null;
            // Explorer finishes its move a moment after the drop returns.
            AppServices.Delay(0.6, () => _model.Prune());
            _window.RefreshAfterGesture();
        }
    }

    /// The cross is drawn 3pt inside the card's top-left corner; the hit box is
    /// a little larger than the badge, as in the original's 26pt target.
    private bool HitCross(MouseButtonEventArgs e)
    {
        Point p = e.GetPosition(_view);
        Rect card = _view.CardRect(_model.Size);
        const double reach = Badges.CrossHit + Layout.FrameInset;
        return p.X <= card.X + reach && p.Y <= card.Y + reach;
    }

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        _model.ScrollBy(e.Delta < 0 ? 12 : -12);
        e.Handled = true;
    }

    // MARK: - context menu

    private void ShowMenu()
    {
        var menu = new ContextMenu { PlacementTarget = _view, Placement = PlacementMode.Bottom };
        string T(string s) => Shell.Localization.T(s);

        Add(menu, T("Copy"), () => { if (Actions.Copy(Item)) { _model.CopiedId = Item.Id; _model.ClearCopiedFlag(); } });
        Add(menu, T("Open"), () => Actions.Open(Item));
        Add(menu, T("Quick Look"), () => Actions.QuickLook(_model, Item));
        Add(menu, T("Markup"), OnLongPress);
        Add(menu, T("Show in folder"), () => Actions.Reveal(Item));

        if (_model.IsInInbox(Item.Id))
            Add(menu, T("Save to Desktop"), () =>
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (Actions.SaveTo(Item, desktop)) _model.Prune();
            });

        menu.Items.Add(new Separator());

        Add(menu, _model.IsInInbox(Item.Id) ? T("Discard") : T("Take down"), () =>
        {
            _model.Discard(Item.Id);
            _window.RefreshAfterGesture();
        });
        Add(menu, T("Take down everything to the left"), () =>
        {
            _model.TakeDownNeighbours(Item.Id, toTheLeft: true);
            _window.RefreshAfterGesture();
        }, _model.NeighboursLeft(Item.Id));
        Add(menu, T("Take down everything to the right"), () =>
        {
            _model.TakeDownNeighbours(Item.Id, toTheLeft: false);
            _window.RefreshAfterGesture();
        }, _model.NeighboursRight(Item.Id));

        _window.MenuOpen = true;
        menu.Closed += (_, _) => { _window.MenuOpen = false; _window.RefreshAfterGesture(); };
        menu.IsOpen = true;
    }

    private static void Add(ContextMenu menu, string text, Action onClick, bool enabled = true)
    {
        var item = new MenuItem { Header = text, IsEnabled = enabled };
        item.Click += (_, _) =>
        {
            try { onClick(); }
            catch (Exception ex) { Log.Error($"menu “{text}”", ex); }
        };
        menu.Items.Add(item);
    }
}
