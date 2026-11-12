namespace CrestApps.OrchardCore.Skills.Models;

/// <summary>
/// The person a skill or certification belongs to.
/// </summary>
/// <param name="Kind">What kind of record the person is: one of the <see cref="SkillSubjectKind"/> constants, or a kind another module adds.</param>
/// <param name="Id">The user id or the staff member id.</param>
public readonly record struct SkillSubject(string Kind, string Id)
{
    /// <summary>
    /// Creates the subject of a user.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <returns>The subject.</returns>
    public static SkillSubject User(string userId)
    {
        return new SkillSubject(SkillSubjectKind.User, userId);
    }

    /// <summary>
    /// A staff member.
    /// </summary>
    /// <param name="staffMemberId">The staff member id.</param>
    /// <returns>The subject.</returns>
    public static SkillSubject Staff(string staffMemberId)
    {
        return new SkillSubject(SkillSubjectKind.Staff, staffMemberId);
    }

    /// <summary>
    /// Gets the subject as one string, such as <c>User:4x2k…</c>, for index columns and lock names.
    /// </summary>
    public string Key => $"{Kind}:{Id}";

    /// <summary>
    /// Gets whether the subject names someone.
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(Id);
}
