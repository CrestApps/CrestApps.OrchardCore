using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Workflows.Flows;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Conversations;

public sealed class AIConversationFlowTests
{
    [Fact]
    public void ParseOutcomes_ReadsNamesAndWhenToTakeThem()
    {
        // Act
        var outcomes = AIConversationFlowPrompt.ParseOutcomes("Verified: the date of birth matches\nFailed\n verified: duplicate\n\n");

        // Assert
        Assert.Equal(2, outcomes.Count);
        Assert.Equal("Verified", outcomes[0].Name);
        Assert.Equal("the date of birth matches", outcomes[0].When);
        Assert.Equal("Failed", outcomes[1].Name);
        Assert.Null(outcomes[1].When);
    }

    [Fact]
    public void Build_GivesTheStageItsWaysOutAndTheOtherStagesInFlexibleMode()
    {
        // Arrange
        var state = new AIConversationFlowState
        {
            StageName = "Verify identity",
            Instructions = "Ask for the date of birth.",
            Outcomes = AIConversationFlowPrompt.ParseOutcomes("Verified: it matches\nFailed: two wrong tries"),
        };

        // Act
        var strict = AIConversationFlowPrompt.Build(state, []);
        var flexible = AIConversationFlowPrompt.Build(state, [("Verify identity", "Ask for the date of birth."), ("Take payment", "Offer a payment plan.")]);

        // Assert
        Assert.Contains("\"Verify identity\"", strict, StringComparison.Ordinal);
        Assert.Contains("Ask for the date of birth.", strict, StringComparison.Ordinal);
        Assert.Contains("- Verified: it matches", strict, StringComparison.Ordinal);
        Assert.Contains(AIConversationFlowPrompt.ToolName, strict, StringComparison.Ordinal);
        Assert.DoesNotContain("Take payment", strict, StringComparison.Ordinal);
        Assert.Contains("### Take payment", flexible, StringComparison.Ordinal);
        Assert.DoesNotContain("### Verify identity", flexible, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_SaysNothingOnceTheFlowHasFinished()
        => Assert.Null(AIConversationFlowPrompt.Build(new AIConversationFlowState { StageName = "Done", Finished = true }, []));

    [Fact]
    public async Task TheToolIsOffered_OnlyWhileTheConversationIsInAStage()
    {
        // Arrange
        var session = new AIChatSession { SessionId = "s-1" };
        var provider = new AIConversationFlowToolProvider();

        // Act
        IReadOnlyList<ToolRegistryEntry> before;

        using (AIConversationScope.Begin(new AIConversationContext { Session = session }))
        {
            before = await provider.GetToolsAsync(null, TestContext.Current.CancellationToken);
        }

        session.Put(new AIConversationFlowState { StageName = "Greet", Outcomes = AIConversationFlowPrompt.ParseOutcomes("Done") });
        IReadOnlyList<ToolRegistryEntry> during;

        using (AIConversationScope.Begin(new AIConversationContext { Session = session }))
        {
            during = await provider.GetToolsAsync(null, TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Empty(before);
        Assert.Equal(AIConversationFlowPrompt.ToolName, Assert.Single(during).Name);
        Assert.Contains("\"Done\"", (await during[0].CreateAsync(null)) is MoveToStageFunction function ? function.JsonSchema.GetRawText() : string.Empty, StringComparison.Ordinal);
    }
}
