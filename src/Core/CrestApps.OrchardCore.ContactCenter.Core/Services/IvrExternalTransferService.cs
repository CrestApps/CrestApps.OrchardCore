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
/// </remarks>
public sealed class IvrExternalTransferService : IIvrExternalTransferService
{
    /// <summary>
    /// The reason recorded when a caller was sent to an external number from the menu.
    /// </summary>
    public const string ReasonCode = "ivr_external_transfer";

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

        var result = await transferProvider.TransferAsync(new ContactCenterVoiceTransferRequest
        {
            InteractionId = interaction.ItemId,
            ProviderCallId = interaction.ProviderInteractionId,
            TransferType = InteractionTransferType.Blind,
            TargetType = InteractionTransferTargetType.External,
            Target = destination.E164Address,
            CallerId = ReadServiceAddress(interaction),
        }, cancellationToken);

        if (result?.Succeeded != true || result.OutcomeUnknown)
        {
            _logger.LogWarning(
                "The provider did not transfer interaction '{InteractionId}' to external destination '{DestinationId}': {Error}",
                interaction.ItemId.SanitizeLogValue(),
                destination.Id.SanitizeLogValue(),
                result?.ErrorMessage.SanitizeLogValue());

            return false;
        }

        var now = _clock.UtcNow;

        // The call has left the contact center: nothing routes it any more and its activity is finished.
        interaction.TechnicalMetadata[RoutingTerminalReasonMetadataKey] = ReasonCode;
        InteractionTransferHistory.Open(interaction, fromAgentId: null, InteractionTransferTargetType.External, destination.Id, now, InteractionTransferHistory.SentToExternalNumber).CompletedUtc = now;
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
                Target = destination.E164Address,
                Reason = "ExternalTransferCompleted",
                EntryPointId = interaction.TechnicalMetadata.TryGetValue(EntryPointFlowResolver.EntryPointMetadataKey, out var entryPointId) ? entryPointId?.ToString() : null,
            },
            now,
            $"ivr:external:{interaction.ItemId}:{destination.Id}",
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

        return true;
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

    private static string ReadServiceAddress(Interaction interaction)
        => interaction.TechnicalMetadata.TryGetValue(ContactCenterConstants.TelephonyMetadata.ServiceAddress, out var value) &&
            value?.ToString() is { Length: > 0 } serviceAddress
            ? serviceAddress
            : null;
}
