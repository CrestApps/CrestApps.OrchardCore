using System.Security.Claims;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

/// <summary>
/// Plays and deletes the voicemails in a signed-in user's soft-phone inbox.
/// </summary>
/// <remarks>
/// <para>
/// One ownership rule decides the list, playback and deletion: a voicemail is the signed-in user's when their own
/// soft-phone inbox holds it as a voicemail. The inbox is what the soft phone lists, so a user can play and delete
/// exactly the voicemails they can see, and never another user's, since every inbox row is read as the caller's own.
/// </para>
/// <para>
/// Which inbox a voicemail lands in is decided once, when the platform projects it: the recipient stamped when the
/// call was sent to voicemail (a direct call, or an agent pressing Voicemail), otherwise the agent the call was last
/// offered to (a queue voicemail reached on max wait after that agent's offer expired). These endpoints do not decide
/// it a second time. They used to, from the recipient stamp and the interaction's agent alone, and a queue voicemail
/// has neither: the soft phone listed it and every attempt to play or delete it was refused.
/// </para>
/// </remarks>
internal static partial class AgentWorkspaceEndpoints
{
    /// <summary>
    /// Streams a voicemail recording from the signed-in user's inbox. Every grant is routed through the
    /// recording-access governance service so it is authorized and written to the recording-access audit trail before
    /// any media is opened.
    /// </summary>
    internal static async Task<IResult> HandleVoicemailMediaAsync(
        string interactionId,
        IInteractionManager interactionManager,
        ITelephonyInteractionStore telephonyInteractionStore,
        HttpContext httpContext)
    {
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return ContactCenterApiResults.Unauthorized();
        }

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(interactionId))
        {
            return ContactCenterApiResults.Forbidden();
        }

        // A voicemail that will not play returns 404 through several distinct branches; logging the specific reason
        // turns an otherwise silent "nothing happens" into a diagnosable cause (a missing recording, a governance
        // feature that is not enabled, media that has not finished ingesting, and so on).
        var logger = CreateVoicemailLogger(httpContext);

        var voicemail = await FindInboxVoicemailAsync(
            interactionId,
            userId,
            interactionManager,
            telephonyInteractionStore,
            httpContext.RequestAborted);

        if (voicemail.Access != VoicemailAccess.Owned)
        {
            LogVoicemailRefused(logger, interactionId, voicemail.Reason);

            return voicemail.Access == VoicemailAccess.NotOwned
                ? ContactCenterApiResults.Forbidden(voicemail.Reason)
                : TypedResults.NotFound();
        }

        var interaction = voicemail.Interaction;

        // The recording may not have finished ingesting yet (the caller just hung up, or the durable ingest job has
        // not run), or the caller hung up before anything was recorded. Treat that as "not available" rather than an
        // error.
        if (interaction is null ||
            string.IsNullOrEmpty(interaction.RecordingReference) ||
            GetStorageReference(interaction) is not { Length: > 0 } storageReference)
        {
            LogVoicemailRefused(logger, interactionId, "the voicemail carries no recording storage reference yet");

            return TypedResults.NotFound();
        }

        // Gate and audit the access. RecordAccessAsync writes the RecordingAccessed audit event and returns false
        // when there is no recording to access, so playback shares the same governance trail as any other recording.
        // The governance service and the media store are owned by the recording feature; when it is not enabled
        // there is nothing to play, so treat a missing service as "not available" rather than failing hard.
        var recordingAccessGovernanceService = httpContext.RequestServices.GetService<IRecordingAccessGovernanceService>();

        if (recordingAccessGovernanceService is null)
        {
            LogVoicemailRefused(logger, interactionId, "the recording governance service is not registered (the Recording Governance feature is not enabled)");

            return TypedResults.NotFound();
        }

        // Audited against the interaction that was resolved, not the identifier the soft phone asked with: the two
        // differ whenever the inbox row carries its own, and an audit trail keyed to a soft-phone row cannot be
        // followed back to the recording it is about.
        var granted = await recordingAccessGovernanceService.RecordAccessAsync(
            interaction.ItemId,
            ContactCenterActor.Agent(userId),
            "voicemail-playback",
            httpContext.RequestAborted);

        if (!granted)
        {
            LogVoicemailRefused(logger, interactionId, "recording access was not granted by governance");

            return TypedResults.NotFound();
        }

        var mediaStore = httpContext.RequestServices.GetService<IRecordingMediaStore>();

        if (mediaStore is null)
        {
            LogVoicemailRefused(logger, interactionId, "no recording media store is registered");

            return TypedResults.NotFound();
        }

        var stream = await mediaStore.OpenReadAsync(storageReference, httpContext.RequestAborted);

        if (stream is null)
        {
            LogVoicemailRefused(logger, interactionId, "the media store has no bytes for the recording storage reference");

            return TypedResults.NotFound();
        }

        return Results.Stream(stream, "audio/mpeg");
    }

    /// <summary>
    /// Deletes a voicemail from the signed-in user's inbox, erasing its recording through recording governance.
    /// </summary>
    internal static async Task<IResult> HandleDeleteVoicemailAsync(
        string interactionId,
        IInteractionManager interactionManager,
        ITelephonyInteractionStore telephonyInteractionStore,
        IAntiforgery antiforgery,
        HttpContext httpContext)
    {
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return ContactCenterApiResults.Unauthorized();
        }

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(interactionId))
        {
            return ContactCenterApiResults.Forbidden();
        }

        // Deleting a voicemail changes state, so it is a POST guarded by antiforgery, unlike the media GET.
        if (!await ContactCenterEndpointAntiforgery.ValidateRequestAsync(antiforgery, httpContext))
        {
            return TypedResults.BadRequest();
        }

        var logger = CreateVoicemailLogger(httpContext);

        var voicemail = await FindInboxVoicemailAsync(
            interactionId,
            userId,
            interactionManager,
            telephonyInteractionStore,
            httpContext.RequestAborted);

        if (voicemail.Access != VoicemailAccess.Owned)
        {
            LogVoicemailRefused(logger, interactionId, voicemail.Reason);

            return voicemail.Access == VoicemailAccess.NotOwned
                ? ContactCenterApiResults.Forbidden(voicemail.Reason)
                : ContactCenterApiResults.NotFound(voicemail.Reason);
        }

        var interaction = voicemail.Interaction;

        // Only a voicemail that recorded something has anything to erase. One the caller hung up on during the
        // greeting has no recording, and asking governance to erase it would only audit a refusal for a delete that
        // did what the user asked.
        if (interaction is not null && !string.IsNullOrEmpty(interaction.RecordingReference))
        {
            var governance = httpContext.RequestServices.GetService<IRecordingAccessGovernanceService>();

            // Read before the erase, which clears the recording metadata from the interaction.
            var storageReference = GetStorageReference(interaction);

            if (governance is not null)
            {
                // Governance decides first, so a recording it must keep (a legal hold) is never destroyed by the media
                // deletion below. Erased by the resolved interaction's identifier, for the same reason the playback
                // audit uses it: an erase recorded against a soft-phone row would leave the recording's own tombstone
                // unwritten.
                var decision = await governance.EraseAsync(
                    interaction.ItemId,
                    ContactCenterActor.Agent(userId),
                    "voicemail-deleted",
                    httpContext.RequestAborted);

                if (decision is { Erased: false, DenyReasonCode: ContactCenterConstants.RecordingErasureDenyReason.LegalHold })
                {
                    LogVoicemailRefused(logger, interactionId, "the recording is under legal hold");

                    return TypedResults.Problem(
                        detail: "The voicemail is under legal hold and cannot be deleted.",
                        statusCode: StatusCodes.Status409Conflict);
                }
            }

            // Delete the encrypted media too. The media store is owned by the Telephony recording feature; the
            // governance erase alone would not remove the bytes here because the media-deletion event handler lives in
            // the (separately enabled) full call-recording feature. Deleting media that is already gone is a no-op.
            if (storageReference is { Length: > 0 })
            {
                var mediaStore = httpContext.RequestServices.GetService<IRecordingMediaStore>();

                if (mediaStore is not null)
                {
                    await mediaStore.DeleteAsync(storageReference, httpContext.RequestAborted);
                }
            }
        }

        // Finally remove the voicemail from the user's inbox: the row the ownership check found, which is the one the
        // soft phone lists.
        await telephonyInteractionStore.DeleteAsync(voicemail.InboxEntry, httpContext.RequestAborted);

        return TypedResults.Ok();
    }

    /// <summary>
    /// Finds a voicemail in the signed-in user's own soft-phone inbox, and the platform interaction behind it.
    /// </summary>
    /// <remarks>
    /// The soft phone addresses a voicemail by the identifier on its own inbox row. That is the platform interaction's
    /// identifier only when the platform created the row; a row the soft phone created itself carries a generated
    /// identifier, and the interaction is then found by the call the row was recorded on. The interaction's own
    /// identifier is accepted too, and resolves to the caller's row for the same call. Either way the row is read as
    /// the caller's own, so an identifier belonging to somebody else resolves to nothing.
    /// </remarks>
    private static async Task<InboxVoicemail> FindInboxVoicemailAsync(
        string requestedId,
        string userId,
        IInteractionManager interactionManager,
        ITelephonyInteractionStore telephonyInteractionStore,
        CancellationToken cancellationToken)
    {
        var inboxEntry = await telephonyInteractionStore.FindByInteractionIdAsync(userId, requestedId, cancellationToken);
        var interaction = await interactionManager.FindByIdAsync(requestedId, cancellationToken);

        if (inboxEntry is null)
        {
            if (interaction is null)
            {
                return new InboxVoicemail(VoicemailAccess.NotFound, "no voicemail with this identifier is in the user's inbox");
            }

            if (!string.IsNullOrEmpty(interaction.ProviderInteractionId))
            {
                inboxEntry = await telephonyInteractionStore.FindByCallIdAsync(userId, interaction.ProviderInteractionId, cancellationToken);
            }
        }
        else if (interaction is null && !string.IsNullOrEmpty(inboxEntry.CallId))
        {
            interaction = await interactionManager.FindByProviderInteractionIdAsync(inboxEntry.CallId, cancellationToken);
        }

        // Only a voicemail is reachable through these endpoints; a normal call recording is governed and surfaced
        // elsewhere, so they deliberately refuse to expose it.
        if (interaction is not null && !IsVoicemailInteraction(interaction))
        {
            return new InboxVoicemail(VoicemailAccess.NotFound, "the interaction is not a voicemail");
        }

        if (inboxEntry is null || !inboxEntry.IsVoicemail)
        {
            return interaction is null
                ? new InboxVoicemail(VoicemailAccess.NotFound, "no voicemail with this identifier is in the user's inbox")
                : new InboxVoicemail(VoicemailAccess.NotOwned, "the voicemail is not in the user's inbox");
        }

        return new InboxVoicemail(VoicemailAccess.Owned, null, inboxEntry, interaction);
    }

    private static bool IsVoicemailInteraction(Interaction interaction)
    {
        return interaction.TechnicalMetadata is not null &&
            interaction.TechnicalMetadata.TryGetValue(ContactCenterConstants.Voicemail.ProjectionMetadataKey, out var value) &&
            (value is bool boolean ? boolean : bool.TryParse(value?.ToString(), out var parsed) && parsed);
    }

    private static string GetStorageReference(Interaction interaction)
    {
        return interaction.TechnicalMetadata is not null &&
            interaction.TechnicalMetadata.TryGetValue(ContactCenterConstants.RecordingMetadata.StorageReference, out var value)
                ? value?.ToString()
                : null;
    }

    private static ILogger CreateVoicemailLogger(HttpContext httpContext)
    {
        return httpContext.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("ContactCenterVoicemailMedia");
    }

    private static void LogVoicemailRefused(ILogger logger, string interactionId, string reason)
    {
        if (logger?.IsEnabled(LogLevel.Debug) == true)
        {
            logger.LogDebug("Voicemail {InteractionId} is unavailable: {Reason}.", interactionId.SanitizeLogValue(), reason);
        }
    }

    private enum VoicemailAccess
    {
        Owned,
        NotFound,
        NotOwned,
    }

    private sealed record InboxVoicemail(
        VoicemailAccess Access,
        string Reason,
        TelephonyInteraction InboxEntry = null,
        Interaction Interaction = null);
}
