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
        var legacyKind = SkillSubjectKind.ToLegacyValue(kind);
        var id = subject.Id;

        // A row the deferred re-index has not reached yet only has the number the kind was stored as.
        var assignments = await Session.Query<SkillAssignment, SkillAssignmentIndex>(
            index => (index.SubjectKindName == kind || (index.SubjectKindName == null && index.SubjectKind == legacyKind)) && index.SubjectId == id,
            collection: CollectionName)
            .ListAsync(cancellationToken);

        return assignments.ToArray();
    }

    public async Task<IReadOnlyList<SkillAssignment>> ListHoldersAsync(string skillId, int minimumProficiency, int dateKey, string kind, CancellationToken cancellationToken = default)
    {
        var legacyKind = SkillSubjectKind.ToLegacyValue(kind);

        var assignments = await Session.Query<SkillAssignment, SkillAssignmentIndex>(
            index => index.SkillId == skillId
                && (index.SubjectKindName == kind || (index.SubjectKindName == null && index.SubjectKind == legacyKind))
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
