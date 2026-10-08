using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the view model for the fields every omnichannel address has. The settings of each capability, such as
/// the SMS provider or the agents who dial from a number, are contributed by the display drivers of the features that
/// offer them.
/// </summary>
public class OmnichannelChannelEndpointViewModel
{
    /// <summary>
    /// Gets or sets the display text.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the address type. Shown read-only; set when the address is created.
    /// </summary>
    [BindNever]
    public string AddressType { get; set; }

    /// <summary>
    /// Gets or sets the localized name of the address type.
    /// </summary>
    [BindNever]
    public string AddressTypeDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the address itself, such as the phone number.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets the capabilities ticked on the address.
    /// </summary>
    public string[] Capabilities { get; set; } = [];

    /// <summary>
    /// Gets or sets the capabilities the enabled features offer for this address type.
    /// </summary>
    [BindNever]
    public IList<OmnichannelAddressCapabilityViewModel> AvailableCapabilities { get; set; } = [];
}

/// <summary>
/// A capability offered as a checkbox on the address editor.
/// </summary>
public class OmnichannelAddressCapabilityViewModel
{
    /// <summary>
    /// Gets or sets the capability's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the capability's display name.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the capability's hint.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the capability is ticked.
    /// </summary>
    public bool Selected { get; set; }
}
