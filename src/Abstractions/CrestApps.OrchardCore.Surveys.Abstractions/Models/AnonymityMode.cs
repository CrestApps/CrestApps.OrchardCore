namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// Whether responses record who responded.
/// </summary>
public enum AnonymityMode
{
    /// <summary>
    /// Responses record the user or contact.
    /// </summary>
    Identified,

    /// <summary>
    /// Responses record nobody; limits use an unlinked pseudonymous key.
    /// </summary>
    Anonymous,
}
