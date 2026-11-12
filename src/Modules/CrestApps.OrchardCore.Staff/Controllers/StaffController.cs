using CrestApps.OrchardCore.Staff.Core.Services;
using System.Security.Claims;
using CrestApps.OrchardCore.Core.Validation;
using CrestApps.OrchardCore.Staff.Drivers;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Staff.ViewModels;
using CrestApps.OrchardCore.Teams.Services;
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
    private readonly IStaffHistoryService _historyService;
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
        IStaffHistoryService historyService,
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
        _historyService = historyService;
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
        if (!await _authorizationService.AuthorizeAsync(User, StaffPermissions.ViewStaff))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var query = new StaffQuery
        {
            Search = model.Search,
            TeamId = string.IsNullOrEmpty(model.TeamId) ? null : model.TeamId,
            Status = Enum.TryParse<StaffStatus>(model.Status, out var status) ? status : null,
            Capability = string.IsNullOrEmpty(model.Capability) ? null : model.Capability,
        };

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
        model.CapabilityOptions = _directory.GetCapabilities()
            .Select(capability => new SelectListItem(capability.DisplayName is null ? capability.Name : HttpContext.RequestServices.GetRequiredService<IStringLocalizerFactory>().Localize(capability.DisplayName).Value, capability.Name, string.Equals(capability.Name, model.Capability, StringComparison.OrdinalIgnoreCase)))
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

        await FillAsync(model, staff, isNew);

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

            await FillAsync(model, staff, isNew);

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

        return RedirectToAction(nameof(Edit), new { id = staff.ItemId });
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
                Url = await _scopeService.CanManageAsync(User, person, HttpContext.RequestAborted) ? Url.Action(nameof(Edit), new { id = person.ItemId }) : null,
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

    private async Task FillAsync(StaffEditorPageViewModel model, StaffMember staff, bool isNew)
    {
        if (isNew)
        {
            return;
        }

        model.Reports = (await _hierarchy.GetReportsAsync(staff.ItemId, includeIndirect: false, HttpContext.RequestAborted)).ToList();
        model.ManagerChain = (await _hierarchy.GetManagerChainAsync(staff.ItemId, HttpContext.RequestAborted)).ToList();

        var history = await _historyService.ListAsync(staff.ItemId, 50, HttpContext.RequestAborted);
        var userIds = history.Select(entry => entry.ChangedByUserId).Where(userId => !string.IsNullOrEmpty(userId)).Distinct(StringComparer.Ordinal).ToArray();
        var users = (await _userSearchService.GetAsync(userIds, HttpContext.RequestAborted)).ToDictionary(user => user.UserId, user => string.IsNullOrEmpty(user.DisplayName) ? user.UserName : user.DisplayName, StringComparer.Ordinal);
        var people = await _directory.GetNamesAsync(history.Where(entry => entry.Field == StaffHistoryFields.Manager).SelectMany(entry => new[] { entry.From, entry.To }), HttpContext.RequestAborted);
        var teamNames = history.Any(entry => entry.Field == StaffHistoryFields.Teams)
            ? (await _teamManager.ListTreeAsync(HttpContext.RequestAborted)).ToDictionary(item => item.Team.ItemId, item => item.Team.Name, StringComparer.Ordinal)
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

        foreach (var entry in history)
        {
            model.History.Add(new StaffHistoryRowViewModel
            {
                Field = entry.Field,
                From = Show(entry.Field, entry.From),
                To = Show(entry.Field, entry.To),
                ChangedBy = entry.ChangedByUserId is { } userId ? users.GetValueOrDefault(userId, userId) : null,
                ChangedLocal = (await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(entry.ChangedUtc, DateTimeKind.Utc))).DateTime,
            });
        }
    }
}
