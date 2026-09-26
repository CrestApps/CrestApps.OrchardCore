using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The default <see cref="ISharedVoicemailService"/>.
/// </summary>
public sealed class SharedVoicemailService : ISharedVoicemailService
{
    /// <summary>
    /// The longest resolution note kept, in characters.
    /// </summary>
    public const int MaxNoteLength = 1000;

    private readonly ISharedVoicemailManager _voicemailManager;
    private readonly ISharedVoicemailAuthorizationService _authorizationService;
    private readonly IContactCenterAuditRecorder _auditRecorder;
    private readonly ICallbackService _callbackService;
    private readonly IInteractionManager _interactionManager;
    private readonly IRecordingAccessGovernanceService _governance;
    private readonly IRecordingMediaStore _mediaStore;
    private readonly IClock _clock;

    // Recording governance and the encrypted media store belong to the recording features. A message can be listed,
    // claimed and called back without them; only playback and erasure need them, and report the recording as
    // unavailable when they are missing rather than failing the whole service.

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedVoicemailService"/> class.
    /// </summary>
    /// <param name="voicemailManager">The shared voicemail manager.</param>
    /// <param name="authorizationService">The authorization that decides which boxes a user may see.</param>
    /// <param name="auditRecorder">The recorder every change is written to.</param>
    /// <param name="callbackService">The callback service a call back is scheduled through.</param>
    /// <param name="interactionManager">The interaction manager used to find a message's recording.</param>
    /// <param name="governanceServices">The optional recording governance that audits playback and decides erasure.</param>
    /// <param name="mediaStores">The optional media store the recordings are kept in.</param>
    /// <param name="clock">The clock used to date every change.</param>
    public SharedVoicemailService(
        ISharedVoicemailManager voicemailManager,
        ISharedVoicemailAuthorizationService authorizationService,
        IContactCenterAuditRecorder auditRecorder,
        ICallbackService callbackService,
        IInteractionManager interactionManager,
        IEnumerable<IRecordingAccessGovernanceService> governanceServices,
        IEnumerable<IRecordingMediaStore> mediaStores,
        IClock clock)
    {
        _voicemailManager = voicemailManager;
        _authorizationService = authorizationService;
        _auditRecorder = auditRecorder;
        _callbackService = callbackService;
        _interactionManager = interactionManager;
        _governance = governanceServices.FirstOrDefault();
        _mediaStore = mediaStores.FirstOrDefault();
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<SharedVoicemail> DeliverAsync(SharedVoicemail voicemail, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(voicemail);
        ArgumentException.ThrowIfNullOrEmpty(voicemail.InteractionId);
        ArgumentException.ThrowIfNullOrEmpty(voicemail.QueueId);

        var existing = await _voicemailManager.FindByInteractionIdAsync(voicemail.InteractionId, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var now = _clock.UtcNow;

        voicemail.Status = SharedVoicemailStatus.New;
        voicemail.CreatedUtc = now;
        voicemail.ModifiedUtc = now;

        if (voicemail.ReceivedUtc == default)
        {
            voicemail.ReceivedUtc = now;
        }

        await _voicemailManager.CreateAsync(voicemail, cancellationToken);
        await RecordAsync(ContactCenterConstants.Events.SharedVoicemailReceived, voicemail, SharedVoicemailStatus.New, null, access: null, voicemail.ReceivedUtc, cancellationToken);

        return voicemail;
    }

    /// <inheritdoc/>
    public async Task<SharedVoicemailPage> ListAsync(ClaimsPrincipal principal, SharedVoicemailQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _authorizationService.GetAccessAsync(principal, cancellationToken);

        if (!access.CanAccess)
        {
            return SharedVoicemailPage.Empty;
        }

        // The requested queues are narrowed to the user's, so a filter naming another team's queue reads nothing from
        // it. No filter reads every queue the user may see, which is every queue for a user who sees them all.
        IReadOnlyCollection<string> queueIds = query.QueueIds is { Count: > 0 } requested
            ? requested.Where(access.CoversQueue).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            : access.AllQueues
                ? null
                : access.QueueIds.ToArray();

        return await _voicemailManager.QueryAsync(new SharedVoicemailQuery
        {
            QueueIds = queueIds,
            Status = query.Status,
            IncludeResolved = query.IncludeResolved,
            Page = query.Page,
            PageSize = query.PageSize,
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SharedVoicemailActionResult> OpenRecordingAsync(ClaimsPrincipal principal, string voicemailId, CancellationToken cancellationToken = default)
    {
        var (access, voicemail) = await FindAsync(principal, voicemailId, cancellationToken);

        if (voicemail is null)
        {
            return SharedVoicemailActionResult.Failure(SharedVoicemailActionStatus.NotFound);
        }

        var interaction = await _interactionManager.FindByIdAsync(voicemail.InteractionId, cancellationToken);
        var storageReference = GetStorageReference(interaction);

        // The caller may have hung up before anything was recorded, or the recording may not have finished ingesting.
        if (storageReference is null || _governance is null || _mediaStore is null)
        {
            return SharedVoicemailActionResult.Failure(SharedVoicemailActionStatus.Unavailable, voicemail, SharedVoicemailReasons.NoRecording);
        }

        // Governance decides and audits the access before any media is opened, so a shared voicemail is listened to on
        // the same recording-access trail as every other recording.
        if (!await _governance.RecordAccessAsync(interaction.ItemId, Actor(access), "shared-voicemail-playback", cancellationToken))
        {
            return SharedVoicemailActionResult.Failure(SharedVoicemailActionStatus.Unavailable, voicemail, SharedVoicemailReasons.NoRecording);
        }

        var stream = await _mediaStore.OpenReadAsync(storageReference, cancellationToken);

        return stream is null
            ? SharedVoicemailActionResult.Failure(SharedVoicemailActionStatus.Unavailable, voicemail, SharedVoicemailReasons.NoRecording)
            : SharedVoicemailActionResult.Success(voicemail, recording: stream);
    }

    /// <inheritdoc/>
    public async Task<SharedVoicemailActionResult> ClaimAsync(ClaimsPrincipal principal, string voicemailId, CancellationToken cancellationToken = default)
    {
        var (access, voicemail) = await FindAsync(principal, voicemailId, cancellationToken);

        if (voicemail is null)
        {
            return SharedVoicemailActionResult.Failure(SharedVoicemailActionStatus.NotFound);
        }

        if (voicemail.Status == SharedVoicemailStatus.Resolved)
        {
            return SharedVoicemailActionResult.Failure(SharedVoicemailActionStatus.Conflict, voicemail, SharedVoicemailReasons.AlreadyResolved);
        }

        if (access.Holds(voicemail))
        {
            return SharedVoicemailActionResult.Success(voicemail);
        }

        // A teammate's claim is theirs; only a user who manages the box may take it over, and the take-over is recorded
        // with whose claim it replaced.
        if (!access.CanClaim(voicemail))
        {
            return SharedVoicemailActionResult.Failure(SharedVoicemailActionStatus.Conflict, voicemail, SharedVoicemailReasons.ClaimedByAnotherUser);
        }

        var previousStatus = voicemail.Status;
        var previousClaimedByUserId = voicemail.ClaimedByUserId;
        var now = _clock.UtcNow;

        Claim(voicemail, access, now);
        voicemail.ModifiedUtc = now;

        await _voicemailManager.UpdateAsync(voicemail, cancellationToken: cancellationToken);
        await RecordAsync(ContactCenterConstants.Events.SharedVoicemailClaimed, voicemail, previousStatus, previousClaimedByUserId, access, now, cancellationToken);

        return SharedVoicemailActionResult.Success(voicemail);
    }

    /// <inheritdoc/>
    public async Task<SharedVoicemailActionResult> ReleaseAsync(ClaimsPrincipal principal, string voicemailId, CancellationToken cancellationToken = default)
    {
        var (access, voicemail) = await FindAsync(principal, voicemailId, cancellationToken);

        if (voicemail is null)
        {
            return SharedVoicemailActionResult.Failure(SharedVoicemailActionStatus.NotFound);
        }

        if (voicemail.Status == SharedVoicemailStatus.New)
        {
            return SharedVoicemailActionResult.Success(voicemail);
        }

        if (!access.CanRelease(voicemail))
        {
            return SharedVoicemailActionResult.Failure(SharedVoicemailActionStatus.Forbidden, voicemail, SharedVoicemailReasons.ClaimedByAnotherUser);
        }

        var previousStatus = voicemail.Status;
        var previousClaimedByUserId = voicemail.ClaimedByUserId;
        var now = _clock.UtcNow;

        // Back in the team's hands, the message is new again: nobody holds it, and how it was once resolved no longer
        // describes it. The event log keeps who held and resolved it before.
        voicemail.Status = SharedVoicemailStatus.New;
        voicemail.ClaimedByUserId = null;
        voicemail.ClaimedByUserName = null;
        voicemail.ClaimedUtc = null;
        voicemail.ResolvedByUserId = null;
        voicemail.ResolvedByUserName = null;
        voicemail.ResolvedUtc = null;
        voicemail.ResolutionNote = null;
        voicemail.ModifiedUtc = now;

        await _voicemailManager.UpdateAsync(voicemail, cancellationToken: cancellationToken);
        await RecordAsync(ContactCenterConstants.Events.SharedVoicemailReleased, voicemail, previousStatus, previousClaimedByUserId, access, now, cancellationToken);

        return SharedVoicemailActionResult.Success(voicemail);
    }

    /// <inheritdoc/>
    public async Task<SharedVoicemailActionResult> ResolveAsync(ClaimsPrincipal principal, string voicemailId, string note, CancellationToken cancellationToken = default)
    {
        var (access, voicemail) = await FindAsync(principal, voicemailId, cancellationToken);

        if (voicemail is null)
        {
            return SharedVoicemailActionResult.Failure(SharedVoicemailActionStatus.NotFound);
        }

        if (voicemail.Status == SharedVoicemailStatus.Resolved)
        {
            return SharedVoicemailActionResult.Failure(SharedVoicemailActionStatus.Conflict, voicemail, SharedVoicemailReasons.AlreadyResolved);
        }

        if (!access.CanWork(voicemail))
        {
            return SharedVoicemailActionResult.Failure(SharedVoicemailActionStatus.Forbidden, voicemail, SharedVoicemailReasons.ClaimedByAnotherUser);
        }

        var previousStatus = voicemail.Status;
        var previousClaimedByUserId = voicemail.ClaimedByUserId;
        var now = _clock.UtcNow;

        // Whoever resolves an unclaimed message handled it, so the team sees who did.
        if (voicemail.Status == SharedVoicemailStatus.New)
        {
            Claim(voicemail, access, now);
        }

        voicemail.Status = SharedVoicemailStatus.Resolved;
        voicemail.ResolvedByUserId = access.UserId;
        voicemail.ResolvedByUserName = access.UserName;
        voicemail.ResolvedUtc = now;
        voicemail.ResolutionNote = NormalizeNote(note);
        voicemail.ModifiedUtc = now;

        await _voicemailManager.UpdateAsync(voicemail, cancellationToken: cancellationToken);
        await RecordAsync(
            ContactCenterConstants.Events.SharedVoicemailResolved,
            voicemail,
            previousStatus,
            previousClaimedByUserId,
            access,
            now,
            cancellationToken,
            note: voicemail.ResolutionNote);

        return SharedVoicemailActionResult.Success(voicemail);
    }

    /// <inheritdoc/>
    public async Task<SharedVoicemailActionResult> RequestCallbackAsync(ClaimsPrincipal principal, string voicemailId, CancellationToken cancellationToken = default)
    {
        var (access, voicemail) = await FindAsync(principal, voicemailId, cancellationToken);

        if (voicemail is null)
        {
            return SharedVoicemailActionResult.Failure(SharedVoicemailActionStatus.NotFound);
        }

        if (voicemail.Status == SharedVoicemailStatus.Resolved)
        {
            return SharedVoicemailActionResult.Failure(SharedVoicemailActionStatus.Conflict, voicemail, SharedVoicemailReasons.AlreadyResolved);
        }

        if (!access.CanWork(voicemail))
        {
            return SharedVoicemailActionResult.Failure(SharedVoicemailActionStatus.Forbidden, voicemail, SharedVoicemailReasons.ClaimedByAnotherUser);
        }

        if (string.IsNullOrWhiteSpace(voicemail.CallerNumber))
        {
            return SharedVoicemailActionResult.Failure(SharedVoicemailActionStatus.Conflict, voicemail, SharedVoicemailReasons.NoCallerNumber);
        }

        var now = _clock.UtcNow;

        // The callback is queued back into the message's own queue, so it is offered to the next available member of the
        // team that owns the message, as a call to place, like any callback a caller asked for while waiting.
        var callback = await _callbackService.ScheduleAsync(new CallbackRequest
        {
            ItemId = IdGenerator.GenerateId(),
            Destination = voicemail.CallerNumber,
            QueueId = voicemail.QueueId,
            ContactContentItemId = voicemail.ContactContentItemId,
            ContactContentType = voicemail.ContactContentType,
            RequestedUtc = now,
            ScheduledUtc = now,
            Notes = $"Callback for the voicemail the caller left at {voicemail.ReceivedUtc:u}.",
        }, cancellationToken);

        // Without the Outbound Dialer feature the tenant has no callbacks: nothing was scheduled, so nothing changes.
        if (callback is null)
        {
            return SharedVoicemailActionResult.Failure(SharedVoicemailActionStatus.Unavailable, voicemail, SharedVoicemailReasons.CallbacksUnavailable);
        }

        var previousStatus = voicemail.Status;
        var previousClaimedByUserId = voicemail.ClaimedByUserId;

        // Asking for the call back is handling the message, so an unclaimed one becomes the user's.
        if (voicemail.Status == SharedVoicemailStatus.New)
        {
            Claim(voicemail, access, now);
        }

        voicemail.CallbackRequestId = callback.ItemId;
        voicemail.CallbackRequestedByUserName = access.UserName;
        voicemail.CallbackRequestedUtc = now;
        voicemail.ModifiedUtc = now;

        await _voicemailManager.UpdateAsync(voicemail, cancellationToken: cancellationToken);
        await RecordAsync(
            ContactCenterConstants.Events.SharedVoicemailCallbackRequested,
            voicemail,
            previousStatus,
            previousClaimedByUserId,
            access,
            now,
            cancellationToken,
            callbackRequestId: callback.ItemId);

        return SharedVoicemailActionResult.Success(voicemail, callback);
    }

    /// <inheritdoc/>
    public async Task<SharedVoicemailActionResult> DeleteAsync(ClaimsPrincipal principal, string voicemailId, CancellationToken cancellationToken = default)
    {
        var (access, voicemail) = await FindAsync(principal, voicemailId, cancellationToken);

        if (voicemail is null)
        {
            return SharedVoicemailActionResult.Failure(SharedVoicemailActionStatus.NotFound);
        }

        if (!access.CanDelete(voicemail))
        {
            return SharedVoicemailActionResult.Failure(SharedVoicemailActionStatus.Forbidden, voicemail, SharedVoicemailReasons.ManagePermissionRequired);
        }

        var interaction = await _interactionManager.FindByIdAsync(voicemail.InteractionId, cancellationToken);
        var actor = Actor(access);

        // Only a message that recorded something has anything to erase. Governance decides first, so a recording it
        // must keep (a legal hold) is never destroyed by the media deletion that follows.
        if (!string.IsNullOrEmpty(interaction?.RecordingReference))
        {
            // Read before the erase, which clears the recording metadata from the interaction.
            var storageReference = GetStorageReference(interaction);

            if (_governance is not null)
            {
                var decision = await _governance.EraseAsync(interaction.ItemId, actor, "shared-voicemail-deleted", cancellationToken);

                if (decision is { Erased: false, DenyReasonCode: ContactCenterConstants.RecordingErasureDenyReason.LegalHold })
                {
                    return SharedVoicemailActionResult.Failure(SharedVoicemailActionStatus.Conflict, voicemail, SharedVoicemailReasons.LegalHold);
                }
            }

            // The governance erase alone does not remove the bytes when the full call-recording feature, which owns the
            // media-deletion handler, is not enabled. Deleting media that is already gone is a no-op.
            if (storageReference is not null && _mediaStore is not null)
            {
                await _mediaStore.DeleteAsync(storageReference, cancellationToken);
            }
        }

        await _voicemailManager.DeleteAsync(voicemail, cancellationToken);
        await RecordAsync(ContactCenterConstants.Events.SharedVoicemailDeleted, voicemail, voicemail.Status, voicemail.ClaimedByUserId, access, _clock.UtcNow, cancellationToken);

        return SharedVoicemailActionResult.Success(voicemail);
    }

    // A message outside the user's queues is reported exactly as a message that does not exist.
    private async Task<(SharedVoicemailAccess Access, SharedVoicemail Voicemail)> FindAsync(
        ClaimsPrincipal principal,
        string voicemailId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(voicemailId))
        {
            return (SharedVoicemailAccess.None, null);
        }

        var access = await _authorizationService.GetAccessAsync(principal, cancellationToken);

        if (!access.CanAccess)
        {
            return (access, null);
        }

        var voicemail = await _voicemailManager.FindByIdAsync(voicemailId, cancellationToken);

        return voicemail is not null && access.CoversQueue(voicemail.QueueId)
            ? (access, voicemail)
            : (access, null);
    }

    private static void Claim(SharedVoicemail voicemail, SharedVoicemailAccess access, DateTime now)
    {
        voicemail.Status = SharedVoicemailStatus.Claimed;
        voicemail.ClaimedByUserId = access.UserId;
        voicemail.ClaimedByUserName = access.UserName;
        voicemail.ClaimedUtc = now;
    }

    // A null access is the platform's own change: the message arriving.
    private Task RecordAsync(
        string eventType,
        SharedVoicemail voicemail,
        SharedVoicemailStatus previousStatus,
        string previousClaimedByUserId,
        SharedVoicemailAccess access,
        DateTime occurredUtc,
        CancellationToken cancellationToken,
        string note = null,
        string callbackRequestId = null)
        => _auditRecorder.RecordSharedVoicemailAsync(eventType, new SharedVoicemailEventData
        {
            SharedVoicemailId = voicemail.ItemId,
            InteractionId = voicemail.InteractionId,
            QueueId = voicemail.QueueId,
            PreviousStatus = previousStatus,
            Status = voicemail.Status,
            UserId = access?.UserId,
            UserName = access?.UserName,
            PreviousClaimedByUserId = previousClaimedByUserId,
            Note = note,
            CallbackRequestId = callbackRequestId,
            OccurredUtc = occurredUtc,
        }, access is null ? ContactCenterActor.System : Actor(access), cancellationToken);

    private static ContactCenterActor Actor(SharedVoicemailAccess access)
        => ContactCenterActor.Agent(access.UserId);

    private static string NormalizeNote(string note)
    {
        var trimmed = note?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length > MaxNoteLength ? trimmed[..MaxNoteLength] : trimmed;
    }

    private static string GetStorageReference(Interaction interaction)
        => interaction is not null &&
            !string.IsNullOrEmpty(interaction.RecordingReference) &&
            interaction.TechnicalMetadata is not null &&
            interaction.TechnicalMetadata.TryGetValue(ContactCenterConstants.RecordingMetadata.StorageReference, out var value) &&
            value?.ToString() is { Length: > 0 } reference
                ? reference
                : null;
}
