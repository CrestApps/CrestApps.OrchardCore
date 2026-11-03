using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Core.Conversations;

namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// An alert rule: a metric of the AI's conversations measured over a window and compared with a threshold, which opens
/// an incident when crossed and resolves it when the metric is back.
/// </summary>
public sealed class AIAlertRule : CatalogItem, INameAwareModel
{
    /// <summary>
    /// The most incidents kept on a rule.
    /// </summary>
    public const int MaxIncidents = 20;

    /// <summary>
    /// Gets or sets the rule's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the rule is checked.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the metric.
    /// </summary>
    public AIConversationMetric Metric { get; set; } = AIConversationMetric.QAFailureRate;

    /// <summary>
    /// Gets or sets the profile measured, or <see langword="null"/> for every profile.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets how the value is compared with the threshold.
    /// </summary>
    public AIConversationAlertCondition Condition { get; set; }

    /// <summary>
    /// Gets or sets the threshold: a value, or a change in percent from the window before.
    /// </summary>
    public double Threshold { get; set; }

    /// <summary>
    /// Gets or sets the window measured, in minutes.
    /// </summary>
    public int WindowMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets the fewest samples a window needs to count.
    /// </summary>
    public int MinimumSampleSize { get; set; } = 10;

    /// <summary>
    /// Gets or sets how often the rule is checked, in minutes.
    /// </summary>
    public int CheckEveryMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets the addresses emailed when the rule triggers and resolves, comma separated.
    /// </summary>
    public string NotifyEmails { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an incident is open.
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// Gets or sets the value of the latest check.
    /// </summary>
    public double? LastValue { get; set; }

    /// <summary>
    /// Gets or sets the samples of the latest check.
    /// </summary>
    public int LastSampleSize { get; set; }

    /// <summary>
    /// Gets or sets when the rule was last checked.
    /// </summary>
    public DateTime? LastCheckedUtc { get; set; }

    /// <summary>
    /// Gets or sets the latest incidents, newest first.
    /// </summary>
    public List<AIAlertIncident> Incidents { get; set; } = [];

    /// <summary>
    /// Gets or sets when the rule was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// A time an alert rule's threshold was crossed.
/// </summary>
public sealed class AIAlertIncident
{
    /// <summary>
    /// Gets or sets when it opened.
    /// </summary>
    public DateTime OpenedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it resolved, or <see langword="null"/> while open.
    /// </summary>
    public DateTime? ResolvedUtc { get; set; }

    /// <summary>
    /// Gets or sets the value that opened it.
    /// </summary>
    public double OpenedValue { get; set; }

    /// <summary>
    /// Gets or sets the worst value seen while it was open.
    /// </summary>
    public double PeakValue { get; set; }
}
