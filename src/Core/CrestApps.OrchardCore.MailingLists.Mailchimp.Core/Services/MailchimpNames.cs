namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Services;

/// <summary>
/// Moves a person's name between the site's one name and Mailchimp's <c>FNAME</c> and <c>LNAME</c> merge fields.
/// </summary>
public static class MailchimpNames
{
    /// <summary>
    /// The merge tag of the first name.
    /// </summary>
    public const string FirstNameTag = "FNAME";

    /// <summary>
    /// The merge tag of the last name.
    /// </summary>
    public const string LastNameTag = "LNAME";

    /// <summary>
    /// Splits a name at its first space: the first word is the first name, the rest the last name.
    /// </summary>
    /// <param name="name">The name, or <see langword="null"/>.</param>
    /// <returns>The first and last names; each is <see langword="null"/> when empty.</returns>
    public static (string FirstName, string LastName) Split(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return (null, null);
        }

        var trimmed = name.Trim();
        var space = trimmed.IndexOf(' ');

        if (space < 0)
        {
            return (trimmed, null);
        }

        var last = trimmed.Substring(space + 1).Trim();

        return (trimmed.Substring(0, space), last.Length == 0 ? null : last);
    }

    /// <summary>
    /// Joins a first and a last name with a space.
    /// </summary>
    /// <param name="firstName">The first name, or <see langword="null"/>.</param>
    /// <param name="lastName">The last name, or <see langword="null"/>.</param>
    /// <returns>The name, or <see langword="null"/> when both are empty.</returns>
    public static string Join(string firstName, string lastName)
    {
        var name = $"{firstName?.Trim()} {lastName?.Trim()}".Trim();

        return name.Length == 0 ? null : name;
    }
}
