namespace CrestApps.OrchardCore.Commerce.Emails;

/// <summary>
/// An email as it was built, and what the store's template of it can use.
/// </summary>
public sealed class CommerceEmailContext
{
    /// <summary>
    /// Gets which email it is, the name of a <see cref="CommerceEmailKind"/>.
    /// </summary>
    public string Kind { get; init; }

    /// <summary>
    /// Gets the records the email is about, by the name the template reads them under, such as <c>Order</c>.
    /// </summary>
    public IReadOnlyDictionary<string, object> Models { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);

    /// <summary>
    /// Gets the built subject.
    /// </summary>
    public string Subject { get; init; }

    /// <summary>
    /// Gets the built text body.
    /// </summary>
    public string Text { get; init; }

    /// <summary>
    /// Gets the built HTML body, which the template reads as <c>{{ Content }}</c>.
    /// </summary>
    public string Html { get; init; }

    /// <summary>
    /// Gets the email's own values, such as the link to pay, by name.
    /// </summary>
    public IReadOnlyDictionary<string, string> Values { get; init; } = new Dictionary<string, string>();
}
