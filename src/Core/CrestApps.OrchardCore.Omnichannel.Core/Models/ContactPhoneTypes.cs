namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// The kinds of phone number a contact's numbers are filed under, and the rules that depend on them.
/// </summary>
/// <remarks>
/// Every reader of a contact's numbers asks the same questions of the type: which number is dialed first, which one
/// can receive a text, which one is the contact's cell or home number. Those rules used to be written out in each
/// reader, matching the type's text with or without regard to case, and the copies had drifted apart. They are
/// answered here, against a type that <see cref="Normalize"/> has already brought to one spelling.
/// </remarks>
public static class ContactPhoneTypes
{
    /// <summary>
    /// A mobile number, the only kind that can receive a text message.
    /// </summary>
    public const string Cell = "Cell";

    /// <summary>
    /// A home or landline number.
    /// </summary>
    public const string Home = "Home";

    /// <summary>
    /// An office number.
    /// </summary>
    public const string Office = "Office";

    /// <summary>
    /// A work number.
    /// </summary>
    public const string Work = "Work";

    /// <summary>
    /// A number of any other kind.
    /// </summary>
    public const string Other = "Other";

    /// <summary>
    /// The spelling a mobile number is sometimes filed under. It is read as <see cref="Cell"/>.
    /// </summary>
    public const string Mobile = "Mobile";

    /// <summary>
    /// Brings a phone type to the spelling of one of the known types, whatever its case or surrounding spaces. A type
    /// that is not one of them is returned trimmed, and an empty one as <see langword="null"/>.
    /// </summary>
    /// <param name="type">The type as it was stored.</param>
    public static string Normalize(string type)
    {
        var trimmed = type?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (Is(trimmed, Cell) || Is(trimmed, Mobile))
        {
            return Cell;
        }

        if (Is(trimmed, Home))
        {
            return Home;
        }

        if (Is(trimmed, Office))
        {
            return Office;
        }

        if (Is(trimmed, Work))
        {
            return Work;
        }

        if (Is(trimmed, Other))
        {
            return Other;
        }

        return trimmed;
    }

    /// <summary>
    /// Whether a number of this type can receive text messages. Only a mobile number can; a home, work or fax line
    /// cannot.
    /// </summary>
    /// <param name="type">The phone type.</param>
    public static bool IsMobile(string type)
        => string.Equals(Normalize(type), Cell, StringComparison.Ordinal);

    /// <summary>
    /// The order in which a contact's numbers are tried when the contact is called: a cell number first, then home,
    /// office, work and other. A number of any other type is not called, and gets <see langword="null"/>.
    /// </summary>
    /// <param name="type">The phone type.</param>
    public static int? GetCallPriority(string type)
        => Normalize(type) switch
        {
            Cell => 1,
            Home => 2,
            Office => 3,
            Work => 4,
            Other => 5,
            _ => null,
        };

    private static bool Is(string value, string type)
        => string.Equals(value, type, StringComparison.OrdinalIgnoreCase);
}
