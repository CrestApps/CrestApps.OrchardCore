using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Designer.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Controllers;

/// <summary>
/// Manages the groups designed reports are listed under, in the admin menu and the report list.
/// </summary>
[Feature(ReportsConstants.BuilderFeature)]
[Admin]
public sealed class ReportGroupsController : Controller
{
    private readonly ReportGroupService _groupService;
    private readonly ReportDesignService _designService;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportGroupsController"/> class.
    /// </summary>
    /// <param name="groupService">The group service.</param>
    /// <param name="designService">The design service, which counts the reports in each group.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public ReportGroupsController(
        ReportGroupService groupService,
        ReportDesignService designService,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<ReportGroupsController> htmlLocalizer)
    {
        _groupService = groupService;
        _designService = designService;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the groups, each followed by the groups nested in it.
    /// </summary>
    /// <param name="q">The optional search text, matched against each group's path.</param>
    /// <returns>The list page.</returns>
    [Admin("reports/groups", "ReportGroupsIndex")]
    public async Task<IActionResult> Index(string q)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ManageReportGroups))
        {
            return Forbid();
        }

        var tree = await _groupService.GetTreeAsync();
        var reportCounts = (await _designService.GetAllAsync())
            .Where(design => tree.Find(design.GroupId) is not null)
            .GroupBy(design => design.GroupId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var search = q?.Trim();
        var entries = tree.Flatten()
            .Where(node => string.IsNullOrEmpty(search) || node.PathText.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            .Select(node => new ReportGroupListEntry
            {
                Group = node.Group,
                PathText = node.PathText,
                Depth = node.Depth,
                GroupCount = tree.GetChildren(node.Group.ItemId).Count,
                ReportCount = reportCounts.GetValueOrDefault(node.Group.ItemId),
            })
            .ToList();

        // The admin-list tag builds a row per group, in the layout (List or Grid) the site or the user picked.
        return View(new ReportGroupsIndexViewModel
        {
            Search = q,
            List = new AdminListContext<ReportGroupListEntry>
            {
                Source = entries,
            },
        });
    }

    /// <summary>
    /// Shows the page that creates a group.
    /// </summary>
    /// <param name="parentId">The group to nest the new group in, if any.</param>
    /// <returns>The edit page.</returns>
    [Admin("reports/groups/create", "ReportGroupsCreate")]
    public async Task<IActionResult> Create(string parentId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ManageReportGroups))
        {
            return Forbid();
        }

        var model = new ReportGroupEditViewModel
        {
            IsNew = true,
            ParentId = parentId,
        };

        await PopulateParentsAsync(model, null);

        return View("Edit", model);
    }

    /// <summary>
    /// Creates a group.
    /// </summary>
    /// <param name="model">The name, parent, and order entered.</param>
    /// <returns>A redirect to the list, or the edit page with the problems.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("reports/groups/create", "ReportGroupsCreate")]
    public async Task<IActionResult> CreatePost(ReportGroupEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ManageReportGroups))
        {
            return Forbid();
        }

        var result = await _groupService.SaveAsync(ToGroup(model), null);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The report group has been created."]);

            return RedirectToAction(nameof(Index));
        }

        AddErrors(result);
        model.IsNew = true;
        await PopulateParentsAsync(model, null);

        return View("Edit", model);
    }

    /// <summary>
    /// Shows the page that changes a group.
    /// </summary>
    /// <param name="id">The group identifier.</param>
    /// <returns>The edit page.</returns>
    [Admin("reports/groups/edit/{id}", "ReportGroupsEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ManageReportGroups))
        {
            return Forbid();
        }

        var group = await _groupService.FindAsync(id);

        if (group is null)
        {
            return NotFound();
        }

        var model = new ReportGroupEditViewModel
        {
            DisplayText = group.DisplayText,
            ParentId = group.ParentId,
            Order = group.Order,
        };

        await PopulateParentsAsync(model, group);

        return View(model);
    }

    /// <summary>
    /// Changes a group.
    /// </summary>
    /// <param name="id">The group identifier.</param>
    /// <param name="model">The name, parent, and order entered.</param>
    /// <returns>A redirect to the list, or the edit page with the problems.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("reports/groups/edit/{id}", "ReportGroupsEdit")]
    public async Task<IActionResult> EditPost(string id, ReportGroupEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ManageReportGroups))
        {
            return Forbid();
        }

        var group = await _groupService.FindAsync(id);

        if (group is null)
        {
            return NotFound();
        }

        var result = await _groupService.SaveAsync(ToGroup(model), group);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The report group has been updated."]);

            return RedirectToAction(nameof(Index));
        }

        AddErrors(result);
        await PopulateParentsAsync(model, group);

        return View(model);
    }

    /// <summary>
    /// Deletes a group that holds no groups and no reports.
    /// </summary>
    /// <param name="id">The group identifier.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("reports/groups/delete/{id}", "ReportGroupsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ManageReportGroups))
        {
            return Forbid();
        }

        var group = await _groupService.FindAsync(id);

        if (group is null)
        {
            return NotFound();
        }

        var result = await _groupService.DeleteAsync(group);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The report group has been deleted."]);
        }
        else
        {
            var (groups, reports) = await _groupService.CountContentsAsync(group.ItemId);

            await _notifier.ErrorAsync(H["The group '{0}' holds {1} group(s) and {2} report(s). Move them to another group, or out of any group, before deleting it.", group.DisplayText, groups, reports]);
        }

        return RedirectToAction(nameof(Index));
    }

    private static ReportGroup ToGroup(ReportGroupEditViewModel model)
    {
        return new ReportGroup
        {
            DisplayText = model.DisplayText,
            ParentId = model.ParentId,
            Order = model.Order,
        };
    }

    private void AddErrors(ReportGroupResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }
    }

    // Every group the edited group can be nested in, with its path: not the group itself, nor a group nested in it.
    private async Task PopulateParentsAsync(ReportGroupEditViewModel model, ReportGroup group)
    {
        var tree = await _groupService.GetTreeAsync();

        model.Parents = tree.Flatten()
            .Where(node => group is null || !tree.IsSelfOrDescendant(node.Group.ItemId, group.ItemId))
            .Select(node => new SelectListItem(node.PathText, node.Group.ItemId, string.Equals(node.Group.ItemId, model.ParentId, StringComparison.Ordinal)))
            .ToList();
    }
}
