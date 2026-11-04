using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using YesSql;

namespace CrestApps.OrchardCore.WorkOrders.Integrations;

/// <summary>
/// The constants of the Create work order subject action.
/// </summary>
public static class WorkOrderSubjectActions
{
    /// <summary>
    /// The action type.
    /// </summary>
    public const string CreateWorkOrder = "CreateWorkOrder";
}

/// <summary>
/// What a Create work order action makes: the type and the priority.
/// </summary>
public sealed class CreateWorkOrderActionMetadata
{
    /// <summary>
    /// Gets or sets the work order type.
    /// </summary>
    public string TypeId { get; set; }

    /// <summary>
    /// Gets or sets the priority, or <see langword="null"/> for the type's.
    /// </summary>
    public WorkOrderPriority? Priority { get; set; }
}

/// <summary>
/// Makes a work order of the chosen type for the activity's contact when its disposition is chosen, once per activity,
/// with the activity's notes as the description.
/// </summary>
internal sealed class CreateWorkOrderSubjectActionHandler : ISubjectActionHandler
{
    private const int MaxSummary = 120;

    private readonly WorkOrderService _workOrders;
    private readonly ISession _session;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public CreateWorkOrderSubjectActionHandler(WorkOrderService workOrders, ISession session, ILogger<CreateWorkOrderSubjectActionHandler> logger, IStringLocalizer<CreateWorkOrderSubjectActionHandler> stringLocalizer)
    {
        _workOrders = workOrders;
        _session = session;
        _logger = logger;
        S = stringLocalizer;
    }

    public string ActionType => WorkOrderSubjectActions.CreateWorkOrder;

    public async Task ExecuteAsync(SubjectAction action, SubjectActionExecutionContext context)
    {
        var activity = context.Activity;

        if (activity is null || !action.TryGet<CreateWorkOrderActionMetadata>(out var metadata) || string.IsNullOrEmpty(metadata.TypeId))
        {
            return;
        }

        // An activity makes one work order, however often its disposition is saved.
        var existing = await _session.QueryIndex<WorkOrderIndex>(index => index.Source == nameof(WorkOrderSource.Activity) && index.SourceId == activity.ItemId, collection: WorkOrdersConstants.CollectionName).FirstOrDefaultAsync();

        if (existing is not null)
        {
            return;
        }

        var workOrder = await _workOrders.NewAsync(metadata.TypeId);

        if (workOrder is null)
        {
            _logger.LogWarning("The Create work order action of activity {ActivityId} names a type that no longer exists.", activity.ItemId.SanitizeLogValue());

            return;
        }

        var contactName = context.Contact?.DisplayText;
        var notes = activity.Notes?.Trim();
        var firstLine = notes?.Split('\n')[0].Trim();

        // The summary is the notes' first line, shortened; the description keeps them whole.
        workOrder.Summary = string.IsNullOrEmpty(firstLine)
            ? S["Follow-up of {0}", string.IsNullOrEmpty(contactName) ? activity.Channel : contactName].Value
            : firstLine.Length > MaxSummary ? firstLine[..MaxSummary].TrimEnd() + "…" : firstLine;
        workOrder.Description = notes;
        workOrder.Priority = metadata.Priority ?? workOrder.Priority;
        workOrder.ContactId = context.Contact?.ContentItemId ?? activity.ContactContentItemId;
        workOrder.AccountId = WorkOrderCustomers.AccountOf(context.Contact);
        workOrder.Contact = new WorkOrderContact { Name = contactName, Phone = activity.PreferredDestination };
        workOrder.Source = WorkOrderSource.Activity;
        workOrder.SourceId = activity.ItemId;

        var result = await _workOrders.CreateAsync(workOrder, activity.CompletedByUsername ?? activity.AssignedToUsername);

        if (result.Errors.Count > 0)
        {
            _logger.LogWarning("The Create work order action of activity {ActivityId} could not create the work order: {Errors}", activity.ItemId.SanitizeLogValue(), string.Join(" ", result.Errors));
        }
    }
}

/// <summary>
/// Edits the type and priority of a Create work order action.
/// </summary>
internal sealed class CreateWorkOrderSubjectActionDisplayDriver : DisplayDriver<SubjectAction>
{
    private readonly WorkOrderSettingManager<WorkOrderType> _types;

    internal readonly IStringLocalizer S;

    public CreateWorkOrderSubjectActionDisplayDriver(WorkOrderSettingManager<WorkOrderType> types, IStringLocalizer<CreateWorkOrderSubjectActionDisplayDriver> stringLocalizer)
    {
        _types = types;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(SubjectAction action, BuildEditorContext context)
    {
        if (!IsCreateWorkOrder(action))
        {
            return null;
        }

        return Initialize<CreateWorkOrderSubjectActionViewModel>("CreateWorkOrderSubjectActionFields_Edit", async model =>
        {
            var metadata = action.TryGet<CreateWorkOrderActionMetadata>(out var stored) ? stored : new CreateWorkOrderActionMetadata();

            model.TypeId = metadata.TypeId;
            model.Priority = metadata.Priority?.ToString();
            model.Types = [.. (await _types.GetAllAsync())
                .OrderBy(type => type.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(type => new SelectListItem(type.Name, type.ItemId, type.ItemId == metadata.TypeId))];
            model.Priorities =
            [
                new SelectListItem(S["The type's"], string.Empty, metadata.Priority is null),
                .. Enum.GetValues<WorkOrderPriority>().Select(priority => new SelectListItem(PriorityText(priority), priority.ToString(), priority == metadata.Priority)),
            ];
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(SubjectAction action, UpdateEditorContext context)
    {
        if (!IsCreateWorkOrder(action))
        {
            return null;
        }

        var model = new CreateWorkOrderSubjectActionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrEmpty(model.TypeId) || await _types.FindByIdAsync(model.TypeId) is null)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.TypeId), S["Choose the type of work order to make."]);
        }

        action.Put(new CreateWorkOrderActionMetadata
        {
            TypeId = model.TypeId,
            Priority = Enum.TryParse<WorkOrderPriority>(model.Priority, out var priority) ? priority : null,
        });

        return Edit(action, context);
    }

    private string PriorityText(WorkOrderPriority priority) => priority switch
    {
        WorkOrderPriority.Low => S["Low"],
        WorkOrderPriority.High => S["High"],
        WorkOrderPriority.Urgent => S["Urgent"],
        _ => S["Normal"],
    };

    private static bool IsCreateWorkOrder(SubjectAction action)
        => string.Equals(action.Source, WorkOrderSubjectActions.CreateWorkOrder, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The fields of a Create work order action.
/// </summary>
public class CreateWorkOrderSubjectActionViewModel
{
    /// <summary>
    /// Gets or sets the work order type.
    /// </summary>
    public string TypeId { get; set; }

    /// <summary>
    /// Gets or sets the priority, or empty for the type's.
    /// </summary>
    public string Priority { get; set; }

    /// <summary>
    /// Gets or sets the types to choose from.
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public IList<SelectListItem> Types { get; set; } = [];

    /// <summary>
    /// Gets or sets the priorities to choose from.
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public IList<SelectListItem> Priorities { get; set; } = [];
}
