using System.Security.Claims;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.Learning.AI;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.Html.Models;
using OrchardCore.Lists.Models;
using OrchardCore.Modules;
using OrchardCore.Title.Models;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// The Learning AI tools on a real site, called directly as the model would call them: registered, and trimmed to what
/// the signed-in person may see.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class LearningAIToolsTests
{
    private const string RightAnswer = "Class C";

    private readonly LearningSiteFixture _site;

    public LearningAIToolsTests(LearningSiteFixture site)
    {
        _site = site;
    }

    [Fact]
    public async Task Tools_AreRegisteredAndSelectable()
    {
        var names = await _site.InTenantAsync(services =>
        {
            var options = services.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value;
            var found = new[]
            {
                LearningAIConstants.Tools.MyProgress,
                LearningAIConstants.Tools.MyNextLesson,
                LearningAIConstants.Tools.ExplainMyResult,
                LearningAIConstants.Tools.OverdueLearners,
                LearningAIConstants.Tools.ExpiringLearners,
            }.Where(name => options.Tools.ContainsKey(name) && services.GetKeyedService<AITool>(name) is not null).ToArray();

            return Task.FromResult(found);
        });

        Assert.Equal(5, names.Length);
    }

    [Fact]
    public async Task LearnerTools_RefuseSomeoneWhoIsNotSignedIn()
    {
        var result = await _site.InTenantAsync(services => InvokeAsync(services, null, LearningAIConstants.Tools.MyProgress));

        Assert.Contains("signed-in", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MyProgressAndNextLesson_ReadOnlyTheLearnersOwnCourses()
    {
        var setup = await SetUpAsync();

        var progress = await _site.InTenantAsync(services => InvokeAsync(services, setup.LearnerName, LearningAIConstants.Tools.MyProgress));
        var next = await _site.InTenantAsync(services => InvokeAsync(services, setup.LearnerName, LearningAIConstants.Tools.MyNextLesson));
        var other = await _site.InTenantAsync(services => InvokeAsync(services, setup.OtherLearnerName, LearningAIConstants.Tools.MyProgress));

        Assert.Contains(setup.CourseTitle, progress, StringComparison.Ordinal);
        Assert.Contains("First lesson " + setup.Suffix, next, StringComparison.Ordinal);
        Assert.Contains($"/learning/courses/{setup.CourseId}/play/", next, StringComparison.Ordinal);
        Assert.DoesNotContain(setup.CourseTitle, other, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExplainMyResult_GivesTheRightAnswerOnlyWhenTheReviewPolicyAllows()
    {
        var setup = await SetUpAsync();

        var scoreOnly = await _site.InTenantAsync(services => InvokeAsync(services, setup.LearnerName, LearningAIConstants.Tools.ExplainMyResult, ("quiz", setup.ScoreOnlyQuizId)));
        var reviewed = await _site.InTenantAsync(services => InvokeAsync(services, setup.LearnerName, LearningAIConstants.Tools.ExplainMyResult, ("quiz", setup.ReviewedQuizId)));
        var stranger = await _site.InTenantAsync(services => InvokeAsync(services, setup.OtherLearnerName, LearningAIConstants.Tools.ExplainMyResult, ("quiz", setup.ReviewedQuizId)));

        Assert.DoesNotContain(RightAnswer, scoreOnly, StringComparison.Ordinal);
        Assert.Contains("\"reviewAvailable\":false", scoreOnly, StringComparison.Ordinal);
        Assert.Contains(RightAnswer, reviewed, StringComparison.Ordinal);
        Assert.DoesNotContain(RightAnswer, stranger, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OverdueAndExpiring_ListOnlyLearnersInTheCallersScope()
    {
        var setup = await SetUpAsync();

        var instructor = await _site.InTenantAsync(services => InvokeAsync(services, "instructor1", LearningAIConstants.Tools.OverdueLearners, ("take", "50")));
        var admin = await _site.InTenantAsync(services => InvokeAsync(services, "admin", LearningAIConstants.Tools.OverdueLearners, ("take", "50")));
        var learner = await _site.InTenantAsync(services => InvokeAsync(services, setup.LearnerName, LearningAIConstants.Tools.OverdueLearners));
        var expiring = await _site.InTenantAsync(services => InvokeAsync(services, "instructor1", LearningAIConstants.Tools.ExpiringLearners, ("days", "30"), ("take", "50")));

        Assert.Contains(setup.LearnerName, instructor, StringComparison.Ordinal);
        Assert.DoesNotContain(setup.OtherLearnerName, instructor, StringComparison.Ordinal);
        Assert.Contains(setup.LearnerName, admin, StringComparison.Ordinal);
        Assert.Contains(setup.OtherLearnerName, admin, StringComparison.Ordinal);
        Assert.Contains("isn't allowed", learner, StringComparison.Ordinal);
        Assert.Contains(setup.LearnerName, expiring, StringComparison.Ordinal);
        Assert.DoesNotContain(setup.OtherLearnerName, expiring, StringComparison.Ordinal);
    }

    private static async Task<string> InvokeAsync(IServiceProvider services, string userName, string toolName, params (string Name, string Value)[] arguments)
    {
        var accessor = services.GetRequiredService<IHttpContextAccessor>();

        if (userName is not null)
        {
            var userManager = services.GetRequiredService<UserManager<IUser>>();
            var user = await userManager.FindByNameAsync(userName);
            accessor.HttpContext.User = await services.GetRequiredService<IUserClaimsPrincipalFactory<IUser>>().CreateAsync(user);
        }
        else
        {
            accessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        }

        var tool = Assert.IsAssignableFrom<AIFunction>(services.GetRequiredKeyedService<AITool>(toolName));
        var values = arguments.ToDictionary(argument => argument.Name, argument => (object)argument.Value);
        var result = await tool.InvokeAsync(new AIFunctionArguments(values) { Services = services }, TestContext.Current.CancellationToken);

        return result?.ToString() ?? string.Empty;
    }

    // The courses and quizzes are saved in one scope; the learner then answers each quiz in a scope of its own, as a
    // request would, so a response never waits on the set-up's open transaction.
    private async Task<ToolsSetup> SetUpAsync()
    {
        var setup = await _site.InTenantAsync(CreateAsync);

        foreach (var quizId in new[] { setup.ScoreOnlyQuizId, setup.ReviewedQuizId })
        {
            await _site.InTenantAsync(async services =>
            {
                var quiz = await services.GetRequiredService<IContentManager>().GetAsync(quizId, VersionOptions.Published);

                await LearningSurveyTestSupport.AnswerAsync(services, setup.LearnerName, quiz, new Dictionary<string, string> { ["q.fire"] = "w" });
            });
        }

        return setup;
    }

    private async Task<ToolsSetup> CreateAsync(IServiceProvider services)
    {
        var suffix = Guid.NewGuid().ToString("n")[..8];
        var contentManager = services.GetRequiredService<IContentManager>();
        var clock = services.GetRequiredService<IClock>();
        var learnerName = "tools-learner-" + suffix;
        var otherName = "tools-other-" + suffix;
        var learnerId = await LearningSiteFixture.CreateUserAsync(services, learnerName);
        var otherId = await LearningSiteFixture.CreateUserAsync(services, otherName);

        var taught = await CourseAsync(contentManager, "Taught course " + suffix, _site.InstructorId);
        var untaught = await CourseAsync(contentManager, "Untaught course " + suffix, null);
        var first = await ItemAsync(contentManager, taught, LearningConstants.ContentTypes.PageLesson, "First lesson " + suffix, null);
        var second = await ItemAsync(contentManager, taught, LearningConstants.ContentTypes.PageLesson, "Second lesson " + suffix, null);
        var untaughtLesson = await ItemAsync(contentManager, untaught, LearningConstants.ContentTypes.PageLesson, "Other lesson " + suffix, null);

        var scoreOnly = await ItemAsync(contentManager, taught, AssessmentsConstants.ContentTypes.Quiz, "Score only quiz " + suffix, quiz => quiz.Alter<QuizPart>(_ => { }));
        var reviewed = await ItemAsync(contentManager, taught, AssessmentsConstants.ContentTypes.Quiz, "Reviewed quiz " + suffix, quiz => quiz.Alter<QuizPart>(_ => { }));

        foreach (var (quiz, shows) in new[] { (scoreOnly, ReviewShows.Score), (reviewed, ReviewShows.All) })
        {
            var definition = LearningSurveyTestSupport.Definition(
                quiz.DisplayText,
                LearningSurveyTestSupport.SingleChoice("fire", "Which extinguisher for electrical fires?", ("w", "Water"), ("c", RightAnswer)));
            definition.Pages[0].Questions[0].Scoring = new SurveyQuestionScoring { Points = 1, CorrectChoiceIds = ["c"] };
            definition.Scoring.Enabled = true;
            definition.Review.ImmediatelyAfter = shows;
            definition.Review.LaterWhileOpen = shows;
            definition.Review.AfterClose = shows;
            await LearningSurveyTestSupport.PublishAsync(services, LearningSurveyTestSupport.SurveyIdOf(quiz), definition);
        }

        await OutlineAsync(contentManager, taught, first, second, scoreOnly, reviewed);
        await OutlineAsync(contentManager, untaught, untaughtLesson);

        var enrollments = services.GetRequiredService<IEnrollmentService>();
        var now = clock.UtcNow;

        foreach (var (userId, courseId) in new[] { (learnerId, taught.ContentItemId), (otherId, untaught.ContentItemId) })
        {
            var result = await enrollments.EnrollAsync(new EnrollmentRequest
            {
                UserId = userId,
                CourseId = courseId,
                Source = LearningConstants.EnrollmentSources.Manual,
                SourceId = "tools-test",
                DueUtc = now.AddDays(-2),
                AccessEndsUtc = now.AddDays(10),
            });

            Assert.True(result.Succeeded, result.ErrorCode);
        }

        return new ToolsSetup(suffix, learnerName, otherName, taught.ContentItemId, taught.DisplayText, scoreOnly.ContentItemId, reviewed.ContentItemId);
    }

    private static async Task<ContentItem> CourseAsync(IContentManager contentManager, string title, string instructorId)
    {
        var course = await contentManager.NewAsync(LearningConstants.ContentTypes.Course);
        course.DisplayText = title;
        course.Alter<TitlePart>(part => part.Title = title);
        course.Alter<CoursePart>(part =>
        {
            part.EnrollmentMode = EnrollmentMode.Open;

            if (instructorId is not null)
            {
                part.Staff = [new CourseStaffMember { UserId = instructorId, Role = LearningConstants.CourseStaffRoles.Instructor }];
            }
        });
        await contentManager.CreateAsync(course, VersionOptions.Draft);

        return course;
    }

    private static async Task<ContentItem> ItemAsync(IContentManager contentManager, ContentItem course, string contentType, string title, Action<ContentItem> alter)
    {
        var item = await contentManager.NewAsync(contentType);
        item.DisplayText = title;
        item.Alter<TitlePart>(part => part.Title = title);
        item.Alter<HtmlBodyPart>(part => part.Html = $"<p>{title}</p>");
        item.Alter<ContainedPart>(part =>
        {
            part.ListContentItemId = course.ContentItemId;
            part.ListContentType = course.ContentType;
        });
        item.Alter<CourseItemPart>(part => part.CompletionMode = contentType == AssessmentsConstants.ContentTypes.Quiz ? CompletionModes.Submitted : CompletionModes.Viewed);
        alter?.Invoke(item);
        await contentManager.CreateAsync(item, VersionOptions.Published);

        return item;
    }

    private static async Task OutlineAsync(IContentManager contentManager, ContentItem course, params ContentItem[] items)
    {
        course.Alter<CourseOutlinePart>(part => part.Sections =
        [
            new CourseSection
            {
                Id = "s1",
                Title = "Basics",
                Items = items.Select(item => new CourseOutlineItem { ContentItemId = item.ContentItemId }).ToList(),
            },
        ]);
        await contentManager.UpdateAsync(course);
        await contentManager.PublishAsync(course);
    }

    private sealed record ToolsSetup(string Suffix, string LearnerName, string OtherLearnerName, string CourseId, string CourseTitle, string ScoreOnlyQuizId, string ReviewedQuizId);
}
