namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Services;

/// <summary>
/// The outcome of checking a Mailchimp webhook's <c>X-Mailchimp-Signature</c> header.
/// </summary>
public enum MailchimpSignatureResult
{
    /// <summary>
    /// The signature was made with the signing secret over the body, and its time is recent.
    /// </summary>
    Valid,

    /// <summary>
    /// The request has no signature header.
    /// </summary>
    Missing,

    /// <summary>
    /// The header is not <c>t=...,v1=...</c> with a Unix time.
    /// </summary>
    Malformed,

    /// <summary>
    /// No signature in the header was made with the signing secret over the body.
    /// </summary>
    Invalid,

    /// <summary>
    /// The signature is right but its time is more than the tolerance away from now (a replay).
    /// </summary>
    Stale,
}
