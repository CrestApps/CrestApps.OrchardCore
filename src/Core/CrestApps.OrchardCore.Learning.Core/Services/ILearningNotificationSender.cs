namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Sends a Learning notice to a site user through Orchard Core's Notifications module (<c>INotificationService</c>),
/// after the current request commits, so the user gets it in their notifications and on the delivery methods they chose
/// for its notification type. Learning never e-mails a site user itself.
/// </summary>
public interface ILearningNotificationSender
{
    /// <summary>
    /// Queues a notice to a user. Nothing is sent when the user doesn't exist or the Notifications feature is off.
    /// </summary>
    /// <param name="userId">The id of the user to tell.</param>
    /// <param name="message">The notice, with its registered notification type, its record and a <c>~/</c> link.</param>
    void Send(string userId, LearningNotificationMessage message);
}
