using System.Data.Common;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The dispatch straight after a commit only saves an event waiting for the outbox's background pass.
/// </summary>
public sealed class ContactCenterEventDispatchContextTests
{
    [Fact]
    public async Task ADatabaseTooBusyToClaimTheEvent_IsLeftForTheOutbox_NotThrown()
    {
        // Arrange: SQLite refusing a second writer as a call ends.
        var interactionEvent = new InteractionEvent { ItemId = "event-1" };
        var store = new Mock<IInteractionEventStore>();
        store.Setup(s => s.FindByIdAsync("event-1", It.IsAny<CancellationToken>())).ReturnsAsync(interactionEvent);
        var outbox = new Mock<IContactCenterOutbox>();
        outbox.Setup(o => o.DispatchAsync(interactionEvent, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusyDatabaseException());

        var context = new ContactCenterEventDispatchContext(store.Object, outbox.Object, NullLogger<ContactCenterEventDispatchContext>.Instance);

        // Act
        var exception = await Record.ExceptionAsync(() => context.DispatchAsync("event-1"));

        // Assert
        Assert.Null(exception);
        outbox.Verify(o => o.DispatchAsync(interactionEvent, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnythingOtherThanTheDatabase_StillThrows()
    {
        var interactionEvent = new InteractionEvent { ItemId = "event-1" };
        var store = new Mock<IInteractionEventStore>();
        store.Setup(s => s.FindByIdAsync("event-1", It.IsAny<CancellationToken>())).ReturnsAsync(interactionEvent);
        var outbox = new Mock<IContactCenterOutbox>();
        outbox.Setup(o => o.DispatchAsync(interactionEvent, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("A handler failed."));

        var context = new ContactCenterEventDispatchContext(store.Object, outbox.Object, NullLogger<ContactCenterEventDispatchContext>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.DispatchAsync("event-1"));
    }

    private sealed class BusyDatabaseException : DbException
    {
        public BusyDatabaseException()
            : base("SQLite Error 5: 'database is locked'.")
        {
        }
    }
}
