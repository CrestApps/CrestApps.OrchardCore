namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// The course page of a course that is not for sale leads with its own top block (summary, facts and Enroll or
/// Continue) and shows no product box; the outline follows.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class CoursePageTests
{
    private readonly LearningSiteFixture _fixture;

    public CoursePageTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AFreeCourse_ShowsNoProductBox_AndPutsTheActionAboveTheOutline()
    {
        // Arrange
        using var learner = await _fixture.CreateClientAsync("learner1");
        using var outsider = await _fixture.CreateClientAsync("learner2");

        // Act
        var enrolled = await learner.GetStringAsync($"Contents/ContentItems/{_fixture.CourseId}", TestContext.Current.CancellationToken);
        var notEnrolled = await outsider.GetStringAsync($"Contents/ContentItems/{_fixture.CourseId}", TestContext.Current.CancellationToken);

        // Assert
        foreach (var page in new[] { enrolled, notEnrolled })
        {
            Assert.DoesNotContain("product-summary", page);
            Assert.Contains("data-learning-course-hero", page);
            Assert.True(page.IndexOf("data-learning-course-hero", StringComparison.Ordinal) < page.IndexOf("learning-outline-", StringComparison.Ordinal));
        }

        Assert.Contains("learning/courses/" + _fixture.CourseId + "/play", enrolled);
    }
}
