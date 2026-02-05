using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

/// <summary>
/// Store for MCP resources with URI uniqueness enforcement.
/// </summary>
public sealed class McpResourceStore : SourceCatalog<McpResource>, IMcpResourceStore
{
    internal readonly IStringLocalizer S;

    public McpResourceStore(
        IDocumentManager<DictionaryDocument<McpResource>> documentManager,
        IStringLocalizer<McpResourceStore> stringLocalizer)
        : base(documentManager)
    {
        S = stringLocalizer;
    }

    public async ValueTask<McpResource> FindByUriAsync(string uri)
    {
        if (string.IsNullOrEmpty(uri))
        {
            return null;
        }

        var document = await DocumentManager.GetOrCreateImmutableAsync();

        var record = document.Records.Values.FirstOrDefault(x =>
            string.Equals(x.Resource?.Uri, uri, StringComparison.OrdinalIgnoreCase));

        return record != null ? Clone(record) : null;
    }

    protected override void Saving(McpResource record, DictionaryDocument<McpResource> document)
    {
        // Enforce URI uniqueness on save
        if (!string.IsNullOrEmpty(record.Resource?.Uri))
        {
            var duplicate = document.Records.Values.FirstOrDefault(x =>
                x.ItemId != record.ItemId &&
                string.Equals(x.Resource?.Uri, record.Resource.Uri, StringComparison.OrdinalIgnoreCase));

            if (duplicate != null)
            {
                throw new InvalidOperationException(S["A resource with the URI '{0}' already exists.", record.Resource.Uri]);
            }
        }

        base.Saving(record, document);
    }

    private static new McpResource Clone(McpResource record)
    {
        return record.Clone();
    }
}
