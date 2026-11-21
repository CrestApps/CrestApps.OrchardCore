using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

public sealed class SurveyPlanBuilderTests
{
    [Fact]
    public void SurveyRandom_SameSeed_GivesSameSequence()
    {
        // Arrange
        var first = new SurveyRandom(42);
        var second = new SurveyRandom(42);

        // Act
        var a = Enumerable.Range(0, 20).Select(_ => first.Next(1000)).ToArray();
        var b = Enumerable.Range(0, 20).Select(_ => second.Next(1000)).ToArray();

        // Assert
        Assert.Equal(a, b);
        Assert.All(a, value => Assert.InRange(value, 0, 999));
    }

    [Fact]
    public async Task Build_SameSeed_GivesSamePlan()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = PooledDefinition();

        // Act
        var (first, second) = await harness.RunAsync(services =>
        {
            var builder = services.GetRequiredService<SurveyPlanBuilder>();

            return Task.FromResult((builder.Build("s", definition, 7), builder.Build("s", definition, 7)));
        });

        // Assert
        Assert.Equal(first.PageOrder, second.PageOrder);
        Assert.Equal(first.QuestionOrder["p1"], second.QuestionOrder["p1"]);
        Assert.Equal(first.ChoiceOrder["c"], second.ChoiceOrder["c"]);
    }

    [Fact]
    public async Task Build_PoolDrawsCount_AndKeepsPinnedQuestionsInPlace()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = PooledDefinition();

        // Act
        var plans = await harness.RunAsync(services =>
        {
            var builder = services.GetRequiredService<SurveyPlanBuilder>();

            return Task.FromResult(Enumerable.Range(1, 30).Select(seed => builder.Build("s", definition, seed)).ToList());
        });

        // Assert
        foreach (var plan in plans)
        {
            var ids = plan.QuestionOrder["p1"];

            Assert.Equal(4, ids.Count);
            Assert.Equal("intro", ids[0]);
            Assert.Equal(2, ids.Count(id => id.StartsWith('m')));
            Assert.Contains("c", ids);
        }

        Assert.True(plans.Select(plan => string.Join(",", plan.QuestionOrder["p1"])).Distinct().Count() > 1);
    }

    [Fact]
    public async Task Build_ShuffledChoices_KeepOtherAndExclusiveLast()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = PooledDefinition();

        // Act
        var plans = await harness.RunAsync(services =>
        {
            var builder = services.GetRequiredService<SurveyPlanBuilder>();

            return Task.FromResult(Enumerable.Range(1, 20).Select(seed => builder.Build("s", definition, seed)).ToList());
        });

        // Assert
        Assert.All(plans, plan =>
        {
            Assert.Equal("none", plan.ChoiceOrder["c"].Last());
            Assert.DoesNotContain("retired", plan.ChoiceOrder["c"]);
        });
    }

    [Fact]
    public async Task Build_QuestionsPerPage_SplitsPagesIntoScreens()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = RuntimeSurveys.Definition(
            RuntimeSurveys.Page("p1", RuntimeSurveys.Text("a"), RuntimeSurveys.Text("b"), RuntimeSurveys.Text("c")),
            RuntimeSurveys.Page("p2", RuntimeSurveys.Text("d")));

        definition.Presentation.QuestionsPerPage = 1;

        // Act
        var plan = await harness.RunAsync(services => Task.FromResult(services.GetRequiredService<SurveyPlanBuilder>().Build("s", definition, 1)));

        // Assert
        Assert.Equal(["p1", "p1#2", "p1#3", "p2"], plan.PageOrder);
        Assert.Equal("p1", SurveyPlanNavigator.PageOf(plan, "p1#3"));
        Assert.Equal(["c"], plan.QuestionOrder["p1#3"]);
    }

    private static SurveyDefinition PooledDefinition()
    {
        var intro = RuntimeSurveys.Text("intro");
        intro.Pinned = true;

        var choice = RuntimeSurveys.Choice("c", "a", "b", "c", "d", "retired", "none");
        choice.ShuffleChoices = true;
        choice.Choices.First(item => item.Id == "none").IsExclusive = true;
        choice.Choices.First(item => item.Id == "retired").Retired = true;

        var page = RuntimeSurveys.Page("p1", intro, RuntimeSurveys.Text("m1"), RuntimeSurveys.Text("m2"), RuntimeSurveys.Text("m3"), RuntimeSurveys.Text("m4"), choice, RuntimeSurveys.Text("last"));

        page.Pools.Add(new QuestionPool { Id = "pool", Name = "pool", DrawCount = 2 });

        foreach (var member in page.Questions.Where(question => question.Id.StartsWith('m')))
        {
            member.PoolId = "pool";
        }

        page.Questions.RemoveAll(question => question.Id == "last");
        page.QuestionOrder = QuestionOrder.Shuffle;

        return RuntimeSurveys.Definition(page);
    }
}
