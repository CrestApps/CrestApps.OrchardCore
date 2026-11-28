using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Surveys.Core.Upgrades;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using Moq;
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
    public void P4L6_ReadLegacyPart_LeavesTheQuestionsOnTheItem_UntilTheyAreSaved()
    {
        // Arrange
        var item = new ContentItem();
        ((JsonObject)item.Content)["SurveyPart"] = new JsonObject { ["Questions"] = new JsonArray(new JsonObject { ["Text"] = "Pace?" }) };

        // Act
        var legacy = LegacyCourseSurveyUpgrade.ReadLegacyPart(item);

        // Assert: reading never takes the part, so a failed upgrade keeps the questions.
        Assert.NotNull(legacy);
        Assert.NotNull(((JsonObject)item.Content)["SurveyPart"]);
        Assert.False(item.TryGet<CourseSurveyPart>(out _));
    }

    [Theory]
    [InlineData(true, 0, true)]
    [InlineData(false, 0, false)]
    [InlineData(true, 1, false)]
    public async Task VL15_ASaveOfAnItemStillHoldingLegacyQuestions_NeverOverwritesADraftBuiltSince(bool freshSurvey, int publishedVersion, bool expectSave)
    {
        // Arrange: the item points at its survey and still holds its legacy questions (the upgrade could not save them).
        var created = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);
        var survey = new Survey
        {
            ItemId = "survey1",
            Title = "Feedback",
            CreatedUtc = created,
            DraftModifiedUtc = freshSurvey ? created : created.AddDays(3),
            PublishedVersionNumber = publishedVersion,
            Draft = new SurveyDefinition(),
        };
        var item = new ContentItem { ContentItemId = "item1", ContentType = "CourseSurvey" };
        ((JsonObject)item.Content)["SurveyPart"] = new JsonObject { ["Questions"] = new JsonArray(new JsonObject { ["Text"] = "Pace?", ["Type"] = 3 }) };
        LegacyCourseSurveyUpgrade.SetSurveyId(item, "survey1");

        var manager = new Moq.Mock<CrestApps.OrchardCore.Surveys.Services.ISurveyManager>();
        manager.Setup(service => service.FindByIdAsync("survey1", Moq.It.IsAny<CancellationToken>())).ReturnsAsync(survey);
        manager.Setup(service => service.SaveDraftAsync(survey, Moq.It.IsAny<SurveyDefinition>(), Moq.It.IsAny<DateTime?>(), Moq.It.IsAny<CancellationToken>())).ReturnsAsync(SurveyErrorCode.None);
        var handler = new CrestApps.OrchardCore.Learning.Surveys.Core.CourseSurveyPartHandler(
            Moq.Mock.Of<CrestApps.OrchardCore.Learning.Core.Surveys.ILearningSurveyService>(),
            Moq.Mock.Of<global::OrchardCore.ContentManagement.Metadata.IContentDefinitionManager>(),
            manager.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CrestApps.OrchardCore.Learning.Surveys.Core.CourseSurveyPartHandler>.Instance);

        // Act
        await handler.UpdatingAsync(new global::OrchardCore.ContentManagement.Handlers.UpdateContentContext(item), item.GetOrCreate<CourseSurveyPart>());

        // Assert: the legacy part goes either way; only a survey made for the questions takes them.
        manager.Verify(service => service.SaveDraftAsync(survey, Moq.It.IsAny<SurveyDefinition>(), Moq.It.IsAny<DateTime?>(), Moq.It.IsAny<CancellationToken>()), expectSave ? Moq.Times.Once() : Moq.Times.Never());
        Assert.Null(((JsonObject)item.Content)["SurveyPart"]);
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
