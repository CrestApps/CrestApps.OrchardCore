namespace CrestApps.OrchardCore.AI.Chat.Reports;

/// <summary>
/// The names of the AI chat report filters, which a report lists in <c>IReportFilterMetadata.FilterNames</c>, and
/// the keys their values are stored under in the report filter.
/// </summary>
public static class AIChatReportFilters
{
    /// <summary>
    /// The filter that narrows a report to one chat AI profile, or leaves it on every profile.
    /// </summary>
    public const string Profile = "AIChatProfile";

    /// <summary>
    /// The filter that narrows a report to one chat AI profile and shows nothing until one is chosen.
    /// </summary>
    public const string RequiredProfile = "AIChatRequiredProfile";

    /// <summary>
    /// The filter that chooses how each table of the AI usage report is grouped.
    /// </summary>
    public const string UsageGrouping = "AIUsageGrouping";

    /// <summary>
    /// The key of the chosen AI profile identifier.
    /// </summary>
    public const string ProfileId = "AIChatProfileId";

    /// <summary>
    /// The key of the grouping of the chat completions table.
    /// </summary>
    public const string CompletionGroupBy = "AICompletionGroupBy";

    /// <summary>
    /// The key of the grouping of the voice calls table.
    /// </summary>
    public const string VoiceGroupBy = "AIVoiceGroupBy";

    /// <summary>
    /// The key of the grouping of the metered usage table.
    /// </summary>
    public const string MeteringGroupBy = "AIMeteringGroupBy";

    /// <summary>
    /// The key of the grouping of the audio usage table.
    /// </summary>
    public const string AudioGroupBy = "AIAudioGroupBy";
}
