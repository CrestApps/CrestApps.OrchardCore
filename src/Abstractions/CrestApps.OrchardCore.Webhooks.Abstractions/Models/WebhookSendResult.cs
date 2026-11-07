namespace CrestApps.OrchardCore.Webhooks.Models;

/// <summary>
/// What one webhook POST came to.
/// </summary>
public sealed class WebhookSendResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the receiver answered with a 2xx status.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the status the receiver answered, or <see langword="null"/> when there was no answer.
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// Gets or sets why the attempt failed: the start of the receiver's answer, or the error reaching it.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the egress guard refused the host because it is on the site's own
    /// network. Sending again is refused again, so it is not worth retrying.
    /// </summary>
    public bool EgressRefused { get; set; }
}
