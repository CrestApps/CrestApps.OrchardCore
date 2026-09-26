namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// How well an agent holds one skill. A bare tag said only that somebody had listed the skill against them, so a
/// queue could not tell an agent who took a Spanish course from one who grew up speaking it.
/// </summary>
public sealed class AgentSkill
{
    /// <summary>
    /// The lowest proficiency a skill can be recorded at.
    /// </summary>
    public const int MinimumProficiency = 1;

    /// <summary>
    /// The highest proficiency a skill can be recorded at.
    /// </summary>
    public const int MaximumProficiency = 5;

    /// <summary>
    /// The proficiency a skill carried over from a bare tag is recorded at: competent, neither expert nor
    /// novice, because the tag never said which and guessing either way would be a claim nobody made.
    /// </summary>
    public const int DefaultProficiency = 3;

    /// <summary>
    /// Gets or sets the skill identifier.
    /// </summary>
    public string SkillId { get; set; }

    /// <summary>
    /// Gets or sets how well the agent holds the skill, from <see cref="MinimumProficiency"/> to
    /// <see cref="MaximumProficiency"/>.
    /// </summary>
    public int Proficiency { get; set; } = DefaultProficiency;
}
