namespace CrestApps.OrchardCore.Skills.Models;

/// <summary>
/// The well-known kinds of record that hold a skill or a certification. A kind is an open string: a module adds its own
/// kind of person (and an <c>ISkillSubjectLinkProvider</c> that links it to the others) without changing Skills.
/// </summary>
public static class SkillSubjectKind
{
    /// <summary>
    /// An Orchard Core user; the subject id is the user id.
    /// </summary>
    public const string User = "User";

    /// <summary>
    /// A workforce worker, who may have no user account; the subject id is the worker id.
    /// </summary>
    public const string Worker = "Worker";

    /// <summary>
    /// Reads a kind someone typed or a file holds: a well-known kind in any case, a legacy number, or another module's
    /// kind as it is written. An empty value is a user.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The kind.</returns>
    public static string Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return User;
        }

        var trimmed = value.Trim();

        if (string.Equals(trimmed, User, StringComparison.OrdinalIgnoreCase))
        {
            return User;
        }

        if (string.Equals(trimmed, Worker, StringComparison.OrdinalIgnoreCase))
        {
            return Worker;
        }

        return long.TryParse(trimmed, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number)
            ? FromLegacyValue(number)
            : trimmed;
    }

    /// <summary>
    /// Returns the number a well-known kind was stored as before kinds were open strings, as the legacy index column
    /// still holds it, or <c>-1</c> for a kind that never had a number.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The legacy number of the kind.</returns>
    public static int ToLegacyValue(string kind)
        => kind switch
        {
            User => 0,
            Worker => 1,
            _ => -1,
        };

    /// <summary>
    /// Returns the kind a legacy number stands for.
    /// </summary>
    /// <param name="value">The number the kind was stored as.</param>
    /// <returns>The kind, or the number as text when it is not a well-known kind.</returns>
    public static string FromLegacyValue(long value)
        => value switch
        {
            0 => User,
            1 => Worker,
            _ => value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
}
