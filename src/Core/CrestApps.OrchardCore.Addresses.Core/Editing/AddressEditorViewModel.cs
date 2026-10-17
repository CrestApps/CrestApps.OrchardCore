using CrestApps.OrchardCore.Addresses.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Addresses.Core.Editing;

/// <summary>
/// The address editor: what it posts, and what it needs to render. Bind it under the prefix the page gave the editor.
/// </summary>
/// <remarks>
/// Not sealed: Orchard Core renders a shape built from a sealed type empty.
/// </remarks>
public class AddressEditorViewModel
{
    public string Name { get; set; }

    public string Company { get; set; }

    public string AddressLine1 { get; set; }

    public string AddressLine2 { get; set; }

    public string DependentLocality { get; set; }

    public string City { get; set; }

    public string Region { get; set; }

    public string PostalCode { get; set; }

    public string SortingCode { get; set; }

    public string Country { get; set; }

    public string Phone { get; set; }

    [BindNever]
    public AddressEditorLayout Layout { get; set; }

    [BindNever]
    public IReadOnlyList<SelectListItem> Countries { get; set; } = [];

    [BindNever]
    public AddressEditorOptions Options { get; set; } = AddressEditorOptions.Default;

    /// <summary>
    /// Whether anything but the country was entered: an optional address left blank is no address.
    /// </summary>
    public bool HasAnyValue()
        => new[] { Name, Company, AddressLine1, AddressLine2, DependentLocality, City, Region, PostalCode, SortingCode, Phone }
            .Any(value => !string.IsNullOrWhiteSpace(value));

    /// <summary>
    /// Copies what the editor posts and needs to render onto <paramref name="target"/>.
    /// </summary>
    public void CopyTo(AddressEditorViewModel target)
    {
        ArgumentNullException.ThrowIfNull(target);

        target.Name = Name;
        target.Company = Company;
        target.AddressLine1 = AddressLine1;
        target.AddressLine2 = AddressLine2;
        target.DependentLocality = DependentLocality;
        target.City = City;
        target.Region = Region;
        target.PostalCode = PostalCode;
        target.SortingCode = SortingCode;
        target.Country = Country;
        target.Phone = Phone;
        target.Layout = Layout;
        target.Countries = Countries;
        target.Options = Options;
    }

    /// <summary>
    /// The address as entered, untidied.
    /// </summary>
    public Address ToAddress()
        => new()
        {
            Name = Name,
            Company = Company,
            AddressLine1 = AddressLine1,
            AddressLine2 = AddressLine2,
            DependentLocality = DependentLocality,
            City = City,
            Region = Region,
            PostalCode = PostalCode,
            SortingCode = SortingCode,
            Country = Country,
            Phone = Phone,
        };
}
