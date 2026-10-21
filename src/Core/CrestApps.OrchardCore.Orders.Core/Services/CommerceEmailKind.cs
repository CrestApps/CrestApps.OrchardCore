using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// A kind of email the store can write its own template of, listed under Settings › Commerce › Emails.
/// </summary>
public sealed class CommerceEmailKind
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CommerceEmailKind"/> class.
    /// </summary>
    /// <param name="name">The kind's name, such as <c>order-confirmation</c>, which <see cref="CommerceEmail.Kind"/> names.</param>
    /// <param name="title">The kind's title on the settings page.</param>
    /// <param name="values">What the email's own values are, such as <c>{{ OrderUrl }}</c>, for the settings page; or
    /// <see langword="null"/> when it has none.</param>
    public CommerceEmailKind(string name, LocalizationSource title, LocalizationSource values = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(title);

        Name = name;
        Title = title;
        Values = values;
    }

    /// <summary>
    /// Gets the kind's name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the kind's title on the settings page.
    /// </summary>
    public LocalizationSource Title { get; }

    /// <summary>
    /// Gets what the email's own values are, or <see langword="null"/>.
    /// </summary>
    public LocalizationSource Values { get; }
}
