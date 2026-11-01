using System.Globalization;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using CrestApps.OrchardCore.WorkOrders.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Settings;
using YesSql;

namespace CrestApps.OrchardCore.WorkOrders.Controllers;

/// <summary>
/// The records work orders are set up with: types, checklists, service levels and sub-statuses.
/// </summary>
public sealed class WorkOrderSettingsController : Controller
{
    /// <summary>
    /// The types list.
    /// </summary>
    public const string Types = "types";

    /// <summary>
    /// The checklists list.
    /// </summary>
    public const string Checklists = "checklists";

    /// <summary>
    /// The service levels list.
    /// </summary>
    public const string ServiceLevels = "service-levels";

    /// <summary>
    /// The sub-statuses list.
    /// </summary>
    public const string SubStatuses = "sub-statuses";

    private const int BlankLines = 2;
    private const int BlankQuestions = 3;

    private readonly IAuthorizationService _authorizationService;
    private readonly WorkOrderSettingManager<WorkOrderType> _types;
    private readonly WorkOrderSettingManager<ChecklistTemplate> _checklists;
    private readonly WorkOrderSettingManager<ServiceLevelPolicy> _policies;
    private readonly WorkOrderSettingManager<WorkOrderSubStatus> _subStatuses;
    private readonly WorkOrderLookups _lookups;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ISiteService _siteService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderSettingsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="types">The types.</param>
    /// <param name="checklists">The checklists.</param>
    /// <param name="policies">The service level policies.</param>
    /// <param name="subStatuses">The sub-statuses.</param>
    /// <param name="lookups">The lookups.</param>
    /// <param name="session">The session.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkOrderSettingsController(
        IAuthorizationService authorizationService,
        WorkOrderSettingManager<WorkOrderType> types,
        WorkOrderSettingManager<ChecklistTemplate> checklists,
        WorkOrderSettingManager<ServiceLevelPolicy> policies,
        WorkOrderSettingManager<WorkOrderSubStatus> subStatuses,
        WorkOrderLookups lookups,
        ISession session,
        IClock clock,
        ISiteService siteService,
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
        _session = session;
        _clock = clock;
        _siteService = siteService;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists one kind of record.
    /// </summary>
    /// <param name="kind">The list: types, checklists, service-levels or sub-statuses.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The page.</returns>
    [Admin("work-orders/settings/{kind}", "WorkOrderSettings")]
    public async Task<IActionResult> Index(string kind, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkOrdersPermissions.ManageWorkOrderSettings))
        {
            return Forbid();
        }

        var rows = kind switch
        {
            Types => await TypeRowsAsync(),
            Checklists => await ChecklistRowsAsync(),
            ServiceLevels => await PolicyRowsAsync(),
            SubStatuses => await SubStatusRowsAsync(),
            _ => null,
        };

        if (rows is null)
        {
            return NotFound();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);

        return View(new WorkOrderSettingsListViewModel
        {
            Kind = kind,
            EditAction = EditActionOf(kind),
            List = new AdminListContext<WorkOrderSettingRow>
            {
                Source = rows.Skip(pager.GetStartIndex()).Take(pager.PageSize).ToList(),
                Pager = await shapeFactory.PagerAsync(pager, rows.Count),
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

        var model = new WorkOrderTypeEditViewModel
        {
            Id = id,
            Name = item.Name,
            Color = item.Color,
            DefaultDurationMinutes = item.DefaultDurationMinutes,
            CrewSize = item.CrewSize,
            DefaultPriority = item.DefaultPriority,
            ServiceLevelPolicyId = item.ServiceLevelPolicyId,
            BillingMode = item.BillingMode,
            Currency = item.Currency,
            FixedPrice = item.FixedPrice,
            RequiresSignature = item.RequiresSignature,
            RequiredPhotos = item.RequiredPhotos,
            ChecklistTemplateIds = [.. item.ChecklistTemplateIds],
            Lines = [.. item.DefaultLines.Select(ToEditModel)],
        };

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

        item.Name = form.Name?.Trim();
        item.Color = string.IsNullOrWhiteSpace(form.Color) ? null : form.Color.Trim();
        item.DefaultDurationMinutes = form.DefaultDurationMinutes;
        item.CrewSize = form.CrewSize;
        item.DefaultPriority = form.DefaultPriority;
        item.ServiceLevelPolicyId = string.IsNullOrEmpty(form.ServiceLevelPolicyId) ? null : form.ServiceLevelPolicyId;
        item.BillingMode = form.BillingMode;
        item.Currency = string.IsNullOrWhiteSpace(form.Currency) ? "USD" : form.Currency.Trim().ToUpperInvariant();
        item.FixedPrice = form.BillingMode == WorkOrderBillingMode.FixedPrice ? form.FixedPrice : null;
        item.RequiresSignature = form.RequiresSignature;
        item.RequiredPhotos = Math.Max(0, form.RequiredPhotos);
        item.ChecklistTemplateIds = [.. (form.ChecklistTemplateIds ?? []).Where(templateId => !string.IsNullOrEmpty(templateId)).Distinct(StringComparer.Ordinal)];
        item.DefaultLines = [.. (form.Lines ?? []).Where(line => !string.IsNullOrWhiteSpace(line.Description)).Select(line => ToLine(line, null))];

        if (form.BillingMode == WorkOrderBillingMode.FixedPrice && form.FixedPrice is not > 0)
        {
            ModelState.AddModelError(nameof(form.FixedPrice), S["Enter the fixed price."]);
        }

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

        var model = new ChecklistTemplateEditViewModel
        {
            Id = id,
            Name = item.Name,
            Questions = [.. item.Questions.Select(question => new QuestionEditModel
            {
                Id = question.Id,
                Section = question.Section,
                Text = question.Text,
                Kind = question.Kind,
                Required = question.Required,
                Choices = string.Join('\n', question.Choices),
                Min = question.Min,
                Max = question.Max,
                Unit = question.Unit ?? question.Meter,
                Help = question.Help,
            })],
        };

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

        var previous = item.Questions.ToDictionary(question => question.Id ?? string.Empty, StringComparer.Ordinal);

        item.Name = form.Name?.Trim();
        item.Questions = [.. (form.Questions ?? []).Where(row => !string.IsNullOrWhiteSpace(row.Text)).Select(row => new ChecklistQuestion
        {
            Id = string.IsNullOrEmpty(row.Id) ? CrestApps.Core.UniqueId.GenerateId() : row.Id,
            Section = string.IsNullOrWhiteSpace(row.Section) ? null : row.Section.Trim(),
            Text = row.Text.Trim(),
            Kind = row.Kind,
            Required = row.Required,
            Choices = row.Kind is ChecklistQuestionKind.Choice or ChecklistQuestionKind.MultiChoice ? [.. (row.Choices ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase)] : [],
            Min = row.Kind is ChecklistQuestionKind.Number or ChecklistQuestionKind.MeterReading ? row.Min : null,
            Max = row.Kind is ChecklistQuestionKind.Number or ChecklistQuestionKind.MeterReading ? row.Max : null,
            Unit = row.Kind == ChecklistQuestionKind.Number && !string.IsNullOrWhiteSpace(row.Unit) ? row.Unit.Trim() : null,
            Meter = row.Kind == ChecklistQuestionKind.MeterReading && !string.IsNullOrWhiteSpace(row.Unit) ? row.Unit.Trim() : null,
            Help = string.IsNullOrWhiteSpace(row.Help) ? null : row.Help.Trim(),
            ShowWhen = !string.IsNullOrEmpty(row.Id) && previous.TryGetValue(row.Id, out var old) ? old.ShowWhen : null,
        })];

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
    /// Shows the service level editor.
    /// </summary>
    /// <param name="id">The policy, or <see langword="null"/> for a new one.</param>
    /// <returns>The page.</returns>
    [Admin("work-orders/service-levels/edit/{id?}", "WorkOrderServiceLevelsEdit")]
    public async Task<IActionResult> EditPolicy(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkOrdersPermissions.ManageWorkOrderSettings))
        {
            return Forbid();
        }

        var item = string.IsNullOrEmpty(id) ? await _policies.NewAsync() : await _policies.FindByIdAsync(id);

        if (item is null)
        {
            return NotFound();
        }

        var weekly = item.WeeklyHours;
        var model = new ServiceLevelPolicyEditViewModel
        {
            Id = id,
            Name = item.Name,
            TimeZoneId = item.TimeZoneId ?? (string.IsNullOrEmpty(id) ? (await _siteService.GetSiteSettingsAsync()).TimeZoneId : null),
            BusinessHoursOnly = weekly is not null,
            Days = weekly is null ? WeeklyHours.Weekdays() : [.. weekly.Days],
            Opens = (weekly ?? new WeeklyHours()).Opens,
            Closes = (weekly ?? new WeeklyHours()).Closes,
            WarningPercent = item.WarningPercent,
            PauseStatuses = [.. item.PauseStatuses],
            Targets = [.. Enum.GetValues<WorkOrderPriority>().OrderDescending().Select(priority => item.Targets.FirstOrDefault(target => target.Priority == priority) is { } target
                ? new TargetEditModel { Priority = priority, ResponseHours = ToHours(target.ResponseMinutes), ResolveHours = ToHours(target.ResolveMinutes) }
                : new TargetEditModel { Priority = priority })],
        };

        Populate(model);

        return View(model);
    }

    /// <summary>
    /// Saves the service level editor.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <returns>The list, or the editor with its errors.</returns>
    [HttpPost]
    [ActionName(nameof(EditPolicy))]
    [Admin("work-orders/service-levels/edit/{id?}", "WorkOrderServiceLevelsEdit")]
    public async Task<IActionResult> EditPolicyPost(ServiceLevelPolicyEditViewModel form)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkOrdersPermissions.ManageWorkOrderSettings))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(form.Id);
        var item = isNew ? await _policies.NewAsync() : await _policies.FindByIdAsync(form.Id);

        if (item is null)
        {
            return NotFound();
        }

        item.Name = form.Name?.Trim();
        item.TimeZoneId = string.IsNullOrEmpty(form.TimeZoneId) ? null : form.TimeZoneId;
        item.WeeklyHours = form.BusinessHoursOnly ? new WeeklyHours { Days = [.. (form.Days ?? []).Distinct().Order()], Opens = form.Opens, Closes = form.Closes } : null;
        item.WarningPercent = form.WarningPercent;
        item.PauseStatuses = [.. (form.PauseStatuses ?? []).Distinct()];
        item.Targets = [.. (form.Targets ?? []).Where(target => target.ResponseHours > 0 || target.ResolveHours > 0).Select(target => new ServiceLevelTarget
        {
            Priority = target.Priority,
            ResponseMinutes = ToMinutes(target.ResponseHours),
            ResolveMinutes = ToMinutes(target.ResolveHours),
        })];

        if (form.BusinessHoursOnly && (item.WeeklyHours.Days.Count == 0 || form.Closes <= form.Opens))
        {
            ModelState.AddModelError(nameof(form.Days), S["Choose the days open, and a closing time after the opening time."]);
        }

        if (form.WarningPercent is < 1 or > 99)
        {
            ModelState.AddModelError(nameof(form.WarningPercent), S["The warning comes at between 1 and 99 percent of a target."]);
        }

        await ValidateAsync(_policies, item);

        if (!ModelState.IsValid)
        {
            Populate(form);

            return View(form);
        }

        await SaveAsync(_policies, item, isNew);
        await _notifier.SuccessAsync(H["The service level was saved."]);

        return RedirectToAction(nameof(Index), new { kind = ServiceLevels });
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

        return View(new WorkOrderSubStatusEditViewModel
        {
            Id = id,
            Name = item.Name,
            ParentStatus = string.IsNullOrEmpty(id) ? WorkOrderStatus.OnHold : item.ParentStatus,
            Color = item.Color,
            PausesSla = item.PausesSla,
        });
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

        item.Name = form.Name?.Trim();
        item.ParentStatus = form.ParentStatus;
        item.Color = string.IsNullOrWhiteSpace(form.Color) ? null : form.Color.Trim();
        item.PausesSla = form.PausesSla;

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

        var refused = kind switch
        {
            Types => await DeleteTypeAsync(id),
            Checklists => await DeleteUnusedAsync(_checklists, id, type => type.ChecklistTemplateIds.Contains(id)),
            ServiceLevels => await DeleteUnusedAsync(_policies, id, type => type.ServiceLevelPolicyId == id),
            SubStatuses => await DeleteUnusedAsync(_subStatuses, id, _ => false),
            _ => null,
        };

        if (refused is null)
        {
            return NotFound();
        }

        if (refused.Length > 0)
        {
            await _notifier.WarningAsync(H["{0} can't be deleted: work order types use it.", refused]);
        }
        else
        {
            await _notifier.SuccessAsync(H["Deleted."]);
        }

        return RedirectToAction(nameof(Index), new { kind });
    }

    private async Task<string> DeleteTypeAsync(string id)
    {
        var item = await _types.FindByIdAsync(id);

        if (item is null)
        {
            return null;
        }

        if (await _session.QueryIndex<WorkOrderIndex>(index => index.TypeId == id, collection: WorkOrdersConstants.CollectionName).FirstOrDefaultAsync(HttpContext.RequestAborted) is not null)
        {
            await _notifier.WarningAsync(H["{0} can't be deleted: work orders use it.", item.Name]);

            return string.Empty;
        }

        await _types.DeleteAsync(item);

        return string.Empty;
    }

    private async Task<string> DeleteUnusedAsync<T>(WorkOrderSettingManager<T> manager, string id, Func<WorkOrderType, bool> uses)
        where T : CrestApps.Core.Models.CatalogItem, new()
    {
        var item = await manager.FindByIdAsync(id);

        if (item is null)
        {
            return null;
        }

        if ((await _types.GetAllAsync()).Any(uses))
        {
            return NameOf(item);
        }

        await manager.DeleteAsync(item);

        return string.Empty;
    }

    private async Task<List<WorkOrderSettingRow>> TypeRowsAsync()
    {
        var policies = (await _policies.GetAllAsync()).ToDictionary(policy => policy.ItemId, policy => policy.Name, StringComparer.Ordinal);
        var counts = (await _session.QueryIndex<WorkOrderIndex>(collection: WorkOrdersConstants.CollectionName).ListAsync(HttpContext.RequestAborted))
            .GroupBy(index => index.TypeId ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        return [.. (await _types.GetAllAsync()).OrderBy(type => type.Name, StringComparer.CurrentCultureIgnoreCase).Select(type => new WorkOrderSettingRow
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

    private async Task<List<WorkOrderSettingRow>> PolicyRowsAsync()
    {
        return [.. (await _policies.GetAllAsync()).OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).Select(item => new WorkOrderSettingRow
        {
            Kind = ServiceLevels,
            Id = item.ItemId,
            Name = item.Name,
            EditAction = nameof(EditPolicy),
            Badges = [
                (item.WeeklyHours is { } weekly
                    ? S["{0} {1}–{2}", string.Join(" ", weekly.Days.Select(day => CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(day))), weekly.Opens.ToString("t", CultureInfo.CurrentCulture), weekly.Closes.ToString("t", CultureInfo.CurrentCulture)]
                    : S["Around the clock"]).Value,
                .. item.Targets.OrderByDescending(target => target.Priority).Select(target => S["{0}: respond {1}, resolve {2}", target.Priority, Duration(target.ResponseMinutes), Duration(target.ResolveMinutes)].Value),
            ],
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
        model.PolicyOptions = [.. (await _policies.GetAllAsync()).OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).Select(item => new SelectListItem(item.Name, item.ItemId, item.ItemId == model.ServiceLevelPolicyId))];
        model.ChecklistOptions = [.. (await _checklists.GetAllAsync()).OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).Select(item => new SelectListItem(item.Name, item.ItemId, model.ChecklistTemplateIds.Contains(item.ItemId)))];
        model.StockItemOptions = await _lookups.StockItemsAsync(HttpContext.RequestAborted);

        while (model.Lines.Count(line => string.IsNullOrWhiteSpace(line.Description)) < BlankLines)
        {
            model.Lines.Add(new LineEditModel());
        }
    }

    private void Populate(ServiceLevelPolicyEditViewModel model)
    {
        model.TimeZoneOptions = [.. _clock.GetTimeZones().Select(zone => zone.TimeZoneId).Order(StringComparer.Ordinal).Select(zoneId => new SelectListItem(zoneId, zoneId, string.Equals(zoneId, model.TimeZoneId, StringComparison.OrdinalIgnoreCase)))];

        foreach (var priority in Enum.GetValues<WorkOrderPriority>().OrderDescending())
        {
            if (!model.Targets.Any(target => target.Priority == priority))
            {
                model.Targets.Add(new TargetEditModel { Priority = priority });
            }
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

    internal static LineEditModel ToEditModel(WorkOrderLine line) => new()
    {
        Id = line.Id,
        Kind = line.Kind,
        Description = line.Description,
        StockItemId = line.StockItemId,
        Quantity = line.QuantityEstimated,
        UnitPrice = line.UnitPrice,
        Billable = line.Billable,
    };

    internal static WorkOrderLine ToLine(LineEditModel row, WorkOrderLine previous) => new()
    {
        Id = previous?.Id ?? (string.IsNullOrEmpty(row.Id) ? null : row.Id),
        Kind = row.Kind,
        Description = row.Description.Trim(),
        StockItemId = row.Kind == WorkOrderLineKind.Product && !string.IsNullOrEmpty(row.StockItemId) ? row.StockItemId : null,
        ProductId = previous?.ProductId,
        QuantityEstimated = Math.Max(0, row.Quantity),
        QuantityUsed = previous?.QuantityUsed ?? 0,
        LocationId = previous?.LocationId,
        UnitPrice = Math.Max(0, row.UnitPrice),
        Billable = row.Billable,
    };

    internal static string EditActionOf(string kind) => kind switch
    {
        Types => nameof(EditType),
        Checklists => nameof(EditChecklist),
        ServiceLevels => nameof(EditPolicy),
        _ => nameof(EditSubStatus),
    };

    private static string NameOf(object item) => item switch
    {
        WorkOrderType type => type.Name,
        ChecklistTemplate checklist => checklist.Name,
        ServiceLevelPolicy policy => policy.Name,
        WorkOrderSubStatus subStatus => subStatus.Name,
        _ => string.Empty,
    };

    private static List<string> Present(params LocalizedString[] badges) => [.. badges.Where(badge => badge is not null).Select(badge => badge.Value)];

    private static decimal? ToHours(int minutes) => minutes > 0 ? Math.Round(minutes / 60m, 2) : null;

    private static int ToMinutes(decimal? hours) => hours > 0 ? (int)Math.Round(hours.Value * 60m) : 0;

    private string Duration(int minutes) => minutes <= 0
        ? S["no target"]
        : minutes % 60 == 0 ? S["{0} h", minutes / 60] : S["{0} min", minutes];
}
