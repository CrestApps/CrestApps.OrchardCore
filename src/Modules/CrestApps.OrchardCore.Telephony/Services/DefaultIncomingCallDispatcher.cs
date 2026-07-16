using CrestApps.OrchardCore.SignalR;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.SignalR;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Default <see cref="IIncomingCallDispatcher"/> implementation. It gathers the contextual cards from
/// the registered <see cref="IIncomingCallContextProvider"/> instances and pushes the ringing inbound
/// call to every soft-phone connection the target user currently has open.
/// </summary>
public sealed class DefaultIncomingCallDispatcher : IIncomingCallDispatcher
{
    private readonly IHubContext<TelephonyHub, ITelephonyClient> _hubContext;
    private readonly IEnumerable<IIncomingCallContextProvider> _contextProviders;
    private readonly ITelephonyInteractionStore _interactionStore;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly string _tenantName;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultIncomingCallDispatcher"/> class.
    /// </summary>
    /// <param name="hubContext">The telephony hub context used to push events to connected clients.</param>
    /// <param name="contextProviders">The registered incoming-call context providers.</param>
    /// <param name="interactionStore">The telephony interaction store.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="shellSettings">The current Orchard shell settings.</param>
    public DefaultIncomingCallDispatcher(
        IHubContext<TelephonyHub, ITelephonyClient> hubContext,
        IEnumerable<IIncomingCallContextProvider> contextProviders,
        ITelephonyInteractionStore interactionStore,
        IClock clock,
        ILogger<DefaultIncomingCallDispatcher> logger,
        ShellSettings shellSettings)
    {
        _hubContext = hubContext;
        _contextProviders = contextProviders;
        _interactionStore = interactionStore;
        _clock = clock;
        _logger = logger;
        _tenantName = shellSettings.Name;
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(string userId, TelephonyCall call, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentNullException.ThrowIfNull(call);

        var contributionContext = new IncomingCallContributionContext(call, userId);

        foreach (var provider in _contextProviders)
        {
            try
            {
                await provider.ContributeAsync(contributionContext, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(OperationalLogRedactor.RedactException(ex), "An incoming-call context provider of type '{ProviderType}' failed while enriching an inbound call.", provider.GetType().FullName);
            }
        }

        var context = new IncomingCallContext
        {
            Heading = contributionContext.Heading,
            Cards = [.. contributionContext.Cards.OrderBy(card => card.Priority)],
            Properties = contributionContext.Properties,
        };

        await RecordInteractionAsync(userId, call, cancellationToken);
        await _hubContext.Clients
            .Group(TenantSignalRGroupName.ForUser(_tenantName, userId))
            .IncomingCall(call, context);
    }

    private async Task RecordInteractionAsync(string userId, TelephonyCall call, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(call.CallId))
        {
            return;
        }

        var existing = await _interactionStore.FindByCallIdAsync(userId, call.CallId, cancellationToken);

        if (existing is null)
        {
            var interaction = new TelephonyInteraction
            {
                InteractionId = IdGenerator.GenerateId(),
                CallId = call.CallId,
                ProviderName = call.ProviderName,
                UserId = userId,
                From = call.From,
                To = call.To,
                Direction = call.Direction,
                Outcome = CallOutcome.InProgress,
                StartedUtc = call.StartedUtc?.UtcDateTime ?? _clock.UtcNow,
            };

            await _interactionStore.CreateAsync(interaction, cancellationToken);

            return;
        }

        existing.ProviderName = call.ProviderName;
        existing.From = string.IsNullOrEmpty(call.From) ? existing.From : call.From;
        existing.To = string.IsNullOrEmpty(call.To) ? existing.To : call.To;
        existing.Direction = call.Direction;

        if (!existing.EndedUtc.HasValue)
        {
            existing.Outcome = CallOutcome.InProgress;
        }

        await _interactionStore.UpdateAsync(existing, cancellationToken);
    }
}
