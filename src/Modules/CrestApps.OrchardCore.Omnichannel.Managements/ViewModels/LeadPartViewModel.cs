using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the editor of a lead's state.
/// </summary>
public class LeadPartViewModel
{
    /// <summary>
    /// Gets or sets the lead status identifier.
    /// </summary>
    public string StatusId { get; set; }

    /// <summary>
    /// Gets or sets the company.
    /// </summary>
    public string Company { get; set; }

    /// <summary>
    /// Gets or sets the lead source.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the list the lead arrived in.
    /// </summary>
    public string ListName { get; set; }

    /// <summary>
    /// Gets or sets the rating.
    /// </summary>
    public string Rating { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who owns the lead.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the statuses the editor can choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Statuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the ratings the editor can choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Ratings { get; set; } = [];

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
