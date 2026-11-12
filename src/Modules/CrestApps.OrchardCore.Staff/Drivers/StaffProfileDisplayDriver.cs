using System.Security.Claims;
using CrestApps.OrchardCore.Staff.Core.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Staff.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Staff.Drivers;

/// <summary>
/// Staff's own parts of the staff profile: the header, the person and their employment, the reporting line and the
/// employment history. Other modules add their sections with their own drivers.
/// </summary>
internal sealed class StaffProfileDisplayDriver : DisplayDriver<StaffMember>
{
    private const int HistoryRows = 50;

    private readonly IStaffHierarchy _hierarchy;
    private readonly IStaffHistoryService _historyService;
    private readonly IStaffDirectory _directory;
    private readonly IStaffScopeService _scopeService;
    private readonly IUserSearchService _userSearchService;
    private readonly StaffTeamReader _teams;
    private readonly ILocalClock _localClock;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public StaffProfileDisplayDriver(
        IStaffHierarchy hierarchy,
        IStaffHistoryService historyService,
        IStaffDirectory directory,
        IStaffScopeService scopeService,
        IUserSearchService userSearchService,
        StaffTeamReader teams,
        ILocalClock localClock,
        IHttpContextAccessor httpContextAccessor)
    {
        _hierarchy = hierarchy;
        _historyService = historyService;
        _directory = directory;
        _scopeService = scopeService;
        _userSearchService = userSearchService;
        _teams = teams;
        _localClock = localClock;
        _httpContextAccessor = httpContextAccessor;
    }

    public override IDisplayResult Display(StaffMember staff, BuildDisplayContext context)
    {
        if (context.DisplayType != StaffProfileSections.DisplayType)
        {
            return null;
        }

        return Combine(
            Initialize<StaffProfileHeaderViewModel>("StaffProfileHeader", async model =>
            {
                var users = string.IsNullOrEmpty(staff.UserId) ? [] : await _userSearchService.GetAsync([staff.UserId]);
                var user = users.Count > 0 ? users[0] : null;
                var teams = await _teams.ListTreeAsync();
                var principal = _httpContextAccessor.HttpContext?.User;

                model.StaffMember = staff;
                model.AvatarUrl = user?.AvatarUrl;
                model.UserName = user?.UserName;
                model.Initials = StaffOrgChartNode.InitialsOf(staff.DisplayName);
                model.TeamName = staff.PrimaryTeamId is { } teamId ? teams.FirstOrDefault(item => item.Team.ItemId == teamId).Team?.Name : null;
                model.CanEdit = principal is not null && await _scopeService.CanManageAsync(principal, staff);
            }).Location(StaffProfileSections.DisplayType, "Header:1"),
            View("StaffProfileDetails", staff)
                .Location(StaffProfileSections.DisplayType, StaffProfileSections.Location(StaffProfileSections.Overview, "1")),
            Initialize<StaffReportingLineViewModel>("StaffProfileReportingLine", async model =>
            {
                var principal = _httpContextAccessor.HttpContext?.User;

                model.StaffMemberId = staff.ItemId;
                model.ManagerChain = [.. await _hierarchy.GetManagerChainAsync(staff.ItemId)];
                model.Reports = [.. await _hierarchy.GetReportsAsync(staff.ItemId, includeIndirect: false)];
                model.CanAddReports = principal is not null && !(await _scopeService.GetScopeAsync(principal)).IsEmpty;
            }).Location(StaffProfileSections.DisplayType, StaffProfileSections.Location(StaffProfileSections.Overview, "5")),
            Initialize<StaffHistoryViewModel>("StaffProfileHistory", async model => model.History = await HistoryAsync(staff))
                .Location(StaffProfileSections.DisplayType, StaffProfileSections.Location(StaffProfileSections.History, "1")));
    }

    private async Task<List<StaffHistoryRowViewModel>> HistoryAsync(StaffMember staff)
    {
        var history = await _historyService.ListAsync(staff.ItemId, HistoryRows);
        var userIds = history.Select(entry => entry.ChangedByUserId).Where(userId => !string.IsNullOrEmpty(userId)).Distinct(StringComparer.Ordinal).ToArray();
        var users = (await _userSearchService.GetAsync(userIds)).ToDictionary(user => user.UserId, user => string.IsNullOrEmpty(user.DisplayName) ? user.UserName : user.DisplayName, StringComparer.Ordinal);
        var people = await _directory.GetNamesAsync(history.Where(entry => entry.Field == StaffHistoryFields.Manager).SelectMany(entry => new[] { entry.From, entry.To }));
        var teamNames = history.Any(entry => entry.Field == StaffHistoryFields.Teams)
            ? (await _teams.ListTreeAsync()).ToDictionary(item => item.Team.ItemId, item => item.Team.Name, StringComparer.Ordinal)
            : [];

        string Show(string field, string value) => field switch
        {
            StaffHistoryFields.Manager => people.GetValueOrDefault(value ?? string.Empty, value),

            // Team memberships are kept as "teamId Role from..to" parts; the team is shown by its name.
            StaffHistoryFields.Teams when !string.IsNullOrEmpty(value) => string.Join("; ", value.Split("; ", StringSplitOptions.RemoveEmptyEntries).Select(part =>
            {
                var space = part.IndexOf(' ');
                var teamId = space < 0 ? part : part[..space];

                return teamNames.GetValueOrDefault(teamId, teamId) + (space < 0 ? string.Empty : part[space..]);
            })),
            _ => value,
        };

        var rows = new List<StaffHistoryRowViewModel>(history.Count);

        foreach (var entry in history)
        {
            rows.Add(new StaffHistoryRowViewModel
            {
                Field = entry.Field,
                From = Show(entry.Field, entry.From),
                To = Show(entry.Field, entry.To),
                ChangedBy = entry.ChangedByUserId is { } userId ? users.GetValueOrDefault(userId, userId) : null,
                ChangedLocal = (await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(entry.ChangedUtc, DateTimeKind.Utc))).DateTime,
            });
        }

        return rows;
    }
}
