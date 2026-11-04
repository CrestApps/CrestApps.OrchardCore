namespace CrestApps.OrchardCore.NLWeb.Models;

/// <summary>
/// The site settings of the NLWeb feature.
/// </summary>
public sealed class NLWebSettings
{
    /// <summary>
    /// Gets or sets the names of the search indexes questions are answered from. The first one is searched by default,
    /// and a caller can pick another one with the query's <c>site</c>.
    /// </summary>
    public string[] IndexProfileNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the content types the answers can include. Empty means every content type.
    /// </summary>
    public string[] ContentTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the most results an answer has.
    /// </summary>
    public int MaxResults { get; set; } = 10;

    /// <summary>
    /// Gets or sets the response mode used when the caller does not ask for one.
    /// </summary>
    public string DefaultMode { get; set; } = NLWebProtocol.Modes.List;

    /// <summary>
    /// Gets or sets a value indicating whether the endpoints also accept the original query-string protocol
    /// (<c>GET /ask?query=...</c>).
    /// </summary>
    public bool AllowLegacyRequests { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether pages and agents on other sites can call the endpoints from a browser.
    /// </summary>
    public bool AllowCrossOrigin { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether an AI model rewrites a follow-up question with the conversation's
    /// context before the site searches for it.
    /// </summary>
    public bool RewriteQueries { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether an AI model orders the results by how well they answer the question and
    /// drops the ones that do not.
    /// </summary>
    public bool RankResults { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether callers can ask for a summary of the results.
    /// </summary>
    public bool AllowSummarize { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether callers can ask for an answer written from the results.
    /// </summary>
    public bool AllowGenerate { get; set; } = true;

    /// <summary>
    /// Gets or sets the AI deployment used to rewrite, rank, summarize and answer. Empty uses the default utility
    /// deployment.
    /// </summary>
    public string DeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the AI data source whose vector index is also searched, or empty for none.
    /// </summary>
    public string DataSourceId { get; set; }
}
