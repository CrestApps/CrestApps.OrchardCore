namespace CrestApps.OrchardCore.Skills.Models;

/// <summary>
/// The person a skill or certification belongs to.
/// </summary>
/// <param name="Kind">What kind of record the person is.</param>
/// <param name="Id">The user id or the worker id.</param>
public readonly record struct SkillSubject(SkillSubjectKind Kind, string Id)
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
    /// Creates the subject of a worker.
    /// </summary>
    /// <param name="workerId">The worker id.</param>
    /// <returns>The subject.</returns>
    public static SkillSubject Worker(string workerId)
    {
        return new SkillSubject(SkillSubjectKind.Worker, workerId);
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
