using CrestApps.OrchardCore.Learning.Lti.Models;

namespace CrestApps.OrchardCore.Learning.Lti.Services;

/// <summary>
/// A score a tool posted, as it was stored.
/// </summary>
public sealed class LtiScoreReceivedContext
{
    /// <summary>
    /// Gets or sets the tool that posted the score.
    /// </summary>
    public ExternalTool Tool { get; set; }

    /// <summary>
    /// Gets or sets the line item.
    /// </summary>
    public LtiLineItem LineItem { get; set; }

    /// <summary>
    /// Gets or sets the score.
    /// </summary>
    public LtiScore Score { get; set; }

    /// <summary>
    /// Gets or sets the score as a percentage of its maximum, or <see langword="null"/> when no score was given.
    /// </summary>
    public decimal? Percent { get; set; }

    /// <summary>
    /// Gets or sets whether the score reached the learner's progress (a fully graded score on a line item bound to a
    /// course item).
    /// </summary>
    public bool RecordedInProgress { get; set; }
}
