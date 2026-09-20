using CrestApps.Core.ContactCenter.Models;

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

        // The key and casing are what the Orchard entity base wrote, so documents stored by an
        // earlier build still read back after the base class was dropped.
        Assert.Equal(
            """{"TestInteractionMetadata":{"Value":"provider-specific"}}""",
            interaction.EntityProperties.ToJsonString());
    }

    private sealed class TestInteractionMetadata
    {
        public string Value { get; set; }
    }
}
