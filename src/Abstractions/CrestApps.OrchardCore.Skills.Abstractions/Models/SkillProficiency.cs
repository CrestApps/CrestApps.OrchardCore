namespace CrestApps.OrchardCore.Skills.Models;

/// <summary>
/// The proficiency scale every skill is held at: 1 (trainee) to 5 (expert).
/// </summary>
public static class SkillProficiency
{
    /// <summary>
    /// The lowest proficiency.
    /// </summary>
    public const int Minimum = 1;

    /// <summary>
    /// The highest proficiency.
    /// </summary>
    public const int Maximum = 5;

    /// <summary>
    /// The proficiency used when none is given: competent, neither expert nor trainee.
    /// </summary>
    public const int Default = 3;

    /// <summary>
    /// Gets whether a value is on the scale.
    /// </summary>
    /// <param name="proficiency">The value.</param>
    /// <returns><see langword="true"/> when it is from <see cref="Minimum"/> to <see cref="Maximum"/>.</returns>
    public static bool IsValid(int proficiency)
    {
        return proficiency is >= Minimum and <= Maximum;
    }
}
