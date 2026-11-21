namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// What retention does with old responses.
/// </summary>
public enum RetentionAction
{
    /// <summary>
    /// Deletes them.
    /// </summary>
    Delete,

    /// <summary>
    /// Removes who responded and keeps the answers.
    /// </summary>
    Anonymize,
}
