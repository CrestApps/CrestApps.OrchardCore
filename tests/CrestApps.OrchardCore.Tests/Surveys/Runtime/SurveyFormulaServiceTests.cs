using CrestApps.OrchardCore.Surveys.Core.Formulas;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

public sealed class SurveyFormulaServiceTests
{
    [Fact]
    public async Task Evaluate_ScoreCategoryAndVariableKeys_Resolve()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Number("q1")));

        definition.ScoreCategories.Add(new ScoreCategory { Id = "cat1", Name = "skill", Title = "Skill" });
        definition.Variables.Add(new SurveyVariable { Name = "bonus", Kind = VariableKind.Hidden, DataType = global::OrchardCore.DataSources.DataFieldType.Decimal });

        var response = new SurveyResponse();

        response.CategoryScores["cat1"] = 4;
        response.Variables["bonus"] = "2";
        response.Answers["q1"] = new SurveyAnswer { Values = ["10"] };

        // Act
        var value = await harness.RunAsync(services => Task.FromResult(services.GetRequiredService<SurveyFormulaService>().Evaluate(definition, response, "[score:skill] + [var:bonus] + [q1]")));

        // Assert
        Assert.Equal(16m, Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task TryCompile_UnknownField_Fails()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Number("q1")));

        // Act
        var (ok, error) = await harness.RunAsync(services =>
        {
            var compiled = services.GetRequiredService<SurveyFormulaService>().TryCompile(definition, "[missing] + 1", out var message);

            return Task.FromResult((compiled, message));
        });

        // Assert
        Assert.False(ok);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public async Task ComputeVariables_FollowsDependencyOrder()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Number("q1")));

        definition.Variables.Add(new SurveyVariable { Name = "total", Kind = VariableKind.Calculated, Formula = "[double] + 1", DataType = global::OrchardCore.DataSources.DataFieldType.Decimal });
        definition.Variables.Add(new SurveyVariable { Name = "double", Kind = VariableKind.Calculated, Formula = "[q1] * 2", DataType = global::OrchardCore.DataSources.DataFieldType.Decimal });

        var response = new SurveyResponse();

        response.Answers["q1"] = new SurveyAnswer { Values = ["5"] };

        // Act
        await harness.RunAsync(services =>
        {
            services.GetRequiredService<SurveyFormulaService>().ComputeVariables(definition, response);

            return Task.FromResult(true);
        });

        // Assert
        Assert.Equal(10m, decimal.Parse(response.Variables["double"], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(11m, decimal.Parse(response.Variables["total"], System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Evaluate_SurveyFunctions_Work()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Choice("q1", "a", "b", "c")));
        var response = new SurveyResponse();

        response.Answers["q1"] = new SurveyAnswer { Values = ["a", "c"] };

        // Act
        var (selected, count, answered) = await harness.RunAsync(services =>
        {
            var formulas = services.GetRequiredService<SurveyFormulaService>();

            return Task.FromResult((formulas.EvaluateBoolean(definition, response, "SELECTED([q1], 'c')"), formulas.Evaluate(definition, response, "COUNTSELECTED([q1])"), formulas.EvaluateBoolean(definition, response, "ANSWERED([q1])")));
        });

        // Assert
        Assert.True(selected);
        Assert.Equal(2L, count);
        Assert.True(answered);
    }
}
