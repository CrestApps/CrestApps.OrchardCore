namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// When the upkeep's close step started on a site that had surveys before it existed. The step submits the responses
/// left in progress on a closed survey; on such a site it leaves alone the responses of the surveys that were already
/// closed then, so the first upkeep after the upgrade doesn't complete (and score, notify and grade) old responses that
/// the earlier version left in progress. Those are abandoned at their idle expiry, as before. A new site has none, and
/// the step submits every response it finds.
/// </summary>
public sealed class SurveyCloseStepStart
{
    /// <summary>
    /// Gets or sets the document id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets when the step started, in UTC.
    /// </summary>
    public DateTime StartedUtc { get; set; }
}
