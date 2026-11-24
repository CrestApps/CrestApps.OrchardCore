using CrestApps.OrchardCore.Skills.Models;
using System.Globalization;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using CrestApps.OrchardCore.WorkOrders.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.WorkOrders.Controllers;

/// <summary>
/// The records work orders are set up with: types, checklists and sub-statuses. Service level policies are the shared
/// ones of the Service Levels feature.
/// </summary>
public sealed class WorkOrderSettingsController : Controller
{
    /// <summary>
    /// The types list.
    /// </summary>
    public const string Types = WorkOrderSettingKinds.Types;

    /// <summary>
    /// The checklists list.
    /// </summary>
    public const string Checklists = WorkOrderSettingKinds.Checklists;

    /// <summary>
    /// The sub-statuses list.
    /// </summary>
    public const string SubStatuses = WorkOrderSettingKinds.SubStatuses;

    private const int BlankLines = 2;
    private const int BlankQuestions = 3;

    private readonly IAuthorizationService _authorizationService;
    private readonly WorkOrderSettingManager<WorkOrderType> _types;
    private readonly WorkOrderSettingManager<ChecklistTemplate> _checklists;
    private readonly IServiceLevelPolicyManager _policies;
    private readonly WorkOrderSettingManager<WorkOrderSubStatus> _subStatuses;
    private readonly WorkOrderLookups _lookups;
    private readonly WorkOrderStore _workOrderStore;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderSettingsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="types">The types.</param>
    /// <param name="checklists">The checklists.</param>
    /// <param name="policies">The shared service level policies.</param>
    /// <param name="subStatuses">The sub-statuses.</param>
    /// <param name="lookups">The lookups.</param>
    /// <param name="workOrderStore">The work order store.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkOrderSettingsController(
        IAuthorizationService authorizationService,
        WorkOrderSettingManager<WorkOrderType> types,
        WorkOrderSettingManager<ChecklistTemplate> checklists,
        IServiceLevelPolicyManager policies,
        WorkOrderSettingManager<WorkOrderSubStatus> subStatuses,
        WorkOrderLookups lookups,
        WorkOrderStore workOrderStore,
        INotifier notifier,
        IHtmlLocalizer<WorkOrderSettingsController> htmlLocalizer,
        IStringLocalizer<WorkOrderSettingsController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _types = types;
        _checklists = checklists;
        _policies = policies;
        _subStatuses = subStatuses;
        _lookups = lookups;
        _workOrderStore = workOrderStore;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists one kind of record.
    /// </summary>
    /// <param name="kind">The list: types, checklists or sub-statuses.</param>
    /// <param name="search">The text the names are searched for.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The page.</returns>
    [Admin("work-orders/settings/{kind}", "WorkOrderSettings")]
    public async Task<IActionResult> Index(string kind, string search, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkOrdersPermissions.ManageWorkOrderSettings))
        {
            return Forbid();
        }

        var rows = kind switch
        {
            Types => await TypeRowsAsync(),
            Checklists => await ChecklistRowsAsync(),
            SubStatuses => await SubStatusRowsAsync(),
            _ => null,
        };

        if (rows is null)
        {
            return NotFound();
        }

        search = search?.Trim();

        if (!string.IsNullOrEmpty(search))
        {
            rows = rows.Where(row => row.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) == true).ToList();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(search))
        {
            routeData.Values.Add("search", search);
        }

        return View(new WorkOrderSettingsListViewModel
        {
            Kind = kind,
            EditAction = EditActionOf(kind),
            Search = search,
            List = new AdminListContext<WorkOrderSettingRow>
            {
                Source = rows.Skip(pager.GetStartIndex()).Take(pager.PageSize).ToList(),
                Pager = await shapeFactory.PagerAsync(pager, rows.Count, routeData),
            },
        });
    }

    /// <summary>
    /// Shows the type editor.
    /// </summary>
    /// <param name="id">The type, or <see langword="null"/> for a new one.</param>
    /// <returns>The page.</returns>
    [Admin("work-orders/types/edit/{id?}", "WorkOrderTypesEdit")]
    public async Task<IActionResult> EditType(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkOrdersPermissions.ManageWorkOrderSettings))
        {
            return Forbid();
        }

        var item = string.IsNullOrEmpty(id) ? await _types.NewAsync() : await _types.FindByIdAsync(id);

        if (item is null)
        {
            return NotFound();
        }

        var model = WorkOrderSettingsEditorRules.FillType(item, id);

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Saves the type editor.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <returns>The list, or the editor with its errors.</returns>
    [HttpPost]
    [ActionName(nameof(EditType))]
    [Admin("work-orders/types/edit/{id?}", "WorkOrderTypesEdit")]
    public async Task<IActionResult> EditTypePost(WorkOrderTypeEditViewModel form)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkOrdersPermissions.ManageWorkOrderSettings))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(form.Id);
        var item = isNew ? await _types.NewAsync() : await _types.FindByIdAsync(form.Id);

        if (item is null)
        {
            return NotFound();
        }

        AddErrors(WorkOrderSettingsEditorRules.ApplyType(item, form, S));
        await ValidateAsync(_types, item);

        if (!ModelState.IsValid)
        {
            await PopulateAsync(form);

            return View(form);
        }

        await SaveAsync(_types, item, isNew);
        await _notifier.SuccessAsync(H["The work order type was saved."]);

        return RedirectToAction(nameof(Index), new { kind = Types });
    }

    /// <summary>
    /// Shows the checklist editor.
    /// </summary>
    /// <param name="id">The checklist, or <see langword="null"/> for a new one.</param>
    /// <returns>The page.</returns>
    [Admin("work-orders/checklists/edit/{id?}", "WorkOrderChecklistsEdit")]
    public async Task<IActionResult> EditChecklist(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkOrdersPermissions.ManageWorkOrderSettings))
        {
            return Forbid();
        }

        var item = string.IsNullOrEmpty(id) ? await _checklists.NewAsync() : await _checklists.FindByIdAsync(id);

        if (item is null)
        {
            return NotFound();
        }

        var model = WorkOrderSettingsEditorRules.FillChecklist(item, id);

        AddBlankQuestions(model);

        return View(model);
    }

    /// <summary>
    /// Saves the checklist editor.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <returns>The list, or the editor with its errors.</returns>
    [HttpPost]
    [ActionName(nameof(EditChecklist))]
    [Admin("work-orders/checklists/edit/{id?}", "WorkOrderChecklistsEdit")]
    public async Task<IActionResult> EditChecklistPost(ChecklistTemplateEditViewModel form)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkOrdersPermissions.ManageWorkOrderSettings))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(form.Id);
        var item = isNew ? await _checklists.NewAsync() : await _checklists.FindByIdAsync(form.Id);

        if (item is null)
        {
            return NotFound();
        }

        AddErrors(WorkOrderSettingsEditorRules.ApplyChecklist(item, form, S));
        await ValidateAsync(_checklists, item);

        if (!ModelState.IsValid)
        {
            AddBlankQuestions(form);

            return View(form);
        }

        await SaveAsync(_checklists, item, isNew);
        await _notifier.SuccessAsync(H["The checklist was saved."]);

        return RedirectToAction(nameof(Index), new { kind = Checklists });
    }

    /// <summary>
    /// Shows the sub-status editor.
    /// </summary>
    /// <param name="id">The sub-status, or <see langword="null"/> for a new one.</param>
    /// <returns>The page.</returns>
    [Admin("work-orders/sub-statuses/edit/{id?}", "WorkOrderSubStatusesEdit")]
    public async Task<IActionResult> EditSubStatus(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkOrdersPermissions.ManageWorkOrderSettings))
        {
            return Forbid();
        }

        var item = string.IsNullOrEmpty(id) ? await _subStatuses.NewAsync() : await _subStatuses.FindByIdAsync(id);

        if (item is null)
        {
            return NotFound();
        }

        return View(WorkOrderSettingsEditorRules.FillSubStatus(item, id));
    }

    /// <summary>
    /// Saves the sub-status editor.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <returns>The list, or the editor with its errors.</returns>
    [HttpPost]
    [ActionName(nameof(EditSubStatus))]
    [Admin("work-orders/sub-statuses/edit/{id?}", "WorkOrderSubStatusesEdit")]
    public async Task<IActionResult> EditSubStatusPost(WorkOrderSubStatusEditViewModel form)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkOrdersPermissions.ManageWorkOrderSettings))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(form.Id);
        var item = isNew ? await _subStatuses.NewAsync() : await _subStatuses.FindByIdAsync(form.Id);

        if (item is null)
        {
            return NotFound();
        }

        WorkOrderSettingsEditorRules.ApplySubStatus(item, form);
        await ValidateAsync(_subStatuses, item);

        if (!ModelState.IsValid)
        {
            return View(form);
        }

        await SaveAsync(_subStatuses, item, isNew);
        await _notifier.SuccessAsync(H["The sub-status was saved."]);

        return RedirectToAction(nameof(Index), new { kind = SubStatuses });
    }

    /// <summary>
    /// Deletes a record nothing uses.
    /// </summary>
    /// <param name="kind">The list.</param>
    /// <param name="id">The record.</param>
    /// <returns>The list.</returns>
    [HttpPost]
    [Admin("work-orders/settings/{kind}/delete/{id}", "WorkOrderSettingsDelete")]
    public async Task<IActionResult> Delete(string kind, string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkOrdersPermissions.ManageWorkOrderSettings))
        {
            return Forbid();
        }

        var result = await HttpContext.RequestServices.GetRequiredService<WorkOrderSettingDeletion>().DeleteAsync(kind, id, HttpContext.RequestAborted);

        if (result is null || result.Outcome == WorkOrderSettingDeleteOutcome.NotFound)
        {
            return NotFound();
        }

        if (result.Outcome == WorkOrderSettingDeleteOutcome.InUse)
        {
            await _notifier.WarningAsync(kind == Types
                ? H["{0} can't be deleted: work orders use it.", result.Name]
                : H["{0} can't be deleted: work order types use it.", result.Name]);
        }
        else
        {
            await _notifier.SuccessAsync(H["Deleted."]);
        }

        return RedirectToAction(nameof(Index), new { kind });
    }

    private async Task<List<WorkOrderSettingRow>> TypeRowsAsync()
    {
        var policies = (await _policies.ListByScopeAsync(WorkOrdersConstants.ServiceLevelScope)).ToDictionary(policy => policy.ItemId, policy => policy.Name, StringComparer.Ordinal);
        var types = await _types.GetAllAsync();

        // Counted in the database, type by type, instead of loading every work order's index row.
        var counts = await _workOrderStore.CountByTypeAsync(types.Select(type => type.ItemId), HttpContext.RequestAborted);

        return [.. types.OrderBy(type => type.Name, StringComparer.CurrentCultureIgnoreCase).Select(type => new WorkOrderSettingRow
        {
            Kind = Types,
            Id = type.ItemId,
            Name = type.Name,
            EditAction = nameof(EditType),
            Badges = Present(
                S["{0} priority", type.DefaultPriority],
                S["{0} min", type.DefaultDurationMinutes],
                type.BillingMode switch
                {
                    WorkOrderBillingMode.FixedPrice => S["Fixed price {0}", (type.FixedPrice ?? 0).ToString("N2", CultureInfo.CurrentCulture)],
                    WorkOrderBillingMode.TimeAndMaterials => S["Time and materials"],
                    _ => S["Not billed"],
                },
                string.IsNullOrEmpty(type.ServiceLevelPolicyId) ? null : S["Service level: {0}", policies.GetValueOrDefault(type.ServiceLevelPolicyId) ?? S["missing"]],
                type.ChecklistTemplateIds.Count == 0 ? null : type.ChecklistTemplateIds.Count == 1 ? S["1 checklist"] : S["{0} checklists", type.ChecklistTemplateIds.Count],
                counts.GetValueOrDefault(type.ItemId) == 1 ? S["1 work order"] : S["{0} work orders", counts.GetValueOrDefault(type.ItemId)]),
        })];
    }

    private async Task<List<WorkOrderSettingRow>> ChecklistRowsAsync()
    {
        var types = await _types.GetAllAsync();

        return [.. (await _checklists.GetAllAsync()).OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).Select(item => new WorkOrderSettingRow
        {
            Kind = Checklists,
            Id = item.ItemId,
            Name = item.Name,
            EditAction = nameof(EditChecklist),
            Badges = Present(
                item.Questions.Count == 1 ? S["1 question"] : S["{0} questions", item.Questions.Count],
                S["{0} required", item.Questions.Count(question => question.Required)],
                string.Join(", ", types.Where(type => type.ChecklistTemplateIds.Contains(item.ItemId)).Select(type => type.Name)) is { Length: > 0 } names ? S["Used by {0}", names] : S["Not used yet"]),
        })];
    }

    private async Task<List<WorkOrderSettingRow>> SubStatusRowsAsync()
    {
        return [.. (await _subStatuses.GetAllAsync()).OrderBy(item => item.ParentStatus).ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).Select(item => new WorkOrderSettingRow
        {
            Kind = SubStatuses,
            Id = item.ItemId,
            Name = item.Name,
            EditAction = nameof(EditSubStatus),
            Badges = Present(S["Under {0}", item.ParentStatus.Text()], item.PausesSla ? S["Stops the clock"] : null),
        })];
    }

    private async Task PopulateAsync(WorkOrderTypeEditViewModel model)
    {
        model.PolicyOptions = [.. (await _policies.ListByScopeAsync(WorkOrdersConstants.ServiceLevelScope)).OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).Select(item => new SelectListItem(item.Name, item.ItemId, item.ItemId == model.ServiceLevelPolicyId))];
        model.ChecklistOptions = [.. (await _checklists.GetAllAsync()).OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).Select(item => new SelectListItem(item.Name, item.ItemId, model.ChecklistTemplateIds.Contains(item.ItemId)))];
        model.StockItemOptions = await _lookups.StockItemsAsync(HttpContext.RequestAborted);
        model.SkillOptions = await _lookups.SkillsAsync(HttpContext.RequestAborted);
        model.CertificationOptions = [.. (await _lookups.CertificationsAsync(HttpContext.RequestAborted)).Select(option => new SelectListItem(option.Text, option.Value, model.RequiredCertificationIds.Contains(option.Value)))];
        model.AppointmentTypeOptions = [.. (await _lookups.AppointmentTypesAsync(HttpContext.RequestAborted)).Select(option => new SelectListItem(option.Text, option.Value, model.AppointmentTypeIds.Contains(option.Value)))];

        // One empty row to add a skill, and two rows at least.
        while (!model.RequiredSkills.Any(skill => string.IsNullOrEmpty(skill.SkillId)) || model.RequiredSkills.Count < 2)
        {
            model.RequiredSkills.Add(new SkillRequirement());
        }

        while (model.Lines.Count(line => string.IsNullOrWhiteSpace(line.Description)) < BlankLines)
        {
            model.Lines.Add(new LineEditModel());
        }
    }

    private static void AddBlankQuestions(ChecklistTemplateEditViewModel model)
    {
        while (model.Questions.Count(question => string.IsNullOrWhiteSpace(question.Text)) < BlankQuestions)
        {
            model.Questions.Add(new QuestionEditModel());
        }
    }

    private async Task ValidateAsync<T>(WorkOrderSettingManager<T> manager, T item)
        where T : CrestApps.Core.Models.CatalogItem, new()
    {
        var validation = await manager.ValidateAsync(item);

        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.MemberNames.FirstOrDefault() ?? string.Empty, error.ErrorMessage);
        }
    }

    private static async Task SaveAsync<T>(WorkOrderSettingManager<T> manager, T item, bool isNew)
        where T : CrestApps.Core.Models.CatalogItem, new()
    {
        if (isNew)
        {
            await manager.CreateAsync(item);
        }
        else
        {
            await manager.UpdateAsync(item);
        }
    }

    private void AddErrors(IEnumerable<(string Field, string Message)> errors)
    {
        foreach (var (field, message) in errors)
        {
            ModelState.AddModelError(field, message);
        }
    }

    internal static string EditActionOf(string kind) => kind switch
    {
        Types => nameof(EditType),
        Checklists => nameof(EditChecklist),
        _ => nameof(EditSubStatus),
    };

    private static List<string> Present(params LocalizedString[] badges) => [.. badges.Where(badge => badge is not null).Select(badge => badge.Value)];
}
