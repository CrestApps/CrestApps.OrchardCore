namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Decides which tools a profile, template or interaction keeps when it is saved. The editor only shows the tools the
/// user may use, but a request can post any key, so the same rule is applied on save: a tool the user may use is kept
/// when posted, and a tool they may not use is kept only when the record already had it (a user can't add it, and
/// saving the record doesn't remove what a more powerful user chose).
/// </summary>
public static class AIToolSelectionFilter
{
    /// <summary>
    /// Returns the tool names to store.
    /// </summary>
    /// <param name="posted">The tool names the editor posted.</param>
    /// <param name="existing">The tool names the record already had.</param>
    /// <param name="selectable">The tools that can be selected at all.</param>
    /// <param name="isAuthorized">Whether the user may use a tool.</param>
    /// <returns>The tool names, in the order of <paramref name="selectable"/>.</returns>
    public static async Task<string[]> FilterAsync(
        IEnumerable<string> posted,
        IEnumerable<string> existing,
        IEnumerable<string> selectable,
        Func<string, Task<bool>> isAuthorized)
    {
        ArgumentNullException.ThrowIfNull(isAuthorized);

        var postedSet = new HashSet<string>(posted ?? [], StringComparer.Ordinal);
        var existingSet = new HashSet<string>(existing ?? [], StringComparer.Ordinal);
        var names = new List<string>();

        foreach (var key in (selectable ?? []).Distinct(StringComparer.Ordinal))
        {
            if (await isAuthorized(key))
            {
                if (postedSet.Contains(key))
                {
                    names.Add(key);
                }
            }
            else if (existingSet.Contains(key))
            {
                names.Add(key);
            }
        }

        return [.. names];
    }

    /// <summary>
    /// Returns the requested tools the caller may not use that the record did not already have, for callers such as an
    /// API that answer with a validation error instead of dropping them.
    /// </summary>
    /// <param name="requested">The tool names requested.</param>
    /// <param name="existing">The tool names the record already had.</param>
    /// <param name="isAuthorized">Whether the caller may use a tool.</param>
    /// <returns>The refused tool names.</returns>
    public static async Task<IReadOnlyList<string>> GetUnauthorizedAdditionsAsync(
        IEnumerable<string> requested,
        IEnumerable<string> existing,
        Func<string, Task<bool>> isAuthorized)
    {
        ArgumentNullException.ThrowIfNull(isAuthorized);

        var existingSet = new HashSet<string>(existing ?? [], StringComparer.Ordinal);
        var refused = new List<string>();

        foreach (var key in (requested ?? []).Where(key => !string.IsNullOrEmpty(key)).Distinct(StringComparer.Ordinal))
        {
            if (!existingSet.Contains(key) && !await isAuthorized(key))
            {
                refused.Add(key);
            }
        }

        return refused;
    }
}
