using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.AI.Chat.Models;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// How the tables of the AI usage report are grouped.
/// </summary>
public class AIUsageGroupingReportFilterViewModel
{
    /// <summary>
    /// Gets or sets the grouping of the metered usage table.
    /// </summary>
    public AIUsageReportGrouping MeteringGroupBy { get; set; }

    /// <summary>
    /// Gets or sets the grouping of the audio usage table.
    /// </summary>
    public AIAudioUsageReportGrouping AudioGroupBy { get; set; }

    /// <summary>
    /// Gets or sets the grouping of the chat completions table.
    /// </summary>
    public AICompletionUsageGroupBy GroupBy { get; set; }

    /// <summary>
    /// Gets or sets the grouping of the voice calls table.
    /// </summary>
    public AIVoiceUsageGroupBy VoiceGroupBy { get; set; }
}
