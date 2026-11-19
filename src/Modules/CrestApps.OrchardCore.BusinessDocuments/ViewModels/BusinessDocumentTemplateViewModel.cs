using CrestApps.OrchardCore.BusinessDocuments.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.BusinessDocuments.ViewModels;

/// <summary>
/// The document template editor.
/// </summary>
public class BusinessDocumentTemplateViewModel
{
    /// <summary>
    /// Gets or sets the name of the template.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets notes about the template.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the kinds of documents the template is for; none for every kind.
    /// </summary>
    public string[] Kinds { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the template is the default for its kinds.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets the address of the logo.
    /// </summary>
    public string LogoUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the documents use an accent color.
    /// </summary>
    public bool UseAccentColor { get; set; }

    /// <summary>
    /// Gets or sets the accent color, written <c>#RRGGBB</c>.
    /// </summary>
    public string AccentColor { get; set; }

    /// <summary>
    /// Gets or sets the paper size.
    /// </summary>
    public BusinessDocumentPaperSize PaperSize { get; set; }

    /// <summary>
    /// Gets or sets the text printed at the bottom of the documents.
    /// </summary>
    public string Footer { get; set; }

    /// <summary>
    /// Gets or sets the kinds of documents to choose from.
    /// </summary>
    [BindNever]
    public List<SelectListItem> KindOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the paper sizes to choose from.
    /// </summary>
    [BindNever]
    public List<SelectListItem> PaperSizeOptions { get; set; } = [];
}
