using System.Net;
using CrestApps.OrchardCore.Learning.AI.Services;
using CrestApps.OrchardCore.Learning.Coursework;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Coursework.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Lists.Models;
using OrchardCore.Title.Models;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// Scoring help on the coursework grading page of a site without an AI model: the panel renders for a grader, a grader
/// gets told there is no model, and a learner cannot call it. Quiz essay answers get scoring help on the Surveys grading
/// page.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class LearningScoringHelpTests
{
    private readonly LearningSiteFixture _site;

    public LearningScoringHelpTests(LearningSiteFixture site)
    {
        _site = site;
    }

    [Fact]
    public async Task SubmissionGradingPage_ShowsScoringHelp()
    {
        var setup = await _site.InTenantAsync(SetUpAsync);
        using var admin = await _site.CreateClientAsync("admin");

        var page = await admin.GetAsync($"Admin/learning/coursework/submissions/{setup.SubmissionId}", TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Suggest a score", html, StringComparison.Ordinal);
        Assert.Contains("name=\"AIAssisted\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Learner_CannotAskForASuggestion_AndAGraderGetsNoModel()
    {
        var setup = await _site.InTenantAsync(SetUpAsync);

        var (asLearner, asAdmin) = await _site.InTenantAsync(async services =>
        {
            var userManager = services.GetRequiredService<UserManager<IUser>>();
            var factory = services.GetRequiredService<IUserClaimsPrincipalFactory<IUser>>();
            var learner = await factory.CreateAsync(await userManager.FindByNameAsync(setup.LearnerName));
            var admin = await factory.CreateAsync(await userManager.FindByNameAsync("admin"));
            var assistant = services.GetRequiredService<EssayScoringAssistant>();

            return (
                await assistant.SuggestForSubmissionAsync(learner, setup.SubmissionId, TestContext.Current.CancellationToken),
                await assistant.SuggestForSubmissionAsync(admin, setup.SubmissionId, TestContext.Current.CancellationToken));
        });

        Assert.Equal(EssayScoringResult.NotFound, asLearner.Error);
        Assert.Equal(EssayScoringResult.NoModel, asAdmin.Error);
    }

    private static async Task<ScoringSetup> SetUpAsync(IServiceProvider services)
    {
        var suffix = Guid.NewGuid().ToString("n")[..8];
        var contentManager = services.GetRequiredService<IContentManager>();
        var learnerName = "scoring-learner-" + suffix;
        var learnerId = await LearningSiteFixture.CreateUserAsync(services, learnerName);

        var course = await contentManager.NewAsync(LearningConstants.ContentTypes.Course);
        course.DisplayText = "Scoring course " + suffix;
        course.Alter<TitlePart>(part => part.Title = course.DisplayText);
        course.Alter<CoursePart>(part => part.EnrollmentMode = EnrollmentMode.Open);
        await contentManager.CreateAsync(course, VersionOptions.Draft);

        var coursework = await ItemAsync(contentManager, course, CourseworkConstants.ContentTypes.Coursework, "Report " + suffix, item =>
        {
            item.Alter<CourseworkPart>(part =>
            {
                part.SubmissionType = CourseworkSubmissionType.Text;
                part.MaxPoints = 10;
            });
        });

        course.Alter<CourseOutlinePart>(part => part.Sections =
        [
            new CourseSection
            {
                Id = "s1",
                Title = "Work",
                Items = [new CourseOutlineItem { ContentItemId = coursework.ContentItemId }],
            },
        ]);
        await contentManager.UpdateAsync(course);
        await contentManager.PublishAsync(course);

        var enrolled = await services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
        {
            UserId = learnerId,
            CourseId = course.ContentItemId,
            Source = LearningConstants.EnrollmentSources.Manual,
            SourceId = "scoring-test",
        });

        Assert.True(enrolled.Succeeded, enrolled.ErrorCode);

        var submitted = await services.GetRequiredService<ISubmissionService>().SubmitAsync(new SubmitCourseworkRequest
        {
            UserId = learnerId,
            CourseworkId = coursework.ContentItemId,
            Text = "Fire needs heat, fuel and oxygen.",
        });

        Assert.True(submitted.Succeeded, submitted.Error);

        return new ScoringSetup(learnerName, submitted.Submission.ItemId);
    }

    private static async Task<ContentItem> ItemAsync(IContentManager contentManager, ContentItem course, string contentType, string title, Action<ContentItem> alter)
    {
        var item = await contentManager.NewAsync(contentType);
        item.DisplayText = title;
        item.Alter<TitlePart>(part => part.Title = title);
        item.Alter<ContainedPart>(part =>
        {
            part.ListContentItemId = course.ContentItemId;
            part.ListContentType = course.ContentType;
        });
        alter(item);
        await contentManager.CreateAsync(item, VersionOptions.Published);

        return item;
    }

    private sealed record ScoringSetup(string LearnerName, string SubmissionId);
}
