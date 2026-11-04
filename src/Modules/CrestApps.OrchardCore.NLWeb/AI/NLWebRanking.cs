namespace CrestApps.OrchardCore.NLWeb.AI;

/// <summary>
/// The model's scores of the results.
/// </summary>
public sealed class NLWebRanking
{
    /// <summary>
    /// Gets or sets the score of each result.
    /// </summary>
    public List<NLWebRankedItem> Items { get; set; } = [];
}
