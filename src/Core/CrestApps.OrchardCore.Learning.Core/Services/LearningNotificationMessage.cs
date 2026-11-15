using OrchardCore.Notifications;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// A plain-text Learning notice to a learner, with its notification type, the enrollment as its reference and a link.
/// </summary>
public sealed class LearningNotificationMessage : INotificationMessage
{
    /// <inheritdoc />
    public string Subject { get; init; }

    /// <inheritdoc />
    public string Summary { get; init; }

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
