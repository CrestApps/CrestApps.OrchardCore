using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Proves the recording erasure guard treats a missing interaction and an interaction bearing an erasure
/// tombstone as erased, so a late recording ingest can never resurrect media that retention deleted or an
/// operator erased. Reads go through an isolated child scope so a tombstone another scope just committed is
/// observed rather than masked by the ambient session identity map.
/// </summary>
public sealed class RecordingErasureGuardTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task IsRecordingErasedAsync_WithNoInteractionId_ReturnsTrue_WithoutOpeningAScope(string interactionId)
    {
        // Arrange
        var scopeExecutor = new Mock<IContactCenterScopeExecutor>(MockBehavior.Strict);
        var guard = new RecordingErasureGuard(scopeExecutor.Object);

        // Act
        var erased = await guard.IsRecordingErasedAsync(interactionId, TestContext.Current.CancellationToken);

        // Assert - fail closed: no identifier means nothing to keep, and no scope is opened.
        Assert.True(erased);
        scopeExecutor.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task IsRecordingErasedAsync_WhenInteractionNoLongerExists_ReturnsTrue()
    {
        // Arrange - retention deleted the interaction; a late ingest must not recreate its media.
        var guard = CreateGuard(interaction: null);

        // Act
        var erased = await guard.IsRecordingErasedAsync("interaction-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(erased);
    }

    [Fact]
    public async Task IsRecordingErasedAsync_WhenInteractionCarriesAnErasureTombstone_ReturnsTrue()
    {
        // Arrange
        var interaction = new Interaction { ItemId = "interaction-1", RecordingErasedUtc = DateTime.UtcNow };
        var guard = CreateGuard(interaction);

        // Act
        var erased = await guard.IsRecordingErasedAsync("interaction-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(erased);
    }

    [Fact]
    public async Task IsRecordingErasedAsync_WhenInteractionHasNoTombstone_ReturnsFalse()
    {
        // Arrange
        var interaction = new Interaction { ItemId = "interaction-1", RecordingErasedUtc = null };
        var guard = CreateGuard(interaction);

        // Act
        var erased = await guard.IsRecordingErasedAsync("interaction-1", TestContext.Current.CancellationToken);

        // Assert - a live interaction with no tombstone may keep its recording.
        Assert.False(erased);
    }

    [Fact]
    public async Task IsRecordingErasedAsync_ResolvesTheInteractionInsideTheChildScope()
    {
        // Arrange
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(x => x.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction { ItemId = "interaction-1" });

        var scopeExecutor = new Mock<IContactCenterScopeExecutor>();
        scopeExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<Func<IInteractionManager, Task>>()))
            .Returns<Func<IInteractionManager, Task>>(operation => operation(interactionManager.Object));

        var guard = new RecordingErasureGuard(scopeExecutor.Object);

        // Act
        await guard.IsRecordingErasedAsync("interaction-1", TestContext.Current.CancellationToken);

        // Assert - the lookup runs through the isolated scope's interaction manager exactly once.
        scopeExecutor.Verify(x => x.ExecuteAsync(It.IsAny<Func<IInteractionManager, Task>>()), Times.Once);
        interactionManager.Verify(x => x.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static RecordingErasureGuard CreateGuard(Interaction interaction)
    {
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var scopeExecutor = new Mock<IContactCenterScopeExecutor>();
        scopeExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<Func<IInteractionManager, Task>>()))
            .Returns<Func<IInteractionManager, Task>>(operation => operation(interactionManager.Object));

        return new RecordingErasureGuard(scopeExecutor.Object);
    }
}
