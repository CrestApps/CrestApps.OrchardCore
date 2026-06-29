using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class InteractionManagerTests
{
    private static readonly DateTime _now = new(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task NewAsync_SetsItemIdCorrelationIdAndCreatedUtc()
    {
        // Arrange
        var manager = CreateManager();

        // Act
        var interaction = await manager.NewAsync();

        // Assert
        Assert.False(string.IsNullOrEmpty(interaction.ItemId));
        Assert.False(string.IsNullOrEmpty(interaction.CorrelationId));
        Assert.Equal(_now, interaction.CreatedUtc);
    }

    [Fact]
    public async Task CreateAsync_WhenIdentityIsMissing_GeneratesIdentityAndCreatedUtc()
    {
        // Arrange
        var store = new Mock<IInteractionStore>();
        store
            .Setup(s => s.CreateAsync(It.IsAny<Interaction>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var manager = CreateManager(store.Object);
        var interaction = new Interaction();

        // Act
        await manager.CreateAsync(interaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(string.IsNullOrEmpty(interaction.ItemId));
        Assert.False(string.IsNullOrEmpty(interaction.CorrelationId));
        Assert.Equal(_now, interaction.CreatedUtc);
        store.Verify(s => s.CreateAsync(interaction, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_SetsModifiedUtc()
    {
        // Arrange
        var store = new Mock<IInteractionStore>();
        store
            .Setup(s => s.UpdateAsync(It.IsAny<Interaction>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var manager = CreateManager(store.Object);
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            CorrelationId = "correlation-1",
        };

        // Act
        await manager.UpdateAsync(interaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_now, interaction.ModifiedUtc);
        store.Verify(s => s.UpdateAsync(interaction, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static InteractionManager CreateManager(IInteractionStore store = null)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new InteractionManager(store ?? Mock.Of<IInteractionStore>(), clock.Object);
    }
}
