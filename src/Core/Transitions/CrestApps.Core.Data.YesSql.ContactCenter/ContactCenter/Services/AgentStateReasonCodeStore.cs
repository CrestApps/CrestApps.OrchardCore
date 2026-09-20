using CrestApps.Core.ContactCenter.Services;
using CrestApps.Core.ContactCenter;
using CrestApps.Core.Data.YesSql.Services;
using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.ContactCenter.Models;
using YesSql;

namespace CrestApps.Core.Data.YesSql.ContactCenter.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IAgentStateReasonCodeStore"/>.
/// </summary>
public sealed class AgentStateReasonCodeStore : ConcurrentDocumentCatalog<AgentStateReasonCode, AgentStateReasonCodeIndex>, IAgentStateReasonCodeStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentStateReasonCodeStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public AgentStateReasonCodeStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<AgentStateReasonCode> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return await Session.Query<AgentStateReasonCode, AgentStateReasonCodeIndex>(
            index => index.Name == name,
            collection: ContactCenterStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<AgentStateReasonCode>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        var reasonCodes = await Session.Query<AgentStateReasonCode, AgentStateReasonCodeIndex>(
            index => index.Enabled,
            collection: ContactCenterStorage.CollectionName)
            .OrderBy(index => index.SortOrder)
            .ThenBy(index => index.Name)
            .ListAsync(cancellationToken);

        return reasonCodes.ToArray();
    }
}
