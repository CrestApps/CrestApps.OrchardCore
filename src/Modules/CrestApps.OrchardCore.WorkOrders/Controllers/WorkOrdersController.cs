using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.WorkOrders.Core;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using CrestApps.OrchardCore.WorkOrders.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.WorkOrders.Controllers;

/// <summary>
/// Work orders: the list, the editor and a work order's page, where it is worked.
/// </summary>
public sealed class WorkOrdersController : Controller
{
    /// <summary>
    /// The status filter for the work orders still to finish.
    /// </summary>
    public const string OpenFilter = "open";

    /// <summary>
    /// The status filter for every work order.
    /// </summary>
    public const string AllFilter = "all";

    private const int BlankLines = 2;

    private readonly IAuthorizationService _authorizationService;
    private readonly WorkOrderService _service;
    private readonly WorkOrderSettingManager<WorkOrderType> _types;
    private readonly WorkOrderSettingManager<ServiceLevelPolicy> _policies;
    private readonly WorkOrderSettingManager<WorkOrderSubStatus> _subStatuses;
    private readonly WorkOrderLookups _lookups;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrdersController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="service">The work order service.</param>
    /// <param name="types">The types.</param>
    /// <param name="policies">The service level policies.</param>
    /// <param name="subStatuses">The sub-statuses.</param>
    /// <param name="lookups">The lookups.</param>
    /// <param name="session">The session.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="localClock">The local clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public WorkOrdersController(
        IAuthorizationService authorizationService,
        WorkOrderService service,
        WorkOrderSettingManager<WorkOrderType> types,
        WorkOrderSettingManager<ServiceLevelPolicy> policies,
        WorkOrderSettingManager<WorkOrderSubStatus> subStatuses,
        WorkOrderLookups lookups,
        ISession session,
        IClock clock,
        ILocalClock localClock,
        INotifier notifier,
        IHtmlLocalizer<WorkOrdersController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _service = service;
        _types = types;
        _policies = policies;
        _subStatuses = subStatuses;
        _lookups = lookups;
        _session = session;
        _clock = clock;
        _localClock = localClock;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the work orders, searched by number or summary, by type, status and priority.
    /// </summary>
    /// <param name="form">The search and filters.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The page.</returns>
    [Admin("work-orders", "WorkOrders")]
    public async Task<IActionResult> Index(WorkOrdersListViewModel form, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkOrdersPermissions.ViewWorkOrders))
        {
            return Forbid();
        }

        form.Status ??= OpenFilter;

        var types = (await _types.GetAllAsync()).OrderBy(type => type.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var subStatuses = (await _subStatuses.GetAllAsync()).ToDictionary(item => item.ItemId, item => item.Name, StringComparer.Ordinal);
        var query = _session.Query<WorkOrder, WorkOrderIndex>(collection: WorkOrdersConstants.CollectionName);

        if (form.Status == OpenFilter)
        {
            query = query.Where(index => index.IsOpen);
        }
        else if (Enum.TryParse<WorkOrderStatus>(form.Status, out var status))
        {
            var value = status.ToString();

            query = query.Where(index => index.Status == value);
        }

        if (!string.IsNullOrEmpty(form.TypeId))
        {
            query = query.Where(index => index.TypeId == form.TypeId);
        }

        if (Enum.TryParse<WorkOrderPriority>(form.Priority, out var priority))
        {
            var value = priority.ToString();

            query = query.Where(index => index.Priority == value);
        }

        if (!string.IsNullOrWhiteSpace(form.Q))
        {
            var q = form.Q.Trim();

            query = query.Where(index => index.Number.Contains(q) || index.Summary.Contains(q));
        }

        if (!string.IsNullOrEmpty(form.AssetId))
        {
            var ids = (await _session.QueryIndex<WorkOrderAssetIndex>(index => index.AssetId == form.AssetId, collection: WorkOrdersConstants.CollectionName).ListAsync(HttpContext.RequestAborted))
                .Select(index => index.WorkOrderId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            query = query.Where(index => index.ItemId.IsIn(ids));
            form.AssetName = (await _lookups.AssetsAsync(HttpContext.RequestAborted)).FirstOrDefault(option => option.Value == form.AssetId)?.Text ?? form.AssetId;
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var count = await query.CountAsync(HttpContext.RequestAborted);
        var page = await query.OrderByDescending(index => index.CreatedUtc).Skip(pager.GetStartIndex()).Take(pager.PageSize).ListAsync(HttpContext.RequestAborted);
        var typeNames = types.ToDictionary(type => type.ItemId, type => type.Name, StringComparer.Ordinal);
        var now = _clock.UtcNow;

        form.TypeOptions = [.. types.Select(type => new SelectListItem(type.Name, type.ItemId, type.ItemId == form.TypeId))];
        form.List = new AdminListContext<WorkOrderRow>
        {
            Source = page.Select(workOrder => new WorkOrderRow
            {
                WorkOrder = workOrder,
                TypeName = typeNames.GetValueOrDefault(workOrder.TypeId ?? string.Empty),
                SubStatusName = subStatuses.GetValueOrDefault(workOrder.SubStatusId ?? string.Empty),
                NowUtc = now,
            }).ToList(),
            Pager = await shapeFactory.PagerAsync(pager, count),
        };

        return View(form);
    }

    /// <summary>
    /// Shows the editor for a new work order of a type.
    /// </summary>
    /// <param name="typeId">The type.</param>
    /// <returns>The page.</returns>
    [Admin("work-orders/new/{typeId}", "WorkOrdersNew")]
    public async Task<IActionResult> New(string typeId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkOrdersPermissions.ManageWorkOrders))
        {
            return Forbid();
        }

        var workOrder = await _service.NewAsync(typeId, HttpContext.RequestAborted);

        if (workOrder is null)
        {
            return NotFound();
        }

        var model = await ToEditModelAsync(workOrder);

        await PopulateAsync(model);

        return View("Edit", model);
    }

    /// <summary>
    /// Creates a work order.
    /// </summary>
    /// <param name="typeId">The type.</param>
    /// <param name="form">The form.</param>
    /// <returns>Its page, or the editor with its errors.</returns>
    [HttpPost]
    [ActionName(nameof(New))]
    [Admin("work-orders/new/{typeId}", "WorkOrdersNew")]
    public async Task<IActionResult> NewPost(string typeId, WorkOrderEditViewModel form)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkOrdersPermissions.ManageWorkOrders))
        {
            return Forbid();
        }

        var workOrder = await _service.NewAsync(typeId, HttpContext.RequestAborted);

        if (workOrder is null)
        {
            return NotFound();
        }

        await ApplyAsync(form, workOrder);

        var result = await _service.CreateAsync(workOrder, User.Identity?.Name, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            return await RedisplayAsync(form, result, typeId);
        }

        await _notifier.SuccessAsync(H["Work order {0} was created.", workOrder.Number]);

        return RedirectToAction(nameof(Details), new { id = workOrder.ItemId });
    }

    /// <summary>
    /// Shows the editor.
    /// </summary>
    /// <param name="id">The work order.</param>
    /// <returns>The page.</returns>
    [Admin("work-orders/edit/{id}", "WorkOrdersEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkOrdersPermissions.ManageWorkOrders))
        {
            return Forbid();
        }

        var workOrder = await _service.FindAsync(id, HttpContext.RequestAborted);

        if (workOrder is null)
        {
            return NotFound();
        }

        var model = await ToEditModelAsync(workOrder);

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Saves the editor.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <returns>Its page, or the editor with its errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("work-orders/edit/{id}", "WorkOrdersEdit")]
    public async Task<IActionResult> EditPost(WorkOrderEditViewModel form)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkOrdersPermissions.ManageWorkOrders))
        {
            return Forbid();
        }

        var workOrder = await _service.FindAsync(form.Id, HttpContext.RequestAborted);

        if (workOrder is null)
        {
            return NotFound();
        }

        await ApplyAsync(form, workOrder);

        var result = await _service.UpdateAsync(workOrder, User.Identity?.Name, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            return await RedisplayAsync(form, result, workOrder.TypeId);
        }

        await _notifier.SuccessAsync(H["Work order {0} was saved.", workOrder.Number]);

        return RedirectToAction(nameof(Details), new { id = workOrder.ItemId });
    }

    /// <summary>
    /// Shows a work order: its service levels, status, lines, checklists, signatures and notes.
    /// </summary>
    /// <param name="id">The work order.</param>
    /// <returns>The page.</returns>
    [Admin("work-orders/view/{id}", "WorkOrdersView")]
    public async Task<IActionResult> Details(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkOrdersPermissions.ViewWorkOrders))
        {
            return Forbid();
        }

        var workOrder = await _service.FindAsync(id, HttpContext.RequestAborted);

        if (workOrder is null)
        {
            return NotFound();
        }

        var type = string.IsNullOrEmpty(workOrder.TypeId) ? null : await _types.FindByIdAsync(workOrder.TypeId);
        var subStatuses = (await _subStatuses.GetAllAsync()).OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var assets = workOrder.AssetIds.Count == 0 ? [] : await _lookups.AssetsAsync(HttpContext.RequestAborted);
        DateOnly? visitDay = null;

        if (workOrder.EarliestUtc is { } earliest && workOrder.LatestUtc is { } latest)
        {
            var from = (await _localClock.ConvertToLocalAsync(earliest)).DateTime;
            var to = (await _localClock.ConvertToLocalAsync(latest)).DateTime;

            if (from.TimeOfDay == TimeSpan.Zero && to == from.AddDays(1))
            {
                visitDay = DateOnly.FromDateTime(from);
            }
        }

        return View(new WorkOrderDetailsViewModel
        {
            VisitDay = visitDay,
            WorkOrder = workOrder,
            Type = type,
            Policy = string.IsNullOrEmpty(workOrder.Sla?.PolicyId) ? null : await _policies.FindByIdAsync(workOrder.Sla.PolicyId),
            SubStatus = subStatuses.FirstOrDefault(item => item.ItemId == workOrder.SubStatusId),
            SubStatuses = subStatuses,
            NextStatuses = [.. WorkOrderStatusRules.Next(workOrder.Status).Where(status => status != WorkOrderStatus.Completed)],
            CanComplete = WorkOrderStatusRules.CanMove(workOrder.Status, WorkOrderStatus.Completed),
            CanManage = await _authorizationService.AuthorizeAsync(User, WorkOrdersPermissions.ManageWorkOrders),
            TracksParts = _service.TracksParts,
            BillableAmount = type is null ? 0 : WorkOrderService.BillableAmount(workOrder, type),
            Assets = [.. assets.Where(asset => workOrder.AssetIds.Contains(asset.Value))],
            LocationOptions = _service.TracksParts ? await _lookups.LocationsAsync(HttpContext.RequestAborted) : [],
            Parent = string.IsNullOrEmpty(workOrder.ParentWorkOrderId) ? null : await _service.FindAsync(workOrder.ParentWorkOrderId, HttpContext.RequestAborted),
            NowUtc = _clock.UtcNow,
        });
    }

    /// <summary>
    /// Moves a work order to another status, or sub-status.
    /// </summary>
    /// <param name="id">The work order.</param>
    /// <param name="status">The status.</param>
    /// <param name="subStatusId">The sub-status, if any.</param>
    /// <param name="note">Why.</param>
    /// <returns>Its page.</returns>
    [HttpPost]
    [Admin("work-orders/view/{id}/status", "WorkOrdersStatus")]
    public Task<IActionResult> Status(string id, WorkOrderStatus status, string subStatusId, string note)
        => ActAsync(id, () => _service.ChangeStatusAsync(id, status, subStatusId, note, User.Identity?.Name, HttpContext.RequestAborted), null);

    /// <summary>
    /// Records parts, labor or expenses used on a line.
    /// </summary>
    /// <param name="id">The work order.</param>
    /// <param name="lineId">The line.</param>
    /// <param name="quantity">How many.</param>
    /// <param name="locationId">Where the parts came from.</param>
    /// <returns>Its page.</returns>
    [HttpPost]
    [Admin("work-orders/view/{id}/use", "WorkOrdersUse")]
    public Task<IActionResult> Use(string id, string lineId, decimal quantity, string locationId)
        => ActAsync(id, () => _service.UsePartAsync(id, lineId, quantity, locationId, User.Identity?.Name, HttpContext.RequestAborted), H["Recorded."]);

    /// <summary>
    /// Saves the answers of a checklist.
    /// </summary>
    /// <param name="id">The work order.</param>
    /// <param name="checklist">The checklist, by position.</param>
    /// <returns>Its page.</returns>
    [HttpPost]
    [Admin("work-orders/view/{id}/checklists/{checklist}", "WorkOrdersAnswers")]
    public async Task<IActionResult> Answers(string id, int checklist)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkOrdersPermissions.ManageWorkOrders))
        {
            return Forbid();
        }

        var workOrder = await _service.FindAsync(id, HttpContext.RequestAborted);

        if (workOrder is null || checklist < 0 || checklist >= workOrder.Checklists.Count)
        {
            return NotFound();
        }

        var errors = new List<string>();

        foreach (var question in workOrder.Checklists[checklist].Questions.ToList())
        {
            var values = Request.Form[AnswerField(question)];
            var value = values.Count == 0 ? null : string.Join('\n', values.Where(item => !string.IsNullOrWhiteSpace(item)));
            var current = workOrder.Checklists[checklist].Answers.FirstOrDefault(answer => answer.QuestionId == question.Id)?.Value;

            if (string.Equals(string.IsNullOrWhiteSpace(value) ? null : value.Trim(), current, StringComparison.Ordinal))
            {
                continue;
            }

            var result = await _service.AnswerAsync(id, checklist, question.Id, value, User.Identity?.Name, HttpContext.RequestAborted);

            errors.AddRange(result.Errors);
        }

        await NotifyAsync(errors, H["The answers were saved."]);

        return RedirectToAction(nameof(Details), null, new { id }, "checklist-" + checklist);
    }

    /// <summary>
    /// Records who signed for the work.
    /// </summary>
    /// <param name="id">The work order.</param>
    /// <param name="name">Who signed.</param>
    /// <returns>Its page.</returns>
    [HttpPost]
    [Admin("work-orders/view/{id}/sign", "WorkOrdersSign")]
    public Task<IActionResult> Sign(string id, string name)
        => ActAsync(id, () => _service.SignAsync(id, name, null, User.Identity?.Name, HttpContext.RequestAborted), H["The signature was recorded."]);

    /// <summary>
    /// Adds a note.
    /// </summary>
    /// <param name="id">The work order.</param>
    /// <param name="text">The note.</param>
    /// <returns>Its page.</returns>
    [HttpPost]
    [Admin("work-orders/view/{id}/note", "WorkOrdersNote")]
    public Task<IActionResult> Note(string id, string text)
        => ActAsync(id, () => _service.AddNoteAsync(id, text, User.Identity?.Name, HttpContext.RequestAborted), H["The note was added."]);

    /// <summary>
    /// Completes a work order once its checklists, signature and photos are done, and invoices it.
    /// </summary>
    /// <param name="id">The work order.</param>
    /// <returns>Its page.</returns>
    [HttpPost]
    [Admin("work-orders/view/{id}/complete", "WorkOrdersComplete")]
    public Task<IActionResult> Complete(string id)
        => ActAsync(id, () => _service.CompleteAsync(id, User.Identity?.Name, HttpContext.RequestAborted), H["The work order was completed."]);

    /// <summary>
    /// Makes a follow-up work order for the same customer, place and assets.
    /// </summary>
    /// <param name="id">The work order.</param>
    /// <param name="summary">What the follow-up is for.</param>
    /// <returns>The follow-up's page.</returns>
    [HttpPost]
    [Admin("work-orders/view/{id}/follow-up", "WorkOrdersFollowUp")]
    public async Task<IActionResult> FollowUp(string id, string summary)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkOrdersPermissions.ManageWorkOrders))
        {
            return Forbid();
        }

        var result = await _service.FollowUpAsync(id, summary, User.Identity?.Name, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            await NotifyAsync(result.Errors, null);

            return RedirectToAction(nameof(Details), new { id });
        }

        await _notifier.SuccessAsync(H["Follow-up {0} was created.", result.WorkOrder.Number]);

        return RedirectToAction(nameof(Details), new { id = result.WorkOrder.ItemId });
    }

    /// <summary>
    /// Gives the form field of a checklist question's answer.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <returns>The field name.</returns>
    public static string AnswerField(ChecklistQuestion question) => "answer-" + question?.Id;

    private async Task<IActionResult> ActAsync(string id, Func<Task<WorkOrderResult>> act, LocalizedHtmlString done)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkOrdersPermissions.ManageWorkOrders))
        {
            return Forbid();
        }

        var result = await act();

        await NotifyAsync(result.Errors, done);

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task NotifyAsync(IEnumerable<string> errors, LocalizedHtmlString done)
    {
        var any = false;

        foreach (var error in errors)
        {
            any = true;
            await _notifier.ErrorAsync(H["{0}", error]);
        }

        if (!any && done is not null)
        {
            await _notifier.SuccessAsync(done);
        }
    }

    private async Task<IActionResult> RedisplayAsync(WorkOrderEditViewModel form, WorkOrderResult result, string typeId)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        form.TypeId = typeId;
        form.TypeName = (await _types.FindByIdAsync(typeId))?.Name;
        form.Number = result.WorkOrder?.Number;
        await PopulateAsync(form);

        return View("Edit", form);
    }

    private async Task<WorkOrderEditViewModel> ToEditModelAsync(WorkOrder workOrder)
    {
        var address = workOrder.ServiceAddress;

        return new WorkOrderEditViewModel
        {
            Id = string.IsNullOrEmpty(workOrder.Number) ? null : workOrder.ItemId,
            TypeId = workOrder.TypeId,
            TypeName = (await _types.FindByIdAsync(workOrder.TypeId))?.Name,
            Number = workOrder.Number,
            Summary = workOrder.Summary,
            Description = workOrder.Description,
            Priority = workOrder.Priority,
            EstimatedMinutes = workOrder.EstimatedMinutes,
            ContactName = workOrder.Contact?.Name,
            ContactPhone = workOrder.Contact?.Phone,
            ContactEmail = workOrder.Contact?.Email,
            AddressLine1 = address?.AddressLine1,
            AddressLine2 = address?.AddressLine2,
            City = address?.City,
            Region = address?.Region,
            PostalCode = address?.PostalCode,
            Country = address?.Country,
            AccessNotes = workOrder.AccessNotes,
            Earliest = workOrder.EarliestUtc is { } earliest ? (await _localClock.ConvertToLocalAsync(earliest)).DateTime : null,
            Latest = workOrder.LatestUtc is { } latest ? (await _localClock.ConvertToLocalAsync(latest)).DateTime : null,
            AssetIds = [.. workOrder.AssetIds],
            Lines = [.. workOrder.Lines.Select(WorkOrderSettingsController.ToEditModel)],
        };
    }

    private async Task ApplyAsync(WorkOrderEditViewModel form, WorkOrder workOrder)
    {
        workOrder.Summary = form.Summary?.Trim();
        workOrder.Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim();
        workOrder.Priority = form.Priority;
        workOrder.EstimatedMinutes = Math.Max(0, form.EstimatedMinutes);
        workOrder.Contact = new WorkOrderContact { Name = Clean(form.ContactName), Phone = Clean(form.ContactPhone), Email = Clean(form.ContactEmail) };
        workOrder.AccessNotes = Clean(form.AccessNotes);
        workOrder.EarliestUtc = form.Earliest is { } earliest ? await _localClock.ConvertToUtcAsync(earliest) : null;
        workOrder.LatestUtc = form.Latest is { } latest ? await _localClock.ConvertToUtcAsync(latest) : null;
        workOrder.AssetIds = [.. (form.AssetIds ?? []).Where(assetId => !string.IsNullOrEmpty(assetId)).Distinct(StringComparer.Ordinal)];

        var previous = workOrder.Lines.Where(line => !string.IsNullOrEmpty(line.Id)).ToDictionary(line => line.Id, StringComparer.Ordinal);

        workOrder.Lines = [.. (form.Lines ?? []).Where(line => !string.IsNullOrWhiteSpace(line.Description)).Select(line => WorkOrderSettingsController.ToLine(line, string.IsNullOrEmpty(line.Id) ? null : previous.GetValueOrDefault(line.Id)))];

        // Lines that already used parts stay, even when their row was cleared: what was used is a record.
        foreach (var used in previous.Values.Where(line => line.QuantityUsed > 0 && !workOrder.Lines.Any(item => item.Id == line.Id)))
        {
            workOrder.Lines.Add(used);
        }

        var address = new Address
        {
            AddressLine1 = Clean(form.AddressLine1),
            AddressLine2 = Clean(form.AddressLine2),
            City = Clean(form.City),
            Region = Clean(form.Region),
            PostalCode = Clean(form.PostalCode),
            Country = Clean(form.Country),
        };
        var current = workOrder.ServiceAddress;

        if (address.AddressLine1 is null && address.City is null && address.PostalCode is null)
        {
            workOrder.ServiceAddress = null;
        }
        else if (current is null
            || current.AddressLine1 != address.AddressLine1
            || current.AddressLine2 != address.AddressLine2
            || current.City != address.City
            || current.Region != address.Region
            || current.PostalCode != address.PostalCode
            || current.Country != address.Country)
        {
            // A new address has no point until it is geocoded.
            workOrder.ServiceAddress = address;
        }
    }

    private async Task PopulateAsync(WorkOrderEditViewModel model)
    {
        model.AssetOptions = [.. (await _lookups.AssetsAsync(HttpContext.RequestAborted)).Select(option => new SelectListItem(option.Text, option.Value, model.AssetIds.Contains(option.Value)))];
        model.StockItemOptions = await _lookups.StockItemsAsync(HttpContext.RequestAborted);

        while (model.Lines.Count(line => string.IsNullOrWhiteSpace(line.Description)) < BlankLines)
        {
            model.Lines.Add(new LineEditModel());
        }
    }

    private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
