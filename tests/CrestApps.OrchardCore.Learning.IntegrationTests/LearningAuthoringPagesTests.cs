using System.Net;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// The authoring assistant page on a site without an AI model: it renders its four author prompts, says no model is set
/// up, and drafts nothing; people without the permission cannot open it.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class LearningAuthoringPagesTests
{
    private readonly LearningSiteFixture _site;

    public LearningAuthoringPagesTests(LearningSiteFixture site)
    {
        _site = site;
    }

    [Fact]
    public async Task Page_RendersTheFourPrompts_AndSaysNoModelIsSetUp()
    {
        using var admin = await _site.CreateClientAsync("admin");

        var response = await admin.GetAsync("Admin/learning/ai/authoring", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Draft a course outline", html, StringComparison.Ordinal);
        Assert.Contains("Draft questions from a lesson", html, StringComparison.Ordinal);
        Assert.Contains("Draft a rubric", html, StringComparison.Ordinal);
        Assert.Contains("Translate a lesson", html, StringComparison.Ordinal);
        Assert.Contains("No AI model is set up", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DraftWithoutAModel_SavesNothing()
    {
        using var admin = await _site.CreateClientAsync("admin");
        var page = await admin.GetStringAsync("Admin/learning/ai/authoring", TestContext.Current.CancellationToken);
        var before = await CountCoursesAsync();

        var response = await admin.PostAsync("Admin/learning/ai/authoring/outline", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Topic"] = "Ladder safety for warehouse staff",
            ["LessonCount"] = "4",
            ["__RequestVerificationToken"] = LearningSiteFixture.Token(page),
        }), TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("nothing was drafted", html, StringComparison.Ordinal);
        Assert.Equal(before, await CountCoursesAsync());
    }

    [Fact]
    public async Task Learner_CannotOpenThePage()
    {
        using var learner = await _site.CreateClientAsync("learner2");

        var response = await learner.GetAsync("Admin/learning/ai/authoring", TestContext.Current.CancellationToken);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    private Task<int> CountCoursesAsync()
        => _site.InTenantAsync(services => services.GetRequiredService<ISession>()
            .Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentType == LearningConstants.ContentTypes.Course)
            .CountAsync());
}
