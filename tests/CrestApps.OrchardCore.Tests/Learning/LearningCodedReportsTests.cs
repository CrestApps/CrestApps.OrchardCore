using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Reports;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// The Learning coded reports count enrollments per course, list completions for one learner or everyone, and list
/// learners idle for longer than the inactive period.
/// </summary>
public sealed class LearningCodedReportsTests
{
    [Fact]
    public async Task CourseCompletion_CountsEnrolledAndCompletedPerCourse()
    {
        // Arrange
        var (harness, courses) = await CreateAsync();
        await using var _ = harness;
        var first = await EnrollAsync(harness, "ann", "course1");
        await EnrollAsync(harness, "bob", "course1");
        await harness.RunAsync(services => services.GetRequiredService<ILearnerProgressService>().RecordAccessAsync(first, "lesson", 0));

        // Act
        var document = await RunAsync<LearningCourseCompletionReport>(harness);

        // Assert
        var table = document.Sections.Single(section => section.Kind == ReportSectionKind.Table);
        Assert.Equal(["Safety", "2", "1", "1", "0", "50.0%"], table.Rows[0].Cells.Take(6));
        Assert.Equal(ReportRowKind.GrandTotal, table.Rows[^1].Kind);
    }

    [Fact]
    public async Task Transcript_ListsCompletions_ForOneLearnerByUserName()
    {
        // Arrange
        var (harness, _) = await CreateAsync();
        await using var _harness = harness;
        var ann = await EnrollAsync(harness, "ann", "course1");
        var bob = await EnrollAsync(harness, "bob", "course1");
        await harness.RunAsync(services => services.GetRequiredService<ILearnerProgressService>().RecordAccessAsync(ann, "lesson", 0));
        await harness.RunAsync(services => services.GetRequiredService<ILearnerProgressService>().RecordAccessAsync(bob, "lesson", 0));

        // Act
        var everyone = await RunAsync<LearningTranscriptReport>(harness);
        var annOnly = await RunAsync<LearningTranscriptReport>(harness, filter => filter.Set(LearningTranscriptReport.LearnerFilterKey, "ann-name"));
        var nobody = await RunAsync<LearningTranscriptReport>(harness, filter => filter.Set(LearningTranscriptReport.LearnerFilterKey, "missing"));

        // Assert
        Assert.Equal(2, everyone.Sections.Single(section => section.Kind == ReportSectionKind.Table).Rows.Count);
        var row = Assert.Single(annOnly.Sections.Single(section => section.Kind == ReportSectionKind.Table).Rows);
        Assert.Equal("ann-name", row.Cells[0]);
        Assert.Equal("Safety", row.Cells[1]);
        Assert.DoesNotContain(nobody.Sections, section => section.Kind == ReportSectionKind.Table);
    }

    [Fact]
    public async Task InactiveLearners_ListsOnlyActiveEnrollmentsIdleLongerThanThePeriod()
    {
        // Arrange
        var (harness, _) = await CreateAsync();
        await using var _harness = harness;
        var idle = await EnrollAsync(harness, "ann", "course1");
        await EnrollAsync(harness, "bob", "course1");
        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<EnrollmentStore>();
            var enrollment = await store.FindByIdAsync(idle.ItemId);

            enrollment.CreatedUtc = LearningTestHarness.Now.AddDays(-40);
            enrollment.LastActivityUtc = LearningTestHarness.Now.AddDays(-20);
            await store.SaveAsync(enrollment);
        });

        // Act
        var document = await RunAsync<LearningInactiveLearnersReport>(harness);

        // Assert
        var row = Assert.Single(document.Sections.Single(section => section.Kind == ReportSectionKind.Table).Rows);
        Assert.Equal("ann-name", row.Cells[0]);
        Assert.Equal("20", row.Cells[4]);
    }

    private static async Task<(CalendarTestHarness Harness, InMemoryCourses Courses)> CreateAsync()
    {
        var (harness, courses, _) = await LearningTestHarness.CreateAsync(services =>
        {
            var localClock = new Mock<ILocalClock>();
            localClock.Setup(clock => clock.ConvertToLocalAsync(It.IsAny<DateTimeOffset>())).Returns<DateTimeOffset>(Task.FromResult);
            services.AddSingleton(localClock.Object);
            services.AddScoped<LearnerUserLookup, FakeUserLookup>();
            services.AddScoped<LearningReportText>();
            services.AddScoped<LearningCourseCompletionReport>();
            services.AddScoped<LearningTranscriptReport>();
            services.AddScoped<LearningInactiveLearnersReport>();
        });

        courses.AddCourse("course1", title: "Safety");
        courses.AddItem("course1", "lesson");

        return (harness, courses);
    }

    private static Task<ReportDocument> RunAsync<TReport>(CalendarTestHarness harness, Action<ReportFilter> configure = null)
        where TReport : IReportRunner
        => harness.RunAsync(services =>
        {
            var filter = new ReportFilter();
            filter.SetDateRange(new ReportDateRange { FromUtc = LearningTestHarness.Now.AddDays(-30), ToUtc = LearningTestHarness.Now.AddDays(1) });
            configure?.Invoke(filter);

            return services.GetRequiredService<TReport>().RunAsync(new ReportContext(filter));
        }, commit: false);

    private static async Task<Enrollment> EnrollAsync(CalendarTestHarness harness, string userId, string courseId)
    {
        var result = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
        {
            UserId = userId,
            CourseId = courseId,
            Source = LearningConstants.EnrollmentSources.Manual,
            SourceId = "m-" + userId,
        }));

        return result.Enrollment;
    }

    private sealed class FakeUserLookup : LearnerUserLookup
    {
        public FakeUserLookup()
            : base(null, null)
        {
        }

        public override Task<IDictionary<string, string>> GetUserNamesAsync(IEnumerable<string> userIds)
            => Task.FromResult<IDictionary<string, string>>(userIds.Distinct().ToDictionary(id => id, id => id + "-name"));

        public override Task<string> FindUserIdAsync(string userName)
            => Task.FromResult(userName.EndsWith("-name", StringComparison.Ordinal) ? userName[..^5] : null);
    }
}
