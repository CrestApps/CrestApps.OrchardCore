using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;

namespace CrestApps.OrchardCore.Tests.Surveys.Extras;

public sealed class SurveyPoolCounterTests
{
    [Fact]
    public async Task Start_EvenlyPresentedPool_PresentsEveryQuestionOnceBeforeAnyTwice()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync(services => services.AddSurveysCollectingExtras());
        var page = RuntimeSurveys.Page("p1", RuntimeSurveys.Text("a"), RuntimeSurveys.Text("b"), RuntimeSurveys.Text("c"));

        page.Pools.Add(new QuestionPool { Id = "pool", Name = "pool", DrawCount = 1, EvenlyPresent = true });

        foreach (var question in page.Questions)
        {
            question.PoolId = "pool";
        }

        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(page));
        var shown = new List<string>();

        // Act
        for (var i = 0; i < 3; i++)
        {
            var response = (await harness.StartAsync(survey, "u" + i)).Response;
            shown.AddRange(response.Plan.QuestionOrder["p1"]);
        }

        // Assert
        Assert.Equal(["a", "b", "c"], shown.Order(StringComparer.Ordinal));
    }
}
