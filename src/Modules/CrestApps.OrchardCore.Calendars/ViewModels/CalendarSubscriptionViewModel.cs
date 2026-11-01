using CrestApps.OrchardCore.Calendars.Core.External;
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

    /// <summary>
    /// Gets or sets whether changes made here are written back to the external calendar.
    /// </summary>
    public bool TwoWay { get; set; }

    /// <summary>
    /// Gets or sets which copy wins when an event changed on both sides.
    /// </summary>
    public CalendarSyncConflictPolicy ConflictPolicy { get; set; }

    /// <summary>
    /// Gets or sets whether the external account refused access.
    /// </summary>
    [BindNever]
    public bool NeedsReconnect { get; set; }

    /// <summary>
    /// Gets or sets the conflict policies to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> ConflictPolicyOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the providers that subscribe with an address.
    /// </summary>
    [BindNever]
    public IList<string> AddressProviders { get; set; } = [];

    /// <summary>
    /// Gets or sets the providers that can write changes back.
    /// </summary>
    [BindNever]
    public IList<string> WritableProviders { get; set; } = [];
}
