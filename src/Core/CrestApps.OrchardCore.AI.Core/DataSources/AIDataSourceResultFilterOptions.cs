namespace CrestApps.OrchardCore.AI.Core.DataSources;

/// <summary>
/// How a data source search asks for more chunks when result filters are registered, so that a trimmed list still
/// holds as many chunks as the caller asked for.
/// </summary>
public sealed class AIDataSourceResultFilterOptions
{
    /// <summary>
    /// Gets or sets how many times more chunks than asked for the index is asked for. The default is 4.
    /// </summary>
    public int OverfetchFactor { get; set; } = 4;

    /// <summary>
    /// Gets or sets the most chunks the index is asked for in one search. The default is 200.
    /// </summary>
    public int MaxCandidates { get; set; } = 200;

    /// <summary>
    /// Gets how many chunks to ask the index for when the caller wants a number of them.
    /// </summary>
    /// <param name="topN">The number of chunks the caller wants.</param>
    /// <returns>The number of chunks to ask for, never fewer than <paramref name="topN"/>.</returns>
    public int GetCandidateCount(int topN)
    {
        if (topN <= 0)
        {
            return topN;
        }

        var wanted = (long)topN * Math.Max(1, OverfetchFactor);
        var cap = Math.Max(topN, MaxCandidates);

        return (int)Math.Min(wanted, cap);
    }
}
