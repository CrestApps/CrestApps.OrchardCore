using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IAgentStateReasonCodeManager"/>.
/// </summary>
public sealed class AgentStateReasonCodeManager : CatalogManager<AgentStateReasonCode>, IAgentStateReasonCodeManager
{
    private readonly IAgentStateReasonCodeStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentStateReasonCodeManager"/> class.
    /// </summary>
    /// <param name="store">The underlying reason code store.</param>
    /// <param name="handlers">The catalog entry handlers for reason codes.</param>
    /// <param name="logger">The logger instance.</param>
    public AgentStateReasonCodeManager(
        IAgentStateReasonCodeStore store,
        IEnumerable<ICatalogEntryHandler<AgentStateReasonCode>> handlers,
        ILogger<CatalogManager<AgentStateReasonCode>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<AgentStateReasonCode> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var reasonCode = await _store.FindByNameAsync(name, cancellationToken);

        if (reasonCode is not null)
        {
            await LoadAsync(reasonCode, cancellationToken);
        }

        return reasonCode;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<AgentStateReasonCode>> ListEnabledAsync(CancellationToken cancellationToken = default)
    {
        var reasonCodes = await _store.ListEnabledAsync(cancellationToken);

        foreach (var reasonCode in reasonCodes)
        {
            await LoadAsync(reasonCode, cancellationToken);
        }

        return reasonCodes;
    }
}
