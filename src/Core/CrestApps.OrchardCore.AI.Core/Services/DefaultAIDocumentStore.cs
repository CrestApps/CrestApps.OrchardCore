using CrestApps.Core.AI.Documents;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql.Indexes.Indexing;
using CrestApps.Core.Data.YesSql.Services;
using YesSql;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Represents the default AI document store.
/// </summary>
public sealed class DefaultAIDocumentStore : DocumentCatalog<AIDocument, AIDocumentIndex>, IAIDocumentStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAIDocumentStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public DefaultAIDocumentStore(ISession session)
    : base(session, AIConstants.AIDocsCollectionName)
    {
    }

    /// <summary>
    /// Retrieves the documents async.
    /// </summary>
    /// <param name="referenceId">The reference id.</param>
    /// <param name="referenceType">The reference type.</param>
    public async Task<IReadOnlyCollection<AIDocument>> GetDocumentsAsync(string referenceId, string referenceType)
    {
        ArgumentException.ThrowIfNullOrEmpty(referenceId);
        ArgumentException.ThrowIfNullOrEmpty(referenceType);

        return (await Session.Query<AIDocument, AIDocumentIndex>(
            x => x.ReferenceId == referenceId && x.ReferenceType == referenceType,
            CollectionName).ListAsync()).ToArray();
    }
}
