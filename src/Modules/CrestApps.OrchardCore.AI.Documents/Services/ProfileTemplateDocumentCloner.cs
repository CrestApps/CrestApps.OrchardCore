using CrestApps.Core;
using CrestApps.Core.AI.Documents;
using CrestApps.Core.AI.Documents.Models;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;

namespace CrestApps.OrchardCore.AI.Documents.Services;

/// <summary>
/// Gives a profile built from a template its own copy of every document the template carries.
/// </summary>
/// <remarks>
/// A template's documents belong to the template. A profile that pointed at them would lose its knowledge
/// the moment the template's documents were edited or removed, so each document is copied, chunks and
/// embeddings included, and the copies are scoped to the profile.
/// </remarks>
internal sealed class ProfileTemplateDocumentCloner
{
    private readonly IAIDocumentStore _documentStore;
    private readonly IAIDocumentChunkStore _chunkStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileTemplateDocumentCloner"/> class.
    /// </summary>
    /// <param name="documentStore">The document store.</param>
    /// <param name="chunkStore">The document chunk store.</param>
    public ProfileTemplateDocumentCloner(
        IAIDocumentStore documentStore,
        IAIDocumentChunkStore chunkStore)
    {
        _documentStore = documentStore;
        _chunkStore = chunkStore;
    }

    /// <summary>
    /// Copies the template's documents to the profile and records the copies on
    /// <paramref name="documentsMetadata"/>.
    /// </summary>
    /// <param name="profile">The profile receiving the copies. It must already have its identifier.</param>
    /// <param name="template">The template whose documents are copied.</param>
    /// <param name="documentsMetadata">The profile's documents metadata, which the copies are added to.</param>
    /// <param name="excludedDocumentIds">The template documents not to copy, for example those removed in the editor.</param>
    /// <returns>The copied documents.</returns>
    /// <remarks>
    /// The copies' chunks are scheduled for indexing once the request completes. Without that the copies sit
    /// in the store where retrieval never looks, and the profile answers as if it had no documents.
    /// </remarks>
    public async Task<IReadOnlyList<AIDocument>> CloneAsync(
        AIProfile profile,
        AIProfileTemplate template,
        DocumentsMetadata documentsMetadata,
        IEnumerable<string> excludedDocumentIds = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(documentsMetadata);

        var templateDocumentsMetadata = template.GetOrCreate<DocumentsMetadata>();

        if (templateDocumentsMetadata.Documents == null || templateDocumentsMetadata.Documents.Count == 0)
        {
            return [];
        }

        var excluded = excludedDocumentIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal) ?? [];

        documentsMetadata.Documents ??= [];

        var clonedDocuments = new List<AIDocument>();

        foreach (var docInfo in templateDocumentsMetadata.Documents)
        {
            if (excluded.Contains(docInfo.DocumentId))
            {
                continue;
            }

            var templateDocument = await _documentStore.FindByIdAsync(docInfo.DocumentId);

            if (templateDocument == null)
            {
                continue;
            }

            var clonedDocument = new AIDocument
            {
                ItemId = UniqueId.GenerateId(),
                ReferenceId = profile.ItemId,
                ReferenceType = AIConstants.DocumentReferenceTypes.Profile,
                FileName = templateDocument.FileName,
                ContentType = templateDocument.ContentType,
                FileSize = templateDocument.FileSize,
                UploadedUtc = templateDocument.UploadedUtc,
            };

            await _documentStore.CreateAsync(clonedDocument);

            var templateChunks = await _chunkStore.GetChunksByAIDocumentIdAsync(templateDocument.ItemId);

            foreach (var templateChunk in templateChunks)
            {
                await _chunkStore.CreateAsync(new AIDocumentChunk
                {
                    ItemId = UniqueId.GenerateId(),
                    AIDocumentId = clonedDocument.ItemId,
                    ReferenceId = profile.ItemId,
                    ReferenceType = AIConstants.DocumentReferenceTypes.Profile,
                    Content = templateChunk.Content,
                    Embedding = templateChunk.Embedding,
                    Index = templateChunk.Index,
                });
            }

            documentsMetadata.Documents.Add(new ChatDocumentInfo
            {
                DocumentId = clonedDocument.ItemId,
                FileName = clonedDocument.FileName,
                ContentType = clonedDocument.ContentType,
                FileSize = clonedDocument.FileSize,
            });

            clonedDocuments.Add(clonedDocument);
        }

        ProfileDocumentChunkIndexer.ScheduleIndexing(clonedDocuments);

        return clonedDocuments;
    }
}
