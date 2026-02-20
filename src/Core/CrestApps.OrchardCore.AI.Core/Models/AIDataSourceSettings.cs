namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// Global site settings for AI Data Source behavior.
/// Configures default values for Preemptive RAG and retrieval parameters.
/// </summary>
public sealed class AIDataSourceSettings
{
    public const int MinStrictness = 1;

    public const int MaxStrictness = 5;

    public const int MinTopNDocuments = 3;

    public const int MaxTopNDocuments = 20;

    /// <summary>
    /// Gets or sets whether Preemptive RAG is enabled by default for data source profiles.
    /// When true, relevant context from data sources is injected into the system message
    /// before the LLM call. Individual profiles can override this setting.
    /// </summary>
    public bool EnablePreemptiveRag { get; set; } = true;

    /// <summary>
    /// Gets or sets the default strictness threshold (1-5) for data source retrieval.
    /// Used when a profile does not specify its own Strictness value.
    /// </summary>
    public int DefaultStrictness { get; set; } = 3;

    /// <summary>
    /// Gets or sets the default number of top documents to retrieve (3-20).
    /// Used when a profile does not specify its own TopNDocuments value.
    /// </summary>
    public int DefaultTopNDocuments { get; set; } = 5;

    public int GetTopNDocuments(int? topN)
    {
        if (topN >= MinTopNDocuments && topN <= MaxTopNDocuments)
        {
            return topN.Value;
        }

        if (DefaultTopNDocuments >= MinTopNDocuments && DefaultTopNDocuments <= MaxTopNDocuments)
        {
            return DefaultTopNDocuments;
        }

        return 5;
    }

    public int GetStrictness(int? strictness)
    {
        if (strictness >= MinStrictness && strictness <= MaxStrictness)
        {
            return strictness.Value;
        }

        if (DefaultStrictness >= MinStrictness && DefaultStrictness <= MaxStrictness)
        {
            return DefaultStrictness;
        }

        return 3;
    }
}
