using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Addresses.Models;
using OrchardCore;

namespace CrestApps.OrchardCore.Accounts.Services;

/// <summary>
/// The rules of an account's locations, as the locations editor applies them: a location is addressed to the account
/// itself (no recipient, the account's name as the company), its address is checked by the shared address editor with a
/// new location starting in the country of the first one, it keeps its id when it changes, and a location with no name
/// is named after its city. The locations editor and the AI tools both call it.
/// </summary>
internal static class AccountLocationRules
{
    /// <summary>
    /// The address editor options of an account's locations.
    /// </summary>
    /// <param name="countries">The countries of the account's locations, in order.</param>
    /// <returns>The options.</returns>
    public static AddressEditorOptions EditorOptions(IEnumerable<string> countries)
        => new()
        {
            ShowName = false,
            ShowCompany = false,
            DefaultCountry = countries?.FirstOrDefault(country => !string.IsNullOrWhiteSpace(country)),
        };

    /// <summary>
    /// Addresses a location's address to the account, before it is checked.
    /// </summary>
    /// <param name="address">The address as entered.</param>
    /// <param name="accountName">The account's display text.</param>
    public static void AddressToAccount(AddressEditorViewModel address, string accountName)
    {
        ArgumentNullException.ThrowIfNull(address);

        address.Name = null;
        address.Company = accountName?.Trim();
    }

    /// <summary>
    /// Builds a location from its checked address.
    /// </summary>
    /// <param name="locationId">The location's id, or <see langword="null"/> for a new location.</param>
    /// <param name="name">The location's name, or <see langword="null"/> to name it after its city.</param>
    /// <param name="address">The address as the address check tidied it.</param>
    /// <returns>The location.</returns>
    public static AccountLocation CreateLocation(string locationId, string name, Address address)
        => new()
        {
            LocationId = string.IsNullOrEmpty(locationId) ? IdGenerator.GenerateId() : locationId,
            Name = string.IsNullOrWhiteSpace(name) ? address?.City : name.Trim(),
            Address = address,
        };
}
