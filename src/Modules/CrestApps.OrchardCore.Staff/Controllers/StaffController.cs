using System.Security.Claims;
using CrestApps.OrchardCore.Core.Validation;
using CrestApps.OrchardCore.Staff.Core.Services;
using CrestApps.OrchardCore.Staff.Drivers;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Staff.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using YesSql;

namespace CrestApps.OrchardCore.Staff.Controllers;

/// <summary>
/// The staff list, the staff editor and the organization chart.
/// </summary>
public sealed class StaffController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IStaffMemberManager _staffManager;
    private readonly IStaffScopeService _scopeService;
    private readonly IStaffDirectory _directory;
    private readonly IStaffHierarchy _hierarchy;
    private readonly StaffTeamReader _teamManager;
    private readonly IDisplayManager<StaffMember> _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IUserSearchService _userSearchService;
    private readonly ILocalClock _localClock;
    private readonly ISession _session;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public StaffController(
        IAuthorizationService authorizationService,
        IStaffMemberManager staffManager,
        IStaffScopeService scopeService,
        IStaffDirectory directory,
        IStaffHierarchy hierarchy,
        StaffTeamReader teamManager,
        IDisplayManager<StaffMember> displayManager,
        IUpdateModelAccessor updateModelAccessor,
        IUserSearchService userSearchService,
        ILocalClock localClock,
        ISession session,
        INotifier notifier,
        IHtmlLocalizer<StaffController> htmlLocalizer,
        IStringLocalizer<StaffController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _staffManager = staffManager;
        _scopeService = scopeService;
        _directory = directory;
        _hierarchy = hierarchy;
        _teamManager = teamManager;
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        _userSearchService = userSearchService;
        _localClock = localClock;
        _session = session;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("staff", "StaffIndex")]
    public async Task<IActionResult> Index(StaffListViewModel model, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var query = new StaffQuery
        {
            Search = model.Search,
            TeamId = string.IsNullOrEmpty(model.TeamId) ? null : model.TeamId,
            Status = Enum.TryParse<StaffStatus>(model.Status, out var status) ? status : null,
            Capability = string.IsNullOrEmpty(model.Capability) ? null : model.Capability,
        };

        // Those who may see all staff see everyone; a manager (of a team, or through another module such as a
        // Workforce team manager) sees the staff they manage: the members of their teams, else their reports.
        if (!await _authorizationService.AuthorizeAsync(User, StaffPermissions.ViewStaff))
        {
            var scope = await _scopeService.GetScopeAsync(User, HttpContext.RequestAborted);

            if (scope.IsEmpty)
            {
                return Forbid();
            }

            if (!scope.All)
            {
                if (scope.TeamIds.Count > 0)
                {
                    query.TeamIds = [.. scope.TeamIds];
                }
                else
                {
                    query.Ids = [.. scope.StaffMemberIds];
                }
            }
        }

        var (count, people) = await _staffManager.SearchAsync(query, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);

        model.List = new AdminListContext<StaffMember>
        {
            Source = people.ToList(),
            Pager = await shapeFactory.PagerAsync(pager, count, new RouteValueDictionary
            {
                { "search", model.Search },
                { "teamId", model.TeamId },
                { "status", model.Status },
                { "capability", model.Capability },
            }),
        };

        model.TeamOptions = (await _teamManager.ListTreeAsync(HttpContext.RequestAborted))
            .Select(item => new SelectListItem(new string(' ', item.Depth * 4) + item.Team.Name, item.Team.ItemId, item.Team.ItemId == model.TeamId))
            .ToList();
        model.StatusOptions = StaffSelectLists.Statuses(S, model.Status);
        model.CapabilityOptions = (await HttpContext.RequestServices.GetRequiredService<IStaffCapabilityService>().ListAsync(includeDisabled: true, HttpContext.RequestAborted))
            .Select(capability => new SelectListItem(capability.DisplayName, capability.Name, string.Equals(capability.Name, model.Capability, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        model.CanCreate = !(await _scopeService.GetScopeAsync(User, HttpContext.RequestAborted)).IsEmpty;

        return View(model);
    }

    [Admin("staff/edit/{id?}", "StaffEdit")]
    public async Task<IActionResult> Edit(string id, string teamId, string managerId)
    {
        var isNew = string.IsNullOrEmpty(id);
        var staff = isNew ? await _staffManager.NewAsync() : await _staffManager.FindByIdAsync(id);

        if (staff is null)
        {
            return NotFound();
        }

        if (!await CanEditAsync(staff, isNew))
        {
            return Forbid();
        }

        if (isNew)
        {
            if (!string.IsNullOrEmpty(teamId))
            {
                staff.TeamMemberships.Add(new TeamMembership { TeamId = teamId });
            }

            staff.ManagerId = managerId;
        }

        var model = new StaffEditorPageViewModel
        {
            Id = id,
            DisplayName = staff.DisplayName,
            Editor = await _displayManager.BuildEditorAsync(staff, _updateModelAccessor.ModelUpdater, isNew),
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("staff/edit/{id?}", "StaffEdit")]
    public async Task<IActionResult> EditPost(string id)
    {
        var isNew = string.IsNullOrEmpty(id);
        var staff = isNew ? await _staffManager.NewAsync() : await _staffManager.FindByIdAsync(id);

        if (staff is null)
        {
            return NotFound();
        }

        if (!await CanEditAsync(staff, isNew))
        {
            return Forbid();
        }

        var displayName = staff.DisplayName;
        var editor = await _displayManager.UpdateEditorAsync(staff, _updateModelAccessor.ModelUpdater, isNew);
        var isValid = await CatalogEntryValidation.ValidateAsync(_staffManager, staff, _updateModelAccessor.ModelUpdater, nameof(StaffMember), HttpContext.RequestAborted);

        if (!isValid || !ModelState.IsValid)
        {
            // A card may already have written records of its own, such as skills: none of it is kept.
            await _session.CancelAsync();

            var model = new StaffEditorPageViewModel
            {
                Id = id,
                DisplayName = displayName,
                Editor = editor,
            };

            return View(model);
        }

        if (isNew)
        {
            await _staffManager.CreateAsync(staff);
        }
        else
        {
            await _staffManager.UpdateAsync(staff);
        }

        await _notifier.SuccessAsync(H["{0} has been saved.", staff.DisplayName]);

        return RedirectToAction(nameof(Profile), new { id = staff.ItemId });
    }

    [Admin("staff/profile/{id}", "StaffProfile")]
    public async Task<IActionResult> Profile(string id)
    {
        var staff = await _staffManager.FindByIdAsync(id);

        if (staff is null)
        {
            return NotFound();
        }

        // Anyone who may see staff, the people who manage this person, and the person themselves.
        var isSelf = !string.IsNullOrEmpty(staff.UserId) && staff.UserId == User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!isSelf
            && !await _authorizationService.AuthorizeAsync(User, StaffPermissions.ViewStaff)
            && !await _scopeService.CanManageAsync(User, staff, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        return View(new StaffProfilePageViewModel
        {
            Id = staff.ItemId,
            DisplayName = staff.DisplayName,
            Profile = await _displayManager.BuildDisplayAsync(staff, _updateModelAccessor.ModelUpdater, StaffProfileSections.DisplayType),
        });
    }

    [HttpPost]
    [Admin("staff/delete/{id}", "StaffDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, StaffPermissions.ManageStaff))
        {
            return Forbid();
        }

        var staff = await _staffManager.FindByIdAsync(id);

        if (staff is null)
        {
            return NotFound();
        }

        if ((await _staffManager.ListDirectReportsAsync(staff.ItemId, HttpContext.RequestAborted)).Count > 0)
        {
            await _notifier.ErrorAsync(H["{0} still has people reporting to them. Give them another manager first.", staff.DisplayName]);

            return RedirectToAction(nameof(Index));
        }

        await _staffManager.DeleteAsync(staff);
        await _notifier.SuccessAsync(H["{0} has been deleted.", staff.DisplayName]);

        return RedirectToAction(nameof(Index));
    }

    [Admin("staff/me", "StaffMe")]
    public async Task<IActionResult> Me([FromServices] IEnumerable<IStaffHomeLinkProvider> linkProviders, [FromServices] StaffTeamReader teamManager)
    {
        if (!await _authorizationService.AuthorizeAsync(User, StaffPermissions.ViewOwnStaffProfile))
        {
            return Forbid();
        }

        var staff = await _directory.FindByUserIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.RequestAborted);
        var model = new StaffHomeViewModel { StaffMember = staff, UsesTeams = teamManager.UsesTeams };

        if (staff is not null)
        {
            var today = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);
            var teams = (await teamManager.ListTreeAsync(HttpContext.RequestAborted)).ToDictionary(item => item.Team.ItemId, item => item.Team.Name, StringComparer.Ordinal);

            model.ManagerChain = (await _hierarchy.GetManagerChainAsync(staff.ItemId, HttpContext.RequestAborted)).ToList();
            model.Reports = (await _hierarchy.GetReportsAsync(staff.ItemId, includeIndirect: false, HttpContext.RequestAborted)).ToList();
            model.Teams = staff.MembershipsOn(today).Select(membership => teams.GetValueOrDefault(membership.TeamId, membership.TeamId)).ToList();

            foreach (var provider in linkProviders)
            {
                model.Links.AddRange(await provider.GetLinksAsync(staff, User, HttpContext.RequestAborted) ?? []);
            }

            model.Links = model.Links.OrderBy(link => link.Position).ThenBy(link => link.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        return View(model);
    }

    [Admin("staff/organization-chart", "StaffOrgChart")]
    public async Task<IActionResult> OrgChart(string teamId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, StaffPermissions.ViewStaff))
        {
            return Forbid();
        }

        var (count, people) = await _staffManager.SearchAsync(new StaffQuery { TeamId = string.IsNullOrEmpty(teamId) ? null : teamId }, 0, 2000, HttpContext.RequestAborted);
        var today = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);
        var working = people.Where(person => person.IsEmployedOn(today)).ToList();
        var teams = (await _teamManager.ListTreeAsync(HttpContext.RequestAborted)).ToDictionary(item => item.Team.ItemId, item => item.Team.Name, StringComparer.Ordinal);
        var shown = working.Select(person => person.ItemId).ToHashSet(StringComparer.Ordinal);

        // The pictures come from the user accounts, through Users, when a picture provider is on.
        var avatars = (await _userSearchService.GetAsync(working.Select(person => person.UserId).Where(id => !string.IsNullOrEmpty(id)), HttpContext.RequestAborted))
            .Where(user => !string.IsNullOrEmpty(user.AvatarUrl))
            .ToDictionary(user => user.UserId, user => Url.Content(user.AvatarUrl), StringComparer.Ordinal);
        var nodes = new List<StaffOrgChartNode>(working.Count);

        foreach (var person in working.OrderBy(person => person.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            nodes.Add(new StaffOrgChartNode
            {
                Id = person.ItemId,

                // A manager outside the chart (another team, or no longer working) puts the person at the top.
                ParentId = !string.IsNullOrEmpty(person.ManagerId) && shown.Contains(person.ManagerId) ? person.ManagerId : null,
                Name = person.DisplayName,
                JobTitle = person.JobTitle,
                TeamName = person.PrimaryTeamId is { } team ? teams.GetValueOrDefault(team) : null,
                AvatarUrl = !string.IsNullOrEmpty(person.UserId) ? avatars.GetValueOrDefault(person.UserId) : null,
                Initials = StaffOrgChartNode.InitialsOf(person.DisplayName),
                Url = Url.Action(nameof(Profile), new { id = person.ItemId }),
            });
        }

        ViewData["TeamOptions"] = teams.Select(team => new SelectListItem(team.Value, team.Key, team.Key == teamId)).OrderBy(option => option.Text, StringComparer.CurrentCultureIgnoreCase).ToList();

        return View(new StaffOrgChartViewModel
        {
            Nodes = nodes,
            Count = working.Count,
        });
    }

    private async Task<bool> CanEditAsync(StaffMember staff, bool isNew)
    {
        var scope = await _scopeService.GetScopeAsync(User, HttpContext.RequestAborted);

        if (isNew)
        {
            return !scope.IsEmpty;
        }

        return await _scopeService.CanManageAsync(User, staff, HttpContext.RequestAborted);
    }
}
