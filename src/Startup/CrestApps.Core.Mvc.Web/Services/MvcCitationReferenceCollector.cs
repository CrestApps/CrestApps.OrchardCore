using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Infrastructure.Indexing;

namespace CrestApps.Core.Mvc.Web.Services;

/// <summary>
/// Collects citation references for the MVC host and resolves any configured
/// links before they are streamed to the chat client.
/// </summary>
public sealed class MvcCitationReferenceCollector
{
    private const string DataSourceReferencesKey = "DataSourceReferences";
    private const string DocumentReferencesKey = "DocumentReferences";

    private readonly CompositeAIReferenceLinkResolver _linkResolver;

    public MvcCitationReferenceCollector(CompositeAIReferenceLinkResolver linkResolver)
    {
        _linkResolver = linkResolver;
    }

    public void CollectPreemptiveReferences(
        OrchestrationContext orchestrationContext,
        Dictionary<string, AICompletionReference> references,
        HashSet<string> contentItemIds)
    {
        CollectFromProperties(orchestrationContext, DataSourceReferencesKey, references);
        CollectFromProperties(orchestrationContext, DocumentReferencesKey, references);
        ResolveLinks(references, contentItemIds);
    }

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
            if (string.IsNullOrEmpty(reference.Link) &&
                !string.IsNullOrEmpty(reference.ReferenceId) &&
                !string.IsNullOrEmpty(reference.ReferenceType))
            {
                reference.Link = _linkResolver.ResolveLink(
                    reference.ReferenceId,
                    reference.ReferenceType,
                    new Dictionary<string, object>
                    {
                        ["Title"] = reference.Title,
                    });
            }

            if (!string.IsNullOrEmpty(reference.ReferenceId) &&
                string.Equals(reference.ReferenceType, IndexProfileTypes.Articles, StringComparison.OrdinalIgnoreCase))
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
