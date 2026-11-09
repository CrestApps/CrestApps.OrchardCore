using CrestApps.OrchardCore.Skills.Models;

namespace CrestApps.OrchardCore.Skills.Services;

/// <summary>
/// Tells Skills which other subjects name the same person as a subject, such as a user and the worker profile linked to
/// it, so a skill held under any of them counts for all of them. A module that keeps its own record of people registers
/// one; Skills never needs to know the module.
/// </summary>
public interface ISkillSubjectLinkProvider
{
    /// <summary>
    /// Gets the other subjects that name the same person as a subject.
    /// </summary>
    /// <param name="subject">The subject.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The other subjects; empty when the provider knows none.</returns>
    Task<IReadOnlyList<SkillSubject>> GetLinkedSubjectsAsync(SkillSubject subject, CancellationToken cancellationToken = default);
}
