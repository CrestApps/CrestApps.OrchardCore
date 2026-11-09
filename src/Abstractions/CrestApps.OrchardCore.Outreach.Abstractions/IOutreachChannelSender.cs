using CrestApps.OrchardCore.Outreach.Models;

namespace CrestApps.OrchardCore.Outreach;

/// <summary>
/// Sends outreach messages on one channel: e-mail, SMS and the other messaging channels.
/// </summary>
public interface IOutreachChannelSender
{
    /// <summary>
    /// Gets the name runs refer to the sender by, in <see cref="OutreachRun.Sender"/>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets whether the run's messages may only go out in its sending hours, such as text messages, which carry rules
    /// about when people may be contacted.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <returns><see langword="true"/> when the engine holds batches outside the run's sending hours.</returns>
    bool ObservesQuietHours(OutreachRun run);

    /// <summary>
    /// Sends one message.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <param name="recipient">The recipient.</param>
    /// <param name="content">The message rendered for the recipient.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>Whether the message was accepted. Throw for a transient failure, so the batch is retried later.</returns>
    Task<OutreachSendResult> SendAsync(OutreachRun run, OutreachRecipient recipient, OutreachContent content, CancellationToken cancellationToken = default);
}
