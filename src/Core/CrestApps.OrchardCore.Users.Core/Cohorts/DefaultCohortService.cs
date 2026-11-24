using CrestApps.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Users;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Users.Core.Cohorts;

/// <summary>
/// Keeps cohorts and their members in the <see cref="UsersConstants.CohortsCollectionName"/> collection: one document
/// per cohort and one per member, so a large cohort is paged from its index and a person's cohorts are found by user.
/// Members are saved before the observers are told, with only the people who actually joined or left.
/// </summary>
public sealed class DefaultCohortService : ICohortService
{
    private const string Collection = UsersConstants.CohortsCollectionName;
    private const int MaxIdLength = 50;
    private const int MaxNameLength = 255;
    private const int BatchSize = 500;

    private readonly ISession _session;
    private readonly IEnumerable<ICohortMembershipObserver> _observers;
    private readonly IEnumerable<ICohortUsage> _usages;
    private readonly UserManager<IUser> _userManager;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCohortService"/> class.
    /// </summary>
    public DefaultCohortService(
        ISession session,
        IEnumerable<ICohortMembershipObserver> observers,
        IEnumerable<ICohortUsage> usages,
        UserManager<IUser> userManager,
        IClock clock,
        IStringLocalizer<DefaultCohortService> stringLocalizer)
    {
        _session = session;
        _observers = observers;
        _usages = usages;
        _userManager = userManager;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<(int Count, IReadOnlyList<Cohort> Items)> PageAsync(string search, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = _session.Query<Cohort, CohortIndex>(collection: Collection);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = CohortNames.Normalize(search);

            query = query.Where(index => index.NormalizedName.Contains(normalized));
        }

        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(index => index.NormalizedName).Skip(Math.Max(0, skip)).Take(Math.Max(1, take)).ListAsync(cancellationToken);

        return (count, items.ToArray());
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Cohort>> ListAsync(int take = 500, CancellationToken cancellationToken = default)
        => (await _session.Query<Cohort, CohortIndex>(collection: Collection)
                .OrderBy(index => index.NormalizedName)
                .Take(Math.Max(1, take))
                .ListAsync(cancellationToken))
            .ToArray();

    /// <inheritdoc/>
    public Task<Cohort> FindByIdAsync(string cohortId, CancellationToken cancellationToken = default)
        => string.IsNullOrWhiteSpace(cohortId)
            ? Task.FromResult<Cohort>(null)
            : _session.Query<Cohort, CohortIndex>(index => index.CohortId == cohortId, Collection).FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<Cohort> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = CohortNames.Normalize(name);

        return normalized is null
            ? Task.FromResult<Cohort>(null)
            : _session.Query<Cohort, CohortIndex>(index => index.NormalizedName == normalized, Collection).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CohortResult> CreateAsync(string name, string description, string cohortId = null, CancellationToken cancellationToken = default)
    {
        var errors = await ValidateAsync(null, name, cancellationToken);

        cohortId = string.IsNullOrWhiteSpace(cohortId) ? UniqueId.GenerateId() : cohortId.Trim();

        if (cohortId.Length > MaxIdLength)
        {
            errors.Add(S["The cohort id may have at most {0} characters.", MaxIdLength]);
        }
        else if (await FindByIdAsync(cohortId, cancellationToken) is not null)
        {
            errors.Add(S["Another cohort has the id '{0}'.", cohortId]);
        }

        if (errors.Count > 0)
        {
            return new CohortResult(null, errors);
        }

        var cohort = new Cohort
        {
            CohortId = cohortId,
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            CreatedUtc = _clock.UtcNow,
        };

        await _session.SaveAsync(cohort, false, Collection, cancellationToken);
        await _session.FlushAsync(cancellationToken);

        return new CohortResult(cohort, []);
    }

    /// <inheritdoc/>
    public async Task<CohortResult> UpdateAsync(string cohortId, string name, string description, CancellationToken cancellationToken = default)
    {
        var cohort = await FindByIdAsync(cohortId, cancellationToken);

        if (cohort is null)
        {
            return new CohortResult(null, [S["The cohort was not found."]]);
        }

        var errors = await ValidateAsync(cohort.CohortId, name, cancellationToken);

        if (errors.Count > 0)
        {
            return new CohortResult(null, errors);
        }

        cohort.Name = name.Trim();
        cohort.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        cohort.ModifiedUtc = _clock.UtcNow;

        await _session.SaveAsync(cohort, false, Collection, cancellationToken);
        await _session.FlushAsync(cancellationToken);

        return new CohortResult(cohort, []);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> DeleteAsync(string cohortId, CancellationToken cancellationToken = default)
    {
        var cohort = await FindByIdAsync(cohortId, cancellationToken);

        if (cohort is null)
        {
            return [S["The cohort was not found."]];
        }

        // A cohort something uses stays: deleting it would leave, for example, a course whose people can never be
        // taken off it by the cohort sync again.
        var uses = await ListUsesAsync(cohort.CohortId, cancellationToken);

        if (uses.Count > 0)
        {
            return [S["The cohort is used by {0}. Change those first.", string.Join(", ", uses)]];
        }

        foreach (var member in await _session.Query<CohortMember, CohortMemberIndex>(index => index.CohortId == cohort.CohortId, Collection).ListAsync(cancellationToken))
        {
            _session.Delete(member, Collection);
        }

        _session.Delete(cohort, Collection);
        await _session.FlushAsync(cancellationToken);

        return [];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ListUsesAsync(string cohortId, CancellationToken cancellationToken = default)
    {
        var uses = new List<string>();

        foreach (var usage in _usages)
        {
            uses.AddRange(await usage.ListUsesAsync(cohortId, cancellationToken));
        }

        return uses;
    }

    /// <inheritdoc/>
    public async Task<(int Count, IReadOnlyList<CohortMember> Items)> PageMembersAsync(string cohortId, int skip, int take, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cohortId))
        {
            return (0, []);
        }

        var query = _session.Query<CohortMember, CohortMemberIndex>(index => index.CohortId == cohortId, Collection);
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(index => index.AddedUtc).ThenBy(index => index.UserId).Skip(Math.Max(0, skip)).Take(Math.Max(1, take)).ListAsync(cancellationToken);

        return (count, items.ToArray());
    }

    /// <inheritdoc/>
    public Task<int> CountMembersAsync(string cohortId, CancellationToken cancellationToken = default)
        => string.IsNullOrWhiteSpace(cohortId)
            ? Task.FromResult(0)
            : _session.QueryIndex<CohortMemberIndex>(index => index.CohortId == cohortId, Collection).CountAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ListMemberIdsAsync(string cohortId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cohortId))
        {
            return [];
        }

        return (await _session.QueryIndex<CohortMemberIndex>(index => index.CohortId == cohortId, Collection).ListAsync(cancellationToken))
            .Select(index => index.UserId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Cohort>> ListForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return [];
        }

        var cohortIds = (await _session.QueryIndex<CohortMemberIndex>(index => index.UserId == userId, Collection).ListAsync(cancellationToken))
            .Select(index => index.CohortId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (cohortIds.Length == 0)
        {
            return [];
        }

        return (await _session.Query<Cohort, CohortIndex>(index => index.CohortId.IsIn(cohortIds), Collection).OrderBy(index => index.NormalizedName).ListAsync(cancellationToken)).ToArray();
    }

    /// <inheritdoc/>
    public async Task<bool> IsMemberAsync(string cohortId, string userId, CancellationToken cancellationToken = default)
        => !string.IsNullOrWhiteSpace(cohortId)
            && !string.IsNullOrWhiteSpace(userId)
            && await _session.QueryIndex<CohortMemberIndex>(index => index.CohortId == cohortId && index.UserId == userId, Collection).CountAsync(cancellationToken) > 0;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> AddMembersAsync(string cohortId, IEnumerable<string> userIds, string actor, CancellationToken cancellationToken = default)
    {
        var cohort = await FindByIdAsync(cohortId, cancellationToken);

        if (cohort is null || userIds is null)
        {
            return [];
        }

        var candidates = userIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.Ordinal).ToList();
        var existing = await ExistingMembersAsync(cohort.CohortId, candidates, cancellationToken);
        var now = _clock.UtcNow;
        var added = new List<string>();

        foreach (var userId in candidates.Where(id => !existing.Contains(id)))
        {
            if (await _userManager.FindByIdAsync(userId) is null)
            {
                continue;
            }

            await _session.SaveAsync(new CohortMember { CohortId = cohort.CohortId, UserId = userId, AddedUtc = now, AddedBy = actor }, false, Collection, cancellationToken);
            added.Add(userId);
        }

        if (added.Count == 0)
        {
            return added;
        }

        await _session.FlushAsync(cancellationToken);

        foreach (var observer in _observers)
        {
            await observer.MembersAddedAsync(cohort, added, actor, cancellationToken);
        }

        return added;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> RemoveMembersAsync(string cohortId, IEnumerable<string> userIds, string actor, CancellationToken cancellationToken = default)
    {
        var cohort = await FindByIdAsync(cohortId, cancellationToken);

        if (cohort is null || userIds is null)
        {
            return [];
        }

        var candidates = userIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.Ordinal).ToList();
        var removed = new List<string>();

        foreach (var batch in candidates.Chunk(BatchSize))
        {
            foreach (var member in await _session.Query<CohortMember, CohortMemberIndex>(index => index.CohortId == cohort.CohortId && index.UserId.IsIn(batch), Collection).ListAsync(cancellationToken))
            {
                _session.Delete(member, Collection);
                removed.Add(member.UserId);
            }
        }

        if (removed.Count == 0)
        {
            return removed;
        }

        await _session.FlushAsync(cancellationToken);

        var distinct = removed.Distinct(StringComparer.Ordinal).ToArray();

        foreach (var observer in _observers)
        {
            await observer.MembersRemovedAsync(cohort, distinct, actor, cancellationToken);
        }

        return distinct;
    }

    /// <inheritdoc/>
    public async Task RemoveFromAllAsync(string userId, string actor, CancellationToken cancellationToken = default)
    {
        foreach (var cohort in await ListForUserAsync(userId, cancellationToken))
        {
            await RemoveMembersAsync(cohort.CohortId, [userId], actor, cancellationToken);
        }
    }

    private async Task<HashSet<string>> ExistingMembersAsync(string cohortId, List<string> userIds, CancellationToken cancellationToken)
    {
        var existing = new HashSet<string>(StringComparer.Ordinal);

        foreach (var batch in userIds.Chunk(BatchSize))
        {
            foreach (var row in await _session.QueryIndex<CohortMemberIndex>(index => index.CohortId == cohortId && index.UserId.IsIn(batch), Collection).ListAsync(cancellationToken))
            {
                existing.Add(row.UserId);
            }
        }

        return existing;
    }

    private async Task<List<string>> ValidateAsync(string cohortId, string name, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(S["The name is required."]);

            return errors;
        }

        if (name.Trim().Length > MaxNameLength)
        {
            errors.Add(S["The name may have at most {0} characters.", MaxNameLength]);
        }

        var other = await FindByNameAsync(name, cancellationToken);

        if (other is not null && !string.Equals(other.CohortId, cohortId, StringComparison.Ordinal))
        {
            errors.Add(S["Another cohort is named '{0}'.", name.Trim()]);
        }

        return errors;
    }
}
