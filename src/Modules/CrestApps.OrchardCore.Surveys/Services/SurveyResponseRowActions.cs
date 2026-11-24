namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Carries, for the responses list of one request, the responses the user may void and delete, so a row shows only the
/// actions the user can use. The list fills it before the rows render; a row rendered without it shows neither action.
/// </summary>
public sealed class SurveyResponseRowActions
{
    /// <summary>
    /// Gets the ids of the responses the user may void and delete.
    /// </summary>
    public HashSet<string> RemovableIds { get; } = new(StringComparer.Ordinal);
}
