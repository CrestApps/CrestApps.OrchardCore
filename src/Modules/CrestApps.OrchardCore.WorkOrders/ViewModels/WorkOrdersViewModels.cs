using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.WorkOrders.ViewModels;

/// <summary>
/// A row of the work order list.
/// </summary>
public sealed class WorkOrderRow
{
    /// <summary>
    /// Gets or sets the work order.
    /// </summary>
    public WorkOrder WorkOrder { get; set; }

    /// <summary>
    /// Gets or sets its type's name.
    /// </summary>
    public string TypeName { get; set; }

    /// <summary>
    /// Gets or sets its sub-status's name.
    /// </summary>
    public string SubStatusName { get; set; }

    /// <summary>
    /// Gets or sets the time now.
    /// </summary>
    public DateTime NowUtc { get; set; }
}

/// <summary>
/// A row of one of the work order settings lists.
/// </summary>
public sealed class WorkOrderSettingRow
{
    /// <summary>
    /// Gets or sets the list it is on, such as types.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the record's id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets its name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the editor's action.
    /// </summary>
    public string EditAction { get; set; }

    /// <summary>
    /// Gets or sets its badges.
    /// </summary>
    public IList<string> Badges { get; set; } = [];
}

/// <summary>
/// One of the work order settings lists.
/// </summary>
public class WorkOrderSettingsListViewModel
{
    /// <summary>
    /// Gets or sets the list, such as types.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the editor's action.
    /// </summary>
    public string EditAction { get; set; }

    /// <summary>
    /// Gets or sets the rows.
    /// </summary>
    public AdminListContext List { get; set; }
}

/// <summary>
/// The work order list.
/// </summary>
public class WorkOrdersListViewModel
{
    /// <summary>
    /// Gets or sets the search.
    /// </summary>
    public string Q { get; set; }

    /// <summary>
    /// Gets or sets the type filter.
    /// </summary>
    public string TypeId { get; set; }

    /// <summary>
    /// Gets or sets the status filter: open, all, or one status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the priority filter.
    /// </summary>
    public string Priority { get; set; }

    /// <summary>
    /// Gets or sets the asset filter: the asset's service history.
    /// </summary>
    public string AssetId { get; set; }

    /// <summary>
    /// Gets or sets the name of the asset filtered by.
    /// </summary>
    [BindNever]
    public string AssetName { get; set; }

    /// <summary>
    /// Gets or sets the types.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TypeOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the rows.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }
}

/// <summary>
/// A line in an editor.
/// </summary>
public class LineEditModel
{
    /// <summary>
    /// Gets or sets the line's id, kept across edits.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets what it is.
    /// </summary>
    public WorkOrderLineKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the stock item, for a counted part.
    /// </summary>
    public string StockItemId { get; set; }

    /// <summary>
    /// Gets or sets how many are planned.
    /// </summary>
    public decimal Quantity { get; set; } = 1;

    /// <summary>
    /// Gets or sets the price of one.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Gets or sets whether the customer pays for it.
    /// </summary>
    public bool Billable { get; set; } = true;
}

/// <summary>
/// The work order type editor.
/// </summary>
public class WorkOrderTypeEditViewModel
{
    /// <summary>
    /// Gets or sets the id, or <see langword="null"/> for a new type.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the color.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets the minutes the job usually takes.
    /// </summary>
    public int DefaultDurationMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets how many people do it.
    /// </summary>
    public int CrewSize { get; set; } = 1;

    /// <summary>
    /// Gets or sets the usual priority.
    /// </summary>
    public WorkOrderPriority DefaultPriority { get; set; } = WorkOrderPriority.Normal;

    /// <summary>
    /// Gets or sets the service level policy.
    /// </summary>
    public string ServiceLevelPolicyId { get; set; }

    /// <summary>
    /// Gets or sets how it is billed.
    /// </summary>
    public WorkOrderBillingMode BillingMode { get; set; }

    /// <summary>
    /// Gets or sets the currency.
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Gets or sets the fixed price.
    /// </summary>
    public decimal? FixedPrice { get; set; }

    /// <summary>
    /// Gets or sets whether the customer must sign.
    /// </summary>
    public bool RequiresSignature { get; set; }

    /// <summary>
    /// Gets or sets how many photos it needs.
    /// </summary>
    public int RequiredPhotos { get; set; }

    /// <summary>
    /// Gets or sets the checklists every work order of the type gets.
    /// </summary>
    public List<string> ChecklistTemplateIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the lines every work order of the type starts with.
    /// </summary>
    public List<LineEditModel> Lines { get; set; } = [];

    /// <summary>
    /// Gets or sets the policies.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> PolicyOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the checklists.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> ChecklistOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the stock items, when Inventory is on.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> StockItemOptions { get; set; } = [];
}

/// <summary>
/// A question in the checklist editor.
/// </summary>
public class QuestionEditModel
{
    /// <summary>
    /// Gets or sets the question's id, kept across edits.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the section.
    /// </summary>
    public string Section { get; set; }

    /// <summary>
    /// Gets or sets the question.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the kind of answer.
    /// </summary>
    public ChecklistQuestionKind Kind { get; set; }

    /// <summary>
    /// Gets or sets whether it must be answered.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the choices, one per line.
    /// </summary>
    public string Choices { get; set; }

    /// <summary>
    /// Gets or sets the lowest number.
    /// </summary>
    public decimal? Min { get; set; }

    /// <summary>
    /// Gets or sets the highest number.
    /// </summary>
    public decimal? Max { get; set; }

    /// <summary>
    /// Gets or sets the unit.
    /// </summary>
    public string Unit { get; set; }

    /// <summary>
    /// Gets or sets the help.
    /// </summary>
    public string Help { get; set; }
}

/// <summary>
/// The checklist template editor.
/// </summary>
public class ChecklistTemplateEditViewModel
{
    /// <summary>
    /// Gets or sets the id, or <see langword="null"/> for a new checklist.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the questions.
    /// </summary>
    public List<QuestionEditModel> Questions { get; set; } = [];
}

/// <summary>
/// The targets of a priority in the service level editor.
/// </summary>
public class TargetEditModel
{
    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    public WorkOrderPriority Priority { get; set; }

    /// <summary>
    /// Gets or sets the hours to respond in.
    /// </summary>
    public decimal? ResponseHours { get; set; }

    /// <summary>
    /// Gets or sets the hours to resolve in.
    /// </summary>
    public decimal? ResolveHours { get; set; }
}

/// <summary>
/// The service level policy editor.
/// </summary>
public class ServiceLevelPolicyEditViewModel
{
    /// <summary>
    /// Gets or sets the id, or <see langword="null"/> for a new policy.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the time zone.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets whether the clock runs only in opening hours.
    /// </summary>
    public bool BusinessHoursOnly { get; set; }

    /// <summary>
    /// Gets or sets the days open.
    /// </summary>
    public List<DayOfWeek> Days { get; set; } = [];

    /// <summary>
    /// Gets or sets when they open.
    /// </summary>
    public TimeOnly Opens { get; set; } = new(9, 0);

    /// <summary>
    /// Gets or sets when they close.
    /// </summary>
    public TimeOnly Closes { get; set; } = new(17, 0);

    /// <summary>
    /// Gets or sets the share of a target used up when the warning is raised.
    /// </summary>
    public int WarningPercent { get; set; } = 80;

    /// <summary>
    /// Gets or sets the statuses the clock stops in.
    /// </summary>
    public List<WorkOrderStatus> PauseStatuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the targets, one per priority.
    /// </summary>
    public List<TargetEditModel> Targets { get; set; } = [];

    /// <summary>
    /// Gets or sets the time zones.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TimeZoneOptions { get; set; } = [];
}

/// <summary>
/// The sub-status editor.
/// </summary>
public class WorkOrderSubStatusEditViewModel
{
    /// <summary>
    /// Gets or sets the id, or <see langword="null"/> for a new sub-status.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the status it belongs to.
    /// </summary>
    public WorkOrderStatus ParentStatus { get; set; } = WorkOrderStatus.OnHold;

    /// <summary>
    /// Gets or sets the color.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets whether it stops the service level clock.
    /// </summary>
    public bool PausesSla { get; set; }
}

/// <summary>
/// The work order editor.
/// </summary>
public class WorkOrderEditViewModel
{
    /// <summary>
    /// Gets or sets the id, or <see langword="null"/> for a new work order.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public string TypeId { get; set; }

    /// <summary>
    /// Gets or sets the number.
    /// </summary>
    [BindNever]
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the type's name.
    /// </summary>
    [BindNever]
    public string TypeName { get; set; }

    /// <summary>
    /// Gets or sets the summary.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    public WorkOrderPriority Priority { get; set; }

    /// <summary>
    /// Gets or sets the minutes the job should take.
    /// </summary>
    public int EstimatedMinutes { get; set; }

    /// <summary>
    /// Gets or sets the contact's name.
    /// </summary>
    public string ContactName { get; set; }

    /// <summary>
    /// Gets or sets the contact's phone.
    /// </summary>
    public string ContactPhone { get; set; }

    /// <summary>
    /// Gets or sets the contact's email.
    /// </summary>
    public string ContactEmail { get; set; }

    /// <summary>
    /// Gets or sets the street address.
    /// </summary>
    public string AddressLine1 { get; set; }

    /// <summary>
    /// Gets or sets the second address line.
    /// </summary>
    public string AddressLine2 { get; set; }

    /// <summary>
    /// Gets or sets the city.
    /// </summary>
    public string City { get; set; }

    /// <summary>
    /// Gets or sets the region.
    /// </summary>
    public string Region { get; set; }

    /// <summary>
    /// Gets or sets the postal code.
    /// </summary>
    public string PostalCode { get; set; }

    /// <summary>
    /// Gets or sets the country.
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// Gets or sets how to get in.
    /// </summary>
    public string AccessNotes { get; set; }

    /// <summary>
    /// Gets or sets the earliest the visit can be, in the site's time zone.
    /// </summary>
    public DateTime? Earliest { get; set; }

    /// <summary>
    /// Gets or sets the latest the visit can be, in the site's time zone.
    /// </summary>
    public DateTime? Latest { get; set; }

    /// <summary>
    /// Gets or sets the assets it is for.
    /// </summary>
    public List<string> AssetIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the lines.
    /// </summary>
    public List<LineEditModel> Lines { get; set; } = [];

    /// <summary>
    /// Gets or sets the assets, when Assets is on.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> AssetOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the stock items, when Inventory is on.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> StockItemOptions { get; set; } = [];
}

/// <summary>
/// A work order's page.
/// </summary>
public class WorkOrderDetailsViewModel
{
    /// <summary>
    /// Gets or sets the work order.
    /// </summary>
    public WorkOrder WorkOrder { get; set; }

    /// <summary>
    /// Gets or sets its type.
    /// </summary>
    public WorkOrderType Type { get; set; }

    /// <summary>
    /// Gets or sets its service level policy.
    /// </summary>
    public ServiceLevelPolicy Policy { get; set; }

    /// <summary>
    /// Gets or sets its sub-status.
    /// </summary>
    public WorkOrderSubStatus SubStatus { get; set; }

    /// <summary>
    /// Gets or sets the sub-statuses.
    /// </summary>
    public IList<WorkOrderSubStatus> SubStatuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the statuses it can move to, without Completed.
    /// </summary>
    public IList<WorkOrderStatus> NextStatuses { get; set; } = [];

    /// <summary>
    /// Gets or sets whether it can be completed now.
    /// </summary>
    public bool CanComplete { get; set; }

    /// <summary>
    /// Gets or sets whether the user can change it.
    /// </summary>
    public bool CanManage { get; set; }

    /// <summary>
    /// Gets or sets whether parts come off Inventory.
    /// </summary>
    public bool TracksParts { get; set; }

    /// <summary>
    /// Gets or sets the amount it would be billed.
    /// </summary>
    public decimal BillableAmount { get; set; }

    /// <summary>
    /// Gets or sets the names of its assets.
    /// </summary>
    public IList<SelectListItem> Assets { get; set; } = [];

    /// <summary>
    /// Gets or sets the stock locations.
    /// </summary>
    public IList<SelectListItem> LocationOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the work order it follows up, if any.
    /// </summary>
    public WorkOrder Parent { get; set; }

    /// <summary>
    /// Gets or sets the time now.
    /// </summary>
    public DateTime NowUtc { get; set; }
}
