namespace CrestApps.OrchardCore.Emails;

/// <summary>
/// What the site wrote over one kind of email: its template, when it's on, and the layout around every email of the
/// group.
/// </summary>
public sealed class TemplatedEmailOverride
{
    /// <summary>
    /// Gets the template of the kind, or <see langword="null"/> when the site wrote none or it's off.
    /// </summary>
    public TemplatedEmailTemplate Template { get; init; }

    /// <summary>
    /// Gets the layout, HTML with Liquid around <c>{{ Content }}</c>, or <see langword="null"/>.
    /// </summary>
    public string Layout { get; init; }

    /// <summary>
    /// Gets more values the group's templates read by name, such as the store's name as <c>{{ StoreName }}</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string> Values { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
