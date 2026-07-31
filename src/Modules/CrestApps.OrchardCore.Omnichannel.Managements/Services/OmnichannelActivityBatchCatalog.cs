using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// A YesSql-backed catalog for <see cref="OmnichannelActivityBatch"/> that returns paged results ordered by newest first.
/// </summary>
public sealed class OmnichannelActivityBatchCatalog : DocumentCatalog<OmnichannelActivityBatch, OmnichannelActivityBatchIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelActivityBatchCatalog"/> class.
    /// </summary>
    /// <param name="session">The YesSql session used for database access.</param>
    public OmnichannelActivityBatchCatalog(ISession session)
        : base(session)
    {
        CollectionName = OmnichannelConstants.CollectionName;
    }

    /// <inheritdoc />
    protected override ValueTask PagingAsync<TQuery>(IQuery<OmnichannelActivityBatch> query, TQuery context)
    {
        if (query is IQuery<OmnichannelActivityBatch, OmnichannelActivityBatchIndex> indexedQuery)
        {
            indexedQuery.OrderByDescending(index => index.CreatedUtc);
        }

        return ValueTask.CompletedTask;
    }
}
