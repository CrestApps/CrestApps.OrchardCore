using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The default <see cref="IProviderCallStateSynchronizationService"/> for a tenant without the Voice feature.
/// There is no telephony provider, so there is no provider truth to reconcile against and the interaction is
/// returned as it was found. Encountering a provider-backed interaction in that state means a tenant has records
/// from a feature it no longer has enabled, so it is logged rather than passed over in silence.
/// </summary>
public sealed class NoProviderCallStateSynchronizationService : IProviderCallStateSynchronizationService
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NoProviderCallStateSynchronizationService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public NoProviderCallStateSynchronizationService(ILogger<NoProviderCallStateSynchronizationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<Interaction> RefreshInteractionAsync(Interaction interaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        _logger.LogWarning(
            "Interaction '{InteractionId}' is provider-backed but the Voice feature is not enabled, so provider truth cannot be reconciled. Enable Voice to restore provider-state healing.",
            interaction.ItemId.SanitizeLogValue());

        return Task.FromResult(interaction);
    }

    /// <inheritdoc/>
    public Task<int> ReconcileActiveInteractionsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    /// <inheritdoc/>
    public Task<int> ReconcileProviderInteractionsAsync(string providerName, CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}
