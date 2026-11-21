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
