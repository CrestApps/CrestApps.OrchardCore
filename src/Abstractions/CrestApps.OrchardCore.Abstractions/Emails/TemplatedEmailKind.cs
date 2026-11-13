using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Emails;

/// <summary>
/// A kind of email the site can write its own template of, listed on the settings page of the module that sends it.
/// </summary>
public sealed class TemplatedEmailKind
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TemplatedEmailKind"/> class.
    /// </summary>
    /// <param name="group">The group the kind is listed in, such as <c>Commerce</c> or <c>Tickets</c>: the module whose
    /// settings page lists it and whose <see cref="ITemplatedEmailTemplateSource"/> keeps its template.</param>
    /// <param name="name">The kind's name, such as <c>ticket-received</c>, which <see cref="TemplatedEmail.Kind"/> names.</param>
    /// <param name="title">The kind's title on the settings page.</param>
    /// <param name="values">What the email's own values are, such as <c>{{ TicketUrl }}</c>, for the settings page; or
    /// <see langword="null"/> when it has none.</param>
    public TemplatedEmailKind(string group, string name, LocalizationSource title, LocalizationSource values = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(group);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(title);

        Group = group;
        Name = name;
        Title = title;
        Values = values;
    }

    /// <summary>
    /// Gets the group the kind is listed in.
    /// </summary>
    public string Group { get; }

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
