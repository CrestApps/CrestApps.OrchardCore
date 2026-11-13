using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.SiteResources;

/// <summary>
/// The rules a site resource name follows: it is part of the resource's address and the name templates ask for.
/// </summary>
public static partial class SiteResourceNames
{
    /// <summary>
    /// The longest name a site resource may have.
    /// </summary>
    public const int MaxLength = 100;

    /// <summary>
    /// Gets whether <paramref name="name"/> is a valid site resource name: 1 to 100 letters, digits, dots, dashes and
    /// underscores, starting with a letter or a digit.
    /// </summary>
    /// <param name="name">The name to check.</param>
    public static bool IsValid([NotNullWhen(true)] string name)
        => !string.IsNullOrEmpty(name) && name.Length <= MaxLength && NamePattern().IsMatch(name);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
}
