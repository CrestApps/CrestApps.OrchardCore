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

            if (requirement.Enabled && !string.IsNullOrEmpty(requirement.AudienceName)
                && (_services.GetService<IUserAudienceService>() is not { } audienceService
                    || !(await audienceService.ListAsync(cancellationToken)).Any(audience => string.Equals(audience.Name, requirement.AudienceName, StringComparison.OrdinalIgnoreCase))))
            {
                // The audience was deleted, renamed or its feature turned off: releasing everyone now would require
                // them again, with new due dates and a second notice, when it comes back. The run fails instead.
                _logger.LogWarning("A required learning rule names an audience that cannot be found; the run changed nothing.");

                return new RequiredLearningRunResult(0, 0, 0, 0);
            }

            if (requirement.Enabled)
            {
                members = await PreviewAsync(requirement, cancellationToken);

                var memberSet = members.ToHashSet(StringComparer.Ordinal);
                var all = await _assignments.ListByRequirementAsync(requirement.ItemId, cancellationToken);
                var existing = new Dictionary<string, RequiredLearningAssignment>(StringComparer.Ordinal);

                // Two runs that overlapped may have made two assignments for one person: the earliest is kept and the
                // others are closed, so a person is never reminded twice.
                foreach (var group in all.GroupBy(assignment => assignment.UserId, StringComparer.Ordinal))
                {
                    var ordered = group.OrderBy(assignment => assignment.RequiredUtc).ThenBy(assignment => assignment.ItemId, StringComparer.Ordinal).ToArray();

                    existing[group.Key] = ordered[0];

                    foreach (var duplicate in ordered.Skip(1).Where(assignment => assignment.State != RequiredLearningState.NotRequired))
                    {
                        duplicate.State = RequiredLearningState.NotRequired;
                        duplicate.RemovedUtc = now;
                        duplicate.ModifiedUtc = now;
                        await _assignments.UpdateAsync(duplicate, cancellationToken);
                    }
                }

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
                        assignment.RequiredNotifiedUtc = null;
                        assignment.OverdueNotifiedUtc = null;
                        assignment.StartedUtc = null;
                        assignment.StartError = null;
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
            // Every open assignment whose target shows nothing is started, not only the ones added now, so a run that
            // was cut short, or a start that was refused, is finished by the next run.
            if (requirement.Enabled)
            {
                foreach (var id in (await _assignments.ListByRequirementAsync(requirement.ItemId, cancellationToken)).Where(assignment => assignment.IsOpen).Select(assignment => assignment.ItemId).ToArray())
                {
                    await StartIfNeededAsync(requirement, target, id, actor, cancellationToken);
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
        var (reminderLocker, reminderLocked) = await _distributedLock.TryAcquireLockAsync("LearningRequirement:Reminders", TimeSpan.FromSeconds(5), TimeSpan.FromHours(1));

        if (!reminderLocked)
        {
            _logger.LogInformation("Another reminder pass of required learning is running; this one is skipped.");

            return 0;
        }

        await using (reminderLocker)
        {
            return await SendRemindersCoreAsync(cancellationToken);
        }
    }

    private async Task<int> SendRemindersCoreAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var sent = 0;
        var requirements = new Dictionary<string, LearningRequirement>(StringComparer.Ordinal);
        var observers = _services.GetServices<IRequiredLearningObserver>().ToArray();

        for (var pass = 0; pass < MaxReminderPasses; pass++)
        {
            var page = await _assignments.ListOpenWithDueAsync(pass * PageSize, PageSize, cancellationToken);
            var pageNotices = new List<Func<Task>>();

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

                var notices = new List<Func<Task>>();

                if (due < now)
                {
                    // The target may have been met since the last run, such as a certificate issued this afternoon: it
                    // is asked before anyone is declared overdue.
                    var target = FindTarget(requirement.TargetKind);
                    var status = target is null ? null : await target.GetStatusAsync(requirement.TargetId, assignment.UserId, cancellationToken);

                    if (status?.CompletedUtc is not null)
                    {
                        await RefreshAsync(requirement, target, assignment, cancellationToken);

                        continue;
                    }

                    if (assignment.State != RequiredLearningState.Overdue || assignment.OverdueNotifiedUtc is null)
                    {
                        var tell = assignment.OverdueNotifiedUtc is null;

                        assignment.State = RequiredLearningState.Overdue;
                        assignment.OverdueNotifiedUtc ??= now;
                        changed = true;

                        if (tell)
                        {
                            notices.Add(async () =>
                            {
                                foreach (var observer in observers)
                                {
                                    await observer.OverdueAsync(assignment, requirement, cancellationToken);
                                }
                            });
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
                            notices.Add(async () =>
                            {
                                foreach (var observer in observers)
                                {
                                    await observer.EscalatedAsync(assignment, requirement, managers, cancellationToken);
                                }
                            });
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

                        var daysLeft = Math.Max(0, (int)Math.Ceiling((due - now).TotalDays));

                        notices.Add(async () =>
                        {
                            foreach (var observer in observers)
                            {
                                await observer.DueSoonAsync(assignment, requirement, daysLeft, cancellationToken);
                            }
                        });
                    }
                }

                if (changed)
                {
                    assignment.ModifiedUtc = now;
                    await _assignments.UpdateAsync(assignment, cancellationToken);
                    pageNotices.AddRange(notices);
                }
            }

            // The marks of the page are saved before anyone is told, so a pass that stops half way never tells anyone
            // twice. One save per page: saving inside the loop would make the session forget the rest of the page.
            await _session.SaveChangesAsync(cancellationToken);

            foreach (var notice in pageNotices)
            {
                await notice();
            }

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
            var started = await target.StartAsync(requirement.TargetId, assignment.UserId, assignment.DueUtc, assignment.ItemId, actor, cancellationToken);

            // Starting the target may commit the session: read the state again before changing it.
            assignment = await _assignments.FindByIdAsync(assignmentId, cancellationToken);
            assignment.Exemption = null;
            assignment.State = RequiredLearningState.Required;
            assignment.StartError = started.Succeeded ? null : started.Error ?? "Refused";
            assignment.StartedUtc = started.Succeeded ? assignment.StartedUtc ?? _clock.UtcNow : assignment.StartedUtc;
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
        var overdueNow = state == RequiredLearningState.Overdue && assignment.OverdueNotifiedUtc is null;

        assignment.State = state;
        assignment.CompletedUtc = state == RequiredLearningState.Completed ? status.CompletedUtc : null;
        assignment.ModifiedUtc = _clock.UtcNow;

        if (overdueNow)
        {
            assignment.OverdueNotifiedUtc = _clock.UtcNow;
        }

        await _assignments.UpdateAsync(assignment, cancellationToken);

        if (completedNow)
        {
            foreach (var observer in _services.GetServices<IRequiredLearningObserver>())
            {
                await observer.CompletedAsync(assignment, requirement, cancellationToken);
            }
        }

        // Whoever sees it first, the refresh or the reminder pass, tells the person once.
        if (overdueNow)
        {
            foreach (var observer in _services.GetServices<IRequiredLearningObserver>())
            {
                await observer.OverdueAsync(assignment, requirement, cancellationToken);
            }
        }

        return true;
    }

    // Starts one open assignment when its target shows nothing yet, records what the start did, and tells the person
    // the learning is required once, after it worked. A completion from before counts: nothing is started and nobody is
    // told; the refresh marks the assignment completed.
    private async Task StartIfNeededAsync(LearningRequirement requirement, IRequiredLearningTarget target, string assignmentId, string actor, CancellationToken cancellationToken)
    {
        var assignment = await _assignments.FindByIdAsync(assignmentId, cancellationToken);

        if (assignment is null || !assignment.IsOpen)
        {
            return;
        }

        var status = await target.GetStatusAsync(requirement.TargetId, assignment.UserId, cancellationToken);

        if (status.CompletedUtc is not null)
        {
            return;
        }

        if (!status.Assigned)
        {
            var started = await target.StartAsync(requirement.TargetId, assignment.UserId, assignment.DueUtc, assignment.ItemId, actor, cancellationToken);

            // Starting the target may commit the session: read the assignment again before changing it.
            assignment = await _assignments.FindByIdAsync(assignmentId, cancellationToken);

            if (!started.Succeeded)
            {
                assignment.StartError = started.Error ?? "Refused";
                assignment.ModifiedUtc = _clock.UtcNow;
                await _assignments.UpdateAsync(assignment, cancellationToken);
                await _session.SaveChangesAsync(cancellationToken);
                _logger.LogWarning("Required learning could not start the target of an assignment: {Error}", assignment.StartError);

                return;
            }
        }

        if (assignment.StartedUtc is not null && assignment.StartError is null && assignment.RequiredNotifiedUtc is not null)
        {
            return;
        }

        assignment.StartedUtc ??= _clock.UtcNow;
        assignment.StartError = null;

        var notify = assignment.RequiredNotifiedUtc is null;

        assignment.RequiredNotifiedUtc ??= _clock.UtcNow;
        assignment.ModifiedUtc = _clock.UtcNow;
        await _assignments.UpdateAsync(assignment, cancellationToken);

        // Marked before anyone is told, so a stop here never tells the person twice.
        await _session.SaveChangesAsync(cancellationToken);

        if (notify)
        {
            foreach (var observer in _services.GetServices<IRequiredLearningObserver>())
            {
                await observer.RequiredAsync(assignment, requirement, cancellationToken);
            }
        }
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
