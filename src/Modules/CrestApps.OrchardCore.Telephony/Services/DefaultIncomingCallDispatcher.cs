using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultIncomingCallDispatcher"/> class.
    /// </summary>
    /// <param name="hubContext">The telephony hub context used to push events to connected clients.</param>
    /// <param name="contextProviders">The registered incoming-call context providers.</param>
    /// <param name="logger">The logger.</param>
    public DefaultIncomingCallDispatcher(
        IHubContext<TelephonyHub, ITelephonyClient> hubContext,
        IEnumerable<IIncomingCallContextProvider> contextProviders,
        ILogger<DefaultIncomingCallDispatcher> logger)
    {
        _hubContext = hubContext;
        _contextProviders = contextProviders;
        _logger = logger;
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
                _logger.LogError(ex, "An incoming-call context provider of type '{ProviderType}' failed while enriching an inbound call.", provider.GetType().FullName);
            }
        }

        var context = new IncomingCallContext
        {
            Heading = contributionContext.Heading,
            Cards = [.. contributionContext.Cards.OrderBy(card => card.Priority)],
            Properties = contributionContext.Properties,
        };

        await _hubContext.Clients.User(userId).IncomingCall(call, context);
    }
}
