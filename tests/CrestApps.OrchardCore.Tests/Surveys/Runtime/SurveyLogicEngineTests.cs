using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Core.Logic;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

public sealed class SurveyLogicEngineTests
{
    [Fact]
    public async Task Evaluate_ChainOfRules_ClosesWhenAnEarlyAnswerChanges()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var q1 = RuntimeSurveys.Choice("q1", "yes", "no");
        var q2 = RuntimeSurveys.Choice("q2", "yes", "no");
        var q3 = RuntimeSurveys.Text("q3");

        q2.Visibility = RuntimeSurveys.ShowWhen("q1", ConditionOperator.Is, "yes");
        q3.Visibility = RuntimeSurveys.ShowWhen("q2", ConditionOperator.Is, "yes");

        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", q1, q2, q3));
        var response = new SurveyResponse();

        response.Answers["q1"] = new SurveyAnswer { Values = ["no"] };
        response.Answers["q2"] = new SurveyAnswer { Values = ["yes"] };

        // Act
        var state = await harness.RunAsync(services => Task.FromResult(services.GetRequiredService<SurveyLogicEngine>().Evaluate(definition, response)));

        // Assert
        Assert.Contains("q2", state.HiddenQuestionIds);
        Assert.Contains("q3", state.HiddenQuestionIds);
    }

    [Fact]
    public async Task Evaluate_HideWhen_HidesWhenTheRuleHolds_AndIgnoresUnknownSources()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var q1 = RuntimeSurveys.Choice("q1", "yes", "no");
        var q2 = RuntimeSurveys.Text("q2");
        var q3 = RuntimeSurveys.Text("q3");

        q2.Visibility = RuntimeSurveys.HideWhen("q1", ConditionOperator.Is, "yes");
        q3.Visibility = RuntimeSurveys.HideWhen("removed-question", ConditionOperator.IsEmpty);

        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", q1, q2, q3));
        var response = new SurveyResponse();

        response.Answers["q1"] = new SurveyAnswer { Values = ["yes"] };

        // Act
        var state = await harness.RunAsync(services => Task.FromResult(services.GetRequiredService<SurveyLogicEngine>().Evaluate(definition, response)));

        // Assert
        Assert.Contains("q2", state.HiddenQuestionIds);
        Assert.DoesNotContain("q3", state.HiddenQuestionIds);
    }

    [Fact]
    public async Task Evaluate_HiddenPage_HidesItsQuestions_AndChoiceVisibilityApplies()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var q1 = RuntimeSurveys.Choice("q1", "a", "b");
        var q2 = RuntimeSurveys.Choice("q2", "x", "y");

        q2.Choices[1].Visibility = RuntimeSurveys.ShowWhen("q1", ConditionOperator.Is, "b");

        var page2 = RuntimeSurveys.Page("p2", RuntimeSurveys.Text("q3"));
        page2.Visibility = RuntimeSurveys.ShowWhen("q1", ConditionOperator.Is, "b");

        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", q1, q2), page2);
        var response = new SurveyResponse();

        response.Answers["q1"] = new SurveyAnswer { Values = ["a"] };

        // Act
        var state = await harness.RunAsync(services => Task.FromResult(services.GetRequiredService<SurveyLogicEngine>().Evaluate(definition, response)));

        // Assert
        Assert.Contains("p2", state.HiddenPageIds);
        Assert.Contains("q3", state.HiddenQuestionIds);
        Assert.True(state.IsChoiceHidden("q2", "y"));
        Assert.False(state.IsChoiceHidden("q2", "x"));
    }

    [Fact]
    public async Task Evaluate_RequiredWhenAndEnabledWhen_FollowTheirRules()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var q1 = RuntimeSurveys.Choice("q1", "yes", "no");
        var q2 = RuntimeSurveys.Text("q2");
        var q3 = RuntimeSurveys.Text("q3", required: true);

        q2.RequiredWhen = RuntimeSurveys.ShowWhen("q1", ConditionOperator.Is, "yes").Rule;
        q3.EnabledWhen = RuntimeSurveys.ShowWhen("q1", ConditionOperator.Is, "no").Rule;

        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", q1, q2, q3));
        var response = new SurveyResponse();

        response.Answers["q1"] = new SurveyAnswer { Values = ["yes"] };

        // Act
        var state = await harness.RunAsync(services => Task.FromResult(services.GetRequiredService<SurveyLogicEngine>().Evaluate(definition, response)));

        // Assert
        Assert.Contains("q2", state.RequiredQuestionIds);
        Assert.Contains("q3", state.DisabledQuestionIds);
    }

    [Fact]
    public async Task Evaluate_VariableAndFormulaSources_AreRead()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var q1 = RuntimeSurveys.Number("q1");
        var q2 = RuntimeSurveys.Text("q2");
        var q3 = RuntimeSurveys.Text("q3");

        q2.Visibility = RuntimeSurveys.ShowWhen("var:region", ConditionOperator.Is, "west");
        q3.Visibility = new SurveyVisibility { Mode = VisibilityMode.ShowWhen, Formula = "[q1] > 5" };

        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", q1, q2, q3));
        definition.Variables.Add(new SurveyVariable { Name = "region", Kind = VariableKind.Hidden });

        var response = new SurveyResponse();

        response.Variables["region"] = "east";
        response.Answers["q1"] = new SurveyAnswer { Values = ["3"] };

        // Act
        var state = await harness.RunAsync(services => Task.FromResult(services.GetRequiredService<SurveyLogicEngine>().Evaluate(definition, response)));

        // Assert
        Assert.Contains("q2", state.HiddenQuestionIds);
        Assert.Contains("q3", state.HiddenQuestionIds);
    }
}
