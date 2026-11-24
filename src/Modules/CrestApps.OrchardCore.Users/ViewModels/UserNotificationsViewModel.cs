using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Users.ViewModels;

/// <summary>
/// A page of the site's Notifications page.
/// </summary>
public class UserNotificationsViewModel
{
    /// <summary>
    /// Gets the notifications of the page, each the Notifications module's notification shape.
    /// </summary>
    public IList<IShape> Notifications { get; } = [];

    /// <summary>
    /// Gets or sets whether the page lists the unread notifications only.
    /// </summary>
    public bool UnreadOnly { get; set; }

    /// <summary>
    /// Gets or sets the page, from 1.
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Gets or sets whether there are older notifications.
    /// </summary>
    public bool HasOlder { get; set; }

    /// <summary>
    /// Gets or sets whether the user has unread notifications.
    /// </summary>
    public bool HasUnread { get; set; }

    /// <summary>
    /// Gets or sets whether the page is framed as a page of the "my account" area.
    /// </summary>
    public bool InAccount { get; set; }

    /// <summary>
    /// Gets or sets whether the user may open their profile in the admin, where they choose how each kind of
    /// notification reaches them.
    /// </summary>
    public bool CanOpenProfile { get; set; }
}
