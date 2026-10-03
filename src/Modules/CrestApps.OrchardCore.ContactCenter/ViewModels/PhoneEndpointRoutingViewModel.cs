using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Edits which entry point answers calls to a phone number.
/// </summary>
public class PhoneEndpointRoutingViewModel
{
    /// <summary>
    /// Gets or sets the identifier of the entry point that answers calls to the number.
    /// </summary>
    public string EntryPointId { get; set; }

    /// <summary>
    /// Gets or sets the entry points to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> EntryPoints { get; set; } = [];

    /// <summary>
    /// Gets or sets the names of the entry points that list the number among their dialed numbers.
    /// </summary>
    [BindNever]
    public IList<string> ListedOnEntryPoints { get; set; } = [];
}
