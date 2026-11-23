namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Models;

/// <summary>
/// A webhook Mailchimp created on an audience.
/// </summary>
public sealed class MailchimpWebhookRegistration
{
    /// <summary>
    /// Gets the webhook's id.
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// Gets the secret Mailchimp signs the webhook's requests with, which it returns only once, at creation; or
    /// <see langword="null"/> when it returned none.
    /// </summary>
    public string SigningSecret { get; init; }

    /// <inheritdoc/>
    public override string ToString()
        => $"Mailchimp webhook {Id}";
}
