using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the lead filters of an inventory load.
/// </summary>
public class LeadBatchFilterViewModel
{
    /// <summary>
    /// Gets or sets whether the filters were on the submitted form.
    /// </summary>
    public bool Rendered { get; set; }

    /// <summary>
    /// Gets or sets the statuses to load.
    /// </summary>
    public string[] StatusIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the identifier of the file import whose leads are loaded.
    /// </summary>
    public string ImportEntryId { get; set; }

    /// <summary>
    /// Gets or sets the content item identifier of the lead source to load.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets the owner whose leads are loaded.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the ratings to load.
    /// </summary>
    public string[] SelectedRatings { get; set; } = [];

    /// <summary>
    /// Gets or sets whether leads whose number belongs to a contact are skipped.
    /// </summary>
    public bool SkipLeadsThatAreContacts { get; set; } = true;

    /// <summary>
    /// Gets or sets the statuses to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Statuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the file imports to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Imports { get; set; } = [];

    /// <summary>
    /// Gets or sets the lead sources to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Sources { get; set; } = [];

    /// <summary>
    /// Gets or sets the ratings to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Ratings { get; set; } = [];

    /// <summary>
    /// Gets or sets the lead types, which decide when the filters show.
    /// </summary>
    [BindNever]
    public string[] LeadContentTypes { get; set; } = [];
}
