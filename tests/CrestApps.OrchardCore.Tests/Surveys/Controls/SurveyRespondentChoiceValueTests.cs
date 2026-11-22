using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Surveys.Controls;

/// <summary>
/// Respondents never get the export values of a scored question's choices: values numbered in the right order would
/// give a ranking's key away.
/// </summary>
public sealed class SurveyRespondentChoiceValueTests
{
    [Fact]
    public void Project_ScoredRanking_LeavesTheChoiceValuesOut()
    {
        // Arrange
        using var root = ControlsTestServices.Build();
        var projector = new SurveyRespondentProjector(ControlsTestServices.Registry(root.CreateScope().ServiceProvider));
        var question = Ranking();
        var definition = new SurveyDefinition { Pages = [new SurveyPage { Questions = [question] }], Scoring = new SurveyScoringSettings { Enabled = true } };

        // Act
        var view = projector.Project(question, definition, plan: null, hiddenChoiceIds: null, pipe: null);

        // Assert
        Assert.Equal(3, view.Choices.Count);
        Assert.All(view.Choices, choice => Assert.Null(choice.Value));
    }

    [Fact]
    public void Project_UnscoredSurvey_KeepsTheChoiceValues()
    {
        // Arrange
        using var root = ControlsTestServices.Build();
        var projector = new SurveyRespondentProjector(ControlsTestServices.Registry(root.CreateScope().ServiceProvider));
        var question = Ranking();
        var definition = new SurveyDefinition { Pages = [new SurveyPage { Questions = [question] }] };

        // Act
        var view = projector.Project(question, definition, plan: null, hiddenChoiceIds: null, pipe: null);

        // Assert
        Assert.Equal(["1", "2", "3"], view.Choices.Select(choice => choice.Value).Order());
    }

    private static SurveyQuestion Ranking()
        => new()
        {
            Id = "q1",
            Name = "q1",
            Title = "Order these",
            ControlType = SurveyControlNames.Ranking,
            Choices =
            [
                new SurveyChoice { Id = "a", Value = "1", Text = "A" },
                new SurveyChoice { Id = "b", Value = "2", Text = "B" },
                new SurveyChoice { Id = "c", Value = "3", Text = "C" },
            ],
        };
}
