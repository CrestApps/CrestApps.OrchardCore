namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// What a definition change did.
/// </summary>
public enum SurveyChangeKind
{
    /// <summary>
    /// An item was added.
    /// </summary>
    Added,

    /// <summary>
    /// An item was removed.
    /// </summary>
    Removed,

    /// <summary>
    /// An item changed.
    /// </summary>
    Changed,

    /// <summary>
    /// An item moved.
    /// </summary>
    Moved,
}
