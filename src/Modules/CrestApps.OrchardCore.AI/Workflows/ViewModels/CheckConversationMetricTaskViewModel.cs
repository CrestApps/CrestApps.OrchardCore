using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Workflows.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Workflows.ViewModels;

/// <summary>
/// Edits the Check AI Conversation Metric task.
/// </summary>
public class CheckConversationMetricTaskViewModel
{
    /// <summary>
    /// Gets or sets the metric to measure.
    /// </summary>
    public AIConversationMetric Metric { get; set; }

    /// <summary>
    /// Gets or sets the AI profile to measure, or empty for all of them.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the length of the window, in minutes.
    /// </summary>
    public int WindowMinutes { get; set; }

    /// <summary>
    /// Gets or sets how the value is compared with the threshold.
    /// </summary>
    public AIConversationAlertCondition Condition { get; set; }

    /// <summary>
    /// Gets or sets the threshold.
    /// </summary>
    public double Threshold { get; set; }

    /// <summary>
    /// Gets or sets the fewest samples that can alert.
    /// </summary>
    public int MinimumSampleSize { get; set; }

    /// <summary>
    /// Gets or sets the metrics to choose from.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Metrics { get; set; } = [];

    /// <summary>
    /// Gets or sets the conditions to choose from.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Conditions { get; set; } = [];

    /// <summary>
    /// Gets or sets the AI profiles to choose from.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Profiles { get; set; } = [];
}
