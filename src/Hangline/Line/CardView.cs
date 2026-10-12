using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hangline.Core;

namespace Hangline.Line;

/// <summary>
/// One photo with its clothespin. All the charm lives here: it drops onto the
/// line, swings, sways with the breeze and falls when you pull it off.
/// <para>
/// Everything is drawn in <see cref="OnRender"/> rather than composed from
/// child elements, because the line can be on screen for hours and a transparent
/// topmost window pays for every element it hosts.
/// </para>
/// </summary>
public sealed class CardView : FrameworkElement
{
    private readonly RotateTransform _swing = new();

    public PeggedItem Item { get; }
    public CardMotion Motion { get; set; } = new();

    /// Where the rope sits at this card's x, in surface coordinates.
    public double RopeY { get; set; }

    public double EdgeFade { get; set; } = 1;
    public bool InInbox { get; set; } = true;
    public bool Copied { get; set; }

    public CardView(PeggedItem item)
    {
        Item = item;
        RenderTransform = _swing;
        IsHitTestVisible = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
    }

    /// The photo fits inside the card area keeping its proportions, so the
    /// border hugs it whether the screenshot is wide or tall.
    public Rect CardRect(LineSize size)
    {
        SizeD s = Layout.CardSize(Item.NaturalSize, size);
        double w = Layout.CardWidth(size);
        return new Rect((w - s.Width) / 2, Layout.CardOffsetBelowTop, s.Width, s.Height);
    }

    /// Where this card's clickable area is, in surface coordinates.
    public Rect HitRect(LineSize size)
    {
        Rect card = CardRect(size);
        return new Rect(Canvas.GetLeft(this) + card.X, Canvas.GetTop(this) + card.Y, card.Width, card.Height);
    }

    /// Push the numbers the motion controller produced into what gets drawn.
    public void ApplyMotion(LineSize size)
    {
        _swing.Angle = Motion.Swing + Item.Tilt;
        _swing.CenterX = Layout.CardWidth(size) / 2;
        _swing.CenterY = 0;
        Opacity = (Item.IsFalling || Item.IsFlying ? 0 : 1) * EdgeFade * Motion.Opacity;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        LineSize size = LineSize;
        double width = Layout.CardWidth(size);
        Clothespin.Draw(dc, new Point(width / 2, 0));

        Rect card = CardRect(size);
        double scale = Motion.Scale;
        var transform = new ScaleTransform(scale, scale, width / 2, card.Top);
        dc.PushTransform(transform);

        bool hovered = Motion.TargetScale() > 1.01;
        GlassFrame.DrawShadow(dc, card, hovered);
        GlassFrame.Draw(dc, card, Layout.FrameRadius);

        Rect photo = GlassFrame.PhotoRect(card);
        if (Item.Thumb is ImageSource image)
        {
            GlassFrame.PushPhotoClip(dc, photo);
            dc.DrawImage(image, photo);
            dc.Pop();
        }
        Badges.StrokePhoto(dc, photo);

        if (Item.IsRecording)
            Badges.DrawPlay(dc, new Point(card.X + card.Width / 2, card.Y + card.Height / 2), 1);

        // The corner badge fades and pops in over its own clock — the progress
        // lives in the controller so the frame loop knows to keep going.
        double badge = Ease.OutQuad(Motion.Badge);
        if (badge > 0.001)
        {
            var center = new Point(card.X + 3 + Badges.CrossDiameter / 2, card.Y + 3 + Badges.CrossDiameter / 2);
            double s = 0.6 + 0.4 * badge;
            dc.PushTransform(new ScaleTransform(s, s, center.X, center.Y));
            if (InInbox) Badges.DrawCross(dc, center, badge);
            else Badges.DrawEyeSlash(dc, center, badge);
            dc.Pop();
        }

        dc.Pop();
    }

    /// Set by the surface, which owns the size tier.
    public LineSize LineSize { get; set; } = LineSize.Medium;
}
