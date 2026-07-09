using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Resolves the currently pending inbound offer for an agent and rebuilds the same incoming-call modal
/// payload used when the offer was first dispatched.
/// </summary>
public sealed class PendingIncomingCallOfferService : IPendingIncomingCallOfferService
{
    private const string ServiceAddressMetadataKey = "serviceAddress";

    private readonly IAgentProfileManager _agentManager;
    private readonly IActivityReservationManager _reservationManager;
    private readonly IInteractionManager _interactionManager;
    private readonly IEnumerable<IIncomingCallContextProvider> _contextProviders;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="PendingIncomingCallOfferService"/> class.
    /// </summary>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="reservationManager">The reservation manager.</param>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="contextProviders">The incoming-call context providers.</param>
    /// <param name="clock">The clock.</param>
    public PendingIncomingCallOfferService(
        IAgentProfileManager agentManager,
        IActivityReservationManager reservationManager,
        IInteractionManager interactionManager,
        IEnumerable<IIncomingCallContextProvider> contextProviders,
        IClock clock)
    {
        _agentManager = agentManager;
        _reservationManager = reservationManager;
        _interactionManager = interactionManager;
        _contextProviders = contextProviders;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<PendingIncomingCallOffer> GetForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var agent = await _agentManager.FindByUserIdAsync(userId, cancellationToken);

        if (agent is null)
        {
            return null;
        }

        var reservation = await _reservationManager.FindPendingByAgentAsync(agent.ItemId, cancellationToken);
        var now = _clock.UtcNow;

        if (reservation is null ||
            reservation.ExpiresUtc <= now)
        {
            return null;
        }

        var interaction = await _interactionManager.FindByActivityIdAsync(reservation.ActivityItemId, cancellationToken);

        if (interaction is null ||
            interaction.Direction != InteractionDirection.Inbound ||
            interaction.Status != InteractionStatus.Ringing ||
            !string.Equals(interaction.AgentId, agent.ItemId, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(interaction.ProviderInteractionId))
        {
            return null;
        }

        var call = BuildCall(interaction, now);
        var contributionContext = new IncomingCallContributionContext(call, userId);

        foreach (var provider in _contextProviders)
        {
            await provider.ContributeAsync(contributionContext, cancellationToken);
        }

        contributionContext.Properties["reservationId"] = reservation.ItemId;
        contributionContext.Properties["expiresUtc"] = reservation.ExpiresUtc.ToString("O");
        contributionContext.Properties["serverTimeUtc"] = now.ToString("O");

        return new PendingIncomingCallOffer
        {
            Call = call,
            Context = new IncomingCallContext
            {
                Heading = contributionContext.Heading,
                Cards = [.. contributionContext.Cards.OrderBy(card => card.Priority)],
                Properties = new Dictionary<string, string>(contributionContext.Properties, StringComparer.OrdinalIgnoreCase),
            },
            ExpiresUtc = reservation.ExpiresUtc,
            ServerTimeUtc = now,
        };
    }

    private static TelephonyCall BuildCall(Interaction interaction, DateTime now)
    {
        return new TelephonyCall
        {
            CallId = interaction.ProviderInteractionId,
            From = interaction.CustomerAddress,
            To = ResolveServiceAddress(interaction),
            State = CallState.Ringing,
            Direction = CallDirection.Inbound,
            ProviderName = interaction.ProviderName,
            StartedUtc = interaction.CreatedUtc == default
                ? now
                : new DateTimeOffset(DateTime.SpecifyKind(interaction.CreatedUtc, DateTimeKind.Utc)),
            Metadata = BuildCallMetadata(interaction),
        };
    }

    private static string ResolveServiceAddress(Interaction interaction)
    {
        return interaction.TechnicalMetadata.TryGetValue(ServiceAddressMetadataKey, out var value)
            ? value?.ToString()
            : null;
    }

    private static Dictionary<string, object> BuildCallMetadata(Interaction interaction)
    {
        var metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(interaction.CustomerAddress))
        {
            metadata["callerAddress"] = interaction.CustomerAddress;
        }

        var serviceAddress = ResolveServiceAddress(interaction);

        if (!string.IsNullOrWhiteSpace(serviceAddress))
        {
            metadata["calledAddress"] = serviceAddress;
        }

        if (!string.IsNullOrWhiteSpace(interaction.ProviderName))
        {
            metadata["providerName"] = interaction.ProviderName;
        }

        if (!string.IsNullOrWhiteSpace(interaction.ItemId))
        {
            metadata["interactionId"] = interaction.ItemId;
        }

        if (!string.IsNullOrWhiteSpace(interaction.ActivityItemId))
        {
            metadata["activityItemId"] = interaction.ActivityItemId;
        }

        if (!string.IsNullOrWhiteSpace(interaction.QueueId))
        {
            metadata["queueId"] = interaction.QueueId;
        }

        return metadata;
    }
}
