namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// What a queue needs from the agent who takes its work, and how hard a need it is.
/// </summary>
public sealed class QueueSkillRequirement
{
    /// <summary>
    /// Gets or sets the skill identifier.
    /// </summary>
    public string SkillId { get; set; }

    /// <summary>
    /// Gets or sets the proficiency an agent must reach to satisfy this requirement.
    /// </summary>
    public int MinimumProficiency { get; set; } = AgentSkill.DefaultProficiency;

    /// <summary>
    /// Gets or sets a value indicating whether an agent who does not meet this requirement is excluded. A
    /// requirement that is not required is a preference: it ranks candidates instead of eliminating them.
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// Gets or sets how long a contact may wait before this requirement is dropped, or <see langword="null"/>
    /// when it never is. A caller who needs a specialist nobody has available otherwise waits forever rather
    /// than reaching a generalist; a requirement with no window is one the queue meant absolutely.
    /// </summary>
    public int? RelaxAfterSeconds { get; set; }
}
