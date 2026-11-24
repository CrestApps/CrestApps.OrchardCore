using CrestApps.OrchardCore.Learning.Lti.Core.Indexes;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Services;

/// <summary>
/// Stores external tools in YesSql, in the External Tools collection.
/// </summary>
public sealed class ExternalToolStore : DocumentCatalog<ExternalTool, ExternalToolIndex>, IExternalToolStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalToolStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public ExternalToolStore(ISession session)
        : base(session)
    {
        CollectionName = LtiConstants.CollectionName;
    }

    /// <inheritdoc />
    public async Task<ExternalTool> FindByClientIdAsync(string clientId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(clientId))
        {
            return null;
        }

        return await Session.Query<ExternalTool, ExternalToolIndex>(index => index.ClientId == clientId, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExternalTool>> ListAllAsync(CancellationToken cancellationToken = default)
        => (await Session.Query<ExternalTool, ExternalToolIndex>(collection: CollectionName)
            .OrderBy(index => index.Name)
            .ListAsync(cancellationToken)).ToArray();
}
