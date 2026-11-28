using CrestApps.OrchardCore.Surveys.AI.Services;
using CrestApps.OrchardCore.Surveys.Core.Schemas;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;

namespace CrestApps.OrchardCore.Tests.Surveys.AI;

public sealed class SurveyDraftAssistantTests
{
    [Fact]
    public void Read_WithADefinitionThatFollowsTheSchema_ReturnsIt()
    {
        // Arrange
        var schema = new SurveyDefinitionSchemaBuilder(AuthoringSurveyHarness.CreateRegistry(), []).BuildDefinition().Build();
        var output = """
            Sure, here is the survey:
            {"Title": "Event feedback", "Pages": [{"Id": "p1", "Name": "page_1", "Title": "Your visit", "Questions": [{"Id": "q1", "Name": "q_enjoy", "ControlType": "SingleChoice", "Title": "Did you enjoy it?", "Choices": [{"Id": "c1", "Value": "1", "Text": "Yes"}]}]}], "Endings": [{"Id": "e1", "Name": "thanks", "Message": "Thanks!", "IsDefault": true}]}
            """;

        // Act
        var definition = SurveyDraftAssistant.Read(output, schema);

        // Assert
        Assert.NotNull(definition);
        Assert.Equal("Event feedback", definition.Title);
        Assert.Equal("Yes", definition.Pages[0].Questions[0].Choices[0].Text);
    }

    // R11-M1: the AI tool's kind argument is checked on the server; the schema's enum only asks the model.
    [Theory]
    [InlineData("Quiz", "Quiz")]
    [InlineData(" Poll ", "Poll")]
    [InlineData(null, "Survey")]
    [InlineData("<img src=x onerror=alert(document.domain)>", "Survey")]
    [InlineData("Assessment", "Survey")]
    public void DraftKind_TakesOnlyABuiltInKind(string kind, string expected)
    {
        // Act
        var result = SurveyDraftAssistant.DraftKind(kind);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("I can't help with that.")]
    [InlineData("{\"Title\": \"No pages\"}")]
    [InlineData("{\"Title\": 5, \"Pages\": \"wrong\"}")]
    public void Read_WithAnInvalidDefinition_ReturnsNull(string output)
    {
        // Arrange
        var schema = new SurveyDefinitionSchemaBuilder(AuthoringSurveyHarness.CreateRegistry(), []).BuildDefinition().Build();

        // Act
        var definition = SurveyDraftAssistant.Read(output, schema);

        // Assert
        Assert.Null(definition);
    }
}
