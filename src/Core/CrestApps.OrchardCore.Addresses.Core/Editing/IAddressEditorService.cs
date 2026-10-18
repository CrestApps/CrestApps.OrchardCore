using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Addresses.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Addresses.Core.Editing;

/// <summary>
/// Builds the address editor and checks what it posts, so every page that asks for an address asks the same way.
/// </summary>
/// <remarks>
/// A page renders the editor as the <c>AddressEditor</c> shape with the prefix it binds the posted
/// <see cref="AddressEditorViewModel"/> under.
/// </remarks>
public interface IAddressEditorService
{
    /// <summary>
    /// The editor for <paramref name="address"/>, or for a new address when it is <see langword="null"/>.
    /// </summary>
    Task<AddressEditorViewModel> BuildAsync(Address address, AddressEditorOptions options = null);

    /// <summary>
    /// Fills in what a posted editor needs to render again, laid out for the country it posted.
    /// </summary>
    Task PopulateAsync(AddressEditorViewModel model, AddressEditorOptions options = null);

    /// <summary>
    /// Checks a posted editor against its country's rules and the page's options. Each problem is added to
    /// <paramref name="modelState"/> under <paramref name="prefix"/> and the field's name, in the visitor's language.
    /// </summary>
    Task<AddressValidationResult> ValidateAsync(AddressEditorViewModel model, ModelStateDictionary modelState, string prefix, AddressEditorOptions options = null);

    /// <summary>
    /// How the editor lays out the country with the code <paramref name="countryCode"/>.
    /// </summary>
    Task<AddressEditorLayout> GetLayoutAsync(string countryCode);
}
