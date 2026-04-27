using CrestApps.Core.AI.Documents;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql.Indexes.Indexing;
using CrestApps.Core.Data.YesSql.Services;
using YesSql;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Represents the default AI document chunk store.
/// </summary>
public sealed class DefaultAIDocumentChunkStore : DocumentCatalog<AIDocumentChunk, AIDocumentChunkIndex>, IAIDocumentChunkStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAIDocumentChunkStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public DefaultAIDocumentChunkStore(ISession session)
    : base(session, AIConstants.AIDocsCollectionName)
    {
    }

    /// <summary>
    /// Retrieves the chunks by AI document id async.
    /// </summary>
    /// <param name="documentId">The document id.</param>
    public async Task<IReadOnlyCollection<AIDocumentChunk>> GetChunksByAIDocumentIdAsync(string documentId)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentId);

        return (await Session.Query<AIDocumentChunk, AIDocumentChunkIndex>(
            x => x.AIDocumentId == documentId,
            CollectionName).ListAsync()).ToArray();
    }

    /// <summary>
    /// Retrieves the chunks by reference async.
    /// </summary>
    /// <param name="referenceId">The reference id.</param>
    /// <param name="referenceType">The reference type.</param>
    public async Task<IReadOnlyCollection<AIDocumentChunk>> GetChunksByReferenceAsync(string referenceId, string referenceType)
    {
        ArgumentException.ThrowIfNullOrEmpty(referenceId);
        ArgumentException.ThrowIfNullOrEmpty(referenceType);

        return (await Session.Query<AIDocumentChunk, AIDocumentChunkIndex>(
            x => x.ReferenceId == referenceId && x.ReferenceType == referenceType,
            CollectionName).ListAsync()).ToArray();
    }

    /// <summary>
    /// Removes the by document id async.
    /// </summary>
    /// <param name="documentId">The document id.</param>
    public async Task DeleteByDocumentIdAsync(string documentId)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentId);

        var chunks = await Session.Query<AIDocumentChunk, AIDocumentChunkIndex>(
            x => x.AIDocumentId == documentId,
            CollectionName).ListAsync();

        foreach (var chunk in chunks)
        {
            Session.Delete(chunk, CollectionName);
        }
    }
}
