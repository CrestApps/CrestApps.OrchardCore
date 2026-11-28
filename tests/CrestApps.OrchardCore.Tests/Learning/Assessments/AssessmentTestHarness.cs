using System.Security.Claims;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Core.Indexes;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

/// <summary>
/// The Learning harness with the assessments core (the quiz course item and grade scales) and a clock the test moves.
/// Quizzes are surveys: tests that answer them use the Surveys harnesses.
/// </summary>
internal sealed class AssessmentTestHarness : IAsyncDisposable
{
    private AssessmentTestHarness(CalendarTestHarness harness, InMemoryCourses courses, TestClock clock)
    {
        Harness = harness;
        Courses = courses;
        Clock = clock;
    }

    public CalendarTestHarness Harness { get; }

    public InMemoryCourses Courses { get; }

    public TestClock Clock { get; }

    public static async Task<AssessmentTestHarness> CreateAsync(
        Action<IServiceCollection> configure = null,
        Func<global::YesSql.Sql.ISchemaBuilder, Task> extraSchema = null,
        global::YesSql.Indexes.IIndexProvider[] extraIndexProviders = null,
        string[] extraCollections = null)
    {
        var clock = new TestClock { UtcNow = LearningTestHarness.Now };

        var (harness, courses, _) = await LearningTestHarness.CreateAsync(
            services =>
            {
                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(clock);
                services.AddLearningAssessmentsCore();
                services.AddSingleton<global::OrchardCore.Infrastructure.Html.IHtmlSanitizerService, global::OrchardCore.Infrastructure.Html.HtmlSanitizerService>();

                // Quiz attempts are survey responses; the tests add them by hand.
                services.TryAddSingleton<InMemorySurveyResponses>();
                services.TryAddSingleton<ISurveyResponseQueries>(provider => provider.GetRequiredService<InMemorySurveyResponses>());
                services.TryAddSingleton(Moq.Mock.Of<ISurveyManager>());
                services.TryAddSingleton(Moq.Mock.Of<IContentManager>());
                configure?.Invoke(services);
            },
            async schemaBuilder =>
            {
                await AssessmentsIndexSchema.CreateAsync(schemaBuilder);

                if (extraSchema is not null)
                {
                    await extraSchema(schemaBuilder);
                }
            },
            [new GradeScaleIndexProvider(), .. extraIndexProviders ?? []],
            [.. extraCollections ?? []]);

        return new AssessmentTestHarness(harness, courses, clock);
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
