using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;
using CrestApps.Core.Telephony;
using CrestApps.Core.Telephony.Models;

namespace CrestApps.Core.Telephony.Services;

/// <summary>
/// Default <see cref="IIncomingCallDispatcher"/> implementation. It gathers the contextual cards from
/// the registered <see cref="IIncomingCallContextProvider"/> instances and pushes the ringing inbound
/// call to every soft-phone connection the target user currently has open.
/// </summary>
public sealed class DefaultIncomingCallDispatcher : IIncomingCallDispatcher
{
    private readonly ITelephonySoftPhoneNotifier _notifier;
    private readonly IEnumerable<IIncomingCallContextProvider> _contextProviders;
    private readonly ITelephonyInteractionStore _interactionStore;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultIncomingCallDispatcher"/> class.
    /// </summary>
    /// <param name="notifier">Pushes the ringing call to the user's soft phones.</param>
    /// <param name="contextProviders">The registered incoming-call context providers.</param>
    /// <param name="interactionStore">The telephony interaction store.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    public DefaultIncomingCallDispatcher(
        ITelephonySoftPhoneNotifier notifier,
        IEnumerable<IIncomingCallContextProvider> contextProviders,
        ITelephonyInteractionStore interactionStore,
        TimeProvider timeProvider,
        ILogger<DefaultIncomingCallDispatcher> logger)
    {
        _notifier = notifier;
        _contextProviders = contextProviders;
        _interactionStore = interactionStore;
        _timeProvider = timeProvider;
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

        await RecordInteractionAsync(userId, call, cancellationToken);
        await _notifier.NotifyIncomingCallAsync(userId, call, context, cancellationToken);
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
                InteractionId = IdentifierGenerator.Generate(),
                CallId = call.CallId,
                ProviderName = call.ProviderName,
                UserId = userId,
                From = call.From,
                To = call.To,
                Direction = call.Direction,
                Outcome = CallOutcome.InProgress,
                StartedUtc = call.StartedUtc?.UtcDateTime ?? _timeProvider.GetUtcNow().UtcDateTime,
            };

            await _interactionStore.CreateAsync(interaction, cancellationToken);

            return;
        }

        await _interactionStore.UpdateByIdAsync(
            existing.InteractionId,
            candidate =>
            {
                candidate.ProviderName = call.ProviderName;
                candidate.From = string.IsNullOrEmpty(call.From) ? candidate.From : call.From;
                candidate.To = string.IsNullOrEmpty(call.To) ? candidate.To : call.To;
                candidate.Direction = call.Direction;

                if (!candidate.EndedUtc.HasValue)
                {
                    candidate.Outcome = CallOutcome.InProgress;
                }

                return true;
            },
            cancellationToken);
    }
}
