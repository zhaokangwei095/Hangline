using Hangline.Core;
using Xunit;

namespace Hangline.Tests;

public class LineRulesTests
{
    private static PeggedItem Item(string path, bool falling = false) =>
        new() { Path = path, IsFalling = falling };

    [Fact]
    public void The_same_file_twice_is_one_photo()
    {
        var items = new List<PeggedItem> { Item(@"C:\s\a.png") };
        Assert.True(LineRules.AlreadyHung(items, @"c:\S\A.PNG"));
        Assert.False(LineRules.AlreadyHung(items, @"C:\s\b.png"));
    }

    [Fact]
    public void A_photo_that_is_falling_off_does_not_block_a_new_one()
    {
        var items = new List<PeggedItem> { Item(@"C:\s\a.png", falling: true) };
        Assert.False(LineRules.AlreadyHung(items, @"C:\s\a.png"));
    }

    [Fact]
    public void A_full_line_lets_exactly_one_photo_go()
    {
        var items = new List<PeggedItem>();
        for (int i = 0; i < 8; i++) items.Add(Item($@"C:\s\{i}.png"));

        Assert.False(LineRules.IsFull(items, 8));
        items.Add(Item(@"C:\s\8.png"));
        Assert.True(LineRules.IsFull(items, 8));
        Assert.Equal(@"C:\s\0.png", LineRules.OldestLive(items)?.Path);
    }

    [Fact]
    public void Falling_photos_do_not_count_towards_capacity()
    {
        var items = new List<PeggedItem>
        {
            Item(@"C:\s\0.png", falling: true),
            Item(@"C:\s\1.png"),
        };
        Assert.Equal(1, LineRules.LiveCount(items));
        Assert.False(LineRules.IsFull(items, 1));
    }

    [Fact]
    public void Lowering_keep_on_line_empties_from_the_oldest_end()
    {
        var items = new List<PeggedItem>();
        for (int i = 0; i < 10; i++) items.Add(Item($@"C:\s\{i}.png"));

        IReadOnlyList<PeggedItem> over = LineRules.OverCapacity(items, 8);
        Assert.Equal(2, over.Count);
        Assert.Equal(@"C:\s\0.png", over[0].Path);
        Assert.Equal(@"C:\s\1.png", over[1].Path);
    }

    [Fact]
    public void Photos_whose_files_vanished_are_the_ones_to_drop()
    {
        var items = new List<PeggedItem>
        {
            Item(@"C:\s\gone.png"),
            Item(@"C:\s\here.png"),
            Item(@"C:\s\falling.png", falling: true),
        };
        IReadOnlyList<PeggedItem> missing = LineRules.MissingFiles(items, p => p.EndsWith("here.png"));

        Assert.Single(missing);
        Assert.Equal(@"C:\s\gone.png", missing[0].Path);
    }

    [Fact]
    public void Neighbours_split_around_a_photo_ignoring_the_falling_ones()
    {
        var a = Item(@"C:\s\a.png");
        var b = Item(@"C:\s\b.png", falling: true);
        var c = Item(@"C:\s\c.png");
        var d = Item(@"C:\s\d.png");
        var items = new List<PeggedItem> { a, b, c, d };

        Assert.Equal(new[] { a }, LineRules.Neighbours(items, c.Id, toTheLeft: true));
        Assert.Equal(new[] { d }, LineRules.Neighbours(items, c.Id, toTheLeft: false));
        Assert.Empty(LineRules.Neighbours(items, Guid.NewGuid(), toTheLeft: true));
    }

    [Fact]
    public void Scrolling_stops_at_the_oldest_photo()
    {
        // 12 photos, 8 visible: four can be scrolled into view.
        Assert.Equal(0, LineRules.ClampScroll(-50, 12, 8, 174), 1);
        Assert.Equal(4 * 174, LineRules.ClampScroll(99_999, 12, 8, 174), 1);
        Assert.Equal(200, LineRules.ClampScroll(200, 12, 8, 174), 1);
    }

    [Fact]
    public void Nothing_can_be_scrolled_when_everything_fits() =>
        Assert.Equal(0, LineRules.ClampScroll(500, 5, 8, 174), 1);

    [Fact]
    public void Gaps_take_the_pointer_only_when_there_is_somewhere_to_scroll()
    {
        // Keep on line set and more photos than fit: a wheel gesture anywhere
        // along the line moves it, gaps included.
        Assert.True(LineRules.ScrollBandTakesPointer(count: 30, visible: 8, keepOnLine: 50));
        // "As many as fit" never overflows, so the gaps stay click-through.
        Assert.False(LineRules.ScrollBandTakesPointer(count: 30, visible: 8, keepOnLine: null));
        Assert.False(LineRules.ScrollBandTakesPointer(count: 6, visible: 8, keepOnLine: 50));
    }

    [Fact]
    public void Restoring_skips_missing_files_and_duplicates_but_not_capacity()
    {
        string[] stored =
        [
            @"C:\s\gone.png",
            @"C:\s\a.png",
            @"C:\S\A.PNG",
            @"C:\s\b.png",
        ];
        IReadOnlyList<string> kept = LineRules.RestorablePaths(stored, p => !p.EndsWith("gone.png"));

        Assert.Equal(new[] { @"C:\s\a.png", @"C:\s\b.png" }, kept);
    }

    [Fact]
    public void A_falling_photo_is_not_saved_and_comes_back_as_hung_again()
    {
        var items = new List<PeggedItem>
        {
            Item(@"C:\s\a.png"),
            Item(@"C:\s\b.png", falling: true),
        };
        Assert.Equal(new[] { @"C:\s\a.png" }, LineRules.PathsToSave(items));
    }

    [Fact]
    public void Capacity_follows_keep_on_line_only_upwards()
    {
        Assert.Equal(8, Layout.MaxItems(8, null));
        Assert.Equal(25, Layout.MaxItems(8, 25));
        // Asking for fewer than fit does not shrink the line below what fits.
        Assert.Equal(8, Layout.MaxItems(8, 3));
    }
}
