using CrestApps.OrchardCore.Skills.Models;

namespace CrestApps.OrchardCore.Skills.Services;

/// <summary>
/// Gives people skills and answers who holds what. A person holds each skill at most once.
/// </summary>
public interface ISkillAssignmentService
{
    /// <summary>
    /// Lists every skill a person holds, including ones that start later or have ended.
    /// </summary>
    /// <param name="subject">The person.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The assignments.</returns>
    Task<IReadOnlyList<SkillAssignment>> GetAssignmentsAsync(SkillSubject subject, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the skills that count for a person on a day: inside their effective dates, the skill enabled and, when the
    /// skill requires a certification, the certification valid that day.
    /// </summary>
    /// <param name="subject">The person.</param>
    /// <param name="date">The day.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The assignments that count.</returns>
    Task<IReadOnlyList<SkillAssignment>> GetEffectiveAsync(SkillSubject subject, DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces every skill a person holds with the given set.
    /// </summary>
    /// <param name="subject">The person.</param>
    /// <param name="assignments">The skills they hold from now on.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether the set was saved, or why not.</returns>
    Task<SkillsResult> SetAsync(SkillSubject subject, IEnumerable<SkillAssignmentInput> assignments, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives a person one skill, or changes how well they hold it.
    /// </summary>
    /// <param name="subject">The person.</param>
    /// <param name="assignment">The skill.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether it was saved, or why not.</returns>
    Task<SkillsResult> AssignAsync(SkillSubject subject, SkillAssignmentInput assignment, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes a skill away from a person.
    /// </summary>
    /// <param name="subject">The person.</param>
    /// <param name="skillId">The skill.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when they held it.</returns>
    Task<bool> RemoveAsync(SkillSubject subject, string skillId, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the assignments of a skill held at a proficiency or above on a day, for the people of one kind.
    /// </summary>
    /// <param name="skillId">The skill.</param>
    /// <param name="minimumProficiency">The lowest proficiency that counts.</param>
    /// <param name="date">The day.</param>
    /// <param name="kind">The kind of people to look at.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The assignments that count that day.</returns>
    Task<IReadOnlyList<SkillAssignment>> FindHoldersAsync(string skillId, int minimumProficiency, DateOnly date, SkillSubjectKind kind, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether a person holds a skill at a proficiency or above on a day, honoring the certification the skill requires.
    /// </summary>
    /// <param name="subject">The person.</param>
    /// <param name="skillId">The skill.</param>
    /// <param name="minimumProficiency">The lowest proficiency that counts.</param>
    /// <param name="date">The day.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when the skill counts for them.</returns>
    Task<bool> HoldsAsync(SkillSubject subject, string skillId, int minimumProficiency, DateOnly date, CancellationToken cancellationToken = default);
}
