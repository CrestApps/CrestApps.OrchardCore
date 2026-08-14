using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the view model for editing omnichannel contact settings.
/// </summary>
public class OmnichannelContactPartViewModel
{
    /// <summary>
    /// Gets or sets the contact's local time zone identifier.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the available time zones for the contact.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> AvailableTimeZones { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the contact time zone is required.
    /// </summary>
    [BindNever]
    public bool RequireTimeZone { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the Do not call preference is available.
    /// </summary>
    [BindNever]
    public bool UseDoNotCall { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the Do not email preference is available.
    /// </summary>
    [BindNever]
    public bool UseDoNotEmail { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the Do not SMS preference is available.
    /// </summary>
    [BindNever]
    public bool UseDoNotSms { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the Do not chat preference is available.
    /// </summary>
    [BindNever]
    public bool UseDoNotChat { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether phone calls are blocked for this contact.
    /// </summary>
    public bool DoNotCall { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when phone calls were blocked for this contact.
    /// </summary>
    public DateTime? DoNotCallUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether email is blocked for this contact.
    /// </summary>
    public bool DoNotEmail { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when email was blocked for this contact.
    /// </summary>
    public DateTime? DoNotEmailUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether SMS is blocked for this contact.
    /// </summary>
    public bool DoNotSms { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when SMS was blocked for this contact.
    /// </summary>
    public DateTime? DoNotSmsUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether chat is blocked for this contact.
    /// </summary>
    public bool DoNotChat { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when chat was blocked for this contact.
    /// </summary>
    public DateTime? DoNotChatUtc { get; set; }
}
