using System.Net;
using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.Lists.Models;
using OrchardCore.Title.Models;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// A quiz is answered only by an enrolled learner, through the published quiz in its course: an unpublished quiz refuses
/// its link and the API, a learner withdrawn mid-attempt cannot finish, and a copied quiz gets a survey of its own.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class QuizAccessHardeningTests
{
    private readonly LearningSiteFixture _fixture;

    public QuizAccessHardeningTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AnUnpublishedQuiz_RefusesAStart_ThroughALinkAndTheApi()
    {
        // Arrange
        var setup = await CreateCourseAsync("unpublished");
        using var learner = await _fixture.CreateClientAsync("learner1");
        var player = await learner.GetStringAsync($"learning/courses/{setup.CourseId}/play/{setup.QuizId}", TestContext.Current.CancellationToken);

        // A quiz's survey gets no public link; an author may still add one by hand.
        var (defaultLink, token) = await _fixture.InTenantAsync(async services =>
        {
            var collectors = services.GetRequiredService<SurveyCollectorStore>();
            var found = await collectors.FindDefaultAsync(setup.SurveyId);
            var collector = new SurveyCollector
            {
                ItemId = IdGenerator.GenerateId(),
                SurveyId = setup.SurveyId,
                Name = "Added link",
                Kind = SurveyCollectorKinds.WebLink,
                Token = SurveyNames.NewToken(),
                Status = CollectorStatus.Open,
                CreatedUtc = DateTime.UtcNow,
            };

            await collectors.CreateAsync(collector);

            return (found, collector.Token);
        });

        // While the quiz is published, the API (which knows the caller by the sub claim of a token) starts the attempt.
        var startedWhilePublished = await StartThroughApiAsync(setup);

        await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var quiz = await contentManager.GetAsync(setup.QuizId, VersionOptions.Published);

            await contentManager.UnpublishAsync(quiz);
        });

        // Act
        var resumedAfterUnpublish = await StartThroughApiAsync(setup);
        var landing = await learner.GetAsync($"s/{token}", TestContext.Current.CancellationToken);
        var linkStart = await learner.PostAsync($"s/{token}/start", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = LearningSiteFixture.Token(player),
        }), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(defaultLink);
        Assert.True(startedWhilePublished.Succeeded, startedWhilePublished.Error.ToString());
        Assert.False(resumedAfterUnpublish.Succeeded);
        Assert.Equal(SurveyErrorCode.NotOpen, resumedAfterUnpublish.Error);
        Assert.DoesNotContain("/s/r/", landing.Headers.Location?.OriginalString ?? string.Empty, StringComparison.Ordinal);
        Assert.NotEqual(HttpStatusCode.Redirect, linkStart.StatusCode);
    }

    [Fact]
    public async Task AWithdrawnLearner_CannotFinishOrResumeTheirAttempt()
    {
        // Arrange
        var setup = await CreateCourseAsync("withdrawn");
        var started = await _fixture.InTenantAsync(async services =>
        {
            var principal = await SignInAsync(services);
            var quiz = await services.GetRequiredService<IContentManager>().GetAsync(setup.QuizId, VersionOptions.Published);

            return await services.GetRequiredService<ILearningSurveyService>().StartAsync(principal, quiz, setup.SurveyId, null);
        });

        await _fixture.InTenantAsync(async services =>
        {
            var changed = await services.GetRequiredService<IEnrollmentService>().ChangeStatusAsync(setup.EnrollmentId, EnrollmentStatus.Withdrawn, "admin", "Left the course");

            Assert.True(changed.Succeeded, changed.ErrorCode);
        });

        // Act
        var (finished, resumed) = await _fixture.InTenantAsync(async services =>
        {
            var principal = await SignInAsync(services);
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var responses = services.GetRequiredService<ISurveyResponseService>();
            var submit = await responses.SubmitPageAsync(new SubmitPageRequest
            {
                ResponseId = started.Response.ItemId,
                PageId = started.Response.CurrentPageId,
                Direction = SurveyPageDirection.Finish,
                Form = new FormCollection(new Dictionary<string, StringValues> { ["q.planet"] = "mars" }),
                Identity = new SurveyRespondentIdentity { UserId = userId },
            });
            var resume = await responses.StartAsync(new StartResponseRequest
            {
                SurveyId = setup.SurveyId,
                Identity = new SurveyRespondentIdentity { UserId = userId },
                Context = LearningSurveyContexts.Enrollment(setup.EnrollmentId),
            });

            return (submit, resume);
        });

        var stored = await _fixture.InTenantAsync(services => services.GetRequiredService<SurveyResponseStore>().FindByIdAsync(started.Response.ItemId).AsTask());

        // Assert
        Assert.True(started.Succeeded, started.Error.ToString());
        Assert.False(finished.Ended);
        Assert.Equal(SurveyErrorCode.NotInAudience, finished.Error);
        Assert.Equal(SurveyErrorCode.NotInAudience, resumed.Error);
        Assert.Equal(SurveyResponseStatus.InProgress, stored.Status);
    }

    [Fact]
    public async Task ACopiedQuiz_GetsASurveyOfItsOwn()
    {
        // Arrange
        var setup = await CreateCourseAsync("copied");

        // Act
        var (copyId, copySurveyId) = await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var quiz = await contentManager.GetAsync(setup.QuizId, VersionOptions.Latest);
            var copy = await contentManager.CloneAsync(quiz);

            return (copy.ContentItemId, LearningSurveyTestSupport.SurveyIdOf(copy));
        });

        var (original, copied, owned) = await _fixture.InTenantAsync(async services =>
        {
            var manager = services.GetRequiredService<ISurveyManager>();

            return (
                await manager.FindByIdAsync(setup.SurveyId),
                await manager.FindByIdAsync(copySurveyId),
                await manager.ListByOwnerAsync(LearningSurveyContexts.Owner(copyId)));
        });

        // Assert
        Assert.False(string.IsNullOrEmpty(copySurveyId));
        Assert.NotEqual(setup.SurveyId, copySurveyId);
        Assert.Equal(LearningSurveyContexts.Owner(setup.QuizId), original.Owner);
        Assert.NotNull(copied);
        Assert.Equal(LearningSurveyContexts.Owner(copyId), copied.Owner);
        Assert.Equal(copySurveyId, Assert.Single(owned).ItemId);
        Assert.Equal(SurveyStatus.Open, copied.Status);
        Assert.Contains(copied.Draft.Pages.SelectMany(page => page.Questions), question => question.Name == "planet");
        Assert.True(copied.NoDefaultWebLink);
        Assert.Equal(SurveyAudienceMode.SignedIn, copied.Access.Audience);
    }

    private async Task<SurveyStartResult> StartThroughApiAsync((string CourseId, string QuizId, string SurveyId, string EnrollmentId) setup)
        => await _fixture.InTenantAsync(async services =>
        {
            // The Surveys API runs on a bearer token whose user id is in the sub claim.
            var accessor = services.GetRequiredService<IHttpContextAccessor>();
            accessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", _fixture.EnrolledUserId)], "Bearer"));

            return await services.GetRequiredService<ISurveyResponseService>().StartAsync(new StartResponseRequest
            {
                SurveyId = setup.SurveyId,
                Identity = new SurveyRespondentIdentity { UserId = _fixture.EnrolledUserId },
                Context = LearningSurveyContexts.Enrollment(setup.EnrollmentId),
            });
        });

    private static async Task<ClaimsPrincipal> SignInAsync(IServiceProvider services)
    {
        var user = await services.GetRequiredService<UserManager<IUser>>().FindByNameAsync("learner1");
        var principal = await services.GetRequiredService<IUserClaimsPrincipalFactory<IUser>>().CreateAsync(user);

        services.GetRequiredService<IHttpContextAccessor>().HttpContext.User = principal;

        return principal;
    }

    private Task<(string CourseId, string QuizId, string SurveyId, string EnrollmentId)> CreateCourseAsync(string name)
        => _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var suffix = Guid.NewGuid().ToString("n")[..6];
            var course = await contentManager.NewAsync(LearningConstants.ContentTypes.Course);

            course.DisplayText = "Quiz access " + name + " " + suffix;
            course.Alter<TitlePart>(part => part.Title = course.DisplayText);
            course.Alter<CoursePart>(part => part.EnrollmentMode = EnrollmentMode.Open);
            await contentManager.CreateAsync(course, VersionOptions.Draft);

            var quiz = await contentManager.NewAsync(AssessmentsConstants.ContentTypes.Quiz);

            quiz.DisplayText = "Planets quiz " + suffix;
            quiz.Alter<TitlePart>(part => part.Title = quiz.DisplayText);
            quiz.Alter<CourseItemPart>(part => part.CompletionMode = CompletionModes.Submitted);
            quiz.Alter<ContainedPart>(part =>
            {
                part.ListContentItemId = course.ContentItemId;
                part.ListContentType = course.ContentType;
            });
            await contentManager.CreateAsync(quiz, VersionOptions.Published);

            var surveyId = LearningSurveyTestSupport.SurveyIdOf(quiz);
            var definition = LearningSurveyTestSupport.Definition(
                quiz.DisplayText,
                LearningSurveyTestSupport.SingleChoice("planet", "Which planet is red?", ("venus", "Venus"), ("mars", "Mars")));

            definition.Pages[0].Questions[0].Scoring = new SurveyQuestionScoring { Points = 1, CorrectChoiceIds = ["mars"] };
            definition.Scoring.Enabled = true;
            await LearningSurveyTestSupport.PublishAsync(services, surveyId, definition);

            course.Alter<CourseOutlinePart>(part => part.Sections =
            [
                new CourseSection { Id = "s1", Title = "Quiz", Items = [new CourseOutlineItem { ContentItemId = quiz.ContentItemId }] },
            ]);
            await contentManager.UpdateAsync(course);
            await contentManager.PublishAsync(course);

            var enrolled = await services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
            {
                UserId = _fixture.EnrolledUserId,
                CourseId = course.ContentItemId,
                Source = LearningConstants.EnrollmentSources.Manual,
                SourceId = "quiz-access-" + name + "-" + suffix,
            });

            Assert.True(enrolled.Succeeded, enrolled.ErrorCode);

            return (course.ContentItemId, quiz.ContentItemId, surveyId, enrolled.Enrollment.ItemId);
        });
}
