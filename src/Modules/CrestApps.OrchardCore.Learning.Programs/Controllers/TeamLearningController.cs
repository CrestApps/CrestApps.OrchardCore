using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Programs.Core;
using CrestApps.OrchardCore.Learning.Programs.Drivers;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using CrestApps.OrchardCore.Learning.Programs.ViewModels;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Programs.Controllers;

/// <summary>
/// The required learning of the people who report to the signed-in manager, directly or not. It shows states and due
/// dates only, never answers or attempts.
/// </summary>
[Feature(ProgramsConstants.Features.TeamLearning)]
public sealed class TeamLearningController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IStaffMemberManager _staff;
    private readonly IStaffHierarchy _hierarchy;
    private readonly IRequiredLearningService _requiredLearning;
    private readonly IEnumerable<IRequiredLearningTarget> _targets;
    private readonly ILocalClock _localClock;

    public TeamLearningController(
        IAuthorizationService authorizationService,
        IStaffMemberManager staff,
        IStaffHierarchy hierarchy,
        IRequiredLearningService requiredLearning,
        IEnumerable<IRequiredLearningTarget> targets,
        ILocalClock localClock)
    {
        _authorizationService = authorizationService;
        _staff = staff;
        _hierarchy = hierarchy;
        _requiredLearning = requiredLearning;
        _targets = targets;
        _localClock = localClock;
    }

    /// <summary>
    /// Lists the team and their required learning.
    /// </summary>
    [Admin("learning/team", "TeamLearning")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningProgramsPermissions.ViewTeamLearning))
        {
            return Forbid();
        }

        var me = await _staff.FindByUserIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.RequestAborted);
        var rows = new List<TeamLearningRow>();

        if (me is not null)
        {
            var reports = (await _hierarchy.GetReportsAsync(me.ItemId, includeIndirect: true, HttpContext.RequestAborted))
                .Where(member => !string.IsNullOrEmpty(member.UserId))
                .ToArray();
            var assignments = (await _requiredLearning.ListForUsersAsync(reports.Select(member => member.UserId).ToArray(), HttpContext.RequestAborted))
                .Where(assignment => assignment.State != RequiredLearningState.NotRequired)
                .ToLookup(assignment => assignment.UserId, StringComparer.Ordinal);
            var names = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var member in reports.OrderBy(member => member.DisplayName, StringComparer.CurrentCultureIgnoreCase))
            {
                var items = new List<TeamLearningItem>();

                foreach (var assignment in assignments[member.UserId])
                {
                    if (!names.TryGetValue(assignment.RequirementId, out var name))
                    {
                        var rule = await _requiredLearning.FindByIdAsync(assignment.RequirementId, HttpContext.RequestAborted);
                        var target = _targets.FirstOrDefault(candidate => candidate.Kind == rule?.TargetKind);

                        name = rule is null ? assignment.RequirementId : target is null ? rule.Name : await target.GetDisplayTextAsync(rule.TargetId, HttpContext.RequestAborted) ?? rule.Name;
                        names[assignment.RequirementId] = name;
                    }

                    items.Add(new TeamLearningItem { Name = name, State = assignment.State, Due = await ProgramsDates.FormatAsync(_localClock, assignment.DueUtc) });
                }

                rows.Add(new TeamLearningRow
                {
                    UserId = member.UserId,
                    Name = member.DisplayName,
                    JobTitle = member.JobTitle,
                    Items = items.OrderBy(item => item.State == RequiredLearningState.Overdue ? 0 : 1).ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToArray(),
                });
            }
        }

        return View(new TeamLearningViewModel
        {
            IsManager = me is not null,
            List = new AdminListContext<TeamLearningRow> { Source = rows },
        });
    }
}
