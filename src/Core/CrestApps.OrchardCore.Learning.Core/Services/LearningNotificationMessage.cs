using System.Net;
using OrchardCore.Notifications;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// A plain-text Learning notice to a learner, with its notification type, the enrollment as its reference and a link.
/// </summary>
public sealed class LearningNotificationMessage : INotificationMessage
{
    /// <inheritdoc />
    public string Subject { get; init; }

    private readonly string _summary;

    /// <summary>
    /// Gets the short text shown in the notification list. It is given as plain text and kept HTML-encoded, because
    /// Notifications shows a summary as (sanitized) HTML, so a course title such as <c>Fire &amp; &lt;b&gt;Safety&lt;/b&gt;</c>
    /// shows as written.
    /// </summary>
    public string Summary
    {
        get => _summary;
        init => _summary = value is null ? null : WebUtility.HtmlEncode(value);
    }

    /// <inheritdoc />
    public string TextBody { get; init; }

    /// <inheritdoc />
    public string HtmlBody => null;

    /// <inheritdoc />
    public bool IsHtmlPreferred => false;

    /// <inheritdoc />
    public string Type { get; init; }

    /// <inheritdoc />
    public string ReferenceId { get; init; }

    /// <inheritdoc />
    public string Url { get; init; }
}
