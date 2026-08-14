using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the view model for editing subject action fields.
/// </summary>
public class SubjectActionViewModel
{
    /// <summary>
    /// Gets or sets the disposition identifier.
    /// </summary>
    public string DispositionId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show communication preferences.
    /// </summary>
    public bool ShowCommunicationPreferences { get; set; }

    /// <summary>
    /// Gets or sets whether to set the contact's "Do Not Call" preference.
    /// </summary>
    public bool? SetDoNotCall { get; set; }

    /// <summary>
    /// Gets or sets whether to set the contact's "Do Not SMS" preference.
    /// </summary>
    public bool? SetDoNotSms { get; set; }

    /// <summary>
    /// Gets or sets whether to set the contact's "Do Not Email" preference.
    /// </summary>
    public bool? SetDoNotEmail { get; set; }

    /// <summary>
    /// Gets or sets whether to set the contact's "Do Not Chat" preference.
    /// </summary>
    public bool? SetDoNotChat { get; set; }

    /// <summary>
    /// Gets or sets the available dispositions.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Dispositions { get; set; }
}
