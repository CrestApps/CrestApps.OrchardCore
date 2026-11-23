using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// The surveys an access provider grants an operation on, listed at once: every survey of the owners, and the surveys
/// named by id.
/// </summary>
public sealed class SurveyAccessGrants
{
    /// <summary>
    /// Gets a list that grants nothing.
    /// </summary>
    public static SurveyAccessGrants None => new();

    /// <summary>
    /// Gets the owners (such as course items) whose surveys are granted.
    /// </summary>
    public IReadOnlyCollection<RecordRef> Owners { get; init; } = [];

    /// <summary>
    /// Gets the ids of other granted surveys.
    /// </summary>
    public IReadOnlyCollection<string> SurveyIds { get; init; } = [];
}
