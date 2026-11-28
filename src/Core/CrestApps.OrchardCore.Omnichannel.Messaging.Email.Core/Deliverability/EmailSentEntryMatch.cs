namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;

/// <summary>
/// A logged send a delivery report was tied to.
/// </summary>
/// <param name="Key">A key that names the one logged send, so the reports about it count once.</param>
/// <param name="IsByRecipientOnly">
/// Whether the send was found only by its recipient and a recent date, because it was logged without a message
/// identifier. Anyone who knows the recipient can write a report that matches such a send, so it is not proof that the
/// email bounced.
/// </param>
public sealed record EmailSentEntryMatch(string Key, bool IsByRecipientOnly);
