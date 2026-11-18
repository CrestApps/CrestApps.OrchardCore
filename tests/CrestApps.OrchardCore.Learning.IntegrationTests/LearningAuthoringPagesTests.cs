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
    public async Task Round9Low_TheLessonList_HoldsOnlyLessonsTheAuthorMayEdit()
    {
        // Arrange: a lesson of the admin's course, and an author who may use the assistant and edit their own content only.
        var title = "Round 9 lesson " + Guid.NewGuid().ToString("n")[..8];

        await _site.InTenantAsync(async services =>
        {
            await LearningSiteFixture.RunRecipeAsync(services, new System.Text.Json.Nodes.JsonObject
            {
                ["name"] = "Round9AuthoringRole",
                ["steps"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject
                {
                    ["name"] = "Roles",
                    ["Roles"] = new System.Text.Json.Nodes.JsonArray(LearningSiteFixture.Role(
                        "Round9Author",
                        CrestApps.OrchardCore.Learning.AI.LearningAIPermissions.UseAuthoringAssistant.Name,
                        "AccessAdminPanel",
                        "EditOwnContent")),
                }),
            });

            var userId = await LearningSiteFixture.CreateUserAsync(services, "round9-author");
            var users = services.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<global::OrchardCore.Users.IUser>>();
            await users.AddToRoleAsync(await users.FindByIdAsync(userId), "Round9Author");

            var contentManager = services.GetRequiredService<global::OrchardCore.ContentManagement.IContentManager>();
            var lesson = await contentManager.NewAsync(LearningConstants.ContentTypes.PageLesson);
            lesson.DisplayText = title;
            lesson.Owner = "someone-else";
            await contentManager.CreateAsync(lesson, global::OrchardCore.ContentManagement.VersionOptions.Draft);
        });

        using var author = await _site.CreateClientAsync("round9-author");

        // Act
        var page = await author.GetStringAsync("Admin/learning/ai/authoring", TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain(title, page, StringComparison.Ordinal);
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
