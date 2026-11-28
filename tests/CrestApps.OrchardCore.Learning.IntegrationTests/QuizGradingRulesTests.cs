using System.Net;
using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Learning.Surveys;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Lists.Models;
using OrchardCore.Title.Models;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// Review round 6, on a running site: grading an anonymous course quiz never touches the grader's own enrollment
/// (R6-M5); a course instructor without Override grades gives points only to the questions that need grading (R6-M6)
/// and can't void a quiz attempt (R6-M7); a quiz with no pass mark completes "When the learner passes" (R6-M8); and a
/// copied course survey gets a copy of the original's survey (R6-M13; the site runs on SQLite, which has no lock wait,
/// so the test checks the outcome, and the unit tests check that no child scope delete runs).
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class QuizGradingRulesTests
{
    private readonly LearningSiteFixture _fixture;

    public QuizGradingRulesTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task R6M5_GradingAnAnonymousCourseQuiz_AsAnEnrolledGrader_ChangesNothingInTheGradersEnrollment()
    {
        // Arrange: an anonymous quiz with an essay; the instructor is enrolled in the course too.
        var setup = await CreateQuizAsync(anonymous: true, enrollInstructor: true, CompletionModes.Passed, definition => { });
        var responseId = await _fixture.InTenantAsync(async services =>
        {
            var quiz = await services.GetRequiredService<IContentManager>().GetAsync(setup.QuizId, VersionOptions.Latest);
            var response = await LearningSurveyTestSupport.AnswerAsync(services, "learner1", quiz, new Dictionary<string, string>
            {
                ["q.planet"] = "venus",
                ["q.essay"] = "Mars is red because of iron oxide.",
            });

            Assert.Equal(GradingStatus.Pending, response.Grading);

            return response.ItemId;
        });

        // Act: the instructor grades the essay in a request of their own.
        await _fixture.InTenantAsync(async services =>
        {
            var principal = await SignInAsync(services, "instructor1");
            var graded = await services.GetRequiredService<ISurveyGradingService>().GradeAsync(
                responseId,
                new Dictionary<string, decimal> { ["essay"] = 4 },
                null,
                null,
                principal.FindFirstValue(ClaimTypes.NameIdentifier),
                TestContext.Current.CancellationToken);

            Assert.True(graded.Succeeded, graded.Error.ToString());
        });

        var (grader, learner) = await _fixture.InTenantAsync(async services =>
        {
            var progress = services.GetRequiredService<ILearnerProgressService>();

            return ((await progress.GetAsync(setup.InstructorEnrollmentId))?.Find(setup.QuizId), (await progress.GetAsync(setup.LearnerEnrollmentId))?.Find(setup.QuizId));
        });

        // Assert: the grader's enrollment has nothing of the quiz; the learner's completion carries no score (R6-L26).
        Assert.True(grader is null || (grader.State != CourseItemState.Completed && grader.Score is null));
        Assert.Equal(CourseItemState.Completed, learner?.State);
        Assert.Null(learner.Score);
        Assert.Null(learner.Passed);
    }

    [Fact]
    public async Task R7M8_AGradableAnonymousQuiz_IsLeftOutOfTheCourseGrade_NotCountedAsZero()
    {
        // Arrange: an anonymous quiz that counts toward the course grade (weight 3), scored without a grader.
        var setup = await CreateQuizAsync(anonymous: true, enrollInstructor: false, CompletionModes.Passed, definition =>
        {
            definition.Pages[0].Questions.RemoveAll(question => question.Id == "essay");
        }, gradable: true);

        // Act: the learner answers it right.
        await _fixture.InTenantAsync(async services =>
        {
            var quiz = await services.GetRequiredService<IContentManager>().GetAsync(setup.QuizId, VersionOptions.Latest);

            await LearningSurveyTestSupport.AnswerAsync(services, "learner1", quiz, new Dictionary<string, string> { ["q.planet"] = "mars" });
        });

        var (item, grade) = await _fixture.InTenantAsync(async services =>
        {
            var structure = services.GetRequiredService<ICourseStructureService>();
            var progress = await services.GetRequiredService<ILearnerProgressService>().GetAsync(setup.LearnerEnrollmentId);
            var outline = await structure.GetOutlineAsync(await structure.GetCourseAsync(setup.CourseId));

            return (progress?.Find(setup.QuizId), CourseGradeCalculator.Compute(outline, progress));
        });

        // Assert: the quiz is completed with its completion only, and the course has no grade rather than 0%.
        Assert.Equal(CourseItemState.Completed, item?.State);
        Assert.True(item.CompletionOnly);
        Assert.Null(item.Score);
        Assert.Null(grade);
    }

    [Fact]
    public async Task R7M9_GradingTheEssay_ThenFixingTheChoiceKey_ScoreAgainMovesTheChoiceScore()
    {
        // Arrange: a pending attempt that chose Venus (wrong, 0 points) with an essay waiting for a grader.
        var setup = await CreateQuizAsync(anonymous: false, enrollInstructor: false, CompletionModes.Graded, definition => { });
        var responseId = await AnswerAsync(setup.QuizId);
        using var instructor = await _fixture.CreateClientAsync("instructor1");
        var cancellationToken = TestContext.Current.CancellationToken;
        var page = await instructor.GetStringAsync($"Admin/surveys/grading/{responseId}", cancellationToken);

        // The grader grades the essay; the choice's read-only points box is not posted.
        await instructor.PostAsync($"Admin/surveys/grading/{responseId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = LearningSiteFixture.Token(page),
            ["Points[essay]"] = "4",
        }), cancellationToken);

        // Act: the author fixes the key (Venus is right after all) and the response is scored again.
        var rescored = await _fixture.InTenantAsync(async services =>
        {
            var response = await services.GetRequiredService<ISurveyResponseQueries>().FindByIdAsync(responseId);
            var version = await services.GetRequiredService<ISurveyVersionCache>().GetAsync(response.SurveyVersionId);

            version.Definition.Pages[0].Questions.Single(question => question.Id == "planet").Scoring.CorrectChoiceIds = ["venus"];

            return await services.GetRequiredService<ISurveyGradingService>().RegradeAsync(responseId, _fixture.InstructorId, "Key fix", cancellationToken);
        });

        // Assert: the choice now scores 1 and the essay keeps the grader's 4.
        Assert.DoesNotContain("name=\"Points[planet]\"", page);
        Assert.True(rescored.Succeeded, rescored.Error.ToString());
        Assert.Null(rescored.Response.Answers["planet"].ManualScore);
        Assert.Equal(1m, rescored.Response.Answers["planet"].Score);
        Assert.Equal(4m, rescored.Response.Answers["essay"].Score);
        Assert.Equal(5m, rescored.Response.Score);
    }

    [Fact]
    public async Task R6M6_AnInstructorWithoutOverrideGrades_GivesPointsOnlyToTheQuestionsThatNeedGrading()
    {
        // Arrange: a pending attempt with a wrong choice (scored 0 by the site) and an essay waiting for a grader.
        var setup = await CreateQuizAsync(anonymous: false, enrollInstructor: false, CompletionModes.Graded, definition => { });
        var responseId = await AnswerAsync(setup.QuizId);
        using var instructor = await _fixture.CreateClientAsync("instructor1");
        var cancellationToken = TestContext.Current.CancellationToken;
        var page = await instructor.GetStringAsync($"Admin/surveys/grading/{responseId}", cancellationToken);

        // Act: full points for the wrong choice, then the essay's points with the choice's score posted unchanged.
        var replaced = await instructor.PostAsync($"Admin/surveys/grading/{responseId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = LearningSiteFixture.Token(page),
            ["Points[planet]"] = "1",
            ["Points[essay]"] = "4",
        }), cancellationToken);
        var afterRefusal = await ReadResponseAsync(responseId);
        var graded = await instructor.PostAsync($"Admin/surveys/grading/{responseId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = LearningSiteFixture.Token(page),
            ["Points[planet]"] = "0",
            ["Points[essay]"] = "4",
        }), cancellationToken);
        var afterGrading = await ReadResponseAsync(responseId);

        // Assert
        Assert.Contains("id=\"points-planet\"", page);
        Assert.Matches("id=\"points-planet\"[^>]*readonly", page);
        Assert.DoesNotMatch("id=\"points-essay\"[^>]*readonly", page);
        // Refused: the site answers a forbidden post with its access denied page, not the grading list a save returns to.
        Assert.DoesNotContain("surveys/grading", replaced.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(GradingStatus.Pending, afterRefusal.Grading);
        Assert.Null(afterRefusal.Answers["planet"].ManualScore);
        Assert.Equal(HttpStatusCode.Redirect, graded.StatusCode);
        Assert.Contains("surveys/grading", graded.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(GradingStatus.Graded, afterGrading.Grading);
        Assert.Equal(4m, afterGrading.Score);
    }

    [Fact]
    public async Task R6M7_ACourseInstructorWithoutOverrideGrades_CantVoidAQuizAttempt()
    {
        // Arrange
        var setup = await CreateQuizAsync(anonymous: false, enrollInstructor: false, CompletionModes.Graded, definition => { });
        var responseId = await AnswerAsync(setup.QuizId);
        using var instructor = await _fixture.CreateClientAsync("instructor1");
        var cancellationToken = TestContext.Current.CancellationToken;
        var page = await instructor.GetStringAsync($"Admin/surveys/grading/{responseId}", cancellationToken);

        // Act
        var voided = await instructor.PostAsync($"Admin/surveys/responses/{responseId}/void", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = LearningSiteFixture.Token(page),
            ["reason"] = "The learner failed; the earlier attempt passed",
        }), cancellationToken);
        var response = await ReadResponseAsync(responseId);

        // Assert: refused (the access denied page, not the response page a void returns to), and the attempt stays.
        Assert.DoesNotContain($"surveys/responses/{responseId}", voided.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SurveyResponseStatus.Completed, response.Status);
    }

    [Fact]
    public async Task R8Mi29_AnInstructorWithoutOverrideGrades_IsOfferedVoidAndDeleteOnlyWhereTheyCanUseThem()
    {
        // Arrange: a pending attempt (it carries a grade) of a course quiz instructor1 teaches.
        var setup = await CreateQuizAsync(anonymous: false, enrollInstructor: false, CompletionModes.Graded, definition => { });
        var responseId = await AnswerAsync(setup.QuizId);
        var surveyId = await _fixture.InTenantAsync(async services =>
            LearningSurveyTestSupport.SurveyIdOf(await services.GetRequiredService<IContentManager>().GetAsync(setup.QuizId, VersionOptions.Latest)));
        using var instructor = await _fixture.CreateClientAsync("instructor1");
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act: the instructor opens the attempt and the list, then voids it from the list's bulk actions.
        var details = await instructor.GetStringAsync($"Admin/surveys/responses/{responseId}", cancellationToken);
        var list = await instructor.GetStringAsync($"Admin/surveys/{surveyId}/responses", cancellationToken);
        var bulk = await instructor.PostAsync($"Admin/surveys/{surveyId}/responses", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = LearningSiteFixture.Token(list),
            ["BulkAction"] = "Void",
            ["itemIds"] = responseId,
            ["submit.BulkAction"] = "submit.BulkAction",
        }), cancellationToken);
        var afterBulk = await instructor.GetStringAsync($"Admin/surveys/{surveyId}/responses", cancellationToken);
        var kept = await ReadResponseAsync(responseId);

        // The same attempt as a test attempt never reaches course progress, so the instructor may void it.
        await _fixture.InTenantAsync(async services =>
        {
            var responses = services.GetRequiredService<ICatalog<SurveyResponse>>();
            var response = await responses.FindByIdAsync(responseId, cancellationToken);

            response.IsTest = true;
            await responses.UpdateAsync(response, cancellationToken);
        });

        var testDetails = await instructor.GetStringAsync($"Admin/surveys/responses/{responseId}", cancellationToken);
        var voided = await instructor.PostAsync($"Admin/surveys/responses/{responseId}/void", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = LearningSiteFixture.Token(testDetails),
            ["reason"] = "A test run",
        }), cancellationToken);

        // Assert: neither page offers Void or Delete on the graded attempt, the bulk action leaves it and says so, and
        // the test attempt offers both and is voided.
        Assert.DoesNotContain($"surveys/responses/{responseId}/void", details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"surveys/responses/{responseId}/delete", details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"surveys/responses/{responseId}/void", list, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Redirect, bulk.StatusCode);
        Assert.Contains("0 responses were changed.", afterBulk);
        Assert.Contains("1 responses were left as they were", afterBulk);
        Assert.Equal(SurveyResponseStatus.Completed, kept.Status);
        Assert.Contains($"surveys/responses/{responseId}/void", testDetails, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"surveys/responses/{responseId}", voided.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SurveyResponseStatus.Voided, (await ReadResponseAsync(responseId)).Status);
    }

    [Fact]
    public async Task R6M8_AQuizWithNoPassMark_CompletesWhenTheLearnerPasses_NeitherPassedNorFailed()
    {
        // Arrange: no pass mark and no score band, no essay.
        var setup = await CreateQuizAsync(anonymous: false, enrollInstructor: false, CompletionModes.Passed, definition =>
        {
            definition.Pages[0].Questions.RemoveAll(question => question.Id == "essay");
            definition.Scoring.PassMarkPercent = null;
        });

        // Act
        await AnswerAsync(setup.QuizId, essay: null);
        var item = await _fixture.InTenantAsync(async services =>
            (await services.GetRequiredService<ILearnerProgressService>().GetAsync(setup.LearnerEnrollmentId))?.Find(setup.QuizId));

        // Assert
        Assert.Equal(CourseItemState.Completed, item?.State);
        Assert.Null(item.Passed);
        Assert.Equal(0m, item.Score);
    }

    [Fact]
    public async Task R6M13_ACopiedCourseSurvey_GetsACopyOfTheOriginalsSurvey_AndNoBlankSurveyIsLeft()
    {
        // Arrange
        var setup = await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var item = await contentManager.NewAsync(LearningSurveysConstants.ContentTypes.FeedbackSurvey);

            item.DisplayText = "Course feedback " + Guid.NewGuid().ToString("n")[..6];
            item.Alter<TitlePart>(part => part.Title = item.DisplayText);
            item.Alter<CourseSurveyPart>(_ => { });
            await contentManager.CreateAsync(item, VersionOptions.Published);

            var surveyId = LearningSurveyTestSupport.SurveyIdOf(item);

            await LearningSurveyTestSupport.PublishAsync(services, surveyId, LearningSurveyTestSupport.Definition("Course feedback", LearningSurveyTestSupport.SingleChoice("again", "Would you take it again?", ("yes", "Yes"), ("no", "No"))));

            return (ItemId: item.ContentItemId, SurveyId: surveyId);
        });

        // Act
        var (copyId, copySurveyId) = await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var copy = await contentManager.CloneAsync(await contentManager.GetAsync(setup.ItemId, VersionOptions.Latest));

            return (copy.ContentItemId, LearningSurveyTestSupport.SurveyIdOf(copy));
        });

        var (copied, owned) = await _fixture.InTenantAsync(async services =>
        {
            var manager = services.GetRequiredService<ISurveyManager>();

            return (await manager.FindByIdAsync(copySurveyId), await manager.ListByOwnerAsync(LearningSurveyContexts.Owner(copyId)));
        });

        // Assert
        Assert.NotEqual(setup.SurveyId, copySurveyId);
        Assert.NotNull(copied);
        Assert.Equal(LearningSurveyContexts.Owner(copyId), copied.Owner);
        Assert.Equal(copySurveyId, Assert.Single(owned).ItemId);
        Assert.Contains(copied.Draft.Pages.SelectMany(page => page.Questions), question => question.Name == "again");
        Assert.Equal(SurveyStatus.Open, copied.Status);
    }

    private Task<string> AnswerAsync(string quizId, string essay = "Mars is red because of iron oxide.")
        => _fixture.InTenantAsync(async services =>
        {
            var quiz = await services.GetRequiredService<IContentManager>().GetAsync(quizId, VersionOptions.Latest);
            var values = new Dictionary<string, string> { ["q.planet"] = "venus" };

            if (essay is not null)
            {
                values["q.essay"] = essay;
            }

            return (await LearningSurveyTestSupport.AnswerAsync(services, "learner1", quiz, values)).ItemId;
        });

    private Task<SurveyResponse> ReadResponseAsync(string responseId)
        => _fixture.InTenantAsync(async services => await services.GetRequiredService<ISurveyResponseQueries>().FindByIdAsync(responseId));

    private static async Task<ClaimsPrincipal> SignInAsync(IServiceProvider services, string userName)
    {
        var user = await services.GetRequiredService<UserManager<IUser>>().FindByNameAsync(userName);
        var principal = await services.GetRequiredService<IUserClaimsPrincipalFactory<IUser>>().CreateAsync(user);

        services.GetRequiredService<IHttpContextAccessor>().HttpContext.User = principal;

        return principal;
    }

    // A course instructor1 teaches, with one quiz: a single choice worth 1 point (Mars is right) and an essay worth 5.
    private Task<(string CourseId, string QuizId, string LearnerEnrollmentId, string InstructorEnrollmentId)> CreateQuizAsync(bool anonymous, bool enrollInstructor, string completionMode, Action<SurveyDefinition> configure, bool gradable = false)
        => _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var suffix = Guid.NewGuid().ToString("n")[..6];
            var course = await contentManager.NewAsync(LearningConstants.ContentTypes.Course);

            course.DisplayText = "Round 6 grading " + suffix;
            course.Alter<TitlePart>(part => part.Title = course.DisplayText);
            course.Alter<CoursePart>(part =>
            {
                part.EnrollmentMode = EnrollmentMode.Open;
                part.Staff = [new CourseStaffMember { UserId = _fixture.InstructorId, Role = LearningConstants.CourseStaffRoles.Instructor }];
            });
            await contentManager.CreateAsync(course, VersionOptions.Draft);

            var quiz = await contentManager.NewAsync(AssessmentsConstants.ContentTypes.Quiz);

            quiz.DisplayText = "Planets quiz " + suffix;
            quiz.Alter<TitlePart>(part => part.Title = quiz.DisplayText);
            quiz.Alter<QuizPart>(part => part.GradingMethod = GradingMethod.Highest);
            quiz.Alter<CourseItemPart>(part =>
            {
                part.CompletionMode = completionMode;

                if (gradable)
                {
                    part.Gradable = true;
                    part.Weight = 3;
                }
            });
            quiz.Alter<ContainedPart>(part =>
            {
                part.ListContentItemId = course.ContentItemId;
                part.ListContentType = course.ContentType;
            });
            await contentManager.CreateAsync(quiz, VersionOptions.Published);

            // A second item keeps the course from completing, so every result lands on an active enrollment.
            var lesson = await contentManager.NewAsync(LearningSurveysConstants.ContentTypes.Choice);

            lesson.DisplayText = "Next step " + suffix;
            lesson.Alter<TitlePart>(part => part.Title = lesson.DisplayText);
            lesson.Alter<CourseSurveyPart>(_ => { });
            lesson.Alter<CourseItemPart>(part =>
            {
                part.CompletionMode = CompletionModes.Submitted;
                part.Gradable = false;
            });
            lesson.Alter<ContainedPart>(part =>
            {
                part.ListContentItemId = course.ContentItemId;
                part.ListContentType = course.ContentType;
            });
            await contentManager.CreateAsync(lesson, VersionOptions.Published);

            var definition = LearningSurveyTestSupport.Definition(
                quiz.DisplayText,
                LearningSurveyTestSupport.SingleChoice("planet", "Which planet is red?", ("venus", "Venus"), ("mars", "Mars")),
                new SurveyQuestion { Id = "essay", Name = "essay", ControlType = SurveyControlNames.LongText, Title = "Why is it red?" });

            definition.Pages[0].Questions[0].Scoring = new SurveyQuestionScoring { Points = 1, CorrectChoiceIds = ["mars"] };
            definition.Pages[0].Questions[1].Scoring = new SurveyQuestionScoring { Points = 5 };
            definition.Scoring.Enabled = true;
            definition.Scoring.PassMarkPercent = 50;
            configure(definition);

            await LearningSurveyTestSupport.PublishAsync(services, LearningSurveyTestSupport.SurveyIdOf(quiz), definition, survey =>
            {
                if (anonymous)
                {
                    survey.Access ??= new SurveyAccessSettings();
                    survey.Access.Anonymity = AnonymityMode.Anonymous;
                }
            });

            course.Alter<CourseOutlinePart>(part => part.Sections =
            [
                new CourseSection { Id = "s1", Title = "Quiz", Items = [new CourseOutlineItem { ContentItemId = quiz.ContentItemId }, new CourseOutlineItem { ContentItemId = lesson.ContentItemId }] },
            ]);
            await contentManager.UpdateAsync(course);
            await contentManager.PublishAsync(course);

            var enrollments = services.GetRequiredService<IEnrollmentService>();
            var learner = await enrollments.EnrollAsync(new EnrollmentRequest
            {
                UserId = _fixture.EnrolledUserId,
                CourseId = course.ContentItemId,
                Source = LearningConstants.EnrollmentSources.Manual,
                SourceId = "round6-learner-" + suffix,
            });

            Assert.True(learner.Succeeded, learner.ErrorCode);

            string instructorEnrollmentId = null;

            if (enrollInstructor)
            {
                var enrolled = await enrollments.EnrollAsync(new EnrollmentRequest
                {
                    UserId = _fixture.InstructorId,
                    CourseId = course.ContentItemId,
                    Source = LearningConstants.EnrollmentSources.Manual,
                    SourceId = "round6-instructor-" + suffix,
                });

                Assert.True(enrolled.Succeeded, enrolled.ErrorCode);
                instructorEnrollmentId = enrolled.Enrollment.ItemId;
            }

            return (course.ContentItemId, quiz.ContentItemId, learner.Enrollment.ItemId, instructorEnrollmentId);
        });
}
