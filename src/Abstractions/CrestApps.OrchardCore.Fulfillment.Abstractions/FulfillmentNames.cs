namespace CrestApps.OrchardCore.Fulfillment;

/// <summary>
/// Splits a recipient's full name for partners that want a first and a last name.
/// </summary>
public static class FulfillmentNames
{
    private static readonly char[] _spaces = [' ', '\t'];

    /// <summary>
    /// Splits a full name into its first word and the rest.
    /// </summary>
    /// <param name="name">The full name.</param>
    /// <returns>
    /// The first word and the other words. A single word is the first name, and the last name is
    /// <see langword="null"/>: a provider whose partner requires a last name decides how to fill it.
    /// </returns>
    public static (string FirstName, string LastName) Split(string name)
    {
        var words = (name ?? string.Empty).Split(_spaces, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return words.Length switch
        {
            0 => (null, null),
            1 => (words[0], null),
            _ => (words[0], string.Join(' ', words.Skip(1))),
        };
    }
}
