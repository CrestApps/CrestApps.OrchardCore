using System.Net;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Services;

/// <summary>
/// Mailchimp answered a call with an error. The message holds the HTTP status and Mailchimp's error title only; the
/// detail, which can name the member's address, is kept apart with every address replaced, and never goes in the
/// message.
/// </summary>
public sealed partial class MailchimpApiException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MailchimpApiException"/> class.
    /// </summary>
    /// <param name="statusCode">The HTTP status.</param>
    /// <param name="title">Mailchimp's error title, such as <c>Member In Compliance State</c>, or <see langword="null"/>.</param>
    /// <param name="detail">Mailchimp's error detail, or <see langword="null"/>.</param>
    public MailchimpApiException(HttpStatusCode statusCode, string title, string detail)
        : base($"Mailchimp answered {(int)statusCode} {Clean(title) ?? statusCode.ToString()}.")
    {
        StatusCode = statusCode;
        Title = Clean(title);
        Detail = ScrubAddresses(Clean(detail));
    }

    /// <summary>
    /// Gets the HTTP status.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Gets Mailchimp's error title, or <see langword="null"/>.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets Mailchimp's error detail with every e-mail address replaced, or <see langword="null"/>. Safe to show an
    /// admin; never logged.
    /// </summary>
    public string Detail { get; }

    /// <summary>
    /// Gets whether sending the call again later can help: a rate limit (429), a time-out (408) or a server error (5xx).
    /// </summary>
    public bool IsRetryable
        => IsRetryableStatus(StatusCode);

    /// <summary>
    /// Returns whether a status says that sending the call again later can help.
    /// </summary>
    /// <param name="statusCode">The HTTP status.</param>
    public static bool IsRetryableStatus(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout || (int)statusCode >= 500;

    /// <summary>
    /// Returns the text with every e-mail address in it replaced by <c>(address)</c>.
    /// </summary>
    /// <param name="text">The text, or <see langword="null"/>.</param>
    public static string ScrubAddresses(string text)
        => string.IsNullOrEmpty(text) ? text : AddressPattern().Replace(text, "(address)");

    private static string Clean(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= 300 ? trimmed : trimmed.Substring(0, 300);
    }

    [GeneratedRegex(@"[^\s""'<>()\[\],;:]+@[^\s""'<>()\[\],;:]+", RegexOptions.CultureInvariant)]
    private static partial Regex AddressPattern();
}
