using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Migrations;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

/// <summary>
/// Migrations walk a table a page at a time and visit each record once, even though saving a record writes its index row
/// again.
/// </summary>
public sealed class SurveyMigrationPagesTests
{
    [Fact]
    public async Task ForEachAsync_SavingEveryRecord_VisitsEachOnceAcrossPages()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();

        for (var index = 0; index < 5; index++)
        {
            await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        }

        var visits = new List<string>();

        // Act: pages of 2, and every record changed and saved.
        var saved = await harness.RunAsync(services => SurveyMigrationPages.ForEachAsync(
            services.GetRequiredService<ISession>(),
            services.GetRequiredService<SurveyStore>(),
            SurveysConstants.CollectionName,
            survey =>
            {
                visits.Add(survey.ItemId);
                survey.Title = "Visited";

                return true;
            },
            pageSize: 2));

        // Assert
        var surveys = await harness.RunAsync(async services => await services.GetRequiredService<SurveyStore>().GetAllAsync());

        Assert.Equal(5, saved);
        Assert.Equal(5, visits.Count);
        Assert.Equal(5, visits.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(5, surveys.Count);
        Assert.All(surveys, survey => Assert.Equal("Visited", survey.Title));
    }
}
