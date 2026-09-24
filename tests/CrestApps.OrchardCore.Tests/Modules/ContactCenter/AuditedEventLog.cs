using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The event log as production writes it: the real publisher and the real audit recorder over an in-memory store,
/// so a test sees every event exactly as it would be stored, after the publisher has filled in what it defaults.
/// </summary>
/// <remarks>
/// A writer is handed <see cref="PublisherMock"/> where it takes a mocked publisher; it forwards to the real one,
/// so the events it publishes itself and the ones it records through <see cref="Recorder"/> land in the same log.
/// </remarks>
internal sealed class AuditedEventLog
{
    private readonly HashSet<string> _keys = new(StringComparer.Ordinal);

    public AuditedEventLog(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        var eventStore = new Mock<IInteractionEventStore>();
        eventStore
            .Setup(store => store.ExistsByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => _keys.Contains(key));
        eventStore
            .Setup(store => store.CreateAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) =>
            {
                if (!string.IsNullOrEmpty(interactionEvent.IdempotencyKey))
                {
                    _keys.Add(interactionEvent.IdempotencyKey);
                }

                Events.Add(interactionEvent);
            })
            .Returns(ValueTask.CompletedTask);

        Publisher = new DefaultContactCenterEventPublisher(
            eventStore.Object,
            new Mock<IContactCenterOutbox>().Object,
            new Mock<IContactCenterScopeExecutor>().Object,
            clock,
            NullLogger<DefaultContactCenterEventPublisher>.Instance);
        Recorder = new ContactCenterAuditRecorder(Publisher, clock);

        PublisherMock
            .Setup(publisher => publisher.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Returns((InteractionEvent interactionEvent, CancellationToken cancellationToken) => Publisher.PublishAsync(interactionEvent, cancellationToken));
    }

    /// <summary>
    /// Gets every stored event, in the order it was stored.
    /// </summary>
    public List<InteractionEvent> Events { get; } = [];

    /// <summary>
    /// Gets the real publisher.
    /// </summary>
    public DefaultContactCenterEventPublisher Publisher { get; }

    /// <summary>
    /// Gets a mocked publisher that forwards to the real one.
    /// </summary>
    public Mock<IContactCenterEventPublisher> PublisherMock { get; } = new();

    /// <summary>
    /// Gets the real audit recorder, writing to the same log.
    /// </summary>
    public ContactCenterAuditRecorder Recorder { get; }

    /// <summary>
    /// Gets the one stored event of a type.
    /// </summary>
    public InteractionEvent Single(string eventType)
        => Assert.Single(Events, interactionEvent => interactionEvent.EventType == eventType);

    /// <summary>
    /// Fails when any stored event does not say who caused it.
    /// </summary>
    /// <remarks>
    /// A payroll audit that cannot say whether the agent, a supervisor or the platform made a change cannot be
    /// defended, so every event names its actor, and an agent's identifier on an event is never mistaken for one.
    /// </remarks>
    public void AssertEveryEventNamesItsActor()
    {
        Assert.NotEmpty(Events);

        var unnamed = Events
            .Where(interactionEvent => interactionEvent.ActorType == ContactCenterActorType.Unspecified)
            .Select(interactionEvent => $"{interactionEvent.EventType} (actor id '{interactionEvent.ActorId}')")
            .ToArray();

        Assert.True(unnamed.Length == 0, "These events do not say who caused them: " + string.Join(", ", unnamed));

        foreach (var interactionEvent in Events)
        {
            // The platform acts under the system actor id and nothing else; every other actor carries its own id.
            if (interactionEvent.ActorType == ContactCenterActorType.System)
            {
                Assert.Equal(ContactCenterConstants.SystemActor, interactionEvent.ActorId);
            }
        }
    }
}
