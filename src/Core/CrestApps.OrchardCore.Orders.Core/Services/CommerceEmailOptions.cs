namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// The kinds of email the store can write its own templates of, in the order the settings page lists them.
/// </summary>
public sealed class CommerceEmailOptions
{
    private readonly List<CommerceEmailKind> _kinds = [];

    /// <summary>
    /// Gets the registered kinds.
    /// </summary>
    public IReadOnlyList<CommerceEmailKind> Kinds
        => _kinds;

    /// <summary>
    /// Adds a kind, or replaces the one with the same name.
    /// </summary>
    /// <param name="kind">The kind.</param>
    public CommerceEmailOptions AddKind(CommerceEmailKind kind)
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
}
