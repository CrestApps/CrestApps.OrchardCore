using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// The rules of the Categories page: who may change a category, what a valid color is, and who owns a saved category.
/// </summary>
internal static partial class CalendarCategoryRules
{
    /// <summary>
    /// Gets whether a color is a #rrggbb color.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <returns><see langword="true"/> when it is valid.</returns>
    public static bool IsValidColor(string color)
    {
        return !string.IsNullOrWhiteSpace(color) && ColorPattern().IsMatch(color.Trim());
    }

    /// <summary>
    /// Gets whether a person may change or delete a category: a shared category needs a calendar administrator, any other
    /// category its owner.
    /// </summary>
    /// <param name="category">The category.</param>
    /// <param name="userId">The person's user ID.</param>
    /// <param name="canManageShared">Whether the person may manage shared categories.</param>
    /// <returns><see langword="true"/> when they may.</returns>
    public static bool CanChange(CalendarCategory category, string userId, bool canManageShared)
    {
        ArgumentNullException.ThrowIfNull(category);

        return category.IsShared
            ? canManageShared
            : string.Equals(category.OwnerId, userId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Applies the edited name, color and sharing to a category.
    /// </summary>
    /// <param name="category">The category.</param>
    /// <param name="name">The name, already checked.</param>
    /// <param name="color">The color, already checked.</param>
    /// <param name="shared">Whether the category should be everyone's.</param>
    /// <param name="canShare">Whether the person may manage shared categories.</param>
    /// <param name="userId">The person's user ID.</param>
    public static void Apply(CalendarCategory category, string name, string color, bool shared, bool canShare, string userId)
    {
        ArgumentNullException.ThrowIfNull(category);

        category.Name = name.Trim();
        category.Color = color.Trim().ToLowerInvariant();

        // Only a calendar manager decides whether a category is everyone's; anyone else's categories are their own. A new
        // category has no owner yet, so it must not be mistaken for a shared one.
        category.OwnerId = canShare && shared ? null : userId;
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex ColorPattern();
}
