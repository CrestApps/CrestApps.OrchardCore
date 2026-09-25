namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// Gives each customer an avatar colour of their own, so a long list is easy to scan and the same customer keeps the
/// same colour in the list and in the open conversation. The colour is derived from the customer key, not stored.
/// </summary>
public static class AvatarPalette
{
    private static readonly string[] _classes =
    [
        "bg-primary-subtle text-primary-emphasis",
        "bg-success-subtle text-success-emphasis",
        "bg-danger-subtle text-danger-emphasis",
        "bg-warning-subtle text-warning-emphasis",
        "bg-info-subtle text-info-emphasis",
        "bg-secondary-subtle text-secondary-emphasis",
    ];

    /// <summary>
    /// Gets the avatar classes for a customer.
    /// </summary>
    /// <param name="key">The customer key.</param>
    /// <returns>The background and text classes.</returns>
    public static string For(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return _classes[^1];
        }

        // A stable hash rather than string.GetHashCode, which differs between processes and would recolour everyone on
        // every restart.
        var hash = 0u;

        foreach (var character in key)
        {
            hash = unchecked((hash * 31) + character);
        }

        return _classes[hash % (uint)_classes.Length];
    }

    /// <summary>
    /// Gets the initials shown in a customer's avatar: the first letter of the first two words of their name, or
    /// <see langword="null"/> for a sender known only by their address.
    /// </summary>
    /// <param name="name">The customer's name.</param>
    /// <returns>The initials, or <see langword="null"/>.</returns>
    public static string Initials(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !char.IsLetter(name.Trim()[0]))
        {
            return null;
        }

        return string.Concat(name
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .Select(word => char.ToUpperInvariant(word[0])));
    }
}
