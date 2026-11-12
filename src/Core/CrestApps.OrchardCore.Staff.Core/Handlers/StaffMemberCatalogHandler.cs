using CrestApps.Core;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Staff.Core.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Core.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Staff.Core.Handlers;

/// <summary>
/// Stamps and tidies staff members, takes the name and e-mail of a linked user from the user, validates the record
/// (unique user and employee number, known teams and time zone, a manager that exists and isn't in a loop), records the
/// employment history and tells the <see cref="IStaffLifecycleObserver"/>s what changed.
/// </summary>
internal sealed class StaffMemberCatalogHandler : CatalogEntryHandlerBase<StaffMember>
{
    private readonly StaffMemberStore _store;
    private readonly StaffHistoryStore _historyStore;
    private readonly TeamStore _teamStore;
    private readonly IUserSearchService _userSearchService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly Dictionary<string, StaffChangedContext> _pending = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<StaffHistoryEntry>> _pendingHistory = new(StringComparer.Ordinal);

    internal readonly IStringLocalizer S;

    public StaffMemberCatalogHandler(
        StaffMemberStore store,
        StaffHistoryStore historyStore,
        TeamStore teamStore,
        IUserSearchService userSearchService,
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<StaffMemberCatalogHandler> logger,
        IStringLocalizer<StaffMemberCatalogHandler> stringLocalizer)
    {
        _store = store;
        _historyStore = historyStore;
        _teamStore = teamStore;
        _userSearchService = userSearchService;
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    public override Task InitializedAsync(InitializedContext<StaffMember> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override async Task CreatingAsync(CreatingContext<StaffMember> context, CancellationToken cancellationToken = default)
    {
        Tidy(context.Model);
        await SyncFromUserAsync(context.Model, cancellationToken);
        Track(context.Model, isNew: true);
    }

    public override async Task UpdatingAsync(UpdatingContext<StaffMember> context, CancellationToken cancellationToken = default)
    {
        Tidy(context.Model);
        await SyncFromUserAsync(context.Model, cancellationToken);
        context.Model.ModifiedUtc = _clock.UtcNow;
        Track(context.Model, isNew: false);
    }

    public override async Task ValidatingAsync(ValidatingContext<StaffMember> context, CancellationToken cancellationToken = default)
    {
        var staff = context.Model;

        Tidy(staff);
        await SyncFromUserAsync(staff, cancellationToken);

        if (string.IsNullOrEmpty(staff.DisplayName))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(StaffMember.DisplayName)]));
        }

        if (!string.IsNullOrEmpty(staff.EmployeeNumber) && await _store.FindByEmployeeNumberAsync(staff.EmployeeNumber, cancellationToken) is { } numbered && numbered.ItemId != staff.ItemId)
        {
            context.Result.Fail(new ValidationResult(S["Another staff member has the employee number '{0}'.", staff.EmployeeNumber], [nameof(StaffMember.EmployeeNumber)]));
        }

        if (!string.IsNullOrEmpty(staff.UserId) && await _store.FindByUserIdAsync(staff.UserId, cancellationToken) is { } linked && linked.ItemId != staff.ItemId)
        {
            context.Result.Fail(new ValidationResult(S["That user is already the staff member '{0}'.", linked.DisplayName], [nameof(StaffMember.UserId)]));
        }

        if (!string.IsNullOrEmpty(staff.TimeZoneId) && !CalendarTimeZones.IsValid(staff.TimeZoneId))
        {
            context.Result.Fail(new ValidationResult(S["'{0}' is not a time zone.", staff.TimeZoneId], [nameof(StaffMember.TimeZoneId)]));
        }

        if (staff.HireDate is { } hired && staff.TerminationDate is { } left && left < hired)
        {
            context.Result.Fail(new ValidationResult(S["The last working day is before the hire date."], [nameof(StaffMember.TerminationDate)]));
        }

        if (staff.TeamMemberships.Any(membership => membership.EffectiveFrom is not null && membership.EffectiveTo is not null && membership.EffectiveTo < membership.EffectiveFrom))
        {
            context.Result.Fail(new ValidationResult(S["A team membership ends before it starts."], [nameof(StaffMember.TeamMemberships)]));
        }

        var teamIds = staff.TeamMemberships.Select(membership => membership.TeamId).Append(staff.PrimaryTeamId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

        if (teamIds.Length > 0)
        {
            var known = (await _teamStore.GetAsync(teamIds, cancellationToken)).Select(team => team.ItemId).ToHashSet(StringComparer.Ordinal);

            if (teamIds.Any(id => !known.Contains(id)))
            {
                context.Result.Fail(new ValidationResult(S["A team the staff member belongs to doesn't exist."], [nameof(StaffMember.TeamMemberships)]));
            }
        }

        if (!string.IsNullOrEmpty(staff.ManagerId))
        {
            if (staff.ManagerId == staff.ItemId)
            {
                context.Result.Fail(new ValidationResult(S["A person can't be their own manager."], [nameof(StaffMember.ManagerId)]));
            }
            else if (await _store.FindByIdAsync(staff.ManagerId, cancellationToken) is null)
            {
                context.Result.Fail(new ValidationResult(S["The manager doesn't exist."], [nameof(StaffMember.ManagerId)]));
            }
            else if (await LeadsBackAsync(staff.ItemId, staff.ManagerId, cancellationToken))
            {
                context.Result.Fail(new ValidationResult(S["That manager reports to this person, directly or through others."], [nameof(StaffMember.ManagerId)]));
            }
        }
    }

    public override Task CreatedAsync(CreatedContext<StaffMember> context, CancellationToken cancellationToken = default)
        => CompleteAsync(context.Model, cancellationToken);

    public override Task UpdatedAsync(UpdatedContext<StaffMember> context, CancellationToken cancellationToken = default)
        => CompleteAsync(context.Model, cancellationToken);

    public override async Task DeletedAsync(DeletedContext<StaffMember> context, CancellationToken cancellationToken = default)
    {
        foreach (var observer in _serviceProvider.GetServices<IStaffLifecycleObserver>())
        {
            try
            {
                await observer.DeletedAsync(context.Model, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "A staff observer ({Observer}) failed after a staff member was deleted.", observer.GetType().Name);
            }
        }
    }

    internal static void Tidy(StaffMember staff)
        => StaffRecords.Tidy(staff);

    // A linked person takes their name and e-mail from the user, so they are never typed twice or left stale.
    private async Task SyncFromUserAsync(StaffMember staff, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(staff.UserId))
        {
            return;
        }

        var users = await _userSearchService.GetAsync([staff.UserId], cancellationToken);
        var user = users.Count > 0 ? users[0] : null;

        if (user is null)
        {
            return;
        }

        var name = string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName;

        if (!string.IsNullOrWhiteSpace(name))
        {
            staff.DisplayName = name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            staff.Email = user.Email.Trim();
        }
    }

    // Walks up from the proposed manager: reaching the person means the manager reports to them.
    private async Task<bool> LeadsBackAsync(string staffId, string managerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(staffId))
        {
            return false;
        }

        var managers = await _store.GetManagersAsync(cancellationToken);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var current = managerId; !string.IsNullOrEmpty(current) && seen.Add(current); current = managers.GetValueOrDefault(current))
        {
            if (current == staffId)
            {
                return true;
            }
        }

        return false;
    }

    private void Track(StaffMember staff, bool isNew)
    {
        var previous = staff.TryGet<StaffSavedState>(out var state) ? state : null;
        var current = StaffSavedState.From(staff);
        var now = _clock.UtcNow;
        var changedBy = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var history = new List<StaffHistoryEntry>();

        void Add(string field, string from, string to)
        {
            history.Add(new StaffHistoryEntry
            {
                ItemId = global::OrchardCore.IdGenerator.GenerateId(),
                StaffMemberId = staff.ItemId,
                Field = field,
                From = from,
                To = to,
                ChangedByUserId = changedBy,
                ChangedUtc = now,
            });
        }

        if (isNew || previous is null)
        {
            if (isNew)
            {
                Add(StaffHistoryFields.Created, null, current.Status.ToString());
            }
        }
        else
        {
            AddIfChanged(StaffHistoryFields.JobTitle, previous.JobTitle, current.JobTitle);
            AddIfChanged(StaffHistoryFields.Status, previous.Status.ToString(), current.Status.ToString());
            AddIfChanged(StaffHistoryFields.EmploymentType, previous.EmploymentType.ToString(), current.EmploymentType.ToString());
            AddIfChanged(StaffHistoryFields.Manager, previous.ManagerId, current.ManagerId);
            AddIfChanged(StaffHistoryFields.Teams, previous.Teams, current.Teams);
            AddIfChanged(StaffHistoryFields.WorkLocation, previous.WorkLocation, current.WorkLocation);
            AddIfChanged(StaffHistoryFields.TerminationDate, previous.TerminationDate, current.TerminationDate);
        }

        void AddIfChanged(string field, string from, string to)
        {
            if (!string.Equals(from ?? string.Empty, to ?? string.Empty, StringComparison.Ordinal))
            {
                Add(field, from, to);
            }
        }

        var wasWorking = !isNew && previous is not null && previous.Status is StaffStatus.Active or StaffStatus.OnLeave;
        var isWorking = current.Status is StaffStatus.Active or StaffStatus.OnLeave;

        _pending[staff.ItemId] = new StaffChangedContext
        {
            StaffMember = staff,
            IsNew = isNew,
            PreviousStatus = previous?.Status,
            PreviousUserId = isNew ? null : previous?.UserId ?? staff.UserId,
            PreviousManagerId = isNew ? null : previous?.ManagerId ?? staff.ManagerId,
            TeamsChanged = isNew ? staff.TeamMemberships.Count > 0 : previous is not null && !string.Equals(previous.Teams, current.Teams, StringComparison.Ordinal),
            Terminated = !isNew && previous is not null && previous.Status != StaffStatus.Terminated && current.Status == StaffStatus.Terminated,
            Activated = isWorking && (isNew || (previous is not null && !wasWorking)),
        };
        _pendingHistory[staff.ItemId] = history;

        staff.Put(current);
    }

    private async Task CompleteAsync(StaffMember staff, CancellationToken cancellationToken)
    {
        if (_pendingHistory.Remove(staff.ItemId, out var history))
        {
            foreach (var entry in history)
            {
                await _historyStore.CreateAsync(entry, cancellationToken);
            }
        }

        if (!_pending.Remove(staff.ItemId, out var change))
        {
            return;
        }

        foreach (var observer in _serviceProvider.GetServices<IStaffLifecycleObserver>())
        {
            try
            {
                await observer.ChangedAsync(change, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // One module failing to react must not undo the change or stop the others.
                _logger.LogError(exception, "A staff observer ({Observer}) failed after a staff member was saved.", observer.GetType().Name);
            }
        }
    }
}

/// <summary>
/// The employment values of a staff member as last saved, kept on the record so the next save can tell what changed
/// however the record was loaded.
/// </summary>
public sealed class StaffSavedState
{
    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public StaffStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the employment type.
    /// </summary>
    public EmploymentType EmploymentType { get; set; }

    /// <summary>
    /// Gets or sets the user.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the manager.
    /// </summary>
    public string ManagerId { get; set; }

    /// <summary>
    /// Gets or sets the job title.
    /// </summary>
    public string JobTitle { get; set; }

    /// <summary>
    /// Gets or sets the team memberships, as text.
    /// </summary>
    public string Teams { get; set; }

    /// <summary>
    /// Gets or sets the work location.
    /// </summary>
    public string WorkLocation { get; set; }

    /// <summary>
    /// Gets or sets the termination date, as text.
    /// </summary>
    public string TerminationDate { get; set; }

    /// <summary>
    /// Reads the values from a staff member.
    /// </summary>
    /// <param name="staff">The staff member.</param>
    /// <returns>The values.</returns>
    public static StaffSavedState From(StaffMember staff)
        => new()
        {
            Status = staff.Status,
            EmploymentType = staff.EmploymentType,
            UserId = staff.UserId,
            ManagerId = staff.ManagerId,
            JobTitle = staff.JobTitle,
            Teams = string.Join("; ", staff.TeamMemberships
                .OrderBy(membership => membership.TeamId, StringComparer.Ordinal)
                .ThenBy(membership => membership.EffectiveFrom)
                .Select(membership => $"{membership.TeamId} {membership.Role} {membership.EffectiveFrom:yyyy-MM-dd}..{membership.EffectiveTo:yyyy-MM-dd}")),
            WorkLocation = staff.WorkLocation,
            TerminationDate = staff.TerminationDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        };
}

/// <summary>
/// Helpers for staff records that the handler and migrations share.
/// </summary>
public static class StaffRecords
{
    /// <summary>
    /// Trims a staff member's values, drops empty ones and keeps the main team among the memberships.
    /// </summary>
    /// <param name="staff">The staff member.</param>
    public static void Tidy(StaffMember staff)
    {
        staff.DisplayName = staff.DisplayName?.Trim();
        staff.EmployeeNumber = Blank(staff.EmployeeNumber);
        staff.ExternalId = Blank(staff.ExternalId);
        staff.JobTitle = Blank(staff.JobTitle);
        staff.Email = Blank(staff.Email);
        staff.Phone = Blank(staff.Phone);
        staff.UserId = Blank(staff.UserId);
        staff.ManagerId = Blank(staff.ManagerId);
        staff.WorkLocation = Blank(staff.WorkLocation);
        staff.TimeZoneId = CalendarTimeZones.Normalize(staff.TimeZoneId);
        staff.TeamMemberships = (staff.TeamMemberships ?? []).Where(membership => !string.IsNullOrEmpty(membership?.TeamId)).ToList();
        staff.Capabilities = (staff.Capabilities ?? []).Where(capability => !string.IsNullOrWhiteSpace(capability)).Select(capability => capability.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (staff.HomeBase is not null && string.IsNullOrWhiteSpace(staff.HomeBase.Address) && (staff.HomeBase.Latitude is null || staff.HomeBase.Longitude is null))
        {
            staff.HomeBase = null;
        }

        if (string.IsNullOrEmpty(staff.PrimaryTeamId) || !staff.TeamMemberships.Any(membership => membership.TeamId == staff.PrimaryTeamId))
        {
            staff.PrimaryTeamId = staff.TeamMemberships.FirstOrDefault()?.TeamId ?? (staff.TeamMemberships.Count == 0 ? null : staff.PrimaryTeamId);
        }
    }

    private static string Blank(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
