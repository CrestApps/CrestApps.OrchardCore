using CrestApps.OrchardCore.BusinessDocuments.Models;

namespace CrestApps.OrchardCore.BusinessDocuments.ViewModels;

/// <summary>
/// The badges of a row of the document templates list.
/// </summary>
public class BusinessDocumentTemplateTagsViewModel
{
    /// <summary>
    /// Gets or sets the template.
    /// </summary>
    public BusinessDocumentTemplate Template { get; set; }

    /// <summary>
    /// Gets or sets the titles of the kinds the template is for; none for every kind.
    /// </summary>
    public List<string> KindTitles { get; set; } = [];
}
