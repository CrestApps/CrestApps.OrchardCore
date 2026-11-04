using CrestApps.OrchardCore.Skills.Core.Indexes;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Skills.Core.Services;

/// <summary>
/// Stores skill assignments.
/// </summary>
internal sealed class SkillAssignmentStore : DocumentCatalog<SkillAssignment, SkillAssignmentIndex>
{
    public SkillAssignmentStore(ISession session)
        : base(session)
    {
        CollectionName = SkillsConstants.CollectionName;
    }

    public async Task<IReadOnlyList<SkillAssignment>> ListForSubjectAsync(SkillSubject subject, CancellationToken cancellationToken = default)
    {
        var kind = subject.Kind;
        var id = subject.Id;

        var assignments = await Session.Query<SkillAssignment, SkillAssignmentIndex>(
            index => index.SubjectKind == kind && index.SubjectId == id,
            collection: CollectionName)
            .ListAsync(cancellationToken);

        return assignments.ToArray();
    }

    public async Task<IReadOnlyList<SkillAssignment>> ListHoldersAsync(string skillId, int minimumProficiency, int dateKey, SkillSubjectKind kind, CancellationToken cancellationToken = default)
    {
        var assignments = await Session.Query<SkillAssignment, SkillAssignmentIndex>(
            index => index.SkillId == skillId
                && index.SubjectKind == kind
                && index.Proficiency >= minimumProficiency
                && (index.EffectiveFrom == null || index.EffectiveFrom <= dateKey)
                && (index.EffectiveTo == null || index.EffectiveTo >= dateKey),
            collection: CollectionName)
            .ListAsync(cancellationToken);

        return assignments.ToArray();
    }

    public async Task<IReadOnlyList<SkillAssignment>> ListStartingOrEndingAsync(int startsKey, int endedKey, CancellationToken cancellationToken = default)
    {
        var assignments = await Session.Query<SkillAssignment, SkillAssignmentIndex>(
            index => index.EffectiveFrom == startsKey || index.EffectiveTo == endedKey,
            collection: CollectionName)
            .ListAsync(cancellationToken);

        return assignments.ToArray();
    }

    public void Delete(SkillAssignment assignment)
    {
        Session.Delete(assignment, CollectionName);
    }
}
