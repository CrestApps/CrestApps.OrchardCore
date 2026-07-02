using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IAgentStateReasonCodeStore"/>.
/// </summary>
public sealed class AgentStateReasonCodeStore : DocumentCatalog<AgentStateReasonCode, AgentStateReasonCodeIndex>, IAgentStateReasonCodeStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentStateReasonCodeStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public AgentStateReasonCodeStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<AgentStateReasonCode> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return await Session.Query<AgentStateReasonCode, AgentStateReasonCodeIndex>(
            index => index.Name == name,
            collection: ContactCenterConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<AgentStateReasonCode>> ListEnabledAsync(CancellationToken cancellationToken = default)
    {
        var reasonCodes = await Session.Query<AgentStateReasonCode, AgentStateReasonCodeIndex>(
            index => index.Enabled,
            collection: ContactCenterConstants.CollectionName)
            .OrderBy(index => index.SortOrder)
            .ThenBy(index => index.Name)
            .ListAsync(cancellationToken);

        return reasonCodes.ToArray();
    }
}
