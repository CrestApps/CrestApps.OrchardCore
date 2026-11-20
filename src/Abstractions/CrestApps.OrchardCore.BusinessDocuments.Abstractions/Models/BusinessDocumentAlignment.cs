namespace CrestApps.OrchardCore.BusinessDocuments.Models;

/// <summary>
/// How the values of a column of a business document's line table are aligned.
/// </summary>
public enum BusinessDocumentAlignment
{
    /// <summary>
    /// At the start of the line, the left in a left-to-right language.
    /// </summary>
    Start,

    /// <summary>
    /// In the center.
    /// </summary>
    Center,

    /// <summary>
    /// At the end of the line, the right in a left-to-right language, as amounts are.
    /// </summary>
    End,
}
