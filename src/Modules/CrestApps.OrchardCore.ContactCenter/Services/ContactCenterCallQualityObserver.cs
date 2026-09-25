using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Stores each call leg's quality against the call and agent it belongs to.
/// </summary>
/// <remarks>
/// A measurement knows its leg only by the provider's call-control id. An agent's leg is found through the call
/// session's legs; the customer's leg is the call session's own provider call. A leg the contact center has no session
/// for, such as an extension call between two agents, is still recorded, against the agent alone, because a poor call
/// is worth keeping whether or not it was routed.
/// </remarks>
public sealed class ContactCenterCallQualityObserver : ICallQualityObserver
{
    private readonly ICallQualityRecordStore _recordStore;
    private readonly ICallSessionStore _callSessionStore;
    private readonly IAgentProfileStore _agentProfileStore;
    private readonly ICallQualityAlertService _alertService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public ContactCenterCallQualityObserver(
        ICallQualityRecordStore recordStore,
        ICallSessionStore callSessionStore,
        IAgentProfileStore agentProfileStore,
        ICallQualityAlertService alertService,
        IClock clock,
        ILogger<ContactCenterCallQualityObserver> logger)
    {
        _recordStore = recordStore;
        _callSessionStore = callSessionStore;
        _agentProfileStore = agentProfileStore;
        _alertService = alertService;
        _clock = clock;
        _logger = logger;
    }

    public async Task ObserveAsync(CallQualityObservation observation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observation);

        var callControlId = observation.ProviderCallControlId;

        // Without the leg's id there is no call to tie the measurement to and no way to tell a redelivery from a
        // second call, so it stays in the log the hub already wrote.
        if (string.IsNullOrEmpty(callControlId))
        {
            return;
        }

        var recordKey = CallQualityRecord.BuildRecordKey(observation.Source, callControlId);

        if (await _recordStore.FindByRecordKeyAsync(recordKey, cancellationToken) is not null)
        {
            return;
        }

        var record = new CallQualityRecord
        {
            RecordKey = recordKey,
            Source = observation.Source,
            Rating = observation.Rating,
            ProviderName = observation.ProviderName,
            ProviderCallControlId = callControlId,
            ProviderLegId = observation.ProviderLegId,
            ProviderSessionId = observation.ProviderSessionId,
            UserId = observation.UserId,
            Browser = observation.Browser,
            Provider = observation.Provider,
            ObservedUtc = observation.ObservedUtc,
            CreatedUtc = _clock.UtcNow,
        };

        CallQualityRecordFigures.Apply(record);
        await AttachCallAsync(record, callControlId, cancellationToken);
        await AttachAgentAsync(record, cancellationToken);

        await _recordStore.CreateAsync(record, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Recorded {Source} call quality {Rating} for leg {CallControlId} of interaction {InteractionId}, agent {AgentId}.",
                record.Source,
                record.Rating,
                callControlId.SanitizeLogValue(),
                record.InteractionId.SanitizeLogValue(),
                record.AgentId.SanitizeLogValue());
        }

        await _alertService.EvaluateAsync(record, cancellationToken);
    }

    private async Task AttachCallAsync(CallQualityRecord record, string callControlId, CancellationToken cancellationToken)
    {
        var session = await _callSessionStore.FindByProviderLegIdAsync(callControlId, cancellationToken);
        var leg = session?.Legs.FirstOrDefault(candidate => string.Equals(candidate.ProviderLegId, callControlId, StringComparison.Ordinal));

        if (session is null)
        {
            session = await _callSessionStore.FindByProviderCallIdAsync(callControlId, cancellationToken);
        }

        if (session is null)
        {
            return;
        }

        record.CallSessionId = session.ItemId;
        record.InteractionId = session.InteractionId;
        record.QueueId = session.QueueId;
        record.ProviderName ??= session.ProviderName;

        // A session's own provider call is the leg that reached the platform first: the customer's on an inbound
        // call. Any other leg is known by the part it was given.
        record.LegRole = leg?.Role ?? (string.Equals(session.ProviderCallId, callControlId, StringComparison.Ordinal)
            ? CallPartyRole.Customer
            : CallPartyRole.Unknown);

        record.AgentId = leg?.AgentId ?? session.AgentId;
    }

    private async Task AttachAgentAsync(CallQualityRecord record, CancellationToken cancellationToken)
    {
        // The soft phone measures the leg of the agent it runs for, whatever the session says, which matters on a
        // transferred call whose session names the agent it ended with.
        if (!string.IsNullOrEmpty(record.UserId))
        {
            var profile = await _agentProfileStore.FindByUserIdAsync(record.UserId, cancellationToken);

            if (profile is not null)
            {
                record.AgentId = profile.ItemId;
            }

            if (record.LegRole == CallPartyRole.Unknown)
            {
                record.LegRole = CallPartyRole.Agent;
            }

            return;
        }

        if (!string.IsNullOrEmpty(record.AgentId))
        {
            var profile = await _agentProfileStore.FindByIdAsync(record.AgentId, cancellationToken);

            record.UserId = profile?.UserId;
        }
    }
}
