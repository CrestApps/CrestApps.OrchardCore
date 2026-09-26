using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using ProviderVoiceEvent = CrestApps.OrchardCore.Telephony.Models.ProviderVoiceEvent;
using VoiceCallState = CrestApps.OrchardCore.Telephony.Models.VoiceCallState;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <inheritdoc />
/// <remarks>
/// The destination is resolved from the tenant's approved catalog alone, exactly as an agent's external transfer is:
/// the menu stores only the catalog entry's identifier, and an entry that has since been disabled, or whose number the
/// dial policy now refuses, is not reachable. The destination is shown the number the caller dialled, so it can see
/// which line the call came through. Every transfer is recorded in the call's audit trail with the number it went to.
/// <para>
/// The provider accepting the transfer only means it has started ringing the destination, which can still be busy,
/// not answer, or refuse the call; Telnyx then reports a hang-up on the leg it rang and leaves the caller's leg up
/// (https://developers.telnyx.com/api-reference/call-commands/transfer-call). Settling the call on acceptance ended
/// the interaction for a caller who was still on the line with nobody on the other end. On a provider that reports
/// the destination leg's outcome the call is held as a transfer in progress until it answers, and a failed one is
/// handed back so the caller is put somewhere else.
/// </para>
/// </remarks>
public sealed class IvrExternalTransferService : IIvrExternalTransferService
{
    /// <summary>
    /// The reason recorded when a caller was sent to an external number from the menu.
    /// </summary>
    public const string ReasonCode = "ivr_external_transfer";

    /// <summary>
    /// The interaction metadata key holding the approved destination a transfer in progress is ringing.
    /// </summary>
    public const string PendingDestinationMetadataKey = "ivrExternalTransferPendingDestination";

    /// <summary>
    /// The interaction metadata key holding the number a transfer in progress is ringing.
    /// </summary>
    public const string PendingAddressMetadataKey = "ivrExternalTransferPendingAddress";

    private const string RoutingTerminalReasonMetadataKey = "routing_terminal_reason";

    private readonly ISiteService _siteService;
    private readonly IDialDestinationPolicy _destinationPolicy;
    private readonly IEnumerable<IContactCenterOwnNumberSource> _ownNumberSources;
    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly IInteractionManager _interactionManager;
    private readonly IContactCenterWorkStateService _workStateService;
    private readonly IContactCenterActivityWriter _activityWriter;
    private readonly IProviderVoiceEventService _providerVoiceEventService;
    private readonly IContactCenterAuditRecorder _auditRecorder;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IvrExternalTransferService"/> class.
    /// </summary>
    public IvrExternalTransferService(
        ISiteService siteService,
        IDialDestinationPolicy destinationPolicy,
        IEnumerable<IContactCenterOwnNumberSource> ownNumberSources,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        IInteractionManager interactionManager,
        IContactCenterWorkStateService workStateService,
        IContactCenterActivityWriter activityWriter,
        IProviderVoiceEventService providerVoiceEventService,
        IContactCenterAuditRecorder auditRecorder,
        ISession session,
        IClock clock,
        ILogger<IvrExternalTransferService> logger)
    {
        _siteService = siteService;
        _destinationPolicy = destinationPolicy;
        _ownNumberSources = ownNumberSources ?? [];
        _voiceProviderResolver = voiceProviderResolver;
        _interactionManager = interactionManager;
        _workStateService = workStateService;
        _activityWriter = activityWriter;
        _providerVoiceEventService = providerVoiceEventService;
        _auditRecorder = auditRecorder;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> TransferAsync(Interaction interaction, string destinationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        var destination = await ResolveDestinationAsync(destinationId);

        if (destination is null)
        {
            _logger.LogWarning(
                "The entry-point menu on interaction '{InteractionId}' named external destination '{DestinationId}', which is not an enabled, dialable destination in the approved catalog.",
                interaction.ItemId.SanitizeLogValue(),
                destinationId.SanitizeLogValue());

            return false;
        }

        var provider = _voiceProviderResolver.Get(interaction.ProviderName);

        if (provider is not IContactCenterVoiceTransferProvider transferProvider ||
            !provider.Capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.CallTransfer) ||
            string.IsNullOrEmpty(interaction.ProviderInteractionId))
        {
            _logger.LogWarning(
                "The voice provider for interaction '{InteractionId}' cannot transfer calls, so the entry-point menu's external transfer was not made.",
                interaction.ItemId.SanitizeLogValue());

            return false;
        }

        var reportsOutcome = provider.Capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.TransferOutcomeReporting);
        var request = new ContactCenterVoiceTransferRequest
        {
            InteractionId = interaction.ItemId,
            ProviderCallId = interaction.ProviderInteractionId,
            TransferType = InteractionTransferType.Blind,
            TargetType = InteractionTransferTargetType.External,
            Target = destination.E164Address,
            CallerId = ReadServiceAddress(interaction),
        };

        if (reportsOutcome)
        {
            request.Metadata[ContactCenterConstants.TransferMetadata.ReportOutcome] = bool.TrueString;
        }

        var result = await transferProvider.TransferAsync(request, cancellationToken);

        if (result?.Succeeded != true || result.OutcomeUnknown)
        {
            _logger.LogWarning(
                "The provider did not transfer interaction '{InteractionId}' to external destination '{DestinationId}': {Error}",
                interaction.ItemId.SanitizeLogValue(),
                destination.Id.SanitizeLogValue(),
                result?.ErrorMessage.SanitizeLogValue());

            return false;
        }

        if (!reportsOutcome)
        {
            await SettleAsync(interaction, destination.Id, destination.E164Address, cancellationToken);

            return true;
        }

        // The destination is ringing. The call stays the contact center's until it answers, so a failure can still
        // put the caller somewhere else.
        interaction.TechnicalMetadata[PendingDestinationMetadataKey] = destination.Id;
        interaction.TechnicalMetadata[PendingAddressMetadataKey] = destination.E164Address;
        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

        await _auditRecorder.RecordIvrAsync(
            ContactCenterConstants.Events.IvrActionTaken,
            interaction,
            new IvrAuditStep
            {
                NodeId = IvrExecutionService.ReadState(interaction).CurrentNodeId,
                Action = nameof(IvrActionKind.ExternalTransfer),
                Target = destination.E164Address,
                Reason = "ExternalTransferRinging",
                EntryPointId = ReadString(interaction, EntryPointFlowResolver.EntryPointMetadataKey),
            },
            _clock.UtcNow,
            $"ivr:external-ringing:{interaction.ItemId}:{destination.Id}",
            cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> CompleteAsync(Interaction interaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        if (!TryReadPending(interaction, out var destinationId, out var address))
        {
            return false;
        }

        ClearPending(interaction);
        await SettleAsync(interaction, destinationId, address, cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<string> FailAsync(Interaction interaction, string hangupCause, bool callerLeft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        if (!TryReadPending(interaction, out var destinationId, out var address))
        {
            return null;
        }

        ClearPending(interaction);
        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

        await _auditRecorder.RecordIvrAsync(
            ContactCenterConstants.Events.IvrFallbackTaken,
            interaction,
            new IvrAuditStep
            {
                NodeId = IvrExecutionService.ReadState(interaction).CurrentNodeId,
                Action = nameof(IvrActionKind.ExternalTransfer),
                Target = address,
                Reason = callerLeft ? "ExternalTransferAbandoned" : "ExternalTransferFailed",
                EntryPointId = ReadString(interaction, EntryPointFlowResolver.EntryPointMetadataKey),
                HangupCause = hangupCause,
            },
            _clock.UtcNow,
            $"ivr:external-failed:{interaction.ItemId}:{destinationId}",
            cancellationToken);

        _logger.LogWarning(
            "The external transfer of interaction '{InteractionId}' to destination '{DestinationId}' did not connect ({Cause}).",
            interaction.ItemId.SanitizeLogValue(),
            destinationId.SanitizeLogValue(),
            hangupCause.SanitizeLogValue());

        return destinationId;
    }

    // The call has left the contact center: nothing routes it any more and its activity is finished.
    private async Task SettleAsync(Interaction interaction, string destinationId, string address, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        interaction.TechnicalMetadata[RoutingTerminalReasonMetadataKey] = ReasonCode;
        InteractionTransferHistory.Open(interaction, fromAgentId: null, InteractionTransferTargetType.External, destinationId, now, InteractionTransferHistory.SentToExternalNumber).CompletedUtc = now;
        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

        await _workStateService.MutateAsync(
            interaction.ActivityItemId,
            workState => workState.TransitionTo(ActivityAssignmentStatus.Released),
            cancellationToken);

        await _activityWriter.ScheduleUpdateAsync(interaction.ActivityItemId, activity =>
        {
            activity.Status = ActivityStatus.Completed;
            activity.TerminalReasonCode = ReasonCode;
            activity.CompletedUtc = now;
        }, cancellationToken);

        await _auditRecorder.RecordIvrAsync(
            ContactCenterConstants.Events.IvrActionTaken,
            interaction,
            new IvrAuditStep
            {
                NodeId = IvrExecutionService.ReadState(interaction).CurrentNodeId,
                Action = nameof(IvrActionKind.ExternalTransfer),
                Target = address,
                Reason = "ExternalTransferCompleted",
                EntryPointId = ReadString(interaction, EntryPointFlowResolver.EntryPointMetadataKey),
            },
            now,
            $"ivr:external:{interaction.ItemId}:{destinationId}",
            cancellationToken);

        await _session.SaveChangesAsync(cancellationToken);

        // Settled through provider truth like every other ending, so the call's outcome, duration and reporting are
        // decided in the one place that decides them.
        await _providerVoiceEventService.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = interaction.ProviderName,
            ProviderCallId = interaction.ProviderInteractionId,
            State = VoiceCallState.Transferred,
            OccurredUtc = now,
            IdempotencyKey = $"ivr-transfer-external:{interaction.ItemId}",
        }, cancellationToken);
    }

    private async Task<ContactCenterExternalDestination> ResolveDestinationAsync(string destinationId)
    {
        if (string.IsNullOrWhiteSpace(destinationId))
        {
            return null;
        }

        var site = await _siteService.GetSiteSettingsAsync();
        var settings = site.GetOrCreate<ContactCenterExternalTransferSettings>();
        var entry = settings.Destinations?.FirstOrDefault(candidate =>
            string.Equals(candidate?.Id, destinationId.Trim(), StringComparison.OrdinalIgnoreCase));

        // Only the curated catalog: a menu has no agent typing a number, so the tenant's opt-in to unlisted numbers
        // does not apply here.
        if (entry is null || !entry.Enabled || string.IsNullOrWhiteSpace(entry.E164Address))
        {
            return null;
        }

        if (!_destinationPolicy.Evaluate(entry.E164Address, new DialDestinationContext { Operation = DialDestinationOperation.Transfer }).IsAllowed)
        {
            return null;
        }

        // A number the contact center owns would ring straight back into it.
        if (await ContactCenterOwnNumbers.ContainsAsync(_ownNumberSources, entry.E164Address))
        {
            return null;
        }

        return entry;
    }

    private static bool TryReadPending(Interaction interaction, out string destinationId, out string address)
    {
        destinationId = ReadString(interaction, PendingDestinationMetadataKey);
        address = ReadString(interaction, PendingAddressMetadataKey);

        return !string.IsNullOrEmpty(destinationId);
    }

    private static void ClearPending(Interaction interaction)
    {
        interaction.TechnicalMetadata.Remove(PendingDestinationMetadataKey);
        interaction.TechnicalMetadata.Remove(PendingAddressMetadataKey);
    }

    private static string ReadServiceAddress(Interaction interaction)
        => ReadString(interaction, ContactCenterConstants.TelephonyMetadata.ServiceAddress);

    // Every value read here was written as a string, and a string metadata value is read back as one.
    private static string ReadString(Interaction interaction, string key)
        => interaction.TechnicalMetadata.TryGetValue(key, out var value) &&
            value?.ToString() is { Length: > 0 } text
            ? text
            : null;
}
