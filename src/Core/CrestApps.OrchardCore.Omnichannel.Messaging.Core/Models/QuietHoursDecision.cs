namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

/// <summary>
/// Whether sending right now would reach the contact outside the hours their queue keeps, and why.
/// </summary>
/// <param name="IsQuietHours">Whether the send would land in quiet hours.</param>
/// <param name="Reason">A sentence an agent can read, or <see langword="null"/> when it is not quiet hours.</param>
public readonly record struct QuietHoursDecision(bool IsQuietHours, string Reason)
{
    /// <summary>
    /// The decision for a send that is inside business hours, or on a queue that keeps none.
    /// </summary>
    public static QuietHoursDecision Open { get; } = new(false, null);
}
