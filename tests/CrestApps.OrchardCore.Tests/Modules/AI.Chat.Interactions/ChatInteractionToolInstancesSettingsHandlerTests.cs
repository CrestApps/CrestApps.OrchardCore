using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Chat.Interactions.Handlers;
using CrestApps.OrchardCore.AI.Tools.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat.Interactions;

public sealed class ChatInteractionToolInstancesSettingsHandlerTests
{
    [Fact]
    public async Task UpdatingAsync_WithSelectedNames_StoresOnlyAccessibleInstances()
    {
        // Arrange
        var handler = CreateHandler("rdi email query", "weather");
        var interaction = new ChatInteraction();

        using var settings = JsonDocument.Parse("""{ "toolInstanceNames": ["RDI Email Query", "not-accessible"] }""");

        // Act
        await handler.UpdatingAsync(interaction, settings.RootElement, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(interaction.TryGet<AIToolInstanceMetadata>(out var metadata));
        Assert.Equal(["rdi email query"], metadata.ToolInstanceNames);
    }

    [Fact]
    public async Task UpdatingAsync_WithEmptySelection_ClearsTheStoredSelection()
    {
        // Arrange
        var handler = CreateHandler("rdi email query");
        var interaction = new ChatInteraction();
        interaction.Put(new AIToolInstanceMetadata { ToolInstanceNames = ["rdi email query"] });

        using var settings = JsonDocument.Parse("""{ "toolInstanceNames": [] }""");

        // Act
        await handler.UpdatingAsync(interaction, settings.RootElement, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(interaction.TryGet<AIToolInstanceMetadata>(out var metadata));
        Assert.Empty(metadata.ToolInstanceNames);
    }

    [Fact]
    public async Task UpdatingAsync_WithoutSelectionInPayload_LeavesTheStoredSelection()
    {
        // Arrange
        var handler = CreateHandler("rdi email query");
        var interaction = new ChatInteraction();
        interaction.Put(new AIToolInstanceMetadata { ToolInstanceNames = ["rdi email query"] });

        using var settings = JsonDocument.Parse("""{ "title": "Renamed" }""");

        // Act
        await handler.UpdatingAsync(interaction, settings.RootElement, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(interaction.TryGet<AIToolInstanceMetadata>(out var metadata));
        Assert.Equal(["rdi email query"], metadata.ToolInstanceNames);
    }

    private static ChatInteractionToolInstancesSettingsHandler CreateHandler(params string[] accessibleNames)
    {
        var accessor = new Mock<IAIToolInstanceAccessor>();

        accessor
            .Setup(instanceAccessor => instanceAccessor.GetAccessibleInstancesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(accessibleNames.Select(name => new AIToolInstance { Name = name }).ToList());

        return new ChatInteractionToolInstancesSettingsHandler(accessor.Object);
    }
}
