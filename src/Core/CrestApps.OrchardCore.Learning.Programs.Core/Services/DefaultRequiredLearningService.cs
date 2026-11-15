using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Users;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Programs.Core.Services;

/// <summary>
/// Applies required learning rules to the people of their audience and named people.
/// </summary>
public sealed class DefaultRequiredLearningService : IRequiredLearningService
{
    private const int PageSize = 500;
    private const int MaxReminderPasses = 200;

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(10);

    private readonly LearningRequirementStore _requirements;
    private readonly RequiredLearningAssignmentStore _assignments;
    private readonly IEnumerable<IRequiredLearningTarget> _targets;
    private readonly IServiceProvider _services;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRequiredLearningService"/> class.
    /// </summary>
    public DefaultRequiredLearningService(
        LearningRequirementStore requirements,
        RequiredLearningAssignmentStore assignments,
        IEnumerable<IRequiredLearningTarget> targets,
        IServiceProvider services,
        IDistributedLock distributedLock,
        ISession session,
        IClock clock,
        ILocalClock localClock,
        ILogger<DefaultRequiredLearningService> logger,
        IStringLocalizer<DefaultRequiredLearningService> stringLocalizer)
    {
        _requirements = requirements;
        _assignments = assignments;
        _targets = targets;
        _services = services;
        _distributedLock = distributedLock;
        _session = session;
        _clock = clock;
        _localClock = localClock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> SaveAsync(LearningRequirement requirement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        var errors = new List<string>();

        requirement.Name = requirement.Name?.Trim();
        requirement.AudienceName = string.IsNullOrWhiteSpace(requirement.AudienceName) ? null : requirement.AudienceName.Trim();
        requirement.UserIds = requirement.UserIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList() ?? [];
        requirement.ReminderDays = requirement.ReminderDays?.Where(days => days > 0).Distinct().OrderDescending().ToList() ?? [];

        if (string.IsNullOrEmpty(requirement.Name))
        {
            errors.Add(S["The name is required."]);
        }

        var target = FindTarget(requirement.TargetKind);

        if (target is null)
        {
            errors.Add(S["Choose what is required."]);
        }
        else if (string.IsNullOrEmpty(requirement.TargetId) || await target.GetDisplayTextAsync(requirement.TargetId, cancellationToken) is null)
        {
            errors.Add(S["Choose the {0} that is required.", target.DisplayName]);
        }

        if (requirement.AudienceName is null && requirement.UserIds.Count == 0)
        {
            errors.Add(S["Choose an audience or people."]);
        }
        else if (requirement.AudienceName is not null
            && _services.GetService<IUserAudienceService>() is { } audiences
            && !(await audiences.ListAsync(cancellationToken)).Any(audience => string.Equals(audience.Name, requirement.AudienceName, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add(S["The audience '{0}' was not found.", requirement.AudienceName]);
        }

        if (requirement.DueRule == RequiredLearningDueRule.FixedDate && requirement.DueDate is null)
        {
            errors.Add(S["Choose the due date."]);
        }

        if (requirement.DueRule is RequiredLearningDueRule.DaysAfterRequired or RequiredLearningDueRule.DaysAfterHire && requirement.DueDays < 0)
        {
            errors.Add(S["The number of days can't be negative."]);
        }

        if (requirement.EscalateAfterDays < 0)
        {
            errors.Add(S["The days before escalation can't be negative."]);
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        if (string.IsNullOrEmpty(requirement.ItemId) || await _requirements.FindByIdAsync(requirement.ItemId, cancellationToken) is null)
        {
            requirement.CreatedUtc = _clock.UtcNow;
            await _requirements.CreateAsync(requirement, cancellationToken);
        }
        else
        {
            requirement.ModifiedUtc = _clock.UtcNow;
            await _requirements.UpdateAsync(requirement, cancellationToken);
        }

        return errors;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(string requirementId, CancellationToken cancellationToken = default)
    {
        var requirement = await _requirements.FindByIdAsync(requirementId, cancellationToken);

        if (requirement is null)
        {
            return false;
        }

        foreach (var assignment in await _assignments.ListByRequirementAsync(requirementId, cancellationToken))
        {
            await _assignments.DeleteAsync(assignment, cancellationToken);
        }

        return await _requirements.DeleteAsync(requirement, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<LearningRequirement> FindByIdAsync(string requirementId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(requirementId) ? null : await _requirements.FindByIdAsync(requirementId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<LearningRequirement>> ListAsync(CancellationToken cancellationToken = default)
        => _requirements.ListAllAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> PreviewAsync(LearningRequirement requirement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        var members = new HashSet<string>(StringComparer.Ordinal);

        if (!string.IsNullOrEmpty(requirement.AudienceName) && _services.GetService<IUserAudienceService>() is { } audiences)
        {
            for (var skip = 0; ; skip += PageSize)
            {
                var (count, userIds) = await audiences.GetMembersAsync(requirement.AudienceName, skip, PageSize, cancellationToken);

                members.UnionWith(userIds);

                if (userIds.Count < PageSize || skip + PageSize >= count)
                {
                    break;
                }
            }
        }

        if (requirement.UserIds.Count > 0)
        {
            var userManager = _services.GetRequiredService<UserManager<IUser>>();

            foreach (var userId in requirement.UserIds)
            {
                if (await userManager.FindByIdAsync(userId) is not null)
                {
                    members.Add(userId);
                }
            }
        }

        return members.Order(StringComparer.Ordinal).ToArray();
    }

    /// <inheritdoc/>
    public async Task<RequiredLearningRunResult> RunAsync(string requirementId, string actor, CancellationToken cancellationToken = default)
    {
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync($"LearningRequirement:{requirementId}", _lockTimeout, _lockExpiration);

        if (!locked)
        {
            _logger.LogWarning("A required learning rule is already running; this run is skipped.");

            return new RequiredLearningRunResult(0, 0, 0, 0);
        }

        await using (locker)
        {
            var requirement = await FindByIdAsync(requirementId, cancellationToken);
            var target = FindTarget(requirement?.TargetKind);

            if (requirement is null || target is null)
            {
                return new RequiredLearningRunResult(0, 0, 0, 0);
            }

            var now = _clock.UtcNow;
            var added = new List<RequiredLearningAssignment>();
            var removed = 0;
            IReadOnlyList<string> members = [];

            if (requirement.Enabled)
            {
                members = await PreviewAsync(requirement, cancellationToken);

                var memberSet = members.ToHashSet(StringComparer.Ordinal);
                var existing = (await _assignments.ListByRequirementAsync(requirement.ItemId, cancellationToken)).ToDictionary(assignment => assignment.UserId, StringComparer.Ordinal);

                foreach (var userId in members)
                {
                    if (!existing.TryGetValue(userId, out var assignment))
                    {
                        assignment = new RequiredLearningAssignment
                        {
                            RequirementId = requirement.ItemId,
                            UserId = userId,
                            State = RequiredLearningState.Required,
                            RequiredUtc = now,
                            DueUtc = await DueAsync(requirement, userId, now, cancellationToken),
                        };

                        await _assignments.CreateAsync(assignment, cancellationToken);
                        added.Add(assignment);
                    }
                    else if (assignment.State == RequiredLearningState.NotRequired)
                    {
                        // Back in the audience: required again, with a due date counted from today.
                        assignment.State = RequiredLearningState.Required;
                        assignment.RemovedUtc = null;
                        assignment.RequiredUtc = now;
                        assignment.DueUtc = await DueAsync(requirement, userId, now, cancellationToken);
                        assignment.RemindersSent = [];
                        assignment.EscalatedUtc = null;
                        assignment.ModifiedUtc = now;
                        await _assignments.UpdateAsync(assignment, cancellationToken);
                        added.Add(assignment);
                    }
                }

                foreach (var assignment in existing.Values.Where(assignment => assignment.IsOpen && !memberSet.Contains(assignment.UserId)))
                {
                    assignment.State = RequiredLearningState.NotRequired;
                    assignment.RemovedUtc = now;
                    assignment.ModifiedUtc = now;
                    await _assignments.UpdateAsync(assignment, cancellationToken);
                    removed++;
                }
            }

            requirement.LastRunUtc = now;
            requirement.LastRunCovered = requirement.Enabled ? members.Count : requirement.LastRunCovered;
            await _requirements.UpdateAsync(requirement, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            // Starting a person on the target enrolls them, which commits the session: everything above is saved first.
            foreach (var assignment in added)
            {
                await target.StartAsync(requirement.TargetId, assignment.UserId, assignment.DueUtc, assignment.ItemId, actor, cancellationToken);

                foreach (var observer in _services.GetServices<IRequiredLearningObserver>())
                {
                    await observer.RequiredAsync(assignment, requirement, cancellationToken);
                }
            }

            var changed = 0;

            foreach (var assignment in await _assignments.ListByRequirementAsync(requirement.ItemId, cancellationToken))
            {
                if (await RefreshAsync(requirement, target, assignment, cancellationToken))
                {
                    changed++;
                }
            }

            await _session.SaveChangesAsync(cancellationToken);

            return new RequiredLearningRunResult(members.Count, added.Count, removed, changed);
        }
    }

    /// <inheritdoc/>
    public async Task RefreshTargetAsync(string targetKind, string targetId, string userId, CancellationToken cancellationToken = default)
    {
        var target = FindTarget(targetKind);

        if (target is null || string.IsNullOrEmpty(targetId) || string.IsNullOrEmpty(userId))
        {
            return;
        }

        var changed = false;

        foreach (var requirement in await _requirements.ListByTargetAsync(targetKind, targetId, cancellationToken))
        {
            var assignment = await _assignments.FindAsync(requirement.ItemId, userId, cancellationToken);

            if (assignment is not null && await RefreshAsync(requirement, target, assignment, cancellationToken))
            {
                changed = true;
            }
        }

        if (changed)
        {
            await _session.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<int> SendRemindersAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var sent = 0;
        var requirements = new Dictionary<string, LearningRequirement>(StringComparer.Ordinal);
        var observers = _services.GetServices<IRequiredLearningObserver>().ToArray();

        for (var pass = 0; pass < MaxReminderPasses; pass++)
        {
            var page = await _assignments.ListOpenWithDueAsync(pass * PageSize, PageSize, cancellationToken);

            foreach (var assignment in page)
            {
                if (!requirements.TryGetValue(assignment.RequirementId, out var requirement))
                {
                    requirement = await _requirements.FindByIdAsync(assignment.RequirementId, cancellationToken);
                    requirements[assignment.RequirementId] = requirement;
                }

                if (requirement is null || !requirement.Enabled || assignment.DueUtc is not { } due)
                {
                    continue;
                }

                var changed = false;

                if (due < now)
                {
                    if (assignment.State != RequiredLearningState.Overdue)
                    {
                        assignment.State = RequiredLearningState.Overdue;
                        changed = true;

                        foreach (var observer in observers)
                        {
                            await observer.OverdueAsync(assignment, requirement, cancellationToken);
                        }
                    }

                    if (requirement.EscalateAfterDays is { } escalateAfter && assignment.EscalatedUtc is null && now >= due.AddDays(escalateAfter))
                    {
                        var managers = await ManagerUserIdsAsync(assignment.UserId, cancellationToken);

                        assignment.EscalatedUtc = now;
                        changed = true;

                        if (managers.Count > 0)
                        {
                            sent++;

                            foreach (var observer in observers)
                            {
                                await observer.EscalatedAsync(assignment, requirement, managers, cancellationToken);
                            }
                        }
                    }
                }
                else
                {
                    // Only the nearest reminder is sent; the earlier ones a late run missed are marked as sent.
                    var dueReminders = requirement.ReminderDays.Where(days => !assignment.RemindersSent.Contains(days) && now >= due.AddDays(-days)).Order().ToArray();

                    if (dueReminders.Length > 0)
                    {
                        assignment.RemindersSent.AddRange(dueReminders);
                        changed = true;
                        sent++;

                        foreach (var observer in observers)
                        {
                            await observer.DueSoonAsync(assignment, requirement, dueReminders[0], cancellationToken);
                        }
                    }
                }

                if (changed)
                {
                    assignment.ModifiedUtc = now;
                    await _assignments.UpdateAsync(assignment, cancellationToken);
                }
            }

            await _session.SaveChangesAsync(cancellationToken);

            if (page.Count < PageSize)
            {
                break;
            }
        }

        return sent;
    }

    /// <inheritdoc/>
    public async Task<RequiredLearningAssignment> ExemptAsync(string assignmentId, RequiredLearningExemption exemption, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exemption);

        var assignment = await FindAssignmentAsync(assignmentId, cancellationToken);

        if (assignment is null || assignment.State is RequiredLearningState.Completed or RequiredLearningState.NotRequired)
        {
            return null;
        }

        exemption.GrantedUtc = _clock.UtcNow;
        exemption.Reason = exemption.Reason?.Trim();
        assignment.Exemption = exemption;
        assignment.State = RequiredLearningState.Exempt;
        assignment.ModifiedUtc = _clock.UtcNow;
        await _assignments.UpdateAsync(assignment, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        var requirement = await FindByIdAsync(assignment.RequirementId, cancellationToken);

        foreach (var observer in _services.GetServices<IRequiredLearningObserver>())
        {
            await observer.ExemptedAsync(assignment, requirement, cancellationToken);
        }

        return assignment;
    }

    /// <inheritdoc/>
    public async Task<RequiredLearningAssignment> RemoveExemptionAsync(string assignmentId, string actor, CancellationToken cancellationToken = default)
    {
        var assignment = await FindAssignmentAsync(assignmentId, cancellationToken);

        if (assignment is null || assignment.State != RequiredLearningState.Exempt)
        {
            return null;
        }

        var requirement = await FindByIdAsync(assignment.RequirementId, cancellationToken);
        var target = FindTarget(requirement?.TargetKind);

        assignment.Exemption = null;
        assignment.State = RequiredLearningState.Required;

        if (requirement is not null && target is not null)
        {
            await target.StartAsync(requirement.TargetId, assignment.UserId, assignment.DueUtc, assignment.ItemId, actor, cancellationToken);

            // Starting the target may commit the session: read the state again before changing it.
            assignment = await _assignments.FindByIdAsync(assignmentId, cancellationToken);
            assignment.Exemption = null;
            assignment.State = RequiredLearningState.Required;
            await RefreshAsync(requirement, target, assignment, cancellationToken, force: true);
        }
        else
        {
            await _assignments.UpdateAsync(assignment, cancellationToken);
        }

        await _session.SaveChangesAsync(cancellationToken);

        return assignment;
    }

    /// <inheritdoc/>
    public async Task<RequiredLearningAssignment> FindAssignmentAsync(string assignmentId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(assignmentId) ? null : await _assignments.FindByIdAsync(assignmentId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<RequiredLearningAssignment>> ListForUsersAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default)
        => _assignments.ListByUsersAsync(userIds ?? [], cancellationToken);

    /// <inheritdoc/>
    public Task<(int Count, IReadOnlyList<RequiredLearningAssignment> Items)> PageAsync(string requirementId, RequiredLearningState? state, int skip, int take, CancellationToken cancellationToken = default)
        => _assignments.PageAsync(requirementId, state, skip, take, cancellationToken);

    /// <summary>
    /// Works out the state from the target: completed, overdue, started or only required.
    /// </summary>
    internal static RequiredLearningState StateFor(RequiredLearningAssignment assignment, RequiredLearningTargetStatus status, DateTime now)
    {
        if (assignment.State is RequiredLearningState.Exempt or RequiredLearningState.NotRequired)
        {
            return assignment.State;
        }

        if (status.CompletedUtc is not null)
        {
            return RequiredLearningState.Completed;
        }

        if (assignment.DueUtc is { } due && due < now)
        {
            return RequiredLearningState.Overdue;
        }

        return status.Started ? RequiredLearningState.InProgress : RequiredLearningState.Required;
    }

    private async Task<bool> RefreshAsync(LearningRequirement requirement, IRequiredLearningTarget target, RequiredLearningAssignment assignment, CancellationToken cancellationToken, bool force = false)
    {
        if (!force && assignment.State is RequiredLearningState.Exempt or RequiredLearningState.NotRequired or RequiredLearningState.Completed)
        {
            return false;
        }

        var status = await target.GetStatusAsync(requirement.TargetId, assignment.UserId, cancellationToken);
        var state = StateFor(assignment, status, _clock.UtcNow);

        if (!force && state == assignment.State)
        {
            return false;
        }

        var completedNow = state == RequiredLearningState.Completed && assignment.State != RequiredLearningState.Completed;

        assignment.State = state;
        assignment.CompletedUtc = state == RequiredLearningState.Completed ? status.CompletedUtc : null;
        assignment.ModifiedUtc = _clock.UtcNow;
        await _assignments.UpdateAsync(assignment, cancellationToken);

        if (completedNow)
        {
            foreach (var observer in _services.GetServices<IRequiredLearningObserver>())
            {
                await observer.CompletedAsync(assignment, requirement, cancellationToken);
            }
        }

        return true;
    }

    private async Task<DateTime?> DueAsync(LearningRequirement requirement, string userId, DateTime requiredUtc, CancellationToken cancellationToken)
    {
        DateOnly? dueDay = requirement.DueRule switch
        {
            RequiredLearningDueRule.FixedDate => requirement.DueDate,
            RequiredLearningDueRule.DaysAfterRequired => (await LocalDayAsync(requiredUtc)).AddDays(requirement.DueDays),
            RequiredLearningDueRule.DaysAfterHire => (await HireDateAsync(userId, cancellationToken) ?? await LocalDayAsync(requiredUtc)).AddDays(requirement.DueDays),
            _ => null,
        };

        if (dueDay is not { } day)
        {
            return null;
        }

        // Due at the last second of the day in the site's time zone; enrollments keep due dates to the second.
        return await _localClock.ConvertToUtcAsync(day.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Unspecified));
    }

    private async Task<DateOnly> LocalDayAsync(DateTime utc)
        => DateOnly.FromDateTime((await _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)))).DateTime);

    private async Task<DateOnly?> HireDateAsync(string userId, CancellationToken cancellationToken)
        => _services.GetService<IStaffMemberManager>() is { } staff ? (await staff.FindByUserIdAsync(userId, cancellationToken))?.HireDate : null;

    private async Task<IReadOnlyList<string>> ManagerUserIdsAsync(string userId, CancellationToken cancellationToken)
    {
        if (_services.GetService<IStaffMemberManager>() is not { } staff)
        {
            return [];
        }

        var member = await staff.FindByUserIdAsync(userId, cancellationToken);

        if (string.IsNullOrEmpty(member?.ManagerId))
        {
            return [];
        }

        var manager = await staff.FindByIdAsync(member.ManagerId, cancellationToken);

        return string.IsNullOrEmpty(manager?.UserId) ? [] : [manager.UserId];
    }

    private IRequiredLearningTarget FindTarget(string kind)
        => string.IsNullOrEmpty(kind) ? null : _targets.FirstOrDefault(target => target.Kind == kind);
}
