namespace CrestApps.OrchardCore.Skills.Core;

/// <summary>
/// Normalizes skill and certification names, so a name is unique whatever its case and spacing.
/// </summary>
public static class SkillsNames
{
    /// <summary>
    /// Gets the form of a name used to compare it with others.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The trimmed name in upper case, or <see langword="null"/>.</returns>
    public static string Normalize(string name)
    {
        return string.IsNullOrWhiteSpace(name)
            ? null
            : name.Trim().ToUpperInvariant();
    }
}
