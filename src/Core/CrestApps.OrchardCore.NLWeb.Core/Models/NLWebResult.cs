using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.NLWeb.Models;

/// <summary>
/// One result of an NLWeb answer: a schema.org object and what the site knows about how it matches the query.
/// </summary>
public sealed class NLWebResult
{
    /// <summary>
    /// Gets or sets the identifier of the item on the site, such as a content item identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the schema.org object of the item.
    /// </summary>
    public JsonObject Item { get; set; }

    /// <summary>
    /// Gets or sets the URL of the item.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets the name of the item.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets how well the item matches the query, when known. Higher is better.
    /// </summary>
    public double? Score { get; set; }

    /// <summary>
    /// Gets or sets a short explanation of why the item matches the query, when a ranker wrote one.
    /// </summary>
    public string Relevance { get; set; }
}
