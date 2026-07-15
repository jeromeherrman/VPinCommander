using VPinCommander.Core.Updates;
using Xunit;

namespace VPinCommander.Core.Tests;

public class AppUpdateParserTests
{
    private const string SampleRelease = """
        {
          "tag_name": "v9.9.9",
          "html_url": "https://github.com/example/repo/releases/tag/v9.9.9",
          "assets": [
            { "name": "VPinCommander-v9.9.9-android.apk", "browser_download_url": "https://example/apk", "size": 100 },
            { "name": "VPinCommander-v9.9.9-win-x64.zip", "browser_download_url": "https://example/zip", "size": 12345 }
          ]
        }
        """;

    [Fact]
    public void Parses_tag_zip_asset_and_release_page()
    {
        var update = AppUpdateParser.Parse(SampleRelease);

        Assert.NotNull(update);
        Assert.Equal(new Version(9, 9, 9), update!.Latest);
        Assert.Equal("v9.9.9", update.TagName);
        Assert.Equal("https://example/zip", update.ZipUrl); // apk must not be picked
        Assert.Equal(12345, update.ZipSize);
        Assert.Equal("https://github.com/example/repo/releases/tag/v9.9.9", update.ReleasePageUrl);
    }

    [Fact]
    public void Release_without_windows_zip_still_parses_with_null_url()
    {
        var update = AppUpdateParser.Parse("""{ "tag_name": "v1.2.3", "assets": [] }""");

        Assert.NotNull(update);
        Assert.Equal(new Version(1, 2, 3), update!.Latest);
        Assert.Null(update.ZipUrl);
    }

    [Theory]
    [InlineData("v0.5.1", 0, 5, 1)]
    [InlineData("0.5.1", 0, 5, 1)]
    [InlineData("V2.0.0", 2, 0, 0)]
    public void Tags_parse_with_or_without_v_prefix(string tag, int major, int minor, int build)
    {
        Assert.Equal(new Version(major, minor, build), AppUpdateParser.ParseTag(tag));
    }

    [Fact]
    public void Garbage_tags_yield_null()
    {
        Assert.Null(AppUpdateParser.ParseTag("latest"));
        Assert.Null(AppUpdateParser.ParseTag(""));
        Assert.Null(AppUpdateParser.ParseTag(null));
    }

    [Theory]
    [InlineData("0.5.1", "0.5.2", true)]
    [InlineData("0.5.1", "0.6.0", true)]
    [InlineData("0.5.1", "1.0.0", true)]
    [InlineData("0.5.1", "0.5.1", false)]
    [InlineData("0.5.2", "0.5.1", false)]
    public void Newer_comparison(string current, string latest, bool expected)
    {
        Assert.Equal(expected, AppUpdateParser.IsNewer(Version.Parse(current), Version.Parse(latest)));
    }

    [Fact]
    public void Revision_noise_is_not_an_update()
    {
        // Assembly versions carry a .0 revision; the tag does not.
        Assert.False(AppUpdateParser.IsNewer(Version.Parse("0.5.1.0"), Version.Parse("0.5.1")));
    }
}
