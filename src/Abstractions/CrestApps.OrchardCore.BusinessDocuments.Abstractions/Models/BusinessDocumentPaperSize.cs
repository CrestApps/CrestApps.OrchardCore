namespace CrestApps.OrchardCore.BusinessDocuments.Models;

/// <summary>
/// The paper size a business document is printed on.
/// </summary>
public enum BusinessDocumentPaperSize
{
    /// <summary>
    /// Not chosen: a template's paper size applies, and a renderer prints on <see cref="Letter"/> when none does.
    /// </summary>
    Default,

    /// <summary>
    /// US Letter, 8.5 by 11 inches.
    /// </summary>
    Letter,

    /// <summary>
    /// ISO A4, 210 by 297 millimeters.
    /// </summary>
    A4,
}
