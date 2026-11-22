using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Surveys.Core.Upgrades;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning.Upgrades;

/// <summary>
/// A course survey from the first Learning release kept its questions in a <c>SurveyPart</c> of the item. The upgrade
/// moves the item to a <see cref="CourseSurveyPart"/> and builds a survey from those questions.
/// </summary>
public sealed class LegacyCourseSurveyUpgradeTests
{
    [Fact]
    public void TakeLegacyPart_MovesTheItemToTheCourseSurveyPart()
    {
        // Arrange
        var item = new ContentItem();
        ((JsonObject)item.Content)["SurveyPart"] = new JsonObject { ["Anonymous"] = true };

        // Act
        var legacy = LegacyCourseSurveyUpgrade.TakeLegacyPart(item);
        LegacyCourseSurveyUpgrade.SetSurveyId(item, "survey1");

        // Assert
        Assert.NotNull(legacy);
        Assert.True(legacy["Anonymous"]!.GetValue<bool>());
        Assert.Null(((JsonObject)item.Content)["SurveyPart"]);
        Assert.True(item.TryGet<CourseSurveyPart>(out var part));
        Assert.Equal("survey1", part.SurveyId);
    }

    [Fact]
    public void TakeLegacyPart_AnItemWithoutTheOldPart_IsLeftAlone()
    {
        // Arrange
        var item = new ContentItem();

        // Act
        var legacy = LegacyCourseSurveyUpgrade.TakeLegacyPart(item);

        // Assert
        Assert.Null(legacy);
    }

    [Fact]
    public void ToDefinition_ConvertsEveryQuestionType_WithItsOptions()
    {
        // Arrange
        var legacy = new JsonObject
        {
            ["Questions"] = new JsonArray
            {
                new JsonObject { ["Text"] = "Pace?", ["Type"] = 0, ["Options"] = new JsonArray("Too slow", "Right", "Too fast"), ["Required"] = true },
                new JsonObject { ["Text"] = "Topics?", ["Type"] = "MultipleChoice", ["Options"] = new JsonArray("A", "B") },
                new JsonObject { ["Text"] = "Rate it", ["Type"] = 2 },
                new JsonObject { ["Text"] = "Anything else?", ["Type"] = 3 },
                new JsonObject { ["Text"] = "  " },
            },
        };

        // Act
        var definition = LegacyCourseSurveyUpgrade.ToDefinition(legacy, "Course feedback");

        // Assert
        var questions = Assert.Single(definition.Pages).Questions;
        Assert.Equal(4, questions.Count);
        Assert.Equal(SurveyControlNames.SingleChoice, questions[0].ControlType);
        Assert.True(questions[0].Required);
        Assert.Equal(["Too slow", "Right", "Too fast"], questions[0].Choices.Select(choice => choice.Text));
        Assert.Equal(SurveyControlNames.MultipleChoice, questions[1].ControlType);
        Assert.Equal(2, questions[1].Choices.Count);
        Assert.Equal(SurveyControlNames.Rating, questions[2].ControlType);
        Assert.Equal(SurveyControlNames.LongText, questions[3].ControlType);
        Assert.Equal(questions.Count, questions.Select(question => question.Id).Distinct().Count());
    }

    [Fact]
    public void ToDefinition_WithoutQuestions_GivesNothing()
    {
        // Act
        var definition = LegacyCourseSurveyUpgrade.ToDefinition(new JsonObject { ["Questions"] = new JsonArray() }, "Empty");

        // Assert
        Assert.Null(definition);
    }

    [Fact]
    public void ApplySettings_CarriesAnonymityAndTheCloseDate()
    {
        // Arrange
        var survey = new Survey();
        var legacy = new JsonObject { ["Anonymous"] = true, ["CloseUtc"] = "2026-12-31T23:00:00Z" };

        // Act
        LegacyCourseSurveyUpgrade.ApplySettings(legacy, survey);

        // Assert
        Assert.Equal(AnonymityMode.Anonymous, survey.Access.Anonymity);
        Assert.Equal(new DateTime(2026, 12, 31, 23, 0, 0, DateTimeKind.Utc), survey.Access.ClosesUtc);
    }
}
