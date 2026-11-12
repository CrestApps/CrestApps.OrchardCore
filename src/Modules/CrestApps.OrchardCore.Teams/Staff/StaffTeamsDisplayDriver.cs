using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Teams.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Teams.Staff;

/// <summary>
/// The Teams card of the staff editor: the teams a person is in, with their role and dates.
/// </summary>
internal sealed class StaffTeamsDisplayDriver : DisplayDriver<StaffMember>
{
    internal const string DriverPrefix = "StaffTeams";

    private const int EmptyMembershipRows = 2;

    private readonly ITeamManager _teamManager;
    private readonly IStaffScopeService _scopeService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public StaffTeamsDisplayDriver(
        ITeamManager teamManager,
        IStaffScopeService scopeService,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<StaffTeamsDisplayDriver> stringLocalizer)
    {
        _teamManager = teamManager;
        _scopeService = scopeService;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gives the Teams card its own prefix, so its fields cannot collide with the staff editor's own.
    /// </summary>
    protected override void BuildPrefix(StaffMember model, string htmlFieldPrefix)
    {
        Prefix = string.IsNullOrEmpty(htmlFieldPrefix) ? DriverPrefix : $"{htmlFieldPrefix}.{DriverPrefix}";
    }

    public override IDisplayResult Display(StaffMember staff, BuildDisplayContext context)
    {
        if (context.DisplayType != StaffProfileSections.DisplayType)
        {
            return null;
        }

        return Initialize<StaffTeamsProfileViewModel>("StaffProfileTeams", async model =>
        {
            var teams = (await _teamManager.ListTreeAsync()).ToDictionary(item => item.Team.ItemId, item => item.Team.Name, StringComparer.Ordinal);

            model.Memberships = [.. staff.TeamMemberships
                .OrderBy(membership => membership.TeamId == staff.PrimaryTeamId ? 0 : 1)
                .ThenByDescending(membership => membership.EffectiveFrom)
                .Select(membership => new StaffTeamProfileRowViewModel
                {
                    TeamName = teams.GetValueOrDefault(membership.TeamId ?? string.Empty, membership.TeamId),
                    Role = membership.Role.ToString(),
                    EffectiveFrom = membership.EffectiveFrom,
                    EffectiveTo = membership.EffectiveTo,
                    IsMain = membership.TeamId == staff.PrimaryTeamId,
                })];
        }).Location(StaffProfileSections.DisplayType, StaffProfileSections.Location(StaffProfileSections.Teams, "1"));
    }

    public override Task<IDisplayResult> EditAsync(StaffMember staff, BuildEditorContext context)
        => EditorAsync(staff, staff.TeamMemberships.Select(membership => new StaffTeamMembershipRowViewModel
        {
            TeamId = membership.TeamId,
            Role = membership.Role.ToString(),
            EffectiveFrom = membership.EffectiveFrom,
            EffectiveTo = membership.EffectiveTo,
        }).ToList());

    public override async Task<IDisplayResult> UpdateAsync(StaffMember staff, UpdateEditorContext context)
    {
        var model = new StaffTeamsEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var memberships = (model.Memberships ?? [])
            .Where(row => !string.IsNullOrEmpty(row.TeamId))
            .Select(row => new TeamMembership
            {
                TeamId = row.TeamId,
                Role = Enum.TryParse<TeamMemberRole>(row.Role, out var role) ? role : TeamMemberRole.Member,
                EffectiveFrom = row.EffectiveFrom,
                EffectiveTo = row.EffectiveTo,
            })
            .ToList();

        // A manager can only add people to the teams they manage; memberships already there stay as they are.
        var scope = await _scopeService.GetScopeAsync(_httpContextAccessor.HttpContext?.User);
        var existing = staff.TeamMemberships.Select(membership => membership.TeamId).ToHashSet(StringComparer.Ordinal);

        if (!scope.All && memberships.Any(membership => !existing.Contains(membership.TeamId) && !scope.CoversTeam(membership.TeamId)))
        {
            context.Updater.ModelState.AddModelError($"{Prefix}.{nameof(StaffTeamsEditViewModel.Memberships)}", S["You can only add people to the teams you manage."]);
        }
        else
        {
            staff.TeamMemberships = memberships;
            staff.PrimaryTeamId = memberships.FirstOrDefault()?.TeamId;
        }

        return await EditorAsync(staff, model.Memberships ?? []);
    }

    private async Task<IDisplayResult> EditorAsync(StaffMember staff, IList<StaffTeamMembershipRowViewModel> rows)
    {
        var scope = await _scopeService.GetScopeAsync(_httpContextAccessor.HttpContext?.User);
        var existingTeams = staff.TeamMemberships.Select(membership => membership.TeamId).ToHashSet(StringComparer.Ordinal);
        var teamOptions = (await _teamManager.ListTreeAsync())
            .Where(item => scope.CoversTeam(item.Team.ItemId) || existingTeams.Contains(item.Team.ItemId))
            .Select(item => new SelectListItem(new string(' ', item.Depth * 4) + item.Team.Name, item.Team.ItemId))
            .ToList();
        var memberships = rows.ToList();

        while (memberships.Count(row => string.IsNullOrEmpty(row.TeamId)) < EmptyMembershipRows)
        {
            memberships.Add(new StaffTeamMembershipRowViewModel { Role = nameof(TeamMemberRole.Member) });
        }

        return Initialize<StaffTeamsEditViewModel>("StaffTeams_Edit", model =>
        {
            model.Memberships = memberships;
            model.TeamOptions = teamOptions;
        }).Location("Content:5");
    }
}
