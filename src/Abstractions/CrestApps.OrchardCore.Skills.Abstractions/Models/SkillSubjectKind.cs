namespace CrestApps.OrchardCore.Skills.Models;

/// <summary>
/// What kind of record holds a skill or a certification.
/// </summary>
public enum SkillSubjectKind
{
    /// <summary>
    /// An Orchard Core user; the subject id is the user id.
    /// </summary>
    User,

    /// <summary>
    /// A workforce worker, who may have no user account; the subject id is the worker id.
    /// </summary>
    Worker,
}
