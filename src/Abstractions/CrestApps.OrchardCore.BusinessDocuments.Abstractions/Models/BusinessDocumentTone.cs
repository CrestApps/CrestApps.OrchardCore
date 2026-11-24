namespace CrestApps.OrchardCore.BusinessDocuments.Models;

/// <summary>
/// The color of the status badge of a business document.
/// </summary>
public enum BusinessDocumentTone
{
    /// <summary>
    /// Gray, for a status that needs nothing, such as a draft.
    /// </summary>
    Neutral,

    /// <summary>
    /// Green, for a good outcome, such as paid.
    /// </summary>
    Success,

    /// <summary>
    /// Amber, for a status that needs attention, such as due.
    /// </summary>
    Warning,

    /// <summary>
    /// Red, for a bad outcome, such as failed or overdue.
    /// </summary>
    Danger,

    /// <summary>
    /// Blue, for information, such as sent.
    /// </summary>
    Info,
}
