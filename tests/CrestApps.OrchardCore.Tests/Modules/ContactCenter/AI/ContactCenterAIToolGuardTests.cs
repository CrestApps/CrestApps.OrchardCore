using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.AI;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.AI;

/// <summary>
/// Every Contact Center AI tool is for a signed-in supervisor, reads nothing for anyone else, and returns a bounded answer.
/// </summary>
public sealed class ContactCenterAIToolGuardTests
{
    public static TheoryData<string> ToolNames =>
    [
        ExplainAgentNoCallsTool.ToolName,
        QueueInventoryTool.ToolName,
        DispatchDiagnosticsTool.ToolName,
        DecisionLogTool.ToolName,
        ExplainActivityTool.ToolName,
        ListConfigurationTool.ToolName,
        AgentRosterTool.ToolName,
        StuckWorkTool.ToolName,
    ];

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task InvokeAsync_WhenNobodyIsSignedIn_RefusesWithoutAskingForPermissionOrReadingAnything(string toolName)
    {
        // Arrange
        var host = new ContactCenterToolHost(authenticated: false);

        // Act
        var result = await host.RunAsync(Create(toolName), ("agent", "x"), ("activityId", "x"));

        // Assert
        Assert.Contains("signed-in", result.GetProperty("error").GetString(), StringComparison.Ordinal);
        Assert.Equal(0, host.Permission.Checks);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task InvokeAsync_WhenTheUserLacksTheSupervisorPermission_RefusesWithoutReadingAnything(string toolName)
    {
        // Arrange: the host registers no data service at all, so any read by the tool would throw instead of returning.
        var host = new ContactCenterToolHost(permitted: false);

        // Act
        var result = await host.RunAsync(Create(toolName), ("agent", "x"), ("activityId", "x"));

        // Assert
        Assert.Contains("permission", result.GetProperty("error").GetString(), StringComparison.Ordinal);
        Assert.Equal(1, host.Permission.Checks);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public void Tool_DescribesItselfWithAValidSchemaForTheModel(string toolName)
    {
        // Act
        var tool = Create(toolName);

        // Assert
        Assert.Equal(toolName, tool.Name);
        Assert.StartsWith("contactCenter", tool.Name, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Contains("Answer only from", tool.Description, StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Object, tool.JsonSchema.ValueKind);
        Assert.False(tool.JsonSchema.GetProperty("additionalProperties").GetBoolean());
    }

    [Fact]
    public async Task InvokeAsync_WhenTheAnswerIsTooLarge_AsksTheManagerToNarrowTheQuestionInsteadOfFloodingTheModel()
    {
        // Arrange
        var host = new ContactCenterToolHost();

        // Act
        var result = await host.RunAsync(new OversizedTool());

        // Assert
        Assert.Contains("narrower filter", result.GetProperty("error").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Limit_KeepsTheFirstRowsAndSaysHowManyThereWere()
    {
        // Act
        var list = ContactCenterAIToolBase.Limit(Enumerable.Range(1, 100));

        // Assert
        Assert.Equal(100, list.Total);
        Assert.True(list.Truncated);
        Assert.Equal(ContactCenterAIToolBase.MaxListItems, list.Items.Count);
    }

    private static AIFunction Create(string toolName)
        => toolName switch
        {
            ExplainAgentNoCallsTool.ToolName => new ExplainAgentNoCallsTool(),
            QueueInventoryTool.ToolName => new QueueInventoryTool(),
            DispatchDiagnosticsTool.ToolName => new DispatchDiagnosticsTool(),
            DecisionLogTool.ToolName => new DecisionLogTool(),
            ExplainActivityTool.ToolName => new ExplainActivityTool(),
            ListConfigurationTool.ToolName => new ListConfigurationTool(),
            AgentRosterTool.ToolName => new AgentRosterTool(),
            StuckWorkTool.ToolName => new StuckWorkTool(),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName)),
        };

    private sealed class OversizedTool : ContactCenterAIToolBase
    {
        public override string Name => "contactCenterOversized";

        public override string Description => "Returns far too much.";

        public override JsonElement JsonSchema { get; } = Schema("""{ "type": "object", "properties": {} }""");

        protected override Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
            => Task.FromResult<object>(new { rows = new string('x', MaxResultCharacters + 1) });
    }
}
