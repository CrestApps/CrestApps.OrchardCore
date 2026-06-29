using CrestApps.OrchardCore.ContactCenter.Core.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class InteractionEntityTests
{
    [Fact]
    public void Interaction_SupportsEntityMetadata()
    {
        // Arrange
        var interaction = new Interaction();
        var metadata = new TestInteractionMetadata
        {
            Value = "provider-specific",
        };

        // Act
        interaction.Put(metadata);
        var found = interaction.TryGet<TestInteractionMetadata>(out var result);

        // Assert
        Assert.True(found);
        Assert.NotNull(result);
        Assert.Equal("provider-specific", result.Value);
    }

    private sealed class TestInteractionMetadata
    {
        public string Value { get; set; }
    }
}
