using System.Security.Claims;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Reports;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

public sealed class AssessmentsReportDataSourceTests
{
    private static readonly ClaimsPrincipal _reporter = CalendarTestHarness.User("reporter", LearningPermissions.ViewLearningReports.Name);

    [Fact]
    public async Task WithoutTheReportPermission_NoAttemptOrAnswerIsExposed()
    {
        // Arrange
        await using var h = await CreateAsync();

        // Act
        var (dataSets, schema) = await h.RunAsync(async services =>
        {
            var source = services.GetRequiredService<AssessmentsReportDataSource>();
            var context = new DataSourceContext { User = CalendarTestHarness.User("learner") };

            return (
                await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken),
                await source.GetSchemaAsync(LearningAttemptAnswersReportDataSet.DataSetName, context, TestContext.Current.CancellationToken));
        });

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
    }

    [Fact]
    public async Task TheDataSets_CarryScoresAndAnswers_ButNoKeyFeedbackOrGuidance()
    {
        // Arrange
        await using var h = await CreateAsync();
        h.Courses.AddCourse("course1");
        h.Questions.AddChoice("q1", "a", options: ["a", "b"]);
        h.AddQuiz("course1", "quiz", quiz => quiz.QuestionIds = ["q1"]);
        await h.EnrollAsync("learner", "course1");
        var attempt = (await h.StartAsync("learner", "quiz")).Attempt;
        await h.RunAsync(service => service.SaveResponseAsync(attempt.ItemId, "learner", 0, new AttemptResponse { ChoiceIds = ["b"] }));
        await h.RunAsync(service => service.SubmitAsync(attempt.ItemId, "learner"));

        // Act
        var (dataSets, schemas, answers) = await h.RunAsync(async services =>
        {
            var source = services.GetRequiredService<AssessmentsReportDataSource>();
            var context = new DataSourceContext { User = _reporter };
            var sets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
            var all = new List<DataSetSchema>();

            foreach (var set in sets)
            {
                all.Add(await source.GetSchemaAsync(set.Name, context, TestContext.Current.CancellationToken));
            }

            var rows = await ReportDesignerTestServices.ReadAsync(source, new DataSourceQuery { DataSet = LearningAttemptAnswersReportDataSet.DataSetName, MaxRows = 100, Context = context }, TestContext.Current.CancellationToken);

            return (sets, all, rows);
        });

        // Assert
        Assert.Equal(["LearningAttempts", "LearningAttemptAnswers", "LearningAttemptScoreChanges", "LearningQuizOverrides"], dataSets.Select(set => set.Name));
        Assert.All(schemas.SelectMany(schema => schema.Fields), field =>
        {
            Assert.DoesNotContain("Key", field.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Correct", field.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Feedback", field.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Guidance", field.Name, StringComparison.OrdinalIgnoreCase);
        });

        var row = Assert.Single(answers.Rows);
        var fields = answers.Fields.Select(field => field.Name).ToList();
        Assert.Equal("Option b", row[fields.IndexOf("Answer")]);
        Assert.Equal(0m, row[fields.IndexOf("Score")]);
    }

    private static Task<AssessmentTestHarness> CreateAsync()
        => AssessmentTestHarness.CreateAsync(services =>
        {
            services.AddScoped<IAssessmentsReportDataSet, LearningAttemptsReportDataSet>();
            services.AddScoped<IAssessmentsReportDataSet, LearningAttemptAnswersReportDataSet>();
            services.AddScoped<IAssessmentsReportDataSet, LearningAttemptScoreChangesReportDataSet>();
            services.AddScoped<IAssessmentsReportDataSet, LearningQuizOverridesReportDataSet>();
            services.AddScoped<AssessmentsReportDataSource>();
        });
}
