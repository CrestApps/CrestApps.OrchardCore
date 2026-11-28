using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Migrations;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

/// <summary>
/// The versions list reads a page of the version index, which holds what its rows show (number, date, publisher,
/// notes), so it loads no version and its definition; versions indexed before those columns get them from a step.
/// </summary>
public sealed class SurveyVersionRowsTests
{
    [Fact]
    public async Task PageRowsAsync_ReadsAPageNewestFirst_WithThePublisherAndNotes()
    {
        // Arrange: a survey with five versions.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));

        await harness.RunAsync(async services =>
        {
            var versions = services.GetRequiredService<SurveyVersionStore>();

            for (var number = 2; number <= 5; number++)
            {
                await versions.CreateAsync(new SurveyVersion
                {
                    SurveyId = survey.ItemId,
                    Number = number,
                    Definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))),
                    PublishedUtc = RuntimeTestHarness.Now.AddDays(number),
                    PublishedBy = "user-author",
                    Notes = number == 5 ? new string('n', 600) : "Release " + number,
                });
            }

            return true;
        });

        // Act
        var (count, rows) = await harness.RunAsync(services => services.GetRequiredService<SurveyVersionStore>().PageRowsAsync(survey.ItemId, 0, 2, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(5, count);
        Assert.Equal([5, 4], rows.Select(row => row.Number));
        Assert.Equal("user-author", rows[0].PublishedBy);
        Assert.Equal(SurveyVersionIndex.NotesLength, rows[0].Notes.Length);
        Assert.EndsWith("…", rows[0].Notes, StringComparison.Ordinal);
        Assert.Equal("Release 4", rows[1].Notes);
    }

    [Fact]
    public async Task SurveyVersionsReindexStep_FillsThePublisherOfVersionsIndexedBefore()
    {
        // Arrange: a version whose index row predates the publisher and notes columns.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));

        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            var configuration = session.Store.Configuration;
            var dialect = configuration.SqlDialect;
            var index = dialect.QuoteForTableName(configuration.TablePrefix + configuration.TableNameConvention.GetIndexTable(typeof(SurveyVersionIndex), SurveysConstants.CollectionName), configuration.Schema);
            var connection = await session.CreateConnectionAsync();

            Assert.Equal(1, await connection.ExecuteAsync(
                $"UPDATE {index} SET {dialect.QuoteForColumnName("PublishedBy")} = NULL WHERE {dialect.QuoteForColumnName("SurveyId")} = @Id",
                new { Id = survey.ItemId }));

            return true;
        });

        // Act
        var done = await harness.RunAsync(async services =>
        {
            var finished = await ActivatorUtilities.CreateInstance<SurveyVersionsReindexStep>(services).RunAsync(new ResumableStepContext(), TestContext.Current.CancellationToken);
            await services.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);

            return finished;
        });

        // Assert: a version nobody is recorded for reads an empty publisher, not "not indexed yet".
        var (_, rows) = await harness.RunAsync(services => services.GetRequiredService<SurveyVersionStore>().PageRowsAsync(survey.ItemId, 0, 10, TestContext.Current.CancellationToken));

        Assert.True(done);
        Assert.Equal(string.Empty, Assert.Single(rows).PublishedBy);
    }
}
