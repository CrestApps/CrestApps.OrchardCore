namespace CrestApps.OrchardCore.Learning.Lti.Core.Services;

/// <summary>
/// What became of a score a tool posted.
/// </summary>
public enum LtiScoreOutcome
{
    /// <summary>
    /// The score was stored.
    /// </summary>
    Stored,

    /// <summary>
    /// No member of the course has the <c>userId</c> for the tool.
    /// </summary>
    UnknownUser,

    /// <summary>
    /// The timestamp is not later than the stored score's, so the score was refused.
    /// </summary>
    NotLater,

    /// <summary>
    /// The line item's resource link is no longer an External tool item of this tool in the line item's course (it was
    /// moved to another tool or course), so the score was refused.
    /// </summary>
    LinkNotOfTool,
}
