using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Takes an interaction's recordings off the call recordings page once its recording is erased. The media itself is
/// deleted by <see cref="RecordingMediaDeletionHandler"/>; this only stops the page listing and playing it.
/// </summary>
public sealed class CallRecordingErasureHandler : IContactCenterEventHandler
{
    private readonly ICallRecordingStore _store;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallRecordingErasureHandler"/> class.
    /// </summary>
    /// <param name="store">The call recordings store.</param>
    /// <param name="clock">The clock.</param>
    public CallRecordingErasureHandler(
        ICallRecordingStore store,
        IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    /// <inheritdoc/>
    public string HandlerId => "ContactCenter/CallRecordingErasure/v1";

    /// <inheritdoc/>
    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.NaturallyIdempotent;

    /// <inheritdoc/>
    public async Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        if (interactionEvent.EventType != ContactCenterConstants.Events.RecordingErased ||
            string.IsNullOrEmpty(interactionEvent.InteractionId))
        {
            return;
        }

        var recordings = await _store.ListByInteractionIdAsync(interactionEvent.InteractionId, cancellationToken);

        foreach (var recording in recordings.Where(recording => !recording.ErasedUtc.HasValue))
        {
            recording.ErasedUtc = _clock.UtcNow;

            await _store.UpdateAsync(recording, cancellationToken);
        }
    }
}
