namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Edits which agents dial out from a phone number.
/// </summary>
public class OutboundLineEndpointViewModel
{
    /// <summary>
    /// Gets or sets the identifiers of the users who dial out from this number.
    /// </summary>
    public string[] UserIds { get; set; } = [];
}
