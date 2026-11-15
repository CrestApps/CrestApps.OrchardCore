using CrestApps.OrchardCore.YesSql.Core.WorkQueues;
using System.Security.Claims;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Core.Indexes;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

/// <summary>
/// The Learning harness with the assessments core, in-memory questions and a clock the test moves.
/// </summary>
internal sealed class AssessmentTestHarness : IAsyncDisposable
{
    private AssessmentTestHarness(CalendarTestHarness harness, InMemoryCourses courses, InMemoryQuestions questions, TestClock clock)
    {
        Harness = harness;
        Courses = courses;
        Questions = questions;
        Clock = clock;
    }

    public CalendarTestHarness Harness { get; }

    public InMemoryCourses Courses { get; }

    public InMemoryQuestions Questions { get; }

    public TestClock Clock { get; }

    public static async Task<AssessmentTestHarness> CreateAsync(Action<IServiceCollection> configure = null)
    {
        var clock = new TestClock { UtcNow = LearningTestHarness.Now };
        InMemoryQuestions questions = null;

        var (harness, courses, _) = await LearningTestHarness.CreateAsync(
            services =>
            {
                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(clock);
                services.AddLearningAssessmentsCore();
                services.RemoveAll<IAssessmentContentSource>();
                services.AddSingleton<IAssessmentContentSource>(provider => questions);
                services.AddSingleton<global::OrchardCore.Infrastructure.Html.IHtmlSanitizerService, global::OrchardCore.Infrastructure.Html.HtmlSanitizerService>();
                configure?.Invoke(services);
            },
            AssessmentsIndexSchema.CreateAsync,
            // The deadline queue's index provider comes from DI on a site; the harness store needs it listed.
            [.. AssessmentsIndexProviders.All(), new LeasedWorkItemIndexProvider<AttemptDeadlineWork>(AttemptDeadlineWork.CollectionName)],
            [AttemptDeadlineWork.CollectionName]);

        questions = new InMemoryQuestions(courses);

        return new AssessmentTestHarness(harness, courses, questions, clock);
    }

    public ContentItem AddQuiz(string courseId, string quizId, Action<QuizPart> configure, string completionMode = CompletionModes.Passed, decimal? passMark = 50)
    {
        var quiz = Courses.AddItem(courseId, quizId, kind: AssessmentsConstants.QuizKind, completionMode: completionMode, configure: part =>
        {
            part.Gradable = true;
            part.PassMark = passMark;
        });

        quiz.Alter<QuizPart>(part => configure?.Invoke(part));

        return quiz;
    }

    public async Task<Enrollment> EnrollAsync(string userId, string courseId)
    {
        var result = await Harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
        {
            UserId = userId,
            CourseId = courseId,
            Source = LearningConstants.EnrollmentSources.Manual,
            SourceId = "m-" + userId,
        }));

        return result.Enrollment;
    }

    public Task<AttemptResult> StartAsync(string userId, string quizId, Action<StartAttemptRequest> configure = null)
        => Harness.RunAsync(services =>
        {
            var request = new StartAttemptRequest { UserId = userId, User = CalendarTestHarness.User(userId), QuizId = quizId, HonorConfirmed = true };
            configure?.Invoke(request);

            return services.GetRequiredService<IAssessmentAttemptService>().StartAsync(request);
        });

    public Task<AttemptResult> RunAsync(Func<IAssessmentAttemptService, Task<AttemptResult>> work)
        => Harness.RunAsync(services => work(services.GetRequiredService<IAssessmentAttemptService>()));

    public Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> work)
        => Harness.RunAsync(work);

    public async Task<CourseItemProgress> ProgressAsync(string enrollmentId, string itemId)
        => (await Harness.RunAsync(services => services.GetRequiredService<ILearnerProgressService>().GetAsync(enrollmentId)))?.Find(itemId);

    public ValueTask DisposeAsync()
        => Harness.DisposeAsync();

    public static ClaimsPrincipal Staff(string userId)
        => CalendarTestHarness.User(userId, LearningPermissions.ManageCourses.Name);
}

/// <summary>
/// A clock the test moves.
/// </summary>
internal sealed class TestClock : IClock
{
    public DateTime UtcNow { get; set; }

    public ITimeZone[] GetTimeZones() => [];

    public ITimeZone GetTimeZone(string timeZoneId) => null;

    public ITimeZone GetSystemTimeZone() => null;

    public DateTimeOffset ConvertToTimeZone(DateTimeOffset dateTimeOffset, ITimeZone timeZone) => dateTimeOffset;
}
