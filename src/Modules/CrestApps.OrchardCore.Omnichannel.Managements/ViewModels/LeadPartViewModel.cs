using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the editor of a lead's status and the summary of the lead.
/// </summary>
public class LeadPartViewModel
{
    /// <summary>
    /// Gets or sets the lead status identifier.
    /// </summary>
    public string StatusId { get; set; }

    /// <summary>
    /// Gets or sets the statuses the editor can choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Statuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the company, for the summary.
    /// </summary>
    [BindNever]
    public string Company { get; set; }

    /// <summary>
    /// Gets or sets the name of the lead source, for the summary.
    /// </summary>
    [BindNever]
    public string SourceName { get; set; }

    /// <summary>
    /// Gets or sets the list the lead arrived in, for the summary.
    /// </summary>
    [BindNever]
    public string ListName { get; set; }

    /// <summary>
    /// Gets or sets the rating, for the summary.
    /// </summary>
    [BindNever]
    public string Rating { get; set; }

    /// <summary>
    /// Gets or sets whether the lead was converted, which makes the editor read-only.
    /// </summary>
    [BindNever]
    public bool IsConverted { get; set; }

    /// <summary>
    /// Gets or sets the lead content item.
    /// </summary>
    [BindNever]
    public ContentItem ContentItem { get; set; }

    /// <summary>
    /// Gets or sets the contact the lead was converted into, when it still exists.
    /// </summary>
    [BindNever]
    public ContentItem ConvertedContact { get; set; }

    /// <summary>
    /// Gets or sets the lead part.
    /// </summary>
    [BindNever]
    public Core.Models.LeadPart Part { get; set; }
}
