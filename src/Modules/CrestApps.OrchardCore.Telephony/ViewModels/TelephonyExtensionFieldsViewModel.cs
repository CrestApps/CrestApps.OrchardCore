using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Telephony.ViewModels;

/// <summary>
/// The editor fields for an internal extension, rendered by <c>ExtensionFields_Edit</c>.
/// </summary>
public class TelephonyExtensionFieldsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the extension is being created.
    /// </summary>
    public bool IsNew { get; set; }

    /// <summary>
    /// Gets or sets the extension entry display name (catalog name).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the dialed extension number.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the Orchard user the extension rings.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the display name shown to a colleague who calls this extension.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the selectable users for the user picker.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Users { get; set; } = [];
}
