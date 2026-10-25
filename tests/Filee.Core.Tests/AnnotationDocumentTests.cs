// fork: the Annotate editor's marks (Core/Editing/AnnotationDocument.cs).

using Filee.Core.Editing;

namespace Filee.Core.Tests;

public class AnnotationDocumentTests
{
    private const string Orange = "#FF5200";

    private static Annotation Callout(double x, double y) => new(AnnotationKind.Callout, x, y, x, y, Orange);

    [Fact]
    public void A_drag_too_short_draws_no_arrow_but_text_and_callouts_need_none()
    {
        var document = new AnnotationDocument();

        Assert.Equal(-1, document.Add(new Annotation(AnnotationKind.Arrow, 0.5, 0.5, 0.502, 0.5, Orange)));
        Assert.Equal(0, document.Add(new Annotation(AnnotationKind.Text, 0.5, 0.5, 0.5, 0.5, Orange, "Hi")));
        Assert.Equal(1, document.Add(Callout(0.2, 0.2)));
    }

    [Fact]
    public void Callouts_are_numbered_in_order_and_renumber_after_a_delete()
    {
        var document = new AnnotationDocument();
        document.Add(Callout(0.1, 0.1));
        document.Add(new Annotation(AnnotationKind.Rectangle, 0, 0, 0.5, 0.5, Orange));
        document.Add(Callout(0.3, 0.3));
        document.Add(Callout(0.6, 0.6));

        Assert.Equal([1, 0, 2, 3], Enumerable.Range(0, 4).Select(document.NumberOf));
        document.Remove(0);
        Assert.Equal([0, 1, 2], Enumerable.Range(0, 3).Select(document.NumberOf));
    }

    [Fact]
    public void Undo_and_redo_walk_the_changes_and_a_new_change_ends_redo()
    {
        var document = new AnnotationDocument();
        document.Add(Callout(0.1, 0.1));
        document.Add(Callout(0.2, 0.2));
        document.Replace(1, Callout(0.2, 0.2) with { Color = "#000000" });

        Assert.True(document.Undo());
        Assert.Equal(Orange, document.Marks[1].Color);
        Assert.True(document.Undo());
        Assert.Single(document.Marks);
        Assert.True(document.Redo());
        Assert.Equal(2, document.Marks.Count);

        document.Remove(0);
        Assert.False(document.CanRedo);
        Assert.True(document.Undo() && document.Undo() && document.Undo());
        Assert.False(document.Undo()); // past the start: nothing more
        Assert.Empty(document.Marks);
    }

    [Fact]
    public void A_click_picks_the_mark_drawn_last_near_it()
    {
        var document = new AnnotationDocument();
        document.Add(new Annotation(AnnotationKind.Rectangle, 0.1, 0.1, 0.5, 0.5, Orange));
        document.Add(new Annotation(AnnotationKind.Arrow, 0, 0.9, 1, 0.9, Orange));
        document.Add(Callout(0.3, 0.3));

        Assert.Equal(2, document.At(0.3, 0.3, 1000, 500));
        Assert.Equal(0, document.At(0.45, 0.45, 1000, 500));
        Assert.Equal(1, document.At(0.7, 0.91, 1000, 500)); // 5 px off the line
        Assert.Equal(-1, document.At(0.8, 0.5, 1000, 500));
    }

    [Fact]
    public void Marks_stay_on_the_picture()
    {
        var document = new AnnotationDocument();
        document.Add(new Annotation(AnnotationKind.Ellipse, -0.5, 0.2, 1.5, 0.4, Orange));

        Assert.Equal((0.0, 1.0), (document.Marks[0].X1, document.Marks[0].X2));
    }

    [Fact]
    public void An_arrow_head_points_back_along_the_line()
    {
        var (left, right) = AnnotationGeometry.ArrowHead(0, 0, 100, 0, 20);

        Assert.True(left.X < 100 && right.X < 100);
        Assert.Equal(-left.Y, right.Y, 6);
        Assert.Equal(5, AnnotationGeometry.DistanceToSegment(50, 5, 0, 0, 100, 0), 6);
    }
}
