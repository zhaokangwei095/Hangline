using Hangline.Core;
using Xunit;

namespace Hangline.Tests;

public class NamingTests
{
    private static readonly DateTime Afternoon = new(2026, 10, 12, 15, 45, 6);

    [Fact]
    public void Chinese_names_read_like_the_system_ones() =>
        Assert.Equal("截屏 2026-10-12 下午3.45.06.png", Naming.ScreenshotName(Afternoon, NameStyle.Chinese));

    [Fact]
    public void English_names_use_a_24_hour_stamp() =>
        Assert.Equal("Screenshot 2026-10-12 at 15.45.06.png", Naming.ScreenshotName(Afternoon, NameStyle.English));

    [Theory]
    [InlineData(0, "上午12.00.00")]
    [InlineData(9, "上午9.00.00")]
    [InlineData(12, "下午12.00.00")]
    [InlineData(23, "下午11.00.00")]
    public void Midnight_and_noon_are_twelve_not_zero(int hour, string expected)
    {
        var at = new DateTime(2026, 10, 12, hour, 0, 0);
        Assert.Equal($"截屏 2026-10-12 {expected}.png", Naming.ScreenshotName(at, NameStyle.Chinese));
    }

    [Fact]
    public void A_taken_name_becomes_name_two()
    {
        string path = Naming.UniquePath(@"C:\s", "a.png", p => p != @"C:\s\a.png");
        Assert.Equal(@"C:\s\a.png", path);

        string taken = Naming.UniquePath(@"C:\s", "a.png", p => p.EndsWith("a.png"));
        Assert.EndsWith(@"a 2.png", taken);
    }

    [Fact]
    public void Candidates_are_numbered_the_way_macOS_numbers_them()
    {
        Assert.Equal(["a.png", "a 2.png", "a 3.png"], Naming.Candidates("a.png").Take(3));
        Assert.Equal("very long name 2.png", Naming.AlternateName("very long name.png", 2));
        Assert.Equal("noext 2", Naming.AlternateName("noext", 2));
    }

    [Fact]
    public void Clipboard_images_get_their_own_prefix() =>
        Assert.Equal("剪贴板 2026-10-12 下午3.45.06.png", Naming.ClipboardName(Afternoon, NameStyle.Chinese));
}

public class MediaKindTests
{
    [Theory]
    [InlineData(@"C:\a\screenshot.png", true)]
    [InlineData(@"C:\a\PHOTO.JPG", true)]
    [InlineData(@"C:\a\note.txt", false)]
    [InlineData(@"C:\a\clip.mp4", false)]
    public void Images_are_candidates_recordings_are_not(string path, bool expected) =>
        Assert.Equal(expected, FileKind.IsCandidate(path, anyImage: false, acceptsRecordings: false));

    [Theory]
    [InlineData(@"C:\a\clip.mp4")]
    [InlineData(@"C:\a\clip.mov")]
    public void Recordings_only_arrive_where_they_are_accepted(string path)
    {
        Assert.False(FileKind.IsCandidate(path, anyImage: true, acceptsRecordings: false));
        Assert.True(FileKind.IsCandidate(path, anyImage: true, acceptsRecordings: true));
    }

    [Fact]
    public void Windows_paths_are_compared_without_case()
    {
        Assert.True(FileKind.SamePath(@"C:\S\A.PNG", @"c:\s\a.png"));
        Assert.True(FileKind.SamePath(@"C:\s\a.png", @"C:\s\.\a.png"));
        Assert.False(FileKind.SamePath(@"C:\s\a.png", @"C:\s\b.png"));
    }

    [Fact]
    public void Inside_a_folder_means_inside_not_alongside()
    {
        Assert.True(FileKind.IsInside(@"C:\app\Screenshots\a.png", @"C:\app\Screenshots"));
        Assert.False(FileKind.IsInside(@"C:\app\ScreenshotsOther\a.png", @"C:\app\Screenshots"));
        Assert.False(FileKind.IsInside(@"C:\app\Screenshots", @"C:\app\Screenshots"));
    }
}
