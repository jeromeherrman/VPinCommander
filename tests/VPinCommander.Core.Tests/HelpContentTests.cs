using VPinCommander.Core.Help;
using Xunit;

namespace VPinCommander.Core.Tests;

public class HelpContentTests
{
    [Fact]
    public void Topics_are_present_and_well_formed()
    {
        Assert.NotEmpty(HelpContent.Topics);
        foreach (var topic in HelpContent.Topics)
        {
            Assert.False(string.IsNullOrWhiteSpace(topic.Title), "every topic needs a title");
            Assert.NotEmpty(topic.Blocks);
            Assert.All(topic.Blocks, b => Assert.False(string.IsNullOrWhiteSpace(b.Text), $"empty block in '{topic.Title}'"));
        }
    }

    [Fact]
    public void Topic_titles_are_unique()
    {
        var titles = HelpContent.Topics.Select(t => t.Title).ToList();
        Assert.Equal(titles.Count, titles.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Includes_a_getting_started_topic()
    {
        Assert.Contains(HelpContent.Topics, t => t.Title.Contains("start", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Links_point_at_the_project()
    {
        Assert.Contains("jeromeherrman/VPinCommander", HelpContent.UserGuideUrl);
        Assert.Contains("jeromeherrman/VPinCommander", HelpContent.IssuesUrl);
    }
}
