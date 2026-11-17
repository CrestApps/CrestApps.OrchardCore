using CrestApps.OrchardCore.Addresses.Core.Editing;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Teams.ViewModels;

/// <summary>
/// The Address card of the team editor.
/// </summary>
public class TeamAddressViewModel
{
    /// <summary>
    /// Gets or sets the address fields.
    /// </summary>
    public AddressEditorViewModel Address { get; set; } = new();

    /// <summary>
    /// Gets or sets the address editor shape.
    /// </summary>
    [BindNever]
    public IShape AddressEditor { get; set; }
}
