using CrestApps.OrchardCore.Learning.Programs.Core.Indexes;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Learning.Programs.Core.Services;

/// <summary>
/// Stores the progress of people on learning paths.
/// </summary>
public sealed class LearningPathProgressStore : DocumentCatalog<LearningPathProgress, LearningPathProgressIndex>
{
    public LearningPathProgressStore(ISession session)
        : base(session)
    {
        CollectionName = ProgramsConstants.CollectionName;
    }

    /// <summary>
    /// Finds a person's progress on a path.
    /// </summary>
    public Task<LearningPathProgress> FindAsync(string pathId, string userId, CancellationToken cancellationToken = default)
        => Session.Query<LearningPathProgress, LearningPathProgressIndex>(index => index.PathId == pathId && index.UserId == userId, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Lists a person's paths.
    /// </summary>
    public async Task<IReadOnlyList<LearningPathProgress>> ListByUserAsync(string userId, CancellationToken cancellationToken = default)
        => (await Session.Query<LearningPathProgress, LearningPathProgressIndex>(index => index.UserId == userId, collection: CollectionName)
            .ListAsync(cancellationToken)).ToArray();

    /// <summary>
    /// Pages the people on a path.
    /// </summary>
    public async Task<(int Count, IReadOnlyList<LearningPathProgress> Items)> PageAsync(string pathId, LearningPathState? state, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<LearningPathProgress, LearningPathProgressIndex>(index => index.PathId == pathId, collection: CollectionName);

        if (state is { } value)
        {
            query = query.Where(index => index.State == value);
        }

        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(index => index.Id).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, items.ToArray());
    }
}

/// <summary>
/// Stores required learning rules.
/// </summary>
public sealed class LearningRequirementStore : DocumentCatalog<LearningRequirement, LearningRequirementIndex>
{
    public LearningRequirementStore(ISession session)
        : base(session)
    {
        CollectionName = ProgramsConstants.CollectionName;
    }

    /// <summary>
    /// Lists every rule.
    /// </summary>
    public async Task<IReadOnlyList<LearningRequirement>> ListAllAsync(CancellationToken cancellationToken = default)
        => (await Session.Query<LearningRequirement, LearningRequirementIndex>(collection: CollectionName).ListAsync(cancellationToken)).ToArray();

    /// <summary>
    /// Lists the enabled rules about a target.
    /// </summary>
    public async Task<IReadOnlyList<LearningRequirement>> ListByTargetAsync(string targetKind, string targetId, CancellationToken cancellationToken = default)
        => (await Session.Query<LearningRequirement, LearningRequirementIndex>(index => index.TargetKind == targetKind && index.TargetId == targetId && index.Enabled, collection: CollectionName)
            .ListAsync(cancellationToken)).ToArray();
}

/// <summary>
/// Stores the states of people under required learning.
/// </summary>
public sealed class RequiredLearningAssignmentStore : DocumentCatalog<RequiredLearningAssignment, RequiredLearningAssignmentIndex>
{
    public RequiredLearningAssignmentStore(ISession session)
        : base(session)
    {
        CollectionName = ProgramsConstants.CollectionName;
    }

    /// <summary>
    /// Lists every state under a rule.
    /// </summary>
    public async Task<IReadOnlyList<RequiredLearningAssignment>> ListByRequirementAsync(string requirementId, CancellationToken cancellationToken = default)
        => (await Session.Query<RequiredLearningAssignment, RequiredLearningAssignmentIndex>(index => index.RequirementId == requirementId, collection: CollectionName)
            .ListAsync(cancellationToken)).ToArray();

    /// <summary>
    /// Finds a person's state under a rule.
    /// </summary>
    public Task<RequiredLearningAssignment> FindAsync(string requirementId, string userId, CancellationToken cancellationToken = default)
        => Session.Query<RequiredLearningAssignment, RequiredLearningAssignmentIndex>(index => index.RequirementId == requirementId && index.UserId == userId, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Lists the states of some people.
    /// </summary>
    public async Task<IReadOnlyList<RequiredLearningAssignment>> ListByUsersAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        var ids = userIds.Distinct(StringComparer.Ordinal).ToArray();

        return (await Session.Query<RequiredLearningAssignment, RequiredLearningAssignmentIndex>(index => index.UserId.IsIn(ids), collection: CollectionName)
            .ListAsync(cancellationToken)).ToArray();
    }

    /// <summary>
    /// Lists the open states with a due date, oldest due first.
    /// </summary>
    public async Task<IReadOnlyList<RequiredLearningAssignment>> ListOpenWithDueAsync(int skip, int take, CancellationToken cancellationToken = default)
        => (await Session.Query<RequiredLearningAssignment, RequiredLearningAssignmentIndex>(
                index => index.DueUtc != null
                    && (index.State == RequiredLearningState.Required || index.State == RequiredLearningState.InProgress || index.State == RequiredLearningState.Overdue),
                collection: CollectionName)
            .OrderBy(index => index.DueUtc)
            .ThenBy(index => index.Id)
            .Skip(skip)
            .Take(take)
            .ListAsync(cancellationToken)).ToArray();

    /// <summary>
    /// Pages the states under a rule.
    /// </summary>
    public async Task<(int Count, IReadOnlyList<RequiredLearningAssignment> Items)> PageAsync(string requirementId, RequiredLearningState? state, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<RequiredLearningAssignment, RequiredLearningAssignmentIndex>(index => index.RequirementId == requirementId, collection: CollectionName);

        if (state is { } value)
        {
            query = query.Where(index => index.State == value);
        }

        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(index => index.Id).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, items.ToArray());
    }
}
