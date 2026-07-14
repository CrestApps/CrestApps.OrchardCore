using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IProviderVoiceWebhookProcessor"/>.
/// </summary>
public sealed class ProviderVoiceWebhookProcessor : IProviderVoiceWebhookProcessor
{
    private readonly IEnumerable<IProviderVoiceWebhookAdapter> _adapters;
    private readonly IProviderWebhookInbox _inbox;
    private readonly IProviderWebhookIngressLimiter _ingressLimiter;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderVoiceWebhookProcessor"/> class.
    /// </summary>
    /// <param name="adapters">The registered provider webhook adapters.</param>
    /// <param name="inbox">The durable provider webhook inbox.</param>
    /// <param name="ingressLimiter">The provider webhook ingress limiter.</param>
    /// <param name="logger">The logger instance.</param>
    public ProviderVoiceWebhookProcessor(
        IEnumerable<IProviderVoiceWebhookAdapter> adapters,
        IProviderWebhookInbox inbox,
        IProviderWebhookIngressLimiter ingressLimiter,
        ILogger<ProviderVoiceWebhookProcessor> logger)
    {
        _adapters = adapters;
        _inbox = inbox;
        _ingressLimiter = ingressLimiter;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ProviderVoiceWebhookOutcome> ProcessAsync(ProviderVoiceWebhookRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var adapter = _adapters.FirstOrDefault(a => string.Equals(a.TechnicalName, request.Provider, StringComparison.OrdinalIgnoreCase));

        if (adapter is null)
        {
            return new ProviderVoiceWebhookOutcome { Status = ProviderVoiceWebhookStatus.UnknownProvider };
        }

        if (!adapter.ValidateSignature(request))
        {
            _logger.LogWarning("Rejected a voice webhook for provider '{Provider}' because the signature failed validation.", adapter.TechnicalName);

            return new ProviderVoiceWebhookOutcome { Status = ProviderVoiceWebhookStatus.InvalidSignature };
        }

        using var rateLease = await _ingressLimiter.AcquireRateAsync(adapter.TechnicalName, cancellationToken);

        if (!rateLease.IsAcquired)
        {
            return new ProviderVoiceWebhookOutcome
            {
                Status = ProviderVoiceWebhookStatus.RateLimited,
                RetryAfter = rateLease.RetryAfter,
            };
        }

        var events = adapter.Parse(request) ?? [];

        if (events.Any(providerEvent =>
            string.IsNullOrEmpty(providerEvent.IdempotencyKey) ||
            providerEvent.IdempotencyKey.Length > ProviderWebhookInbox.MaxDeliveryIdLength))
        {
            _logger.LogWarning(
                "Rejected a voice webhook for provider '{Provider}' because a parsed event had a missing or oversized idempotency key.",
                adapter.TechnicalName);

            return new ProviderVoiceWebhookOutcome { Status = ProviderVoiceWebhookStatus.MissingIdempotencyKey };
        }

        if (events.Any(providerEvent => !_ingressLimiter.IsFresh(providerEvent.OccurredUtc)))
        {
            _logger.LogWarning("Rejected a voice webhook for provider '{Provider}' because a parsed event timestamp was missing, non-UTC, stale, or too far in the future.", adapter.TechnicalName);

            return new ProviderVoiceWebhookOutcome { Status = ProviderVoiceWebhookStatus.StaleDelivery };
        }

        var accepted = 0;

        foreach (var providerEvent in events)
        {
            providerEvent.ProviderName ??= adapter.TechnicalName;
            var acceptance = await _inbox.AcceptAsync(new ProviderWebhookInboxDelivery
            {
                ProviderName = adapter.TechnicalName,
                DeliveryId = providerEvent.IdempotencyKey,
                HandlerName = ProviderVoiceEventInboxHandler.HandlerTechnicalName,
                Payload = JsonSerializer.Serialize(providerEvent),
            }, cancellationToken);

            if (acceptance.Status == ProviderWebhookInboxAcceptanceStatus.Busy)
            {
                return new ProviderVoiceWebhookOutcome { Status = ProviderVoiceWebhookStatus.InboxBusy };
            }

            if (acceptance.Status == ProviderWebhookInboxAcceptanceStatus.Accepted)
            {
                accepted++;
            }

            await _inbox.DispatchAsync(acceptance.MessageId, CancellationToken.None);
        }

        return new ProviderVoiceWebhookOutcome
        {
            Status = ProviderVoiceWebhookStatus.Accepted,
            ProcessedCount = accepted,
        };
    }
}
