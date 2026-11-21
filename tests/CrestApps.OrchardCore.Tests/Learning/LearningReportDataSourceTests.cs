using System.Security.Claims;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Reports;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Tests.Learning;

public sealed class LearningReportDataSourceTests
{
    private static readonly ClaimsPrincipal _reporter = CalendarTestHarness.User("reporter", LearningPermissions.ViewLearningReports.Name);

    [Fact]
    public async Task WithoutTheReportPermission_NothingIsExposed()
    {
        // Arrange
        var (harness, _, _) = await CreateAsync();
        await using var __ = harness;

        // Act
        var (dataSets, schema, anonymous) = await harness.RunAsync(async services =>
        {
            var source = services.GetRequiredService<LearningReportDataSource>();
            var context = new DataSourceContext { User = CalendarTestHarness.User("learner") };

            return (
                await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken),
                await source.GetSchemaAsync(LearningEnrollmentsReportDataSet.DataSetName, context, TestContext.Current.CancellationToken),
                await source.GetDataSetsAsync(new DataSourceContext { User = null }, TestContext.Current.CancellationToken));
        }, commit: false);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(anonymous);
    }

    [Fact]
    public async Task DataSets_AreListedForAReporter_WithStableNamesAndReferences()
    {
        // Arrange
        var (harness, _, _) = await CreateAsync();
        await using var __ = harness;

        // Act
        var (dataSets, schema) = await harness.RunAsync(async services =>
        {
            var source = services.GetRequiredService<LearningReportDataSource>();
            var context = new DataSourceContext { User = _reporter };

            return (
                await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken),
                await source.GetSchemaAsync(LearningEnrollmentsReportDataSet.DataSetName, context, TestContext.Current.CancellationToken));
        }, commit: false);

        // Assert
        Assert.Equal(["LearningEnrollments", "LearningItemProgress", "LearningCompletions", "LearningCourseStaff"], dataSets.Select(dataSet => dataSet.Name));
        Assert.True(schema.Fields.Single(field => field.Name == "UserId").IsIdentifier);
        Assert.True(schema.Fields.Single(field => field.Name == "CourseId").IsIdentifier);
        Assert.Equal(DataFieldType.DateTime, schema.Fields.Single(field => field.Name == "CreatedUtc").Type);
    }

    [Fact]
    public async Task Enrollments_ReturnTypedValues_NewestFirst_AndStopAtMaxRows()
    {
        // Arrange
        var clock = new LearningTestClock();
        var (harness, courses, _) = await CreateAsync(clock);
        await using var __ = harness;
        courses.AddCourse("course1", title: "Safety");
        courses.AddItem("course1", "lesson", completionMode: CompletionModes.MarkedDone);
        var first = await EnrollAsync(harness, "learner1");
        await harness.RunAsync(services => services.GetRequiredService<ILearnerProgressService>().RecordAccessAsync(first, "lesson", 0));
        clock.UtcNow = LearningTestHarness.Now.AddSeconds(40);
        await harness.RunAsync(services => services.GetRequiredService<ILearnerProgressService>().RecordAccessAsync(first, "lesson", 40));
        await harness.RunAsync(services => services.GetRequiredService<ILearnerProgressService>().MarkDoneAsync(first, "lesson"));
        await EnrollAsync(harness, "learner2");

        // Act
        var (all, limited, progress, completions) = await harness.RunAsync(async services =>
        {
            var source = services.GetRequiredService<LearningReportDataSource>();

            var limitedQuery = Query(LearningEnrollmentsReportDataSet.DataSetName);
            limitedQuery.MaxRows = 1;

            return (
                Rows(await ReportDesignerTestServices.ReadAsync(source, Query(LearningEnrollmentsReportDataSet.DataSetName), TestContext.Current.CancellationToken)),
                await ReportDesignerTestServices.ReadAsync(source, limitedQuery, TestContext.Current.CancellationToken),
                Rows(await ReportDesignerTestServices.ReadAsync(source, Query(LearningItemProgressReportDataSet.DataSetName), TestContext.Current.CancellationToken)),
                Rows(await ReportDesignerTestServices.ReadAsync(source, Query(LearningCompletionsReportDataSet.DataSetName), TestContext.Current.CancellationToken)));
        }, commit: false);

        // Assert
        Assert.True(all.Count == 2, string.Join("; ", all.Select(row => $"{row["ItemId"]} {row["UserId"]} {row["Status"]}")));
        Assert.Equal(["learner2", "learner1"], all.Select(row => row["UserId"]).Cast<string>().OrderByDescending(id => id));

        var completed = all.Single(row => (string)row["UserId"] == "learner1");

        Assert.Equal("Completed", completed["Status"]);
        Assert.Equal(100L, completed["ProgressPercent"]);
        Assert.Equal(1L, completed["Cycle"]);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(completed["CreatedUtc"]).Kind);
        Assert.Null(all.Single(row => (string)row["UserId"] == "learner2")["CompletedUtc"]);

        Assert.True(limited.Truncated);
        Assert.Single(limited.Rows);

        var item = Assert.Single(progress);

        Assert.Equal("lesson", item["ContentItemId"]);
        Assert.Equal("Completed", item["State"]);
        Assert.Equal(40L, item["SecondsSpent"]);

        var completion = Assert.Single(completions);

        Assert.Equal("Safety", completion["CourseTitle"]);
        Assert.Equal(40L, completion["SecondsSpent"]);
    }

    private static Task<(CalendarTestHarness Harness, InMemoryCourses Courses, RecordingEnrollmentObserver Observer)> CreateAsync(LearningTestClock clock = null)
        => LearningTestHarness.CreateAsync(services =>
        {
            clock?.Use(services);
            services.AddScoped<ILearningReportDataSet, LearningEnrollmentsReportDataSet>();
            services.AddScoped<ILearningReportDataSet, LearningItemProgressReportDataSet>();
            services.AddScoped<ILearningReportDataSet, LearningCompletionsReportDataSet>();
            services.AddScoped<ILearningReportDataSet, LearningCourseStaffReportDataSet>();
            services.AddScoped<LearningReportDataSource>();
        });

    private static async Task<Enrollment> EnrollAsync(CalendarTestHarness harness, string userId)
    {
        var result = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
        {
            UserId = userId,
            CourseId = "course1",
            Source = LearningConstants.EnrollmentSources.Manual,
            SourceId = "m-" + userId,
        }));

        return result.Enrollment;
    }

    private static DataSourceQuery Query(string dataSet)
        => new()
        {
            DataSet = dataSet,
            MaxRows = 100,
            Context = new DataSourceContext { User = _reporter },
        };

    private static List<Dictionary<string, object>> Rows(DataRowSet table)
        => table.Rows
            .Select(row => table.Fields.Select((field, index) => (field.Name, Value: row[index])).ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal))
            .ToList();
}
