using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Cohorts;

/// <summary>
/// Keeps the enrollments of courses and learning paths in step with the cohorts they are linked to: a person who joins
/// a linked cohort is enrolled, a person who leaves it loses what the cohort gave them, and linking or unlinking a
/// cohort does the same for all its members. Each course or path is enrolled through the
/// <see cref="ICohortLearningTarget"/> of the part that marks it. Linking a cohort enrolls everyone in it, so a change
/// of the linked cohorts applies only when the person who made it may manage enrollments; the link of an item is read
/// and saved under a lock per item.
/// </summary>
public sealed class CohortLearningSync
{
    /// <summary>
    /// The actor recorded on the changes the sync makes when no person made them.
    /// </summary>
    public const string Actor = "CohortSync";

    private const string Collection = LearningConstants.CollectionName;

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(2);

    private readonly ISession _session;
    private readonly IContentManager _contentManager;
    private readonly ICohortService _cohorts;
    private readonly IEnumerable<ICohortLearningTarget> _targets;
    private readonly IDistributedLock _distributedLock;
    private readonly IAuthorizationService _authorizationService;
    private readonly IServiceProvider _services;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CohortLearningSync"/> class.
    /// </summary>
    public CohortLearningSync(
        ISession session,
        IContentManager contentManager,
        ICohortService cohorts,
        IEnumerable<ICohortLearningTarget> targets,
        IDistributedLock distributedLock,
        IAuthorizationService authorizationService,
        IServiceProvider services,
        IClock clock,
        ILogger<CohortLearningSync> logger)
    {
        _session = session;
        _contentManager = contentManager;
        _cohorts = cohorts;
        _targets = targets;
        _distributedLock = distributedLock;
        _authorizationService = authorizationService;
        _services = services;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Finds the target that enrolls people in a content item, from the parts it has.
    /// </summary>
    /// <param name="contentItem">The course or path.</param>
    /// <returns>The target, or <see langword="null"/> when no target handles the item.</returns>
    public ICohortLearningTarget FindTarget(ContentItem contentItem)
        => contentItem is null ? null : _targets.FirstOrDefault(target => contentItem.Has(target.PartName));

    /// <summary>
    /// Lists the links of the courses and paths a cohort is linked to.
    /// </summary>
    /// <param name="cohortId">The cohort.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The links.</returns>
    public async Task<IReadOnlyList<CohortLearningLink>> ListLinksAsync(string cohortId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(cohortId)
            ? []
            : (await _session.Query<CohortLearningLink, CohortLearningLinkIndex>(index => index.CohortId == cohortId, Collection).ListAsync(cancellationToken))
                .DistinctBy(link => link.TargetId, StringComparer.Ordinal)
                .ToArray();

    /// <summary>
    /// Finds the link of a course or path.
    /// </summary>
    /// <param name="targetId">The content item id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The link, or <see langword="null"/> when no cohort is applied to it.</returns>
    public async Task<CohortLearningLink> FindLinkAsync(string targetId, CancellationToken cancellationToken = default)
        => (await ListTargetLinksAsync(targetId, cancellationToken)).FirstOrDefault();

    /// <summary>
    /// Applies the cohorts of a published course or path: the members of a newly linked cohort are enrolled, and the
    /// members of an unlinked one lose what it gave them. A change of the linked cohorts applies only when the person
    /// who made it may manage enrollments: <paramref name="actor"/>, or else the person the item's
    /// <see cref="LearningCohortsPart.LinkedBy"/> names; with neither (a recipe, setup) the site made it.
    /// </summary>
    /// <param name="targetId">The content item id.</param>
    /// <param name="actor">Who published the item, or <see langword="null"/> when no person did.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task ApplyAsync(string targetId, string actor, CancellationToken cancellationToken = default)
    {
        var item = await _contentManager.GetAsync(targetId, VersionOptions.Published);
        var target = FindTarget(item);

        if (target is null)
        {
            return;
        }

        var part = item.TryGet<LearningCohortsPart>(out var cohortsPart) ? cohortsPart : null;
        var wanted = new List<string>();

        foreach (var cohortId in (part?.CohortIds ?? []).Distinct(StringComparer.Ordinal))
        {
            // A cohort that is gone links nobody; it stays on the item until someone saves it again.
            if (await _cohorts.FindByIdAsync(cohortId, cancellationToken) is not null)
            {
                wanted.Add(cohortId);
            }
        }

        string[] added;
        string[] removed;

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockKey(targetId), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            _logger.LogWarning("The cohorts of a course or path could not be locked; they are applied on the next publish.");

            return;
        }

        await using (locker)
        {
            // Two publishes close together may each have saved a link before this lock existed: keep the first, read
            // what all of them applied, and remove the others.
            var links = await ListTargetLinksAsync(targetId, cancellationToken);
            var link = links.FirstOrDefault();
            var applied = links.SelectMany(candidate => candidate.CohortIds ?? []).Distinct(StringComparer.Ordinal).ToList();

            foreach (var extra in links.Skip(1))
            {
                _session.Delete(extra, Collection);
            }

            added = [.. wanted.Except(applied, StringComparer.Ordinal)];
            removed = [.. applied.Except(wanted, StringComparer.Ordinal)];

            if ((added.Length > 0 || removed.Length > 0) && !await MayLinkAsync(actor ?? part?.LinkedBy))
            {
                // Linking a cohort enrolls all its members: someone who may not manage enrollments changes nothing.
                _logger.LogWarning("The cohorts of a course or path were changed by someone who may not manage enrollments; the change was not applied.");

                wanted = applied;
                added = [];
                removed = [];
            }

            if (wanted.Count == 0)
            {
                if (link is not null)
                {
                    _session.Delete(link, Collection);
                }
            }
            else
            {
                link ??= new CohortLearningLink { TargetId = targetId };
                link.PartName = target.PartName;
                link.DisplayText = item.DisplayText;
                link.CohortIds = wanted;
                link.ModifiedUtc = _clock.UtcNow;

                await _session.SaveAsync(link, false, Collection, cancellationToken);
            }

            // The link is saved first: enrolling commits the session, and a person who left meanwhile is then judged on it.
            await _session.SaveChangesAsync(cancellationToken);
        }

        foreach (var cohortId in added)
        {
            foreach (var userId in await _cohorts.ListMemberIdsAsync(cohortId, cancellationToken))
            {
                await JoinAsync(target, targetId, cohortId, userId, actor, cancellationToken);
            }
        }

        foreach (var cohortId in removed)
        {
            foreach (var userId in await _cohorts.ListMemberIdsAsync(cohortId, cancellationToken))
            {
                await LeaveAsync(target, targetId, cohortId, userId, wanted, actor, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Forgets the cohorts of a course or path that was deleted. Nobody's enrollment changes: the course is gone.
    /// </summary>
    /// <param name="targetId">The content item id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task ForgetAsync(string targetId, CancellationToken cancellationToken = default)
    {
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockKey(targetId), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            _logger.LogWarning("The cohorts of a deleted course or path could not be locked; they were not forgotten.");

            return;
        }

        await using (locker)
        {
            var links = await ListTargetLinksAsync(targetId, cancellationToken);

            foreach (var link in links)
            {
                _session.Delete(link, Collection);
            }

            if (links.Count > 0)
            {
                await _session.SaveChangesAsync(cancellationToken);
            }
        }
    }

    /// <summary>
    /// Enrolls people who joined a cohort in every course and path it is linked to.
    /// </summary>
    /// <param name="cohortId">The cohort.</param>
    /// <param name="userIds">The people who joined.</param>
    /// <param name="actor">Who added them.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task MembersAddedAsync(string cohortId, IReadOnlyList<string> userIds, string actor, CancellationToken cancellationToken = default)
    {
        foreach (var link in await ListLinksAsync(cohortId, cancellationToken))
        {
            var target = FindTarget(link.PartName);

            if (target is null)
            {
                continue;
            }

            foreach (var userId in userIds)
            {
                await JoinAsync(target, link.TargetId, cohortId, userId, actor, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Takes away what a cohort gave the people who left it, in every course and path it is linked to.
    /// </summary>
    /// <param name="cohortId">The cohort.</param>
    /// <param name="userIds">The people who left.</param>
    /// <param name="actor">Who removed them.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task MembersRemovedAsync(string cohortId, IReadOnlyList<string> userIds, string actor, CancellationToken cancellationToken = default)
    {
        foreach (var link in await ListLinksAsync(cohortId, cancellationToken))
        {
            var target = FindTarget(link.PartName);

            if (target is null)
            {
                continue;
            }

            foreach (var userId in userIds)
            {
                await LeaveAsync(target, link.TargetId, cohortId, userId, link.CohortIds, actor, cancellationToken);
            }
        }
    }

    private async Task<IReadOnlyList<CohortLearningLink>> ListTargetLinksAsync(string targetId, CancellationToken cancellationToken)
        => string.IsNullOrEmpty(targetId)
            ? []
            : (await _session.Query<CohortLearningLink, CohortLearningLinkIndex>(index => index.TargetId == targetId, Collection).ListAsync(cancellationToken))
                .DistinctBy(link => link.Id)
                .OrderBy(link => link.Id)
                .ToArray();

    // No person (a recipe, setup, a background job with no one behind it) is the site itself.
    private async Task<bool> MayLinkAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return true;
        }

        var principal = await LearningUserPrincipals.CreateAsync(_services, userId);

        return principal is not null && await _authorizationService.AuthorizeAsync(principal, LearningPermissions.ManageEnrollments);
    }

    private ICohortLearningTarget FindTarget(string partName)
        => _targets.FirstOrDefault(target => string.Equals(target.PartName, partName, StringComparison.Ordinal));

    private static string LockKey(string targetId)
        => $"LearningCohortLink:{targetId}";

    private async Task JoinAsync(ICohortLearningTarget target, string targetId, string cohortId, string userId, string actor, CancellationToken cancellationToken)
    {
        try
        {
            await target.JoinAsync(targetId, cohortId, userId, actor ?? Actor, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // One person's refusal (a lock held elsewhere, a course that changed) doesn't stop the others.
            _logger.LogWarning(exception, "The cohort sync could not enroll a person in a linked course or path.");
        }
    }

    private async Task LeaveAsync(ICohortLearningTarget target, string targetId, string cohortId, string userId, IEnumerable<string> linkedCohortIds, string actor, CancellationToken cancellationToken)
    {
        try
        {
            var inAnother = false;

            foreach (var otherId in linkedCohortIds.Where(id => !string.Equals(id, cohortId, StringComparison.Ordinal)))
            {
                if (await _cohorts.IsMemberAsync(otherId, userId, cancellationToken))
                {
                    inAnother = true;

                    break;
                }
            }

            await target.LeaveAsync(targetId, cohortId, userId, inAnother, actor ?? Actor, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "The cohort sync could not end what a cohort gave a person who left it.");
        }
    }
}
