using CrestApps.Core.Data.YesSql.Services;
using YesSql;

namespace CrestApps.OrchardCore.YesSql.Core.Services;

/// <summary>
/// YesSql-backed implementation of <see cref="CrestApps.Core.Services.ICatalog{T}"/> that stores
/// catalog entries as individual YesSql documents with a corresponding index.
/// </summary>
/// <remarks>
/// The implementation lives on the suite's <see cref="ConcurrentDocumentCatalog{T, TIndex}"/>. This type keeps
/// its own name and shape because it is part of the published
/// <c>CrestApps.OrchardCore.YesSql.Core</c> surface that consumers outside this repository derive
/// from.
/// </remarks>
/// <typeparam name="T">The type of catalog item managed by this catalog.</typeparam>
/// <typeparam name="TIndex">The YesSql index type used to query catalog items.</typeparam>
public class DocumentCatalog<T, TIndex> : ConcurrentDocumentCatalog<T, TIndex>
    where T : CrestApps.Core.Models.CatalogItem
    where TIndex : CrestApps.Core.Data.YesSql.Indexes.CatalogItemIndex
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentCatalog{T, TIndex}"/> class.
    /// </summary>
    /// <param name="session">The YesSql session for database access.</param>
    public DocumentCatalog(ISession session)
        : base(session)
    {
    }

    internal DocumentCatalog(ISession session, string collectionName)
        : base(session, collectionName)
    {
    }
}
