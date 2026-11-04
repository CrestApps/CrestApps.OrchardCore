using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.NLWeb.Models;

/// <summary>
/// The query of an NLWeb request.
/// </summary>
public sealed class NLWebQuery
{
    /// <summary>
    /// Gets or sets the natural language query.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the part of the site to search (a sub-corpus), or <see langword="null"/> for the default.
    /// </summary>
    public string Site { get; set; }

    /// <summary>
    /// Gets or sets the schema.org type of the items requested, such as <c>Recipe</c>.
    /// </summary>
    public string ItemType { get; set; }

    /// <summary>
    /// Gets or sets a query that already includes the conversation's context, so the site does not need to rewrite
    /// the query.
    /// </summary>
    public string DecontextualizedText { get; set; }

    /// <summary>
    /// Gets or sets the other filters of the query, such as <c>location</c> or <c>price</c>, as the caller sent them.
    /// </summary>
    public JsonObject Filters { get; set; } = [];
}
