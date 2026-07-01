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
    private readonly IProviderVoiceEventService _providerVoiceEventService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderVoiceWebhookProcessor"/> class.
    /// </summary>
    /// <param name="adapters">The registered provider webhook adapters.</param>
    /// <param name="providerVoiceEventService">The provider voice event ingestion service.</param>
    /// <param name="logger">The logger instance.</param>
    public ProviderVoiceWebhookProcessor(
        IEnumerable<IProviderVoiceWebhookAdapter> adapters,
        IProviderVoiceEventService providerVoiceEventService,
        ILogger<ProviderVoiceWebhookProcessor> logger)
    {
        _adapters = adapters;
        _providerVoiceEventService = providerVoiceEventService;
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
            _logger.LogWarning("Rejected a voice webhook for provider '{Provider}' because the signature failed validation.", request.Provider);

            return new ProviderVoiceWebhookOutcome { Status = ProviderVoiceWebhookStatus.InvalidSignature };
        }

        var events = adapter.Parse(request) ?? [];

        if (events.Any(providerEvent => string.IsNullOrEmpty(providerEvent.IdempotencyKey)))
        {
            _logger.LogWarning("Rejected a voice webhook for provider '{Provider}' because a parsed event was missing an idempotency key.", request.Provider);

            return new ProviderVoiceWebhookOutcome { Status = ProviderVoiceWebhookStatus.MissingIdempotencyKey };
        }

        var processed = 0;

        foreach (var providerEvent in events)
        {
            providerEvent.ProviderName ??= adapter.TechnicalName;
            await _providerVoiceEventService.IngestAsync(providerEvent, cancellationToken);
            processed++;
        }

        return new ProviderVoiceWebhookOutcome
        {
            Status = ProviderVoiceWebhookStatus.Accepted,
            ProcessedCount = processed,
        };
    }
}
