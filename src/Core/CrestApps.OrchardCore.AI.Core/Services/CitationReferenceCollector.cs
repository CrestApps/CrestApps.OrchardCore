using CrestApps.OrchardCore.AI.Models;
using static CrestApps.OrchardCore.AI.Core.AIConstants;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Collects citation references from orchestration context properties and the
/// <see cref="AIInvocationScope"/>, resolves links via
/// <see cref="CompositeAIReferenceLinkResolver"/>, and merges into a final
/// references dictionary and content-item-ID set.
/// </summary>
public sealed class CitationReferenceCollector
{
    private const string DataSourceReferencesKey = "DataSourceReferences";
    private const string DocumentReferencesKey = "DocumentReferences";

    private readonly CompositeAIReferenceLinkResolver _linkResolver;

    public CitationReferenceCollector(CompositeAIReferenceLinkResolver linkResolver)
    {
        _linkResolver = linkResolver;
    }

    /// <summary>
    /// Collects preemptive RAG references that are known before the streaming loop starts.
    /// Call this immediately after building the orchestration context, before entering the
    /// <c>await foreach</c> streaming loop, so the first chunk sent to the client already
    /// contains these references.
    /// </summary>
    /// <param name="orchestrationContext">The orchestration context containing preemptive RAG reference data.</param>
    /// <param name="references">The target dictionary to merge resolved references into.</param>
    /// <param name="contentItemIds">The target set to add content item IDs into.</param>
    public void CollectPreemptiveReferences(
        OrchestrationContext orchestrationContext,
        Dictionary<string, AICompletionReference> references,
        HashSet<string> contentItemIds)
    {
        // Collect from preemptive RAG handlers (data sources).
        CollectFromProperties(orchestrationContext, DataSourceReferencesKey, references);

        // Collect from preemptive RAG handlers (documents).
        CollectFromProperties(orchestrationContext, DocumentReferencesKey, references);

        // Resolve links for the collected references.
        ResolveLinks(references, contentItemIds);
    }

    /// <summary>
    /// Collects any new tool references added during streaming (e.g., from
    /// <c>DataSourceSearchTool</c> or <c>SearchDocumentsTool</c> invoked by the AI model).
    /// Call this inside the streaming loop on each chunk to progressively deliver newly
    /// discovered references to the client.
    /// </summary>
    /// <param name="references">The target dictionary to merge new tool references into.</param>
    /// <param name="contentItemIds">The target set to add content item IDs into.</param>
    /// <returns><c>true</c> if any new references were added; <c>false</c> otherwise.</returns>
    public bool CollectToolReferences(
        Dictionary<string, AICompletionReference> references,
        HashSet<string> contentItemIds)
    {
        var invocationContext = AIInvocationScope.Current;

        if (invocationContext is null)
        {
            return false;
        }

        var added = false;

        foreach (var (key, value) in invocationContext.ToolReferences)
        {
            if (references.TryAdd(key, value))
            {
                added = true;
            }
        }

        if (added)
        {
            ResolveLinks(references, contentItemIds);
        }

        return added;
    }

    private void ResolveLinks(Dictionary<string, AICompletionReference> references, HashSet<string> contentItemIds)
    {
        foreach (var (_, reference) in references)
        {
            if (string.IsNullOrEmpty(reference.Link) && !string.IsNullOrEmpty(reference.ReferenceId))
            {
                reference.Link = _linkResolver.ResolveLink(
                    reference.ReferenceId,
                    reference.ReferenceType,
                    new Dictionary<string, object>
                    {
                        ["Title"] = reference.Title,
                    });
            }

            if (!string.IsNullOrEmpty(reference.ReferenceId) && reference.ReferenceType == DataSourceReferenceTypes.Content)
            {
                contentItemIds.Add(reference.ReferenceId);
            }
        }
    }

    private static void CollectFromProperties(
        OrchestrationContext orchestrationContext,
        string propertyKey,
        Dictionary<string, AICompletionReference> target)
    {
        if (orchestrationContext.Properties.TryGetValue(propertyKey, out var refsObj) &&
            refsObj is Dictionary<string, AICompletionReference> refs)
        {
            foreach (var (key, value) in refs)
            {
                target.TryAdd(key, value);
            }
        }
    }
}
