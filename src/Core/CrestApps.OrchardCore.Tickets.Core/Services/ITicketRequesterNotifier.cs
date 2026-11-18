using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.Tickets.Core.Indexes;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// Tells a requester who has a site account about their request through the site's notifications (the Tickets module
/// registers it with Orchard Core's Notifications feature): the notification center and the methods the requester
/// chose, such as e-mail or SMS. A requester without an account gets the same message by e-mail.
/// </summary>
public interface ITicketRequesterNotifier
{
    /// <summary>
    /// Sends a requester message as a notification.
    /// </summary>
    /// <param name="userId">The requester's user id.</param>
    /// <param name="item">The message, with the ticket it is about and its kind (<see cref="TicketsConstants.EmailKinds"/>).</param>
    /// <param name="message">The message as the site's notification template wrote it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="false"/> when the user can't be found, so the message goes by e-mail instead.</returns>
    Task<bool> NotifyAsync(string userId, TicketEmailOutboxItem item, TemplatedEmailMessage message, CancellationToken cancellationToken = default);
}
