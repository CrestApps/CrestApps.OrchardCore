using CrestApps.OrchardCore.AI.Chat.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// Represents the view model for usage analytics index.
/// </summary>
public sealed class UsageAnalyticsIndexViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether is AI usage tracking enabled.
    /// </summary>
    public bool IsAIUsageTrackingEnabled { get; set; }

    /// <summary>
    /// Gets or sets the start date in local time.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the end date in local time.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the AI profile the report is limited to, or <see langword="null"/> for every profile.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets what the completion table is grouped by.
    /// </summary>
    public AICompletionUsageGroupBy GroupBy { get; set; }

    /// <summary>
    /// Gets or sets what the voice table is grouped by.
    /// </summary>
    public AIVoiceUsageGroupBy VoiceGroupBy { get; set; }

    /// <summary>
    /// Gets or sets the profiles the report can be limited to.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Profiles { get; set; } = [];

    /// <summary>
    /// Gets or sets the show report.
    /// </summary>
    public bool ShowReport { get; set; }

    /// <summary>
    /// Gets or sets the total calls.
    /// </summary>
    public int TotalCalls { get; set; }

    /// <summary>
    /// Gets or sets the total sessions.
    /// </summary>
    public int TotalSessions { get; set; }

    /// <summary>
    /// Gets or sets the total chat interactions.
    /// </summary>
    public int TotalChatInteractions { get; set; }

    /// <summary>
    /// Gets or sets the total tokens.
    /// </summary>
    public long TotalTokens { get; set; }

    /// <summary>
    /// Gets or sets the rows.
    /// </summary>
    public IReadOnlyList<AICompletionUsageSummaryViewModel> Rows { get; set; } = [];

    /// <summary>
    /// Gets or sets the voice usage of every automated call in the range.
    /// </summary>
    [BindNever]
    public AIVoiceUsageSummaryViewModel VoiceTotals { get; set; }

    /// <summary>
    /// Gets or sets the voice usage grouped by <see cref="VoiceGroupBy"/>.
    /// </summary>
    [BindNever]
    public IReadOnlyList<AIVoiceUsageSummaryViewModel> VoiceRows { get; set; } = [];
}
