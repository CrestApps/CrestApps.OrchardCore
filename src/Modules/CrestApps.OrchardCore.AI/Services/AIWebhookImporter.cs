using System.Security.Cryptography;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core.Webhooks;
using CrestApps.OrchardCore.Webhooks.Core.Models;
using CrestApps.OrchardCore.Webhooks.Core.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Moves the AI webhook subscriptions and their pending deliveries into the Webhooks building block, where AI events
/// are now sent from. Nothing is deleted: each moved subscription is turned off and each moved delivery is settled, so
/// nothing is sent twice and the old delivery log ages out with its 30-day purge.
/// </summary>
internal sealed class AIWebhookImporter
{
    /// <summary>
    /// The error a moved AI delivery is settled with.
    /// </summary>
    internal const string MovedError = "Moved to the Webhooks delivery outbox.";

    private readonly IAIWebhookSubscriptionStore _aiSubscriptions;
    private readonly IAIWebhookDeliveryStore _aiDeliveries;
    private readonly IWebhookSubscriptionStore _subscriptions;
    private readonly IWebhookDeliveryStore _deliveries;
    private readonly WebhookDispatcher _dispatcher;
    private readonly IDataProtector _aiProtector;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public AIWebhookImporter(
        IAIWebhookSubscriptionStore aiSubscriptions,
        IAIWebhookDeliveryStore aiDeliveries,
        IWebhookSubscriptionStore subscriptions,
        IWebhookDeliveryStore deliveries,
        WebhookDispatcher dispatcher,
        IDataProtectionProvider dataProtectionProvider,
        IClock clock,
        ILogger<AIWebhookImporter> logger)
    {
        _aiSubscriptions = aiSubscriptions;
        _aiDeliveries = aiDeliveries;
        _subscriptions = subscriptions;
        _deliveries = deliveries;
        _dispatcher = dispatcher;
        _aiProtector = dataProtectionProvider.CreateProtector(AIWebhookPublisher.ProtectorPurpose);
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Moves every AI subscription not moved yet, and every pending AI delivery.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many subscriptions and deliveries were moved.</returns>
    public async Task<(int Subscriptions, int Deliveries)> ImportAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var movedSubscriptions = 0;
        var movedDeliveries = 0;

        foreach (var aiSubscription in await _aiSubscriptions.GetAllAsync(cancellationToken))
        {
            // The subscription keeps its id, so its pending deliveries still point at it.
            if (await _subscriptions.FindByIdAsync(aiSubscription.ItemId, cancellationToken) is not null)
            {
                continue;
            }

            await _subscriptions.CreateAsync(new WebhookSubscription
            {
                ItemId = aiSubscription.ItemId,
                Name = aiSubscription.Name,
                Url = aiSubscription.Url,
                Events = [.. aiSubscription.Events ?? []],
                IsEnabled = aiSubscription.Enabled,
                ProtectedSecret = MoveSecret(aiSubscription),
                CreatedUtc = aiSubscription.CreatedUtc,
                ModifiedUtc = now,
            }, cancellationToken);

            aiSubscription.Enabled = false;
            await _aiSubscriptions.UpdateAsync(aiSubscription, cancellationToken);
            movedSubscriptions++;
        }

        foreach (var aiDelivery in await _aiDeliveries.ListDueAsync(DateTime.MaxValue, int.MaxValue, cancellationToken))
        {
            await _deliveries.CreateAsync(new WebhookDelivery
            {
                ItemId = IdGenerator.GenerateId(),
                SubscriptionId = aiDelivery.SubscriptionId,

                // The id receivers already saw for this delivery, so a receiver that got it drops the repeat.
                EventId = aiDelivery.ItemId,
                EventName = aiDelivery.EventName,
                Payload = aiDelivery.Payload,
                Status = WebhookDeliveryStatus.Pending,
                Attempts = aiDelivery.Attempts,
                NextAttemptUtc = aiDelivery.NextAttemptUtc,
                LastStatusCode = aiDelivery.StatusCode,
                LastError = aiDelivery.Error,
                CreatedUtc = aiDelivery.CreatedUtc,
            }, cancellationToken);

            aiDelivery.Status = AIWebhookDeliveryStatus.Failed;
            aiDelivery.Error = MovedError;
            aiDelivery.CompletedUtc = now;
            await _aiDeliveries.UpdateAsync(aiDelivery, cancellationToken);
            movedDeliveries++;
        }

        if ((movedSubscriptions > 0 || movedDeliveries > 0) && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Moved {Subscriptions} AI webhook subscription(s) and {Deliveries} pending delivery(ies) to the Webhooks feature.", movedSubscriptions, movedDeliveries);
        }

        return (movedSubscriptions, movedDeliveries);
    }

    // The secret is read with the AI purpose and protected again with the Webhooks purpose, so receivers keep checking
    // the same signature. A subscription without a readable secret gets a new one, which its receiver has to be given.
    private string MoveSecret(AIWebhookSubscription aiSubscription)
    {
        if (!string.IsNullOrEmpty(aiSubscription.ProtectedSecret))
        {
            try
            {
                var secret = _aiProtector.Unprotect(aiSubscription.ProtectedSecret);

                if (!string.IsNullOrEmpty(secret))
                {
                    return _dispatcher.ProtectSecret(secret);
                }
            }
            catch (CryptographicException exception)
            {
                _logger.LogWarning(exception, "The secret of AI webhook subscription '{SubscriptionId}' can't be read.", aiSubscription.ItemId.SanitizeLogValue());
            }
        }

        _logger.LogWarning("AI webhook subscription '{SubscriptionId}' was given a new signing secret; copy it into its receiver.", aiSubscription.ItemId.SanitizeLogValue());

        return _dispatcher.NewProtectedSecret();
    }
}
