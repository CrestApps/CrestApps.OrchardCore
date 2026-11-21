using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Coursework;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Learning.Assessments;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.Coursework;

/// <summary>
/// Learning plan Phase 7: the grading list merges the work of every source (quiz attempts, coursework submissions) by
/// submitted time, oldest first, pages across the merged list, and asks each source for no more than it needs.
/// </summary>
public sealed class GradingQueueTests
{
    [Fact]
    public async Task Page_MergesTheSourcesOldestFirst_AndPagesAcrossThem()
    {
        // Arrange
        var start = new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
        var attempts = new FakeSource("Attempt", start, start.AddHours(2), start.AddHours(4));
        var submissions = new FakeSource("Submission", start.AddHours(1), start.AddHours(3), start.AddHours(5));
        var queue = new GradingQueue([attempts, submissions]);

        // Act
        var first = await queue.PageAsync(null, 1, 2, TestContext.Current.CancellationToken);
        var second = await queue.PageAsync(null, 2, 2, TestContext.Current.CancellationToken);
        var third = await queue.PageAsync(null, 3, 2, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(6, first.Count);
        Assert.Equal(["Attempt0", "Submission0"], first.Items.Select(item => item.Id));
        Assert.Equal(["Attempt1", "Submission1"], second.Items.Select(item => item.Id));
        Assert.Equal(["Attempt2", "Submission2"], third.Items.Select(item => item.Id));
        Assert.Equal([2, 4, 6], attempts.Takes);
        Assert.Equal([2, 4, 6], submissions.Takes);
    }

    [Fact]
    public async Task Page_PassesTheGradersCoursesToEverySource()
    {
        // Arrange
        var source = new FakeSource("Attempt", DateTime.UtcNow);
        var queue = new GradingQueue([source]);

        // Act
        await queue.PageAsync(["course1"], 1, 10, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["course1"], source.CourseIds);
    }

    [Fact]
    public async Task TheRealSources_ListAnEssayAttemptAndACourseworkSubmissionTogether_OldestFirst()
    {
        // Arrange: a quiz attempt (a survey response) that waits for a grader, then a coursework submission an hour later.
        await using var h = await CourseworkTestHarness.CreateAsync();
        h.Courses.AddCourse("course1");
        h.AddCoursework("course1", "work1");
        var enrollment = await h.EnrollAsync("ana", "course1");
        var attempt = await h.RunAsync(services =>
        {
            var response = new SurveyResponse
            {
                ItemId = "response1",
                SurveyId = "quiz-survey",
                UserId = "ana",
                Status = SurveyResponseStatus.Completed,
                Grading = GradingStatus.Pending,
                Context = LearningSurveyContexts.Enrollment(enrollment.ItemId),
                StartedUtc = LearningTestHarness.Now,
                CompletedUtc = LearningTestHarness.Now,
            };

            response.Properties[LearningSurveyContexts.CourseIdProperty] = "course1";
            response.Properties[LearningSurveyContexts.CourseItemIdProperty] = "quiz";
            services.GetRequiredService<InMemorySurveyResponses>().Add(response);

            return Task.FromResult(response);
        });
        h.Clock.UtcNow = LearningTestHarness.Now.AddHours(1);
        var submission = (await h.SubmitAsync("ana", "work1", "My answer")).Submission;

        // Act
        var page = await h.RunAsync(services => services.GetRequiredService<GradingQueue>().PageAsync(null, 1, 10));
        var otherCourse = await h.RunAsync(services => services.GetRequiredService<GradingQueue>().PageAsync(["course2"], 1, 10));

        // Assert
        Assert.NotNull(submission);
        Assert.Equal(2, page.Count);
        Assert.Collection(
            page.Items,
            item =>
            {
                Assert.Equal(AssessmentsConstants.QuizResponseGradingQueueKind, item.Kind);
                Assert.Equal(attempt.ItemId, item.Id);
                Assert.Equal("SurveyGrading", item.Controller);
            },
            item =>
            {
                Assert.Equal(CourseworkConstants.GradingQueueKind, item.Kind);
                Assert.Equal(submission.ItemId, item.Id);
                Assert.Equal("CourseworkGrading", item.Controller);
                Assert.Equal("ana", item.UserId);
            });
        Assert.Equal(0, otherCourse.Count);
        Assert.Empty(otherCourse.Items);
    }

    private sealed class FakeSource : IGradingQueueSource
    {
        private readonly string _kind;
        private readonly DateTime[] _times;

        public FakeSource(string kind, params DateTime[] times)
        {
            _kind = kind;
            _times = times;
        }

        public List<int> Takes { get; } = [];

        public IReadOnlyCollection<string> CourseIds { get; private set; }

        public Task<GradingQueuePage> ListWaitingAsync(GradingQueueQuery query, CancellationToken cancellationToken = default)
        {
            Takes.Add(query.Take);
            CourseIds = query.CourseIds;

            return Task.FromResult(new GradingQueuePage
            {
                Count = _times.Length,
                Items = _times
                    .Select((time, index) => new GradingQueueItem { Kind = _kind, Id = _kind + index, SubmittedUtc = time })
                    .Take(query.Take)
                    .ToArray(),
            });
        }
    }
}
