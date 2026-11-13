namespace CrestApps.OrchardCore.Emails;

/// <summary>
/// The kinds of email the site can write its own templates of, in the order the settings page lists them.
/// </summary>
public sealed class TemplatedEmailOptions
{
    private readonly List<TemplatedEmailKind> _kinds = [];

    /// <summary>
    /// Gets the registered kinds.
    /// </summary>
    public IReadOnlyList<TemplatedEmailKind> Kinds
        => _kinds;

    /// <summary>
    /// Adds a kind, or replaces the one with the same name.
    /// </summary>
    /// <param name="kind">The kind.</param>
    public TemplatedEmailOptions AddKind(TemplatedEmailKind kind)
    {
        ArgumentNullException.ThrowIfNull(kind);

        var index = _kinds.FindIndex(existing => string.Equals(existing.Name, kind.Name, StringComparison.Ordinal));

        if (index >= 0)
        {
            _kinds[index] = kind;
        }
        else
        {
            _kinds.Add(kind);
        }

        return this;
    }

    /// <summary>
    /// Returns whether a kind of that name is registered.
    /// </summary>
    /// <param name="name">The kind's name.</param>
    public bool HasKind(string name)
        => _kinds.Exists(kind => string.Equals(kind.Name, name, StringComparison.Ordinal));

    /// <summary>
    /// Returns the kind of that name, or <see langword="null"/>.
    /// </summary>
    /// <param name="name">The kind's name.</param>
    /// <returns>The kind.</returns>
    public TemplatedEmailKind Find(string name)
        => _kinds.Find(kind => string.Equals(kind.Name, name, StringComparison.Ordinal));

    /// <summary>
    /// Returns the kinds of one group, in the order they were added.
    /// </summary>
    /// <param name="group">The group, such as <c>Tickets</c>.</param>
    /// <returns>The kinds.</returns>
    public IReadOnlyList<TemplatedEmailKind> GetKinds(string group)
        => _kinds.FindAll(kind => string.Equals(kind.Group, group, StringComparison.Ordinal));
}
