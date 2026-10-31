using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Calendars.ViewModels;

/// <summary>
/// The Subscription card of the calendar editor.
/// </summary>
public class CalendarSubscriptionViewModel
{
    /// <summary>
    /// Gets or sets the provider.
    /// </summary>
    public string SubscriptionProvider { get; set; }

    /// <summary>
    /// Gets or sets the external calendar's address.
    /// </summary>
    public string SubscriptionUrl { get; set; }

    /// <summary>
    /// Gets or sets how often to synchronize, in minutes.
    /// </summary>
    public int SubscriptionInterval { get; set; } = 60;

    /// <summary>
    /// Gets or sets whether synchronization is on.
    /// </summary>
    public bool SubscriptionEnabled { get; set; }

    /// <summary>
    /// Gets or sets the providers to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> ProviderOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets when the calendar was last synchronized.
    /// </summary>
    [BindNever]
    public DateTime? LastSyncedUtc { get; set; }

    /// <summary>
    /// Gets or sets why the last synchronization failed.
    /// </summary>
    [BindNever]
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets what the last synchronization did.
    /// </summary>
    [BindNever]
    public string LastResult { get; set; }
}
