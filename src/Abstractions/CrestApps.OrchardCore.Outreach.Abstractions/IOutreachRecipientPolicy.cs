using CrestApps.OrchardCore.Outreach.Models;

namespace CrestApps.OrchardCore.Outreach;

/// <summary>
/// Decides whether an outreach recipient may be contacted, just before the message is sent: opt-outs, consent,
/// do-not-contact lists. Every registered policy is asked; the first that gives a reason skips the recipient.
/// </summary>
public interface IOutreachRecipientPolicy
{
    /// <summary>
    /// Gets why the recipient must not be contacted by the run.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <param name="recipient">The recipient.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The skip reason, such as <see cref="OutreachConstants.SkipReasons.OptedOut"/>, or <see langword="null"/> when the recipient may be contacted.</returns>
    Task<string> GetSkipReasonAsync(OutreachRun run, OutreachRecipient recipient, CancellationToken cancellationToken = default);
}
