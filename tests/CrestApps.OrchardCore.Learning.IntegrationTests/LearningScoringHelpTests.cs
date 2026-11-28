using System.Net;
using CrestApps.OrchardCore.Learning.AI.Services;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
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
/// Scoring help on the grading pages of a site without an AI model: the panel renders for a grader, the suggestion
/// address answers that there is no model and changes nothing, a learner cannot call it, and a grade the grader saves
/// after using the help says it was AI-assisted.
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
    public async Task EssayGradingPage_ShowsScoringHelp_AndTheSuggestionSaysNoModel()
    {
        var setup = await _site.InTenantAsync(SetUpAsync);
        using var admin = await _site.CreateClientAsync("admin");

        var page = await admin.GetAsync($"Admin/learning/grading/attempts/{setup.AttemptId}", TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var suggestion = await admin.PostAsync($"Admin/learning/ai/scoring-help/attempts/{setup.AttemptId}/0", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = LearningSiteFixture.Token(html),
        }), TestContext.Current.CancellationToken);
        var json = await suggestion.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var attempt = await _site.InTenantAsync(async services => await services.GetRequiredService<AssessmentAttemptStore>().FindByIdAsync(setup.AttemptId));

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Suggest a Score", html, StringComparison.Ordinal);
        Assert.Contains("name=\"aiAssisted0\"", html, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, suggestion.StatusCode);
        Assert.Contains("NoModel", json, StringComparison.Ordinal);
        Assert.Equal(AttemptStatus.NeedsGrading, attempt.Status);
    }

    [Fact]
    public async Task SubmissionGradingPage_ShowsScoringHelp()
    {
        var setup = await _site.InTenantAsync(SetUpAsync);
        using var admin = await _site.CreateClientAsync("admin");

        var page = await admin.GetAsync($"Admin/learning/coursework/submissions/{setup.SubmissionId}", TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Suggest a Score", html, StringComparison.Ordinal);
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
                await assistant.SuggestForAttemptItemAsync(learner, setup.AttemptId, 0, TestContext.Current.CancellationToken),
                await assistant.SuggestForSubmissionAsync(admin, setup.SubmissionId, TestContext.Current.CancellationToken));
        });

        Assert.Equal(EssayScoringResult.NotFound, asLearner.Error);
        Assert.Equal(EssayScoringResult.NoModel, asAdmin.Error);
    }

    [Fact]
    public async Task GradeSavedAfterUsingTheHelp_IsMarkedAIAssisted()
    {
        var setup = await _site.InTenantAsync(SetUpAsync);
        using var admin = await _site.CreateClientAsync("admin");
        var html = await admin.GetStringAsync($"Admin/learning/grading/attempts/{setup.AttemptId}", TestContext.Current.CancellationToken);

        // The site has no AI model, so the suggestion the endpoint would record is recorded here, as it does (round 9,
        // M12: the flag comes from the server, not from the form).
        await _site.InTenantAsync(async services =>
        {
            var users = services.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<global::OrchardCore.Users.IUser>>();
            var adminId = await users.GetUserIdAsync(await users.FindByNameAsync("admin"));

            return await services.GetRequiredService<IAssessmentAttemptService>().RecordScoringHelpAsync(setup.AttemptId, 0, adminId);
        });

        var response = await admin.PostAsync($"Admin/learning/grading/attempts/{setup.AttemptId}/grade", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["score0"] = "2",
            ["comment0"] = "Names two of the three.",
            ["__RequestVerificationToken"] = LearningSiteFixture.Token(html),
        }), TestContext.Current.CancellationToken);
        var attempt = await _site.InTenantAsync(async services => await services.GetRequiredService<AssessmentAttemptStore>().FindByIdAsync(setup.AttemptId));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(AttemptStatus.Graded, attempt.Status);
        Assert.True(attempt.Items[0].AIAssisted);
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

        var essay = await contentManager.NewAsync(AssessmentsConstants.ContentTypes.EssayQuestion);
        essay.DisplayText = "Fire triangle " + suffix;
        essay.Alter<TitlePart>(part => part.Title = essay.DisplayText);
        essay.Alter<QuestionPart>(part =>
        {
            part.Prompt = "<p>Name the three sides of the fire triangle.</p>";
            part.Points = 3;
        });
        essay.Alter<EssayQuestionPart>(part => part.GraderGuidance = "One point for each of heat, fuel and oxygen.");
        await contentManager.CreateAsync(essay, VersionOptions.Published);

        var quiz = await ItemAsync(contentManager, course, AssessmentsConstants.ContentTypes.Quiz, "Essay quiz " + suffix, item =>
        {
            item.Alter<QuizPart>(part =>
            {
                part.QuestionIds = [essay.ContentItemId];
                part.MaxAttempts = null;
            });
            item.Alter<CourseItemPart>(part => part.CompletionMode = CompletionModes.Submitted);
        });
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
                Items = [new CourseOutlineItem { ContentItemId = quiz.ContentItemId }, new CourseOutlineItem { ContentItemId = coursework.ContentItemId }],
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

        var userManager = services.GetRequiredService<UserManager<IUser>>();
        var principal = await services.GetRequiredService<IUserClaimsPrincipalFactory<IUser>>().CreateAsync(await userManager.FindByNameAsync(learnerName));
        var attempts = services.GetRequiredService<IAssessmentAttemptService>();
        var started = await attempts.StartAsync(new StartAttemptRequest { UserId = learnerId, User = principal, QuizId = quiz.ContentItemId, HonorConfirmed = true });

        Assert.True(started.Succeeded, started.ErrorCode);

        await attempts.SaveResponseAsync(started.Attempt.ItemId, learnerId, 0, new AttemptResponse { Texts = ["Heat and fuel."] });
        await attempts.SubmitAsync(started.Attempt.ItemId, learnerId);

        var submitted = await services.GetRequiredService<ISubmissionService>().SubmitAsync(new SubmitCourseworkRequest
        {
            UserId = learnerId,
            CourseworkId = coursework.ContentItemId,
            Text = "Fire needs heat, fuel and oxygen.",
        });

        Assert.True(submitted.Succeeded, submitted.Error);

        return new ScoringSetup(learnerName, started.Attempt.ItemId, submitted.Submission.ItemId);
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

    private sealed record ScoringSetup(string LearnerName, string AttemptId, string SubmissionId);
}
