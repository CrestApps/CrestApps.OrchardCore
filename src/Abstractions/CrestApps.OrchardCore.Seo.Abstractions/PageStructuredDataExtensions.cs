using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Seo;

/// <summary>
/// Adds JSON-LD written as text, such as in a Razor view or a Liquid template, to the page's structured data.
/// </summary>
public static class PageStructuredDataExtensions
{
    /// <summary>
    /// Adds JSON-LD to the page: one node, an array of nodes, or a document with a <c>@graph</c>. A node without
    /// <c>@id</c> describes the page: it becomes the page's <see cref="IPageStructuredData.MainEntity"/>, or adds its
    /// properties to it. A node with an <c>@id</c> is added to the page's graph.
    /// </summary>
    /// <param name="pageStructuredData">The page's structured data.</param>
    /// <param name="json">The JSON-LD.</param>
    /// <returns><see langword="false"/> when the text is not JSON-LD, in which case nothing is added.</returns>
    public static bool AddJsonLd(this IPageStructuredData pageStructuredData, string json)
    {
        ArgumentNullException.ThrowIfNull(pageStructuredData);

        if (!JsonLd.TryParseNodes(json, out var nodes))
        {
            return false;
        }

        foreach (var node in nodes)
        {
            if (JsonLd.GetString(node, StructuredDataConstants.Keywords.Id) is not null)
            {
                pageStructuredData.Add(node);

                continue;
            }

            node.Remove(StructuredDataConstants.Keywords.Context);

            if (pageStructuredData.MainEntity is null)
            {
                pageStructuredData.MainEntity = node;
            }
            else
            {
                JsonLd.Merge(pageStructuredData.MainEntity, node);
            }
        }

        return true;
    }
}
