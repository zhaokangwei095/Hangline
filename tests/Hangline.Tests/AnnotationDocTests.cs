using Hangline.Core;
using Hangline.Overlay;
using Xunit;

namespace Hangline.Tests;

/// <summary>
/// The editor's model, without a window: what one gesture costs in undo steps,
/// and that marks accumulate the way a person expects.
/// </summary>
public class AnnotationDocTests
{
    private static Stroke Stroke(AnnotationTool tool, params Pt[] points)
    {
        var stroke = new Stroke { Tool = tool };
        foreach (Pt p in points) stroke.Points.Add(p);
        return stroke;
    }

    [Fact]
    public void A_new_document_has_nothing_to_undo()
    {
        var doc = new AnnotationDoc();
        Assert.False(doc.CanUndo);
        Assert.False(doc.CanRedo);
        Assert.Empty(doc.Strokes);
    }

    [Fact]
    public void One_drag_is_one_undo_step()
    {
        var doc = new AnnotationDoc();
        doc.PushState();
        doc.Add(Stroke(AnnotationTool.Pen, new Pt(0, 0), new Pt(10, 5), new Pt(20, 0)));

        Assert.Single(doc.Strokes);
        Assert.True(doc.Undo());
        Assert.Empty(doc.Strokes);
        Assert.True(doc.Redo());
        Assert.Single(doc.Strokes);
    }

    [Fact]
    public void Undo_then_a_new_mark_clears_the_redo_stack()
    {
        var doc = new AnnotationDoc();
        doc.PushState();
        doc.Add(Stroke(AnnotationTool.Rectangle, new Pt(0, 0), new Pt(10, 10)));
        doc.Undo();
        Assert.True(doc.CanRedo);

        doc.PushState();
        doc.Add(Stroke(AnnotationTool.Arrow, new Pt(1, 1), new Pt(5, 5)));
        Assert.False(doc.CanRedo);
    }

    [Fact]
    public void Undo_stops_at_the_beginning_instead_of_empting_the_line()
    {
        var doc = new AnnotationDoc();
        Assert.False(doc.Undo());
        Assert.Empty(doc.Strokes);
    }

    [Fact]
    public void A_rectangle_keeps_two_corners_and_a_freehand_band_keeps_every_point()
    {
        var box = Stroke(AnnotationTool.Rectangle, new Pt(0, 0), new Pt(4, 4));
        box.Points[^1] = new Pt(9, 9);
        Assert.Equal(2, box.Points.Count);
        Assert.Equal(new Pt(9, 9), box.End);

        var band = Stroke(AnnotationTool.Pen, new Pt(0, 0));
        for (int i = 1; i < 30; i++) band.Points.Add(new Pt(i, i));
        Assert.Equal(30, band.Points.Count);
    }

    [Fact]
    public void Sixty_steps_is_enough_history_for_a_screenshot()
    {
        var doc = new AnnotationDoc();
        for (int i = 0; i < 200; i++)
        {
            doc.PushState();
            doc.Add(Stroke(AnnotationTool.Pen, new Pt(i, i)));
        }

        int undone = 0;
        while (doc.Undo()) undone++;
        Assert.Equal(60, undone);
    }
}
