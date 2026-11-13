using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AdminTheme;

/// <summary>
/// The color schemes of the admin. Their colors live in Assets/scss/_schemes.scss; the ids here must match the keys of
/// its '$schemes' map, and <see cref="BuiltIn"/> its '$default-scheme'.
/// </summary>
public static class AdminColorSchemes
{
    /// <summary>
    /// The scheme the stylesheet applies when the page names none.
    /// </summary>
    public const string BuiltIn = "gray";

    /// <summary>
    /// Every scheme, in the order of the pickers.
    /// </summary>
    public static readonly IReadOnlyList<string> All = [BuiltIn, "blue", "ocean", "forest", "plum", "sunset", "graphite", "gold"];

    /// <summary>
    /// Returns the scheme with the given id, or <see langword="null"/> when there is none.
    /// </summary>
    public static string Find(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        foreach (var scheme in All)
        {
            if (string.Equals(scheme, id, StringComparison.Ordinal))
            {
                return scheme;
            }
        }

        return null;
    }

    /// <summary>
    /// The name of the scheme shown to people.
    /// </summary>
    public static LocalizedString GetDisplayName(string id, IStringLocalizer S)
        => id switch
        {
            "blue" => S["Blue"],
            "ocean" => S["Ocean"],
            "forest" => S["Forest"],
            "plum" => S["Plum"],
            "sunset" => S["Sunset"],
            "graphite" => S["Graphite"],
            "gold" => S["Gold"],
            _ => S["Gray"],
        };
}
