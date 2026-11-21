using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// The player shows an item's title once, titles the tab "Item - Course - Site", and labels the progress bar with the
/// count of completed items.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed partial class PlayerLayoutTests
{
    private readonly LearningSiteFixture _fixture;

    public PlayerLayoutTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ThePlayer_ShowsTheTitleOnce_TitlesTheTab_AndLabelsTheProgress()
    {
        // Arrange
        using var learner = await _fixture.CreateClientAsync("learner1");

        // Act
        var page = await learner.GetStringAsync($"learning/courses/{_fixture.CourseId}/play/{_fixture.PageLessonId}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(HeadingOne().Matches(page));
        Assert.StartsWith("Fire exits - Workplace Safety - ", TitleTag().Match(page).Groups[1].Value.Trim());
        Assert.Matches(ProgressText(), page);
    }

    [GeneratedRegex(@"<h1[\s>][^<]*Fire exits", RegexOptions.IgnoreCase)]
    private static partial Regex HeadingOne();

    [GeneratedRegex(@"<title>([^<]*)</title>", RegexOptions.IgnoreCase)]
    private static partial Regex TitleTag();

    [GeneratedRegex(@"data-learning-progress-text[^>]*>\s*\d+ of \d+ complete")]
    private static partial Regex ProgressText();
}
