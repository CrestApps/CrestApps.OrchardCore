using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class InteractionEventTests
{
    [Fact]
    public void SetData_ThenGetData_RoundTripsThePayload()
    {
        // Arrange
        var interactionEvent = new InteractionEvent();
        var payload = new Dictionary<string, string>
        {
            ["queueId"] = "queue-1",
            ["agentId"] = "agent-7",
        };

        // Act
        interactionEvent.SetData(payload);
        var result = interactionEvent.GetData<Dictionary<string, string>>();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("queue-1", result["queueId"]);
        Assert.Equal("agent-7", result["agentId"]);
    }

    [Fact]
    public void GetData_WhenNoPayload_ReturnsDefault()
    {
        // Arrange
        var interactionEvent = new InteractionEvent();

        // Act
        var result = interactionEvent.GetData<Dictionary<string, string>>();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void SetData_WithNull_ClearsThePayload()
    {
        // Arrange
        var interactionEvent = new InteractionEvent();
        interactionEvent.SetData(new Dictionary<string, string> { ["queueId"] = "queue-1" });

        // Act
        interactionEvent.SetData<Dictionary<string, string>>(null);

        // Assert
        Assert.Null(interactionEvent.Data);
    }

    [Fact]
    public void SchemaVersion_DefaultsToCurrent()
    {
        // Arrange & Act
        var interactionEvent = new InteractionEvent();

        // Assert
        Assert.Equal(ContactCenterConstants.CurrentEventSchemaVersion, interactionEvent.SchemaVersion);
    }
}
