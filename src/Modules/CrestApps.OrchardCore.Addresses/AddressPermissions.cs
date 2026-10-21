using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Addresses;

public static class AddressPermissions
{
    /// <summary>
    /// Sets up the service address suggestions come from, such as its API key.
    /// </summary>
    public static readonly Permission ManageAddressSuggestions = new(
        "ManageAddressSuggestions",
        LocalizationSource.Create("Manage address suggestions", typeof(AddressPermissions)));
}
