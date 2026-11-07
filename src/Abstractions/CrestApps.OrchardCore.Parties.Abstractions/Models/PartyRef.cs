namespace CrestApps.OrchardCore.Parties.Models;

/// <summary>
/// A reference to a party, the person or organization a record belongs to: an open kind (one of
/// <see cref="PartyKinds"/>, or another a module adds) and the id of the party in that kind. Use it instead of a
/// <c>CustomerId</c> that is sometimes a user id and sometimes a customer record id.
/// </summary>
/// <param name="Kind">The kind of party, such as <see cref="PartyKinds.User"/>.</param>
/// <param name="Id">The party's id in its kind: a user id, a customer record id, an account content item id.</param>
public readonly record struct PartyRef(string Kind, string Id)
{
    /// <summary>
    /// Gets whether the reference names no party.
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(Kind) || string.IsNullOrEmpty(Id);

    /// <summary>
    /// Formats the reference as <c>Kind:Id</c>, the form <see cref="TryParse"/> reads.
    /// </summary>
    /// <returns>The text.</returns>
    public override string ToString()
        => IsEmpty ? string.Empty : $"{Kind}:{Id}";

    /// <summary>
    /// Reads a reference written as <c>Kind:Id</c>.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <param name="party">The reference.</param>
    /// <returns><see langword="true"/> when the text holds a kind and an id.</returns>
    public static bool TryParse(string value, out PartyRef party)
    {
        party = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var separator = value.IndexOf(':', StringComparison.Ordinal);

        if (separator <= 0 || separator == value.Length - 1)
        {
            return false;
        }

        party = new PartyRef(value[..separator], value[(separator + 1)..]);

        return true;
    }
}
