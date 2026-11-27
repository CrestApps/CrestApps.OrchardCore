using System.Net;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Html.Models;
using OrchardCore.Lists.Models;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// The quiz pages stay part of the course: "Back to the Course" at the top, the site's own confirmation before
/// submitting (not the browser's), a Continue to the next item on the result page, and a sign-in prompt, not a 404,
/// for someone whose sign-in has ended.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class QuizPagesTests
{
    private readonly LearningSiteFixture _fixture;

    public QuizPagesTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TheAttemptAndResultPages_LeadBackToTheCourse_AndOnToTheNextItem()
    {
        // Arrange
        var (courseId, quizId, nextId) = await CreateCourseAsync();
        using var learner = await _fixture.CreateClientAsync("learner1");
        var play = await learner.GetStringAsync($"learning/courses/{courseId}/play/{quizId}", TestContext.Current.CancellationToken);
        var start = await learner.PostAsync($"learning/quizzes/{quizId}/start", Form(("__RequestVerificationToken", LearningSiteFixture.Token(play))), TestContext.Current.CancellationToken);
        var attemptUrl = start.Headers.Location!.OriginalString.TrimStart('/');

        // Act
        var attemptPage = await learner.GetStringAsync(attemptUrl, TestContext.Current.CancellationToken);
        var attemptId = attemptUrl[(attemptUrl.LastIndexOf('/') + 1)..];
        var submit = await learner.PostAsync($"learning/attempts/{attemptId}/submit", Form(("__RequestVerificationToken", LearningSiteFixture.Token(attemptPage))), TestContext.Current.CancellationToken);
        var resultPage = await learner.GetStringAsync(attemptUrl, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        Assert.Contains("data-learning-back-to-course", attemptPage);
        Assert.Contains("data-learning-submit-confirm", attemptPage);
        Assert.DoesNotContain("data-confirm-text", attemptPage);
        Assert.Equal(HttpStatusCode.Redirect, submit.StatusCode);
        Assert.Contains("data-learning-back-to-course", resultPage);
        Assert.Contains($"play/{nextId}\" data-learning-continue", resultPage);
    }

    [Fact]
    public async Task AnAttempt_AskedForWithoutSigningIn_LeadsToTheSignIn()
    {
        // Arrange
        using var anonymous = await _fixture.CreateClientAsync();

        // Act
        var response = await anonymous.GetAsync("learning/attempts/unknown", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Login", response.Headers.Location!.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    private static FormUrlEncodedContent Form(params (string Key, string Value)[] fields)
        => new(fields.Select(field => new KeyValuePair<string, string>(field.Key, field.Value)));

    private Task<(string CourseId, string QuizId, string NextId)> CreateCourseAsync()
        => _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var suffix = Guid.NewGuid().ToString("n")[..6];
            var course = await contentManager.NewAsync(LearningConstants.ContentTypes.Course);

            course.DisplayText = "Quiz pages " + suffix;
            course.Alter<TitlePart>(part => part.Title = course.DisplayText);
            course.Alter<CoursePart>(part => part.EnrollmentMode = EnrollmentMode.Open);
            await contentManager.CreateAsync(course, VersionOptions.Draft);

            var question = await contentManager.NewAsync(AssessmentsConstants.ContentTypes.TrueFalseQuestion);

            question.DisplayText = "Pages question " + suffix;
            question.Alter<TitlePart>(part => part.Title = question.DisplayText);
            question.Alter<QuestionPart>(part => part.Prompt = "<p>The quiz stays in the course.</p>");
            question.Alter<TrueFalseQuestionPart>(part => part.Answer = true);
            await contentManager.CreateAsync(question, VersionOptions.Published);

            async Task<ContentItem> ItemAsync(string type, string title, string mode, Action<ContentItem> configure = null)
            {
                var item = await contentManager.NewAsync(type);

                item.DisplayText = title;
                item.Alter<TitlePart>(part => part.Title = title);
                item.Alter<HtmlBodyPart>(part => part.Html = $"<p>{title}</p>");
                configure?.Invoke(item);
                item.Alter<CourseItemPart>(part => part.CompletionMode = mode);
                item.Alter<ContainedPart>(part =>
                {
                    part.ListContentItemId = course.ContentItemId;
                    part.ListContentType = course.ContentType;
                });
                await contentManager.CreateAsync(item, VersionOptions.Published);

                return item;
            }

            var quiz = await ItemAsync(AssessmentsConstants.ContentTypes.Quiz, "Pages quiz " + suffix, CompletionModes.Submitted, item =>
                item.Alter<QuizPart>(part =>
                {
                    part.QuestionIds = [question.ContentItemId];
                    part.MaxAttempts = null;
                }));
            var next = await ItemAsync(LearningConstants.ContentTypes.PageLesson, "After the quiz " + suffix, CompletionModes.MarkedDone);

            course.Alter<CourseOutlinePart>(part => part.Sections =
            [
                new CourseSection
                {
                    Id = "s1",
                    Title = "Quiz",
                    Items = [new CourseOutlineItem { ContentItemId = quiz.ContentItemId }, new CourseOutlineItem { ContentItemId = next.ContentItemId }],
                },
            ]);
            await contentManager.UpdateAsync(course);
            await contentManager.PublishAsync(course);

            var enrolled = await services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
            {
                UserId = _fixture.EnrolledUserId,
                CourseId = course.ContentItemId,
                Source = LearningConstants.EnrollmentSources.Manual,
                SourceId = "quiz-pages-" + suffix,
            });

            Assert.True(enrolled.Succeeded, enrolled.ErrorCode);

            return (course.ContentItemId, quiz.ContentItemId, next.ContentItemId);
        });
}
