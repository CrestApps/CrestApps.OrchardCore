using CrestApps.OrchardCore.Addresses.Core.Editing;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Commerce.StoreProfile.ViewModels;

/// <summary>
/// The Store settings page: who the store is, how to reach it and where it is.
/// </summary>
public class StoreProfileSettingsViewModel
{
    public string Name { get; set; }

    public string LogoUrl { get; set; }

    public string TaxId { get; set; }

    public string Email { get; set; }

    public string Phone { get; set; }

    /// <summary>
    /// Gets or sets the store's address as entered.
    /// </summary>
    public AddressEditorViewModel Address { get; set; } = new();

    /// <summary>
    /// Gets or sets the address editor shape, laid out for the address's country; not posted back.
    /// </summary>
    [BindNever]
    public IShape AddressEditor { get; set; }

    /// <summary>
    /// Gets or sets the site's name, shown when the store has no name of its own; not posted back.
    /// </summary>
    [BindNever]
    public string SiteName { get; set; }
}
