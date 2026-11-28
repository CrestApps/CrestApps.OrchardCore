using CrestApps.Core.AI;
using CrestApps.OrchardCore.WorkOrders.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.WorkOrders;

/// <summary>
/// Registers the work order AI tools: the list, changes and notes, statuses, parts used, checklist answers and completion
/// of work orders, and their types, checklists, service levels and sub-statuses. They work without Dispatch,
/// only for a signed-in person with the permission of the matching admin page.
/// </summary>
[Feature(WorkOrdersConstants.Features.WorkOrders)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class WorkOrdersAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrdersAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkOrdersAIToolsStartup(IStringLocalizer<WorkOrdersAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListWorkOrdersTool>(ListWorkOrdersTool.ToolName)
            .WithTitle(S["List work orders"])
            .WithDescription(S["Lists work orders, newest first, filtered by status (or all open), type, priority, asset and a search on number or summary, with paging. Use it to find a work order's number."])
            .WithCategory(S["Field Service"])
            .Selectable();

        services.AddCoreAITool<UpdateWorkOrderTool>(UpdateWorkOrderTool.ToolName)
            .WithTitle(S["Update work order"])
            .WithDescription(S["Changes a work order's summary, description, priority, customer, contact, service address, access notes, assets, time window, estimated length and lines, and adds a note to its history. It does not change the status."])
            .WithCategory(S["Field Service"])
            .Selectable();

        services.AddCoreAITool<SetWorkOrderStatusTool>(SetWorkOrderStatusTool.ToolName)
            .WithTitle(S["Set work order status"])
            .WithDescription(S["Moves an open work order to Ready, Scheduled, In progress or On hold, with an optional sub-status and note. Only the moves the current status allows are accepted."])
            .WithCategory(S["Field Service"])
            .Selectable();

        services.AddCoreAITool<CloseWorkOrderTool>(CloseWorkOrderTool.ToolName)
            .WithTitle(S["Close work order"])
            .WithDescription(S["Cancels an open work order (a reason is required) or closes a completed one. Both are final: the work order cannot be reopened."])
            .WithCategory(S["Field Service"])
            .Selectable()
            .AsDestructive();

        services.AddCoreAITool<RecordWorkOrderPartUsedTool>(RecordWorkOrderPartUsedTool.ToolName)
            .WithTitle(S["Record work order part used"])
            .WithDescription(S["Records the quantity used of a product, labor or expense line of a work order, taken from a stock location when Inventory is on."])
            .WithCategory(S["Field Service"])
            .Selectable();

        services.AddCoreAITool<AnswerWorkOrderChecklistTool>(AnswerWorkOrderChecklistTool.ToolName)
            .WithTitle(S["Answer work order checklist"])
            .WithDescription(S["Saves answers to the questions of one of a work order's checklists. Each answer must fit the question's kind and limits."])
            .WithCategory(S["Field Service"])
            .Selectable();

        services.AddCoreAITool<CompleteWorkOrderTool>(CompleteWorkOrderTool.ToolName)
            .WithTitle(S["Complete work order"])
            .WithDescription(S["Completes a work order once its required answers, signature and photos are in, and creates the customer's invoice when the type is billable. It returns what is still missing instead when it cannot complete."])
            .WithCategory(S["Field Service"])
            .Selectable()
            .AsDestructive();

        services.AddCoreAITool<ListWorkOrderSettingsTool>(ListWorkOrderSettingsTool.ToolName)
            .WithTitle(S["List work order settings"])
            .WithDescription(S["Lists the work order types, checklists, service levels or sub-statuses (choose the kind), with a name search."])
            .WithCategory(S["Field Service"])
            .Selectable();

        services.AddCoreAITool<GetWorkOrderSettingTool>(GetWorkOrderSettingTool.ToolName)
            .WithTitle(S["Get work order setting"])
            .WithDescription(S["Gets one work order type, checklist, service level or sub-status in full, with the JSON schema of that kind. Use it before saveWorkOrderSetting."])
            .WithCategory(S["Field Service"])
            .Selectable();

        services.AddCoreAITool<SaveWorkOrderSettingTool>(SaveWorkOrderSettingTool.ToolName)
            .WithTitle(S["Save work order setting"])
            .WithDescription(S["Creates or updates a work order type, checklist, service level or sub-status. A type refers to checklists, a service level, skills, certifications and equipment types by id, so create those first."])
            .WithCategory(S["Field Service"])
            .Selectable();

        services.AddCoreAITool<DeleteWorkOrderSettingTool>(DeleteWorkOrderSettingTool.ToolName)
            .WithTitle(S["Delete work order setting"])
            .WithDescription(S["Deletes a work order type, checklist, service level or sub-status. Refused while work orders use the type, or while a type uses the checklist or service level."])
            .WithCategory(S["Field Service"])
            .Selectable()
            .AsDestructive();
    }
}

/// <summary>
/// Registers the service agreement AI tools: read an agreement's field-service terms, and save, run and delete
/// agreements. Agreements are listed by the generic record tools (the Agreements module's <c>agreement</c> kind). They
/// work only for a signed-in person who may view or manage agreements.
/// </summary>
[Feature(WorkOrdersConstants.Features.Agreements)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class ServiceAgreementsAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAgreementsAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ServiceAgreementsAIToolsStartup(IStringLocalizer<ServiceAgreementsAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<GetServiceAgreementTool>(GetServiceAgreementTool.ToolName)
            .WithTitle(S["Get service agreement"])
            .WithDescription(S["Gets one agreement's field-service terms: customer, contact, place, assets, period, the hours it covers, pricing, each schedule with its repeat rule and next visit dates, and the work orders already made."])
            .WithCategory(S["Field Service"])
            .Selectable();

        // Asks, as running does: an active agreement makes the same work orders, visits and customer notices through the
        // hourly task.
        services.AddCoreAITool<SaveServiceAgreementTool>(SaveServiceAgreementTool.ToolName)
            .WithTitle(S["Save service agreement"])
            .WithDescription(S["Creates or updates a service agreement: customer, contact, address, assets, start and end, the hours it covers, schedules (work order type, repeat rule, lead days), pricing and status. Setting the status to Active starts making work orders; Canceled stops them."])
            .WithCategory(S["Field Service"])
            .Selectable()
            .AsWrite()
            .RequireApproval();

        services.AddCoreAITool<RunServiceAgreementTool>(RunServiceAgreementTool.ToolName)
            .WithTitle(S["Run service agreement"])
            .WithDescription(S["Makes the work orders of an active agreement that are due now, once each, and returns how many were made. The background task does this hourly; use it to avoid waiting. With Dispatch set to create visits for new work orders, each one gets a visit."])
            .WithCategory(S["Field Service"])
            .Selectable()
            .AsWrite()
            .RequireApproval();

        services.AddCoreAITool<DeleteServiceAgreementTool>(DeleteServiceAgreementTool.ToolName)
            .WithTitle(S["Delete service agreement"])
            .WithDescription(S["Deletes a draft service agreement. An agreement that is not a draft cannot be deleted; cancel it with saveServiceAgreement instead."])
            .WithCategory(S["Field Service"])
            .Selectable()
            .AsDestructive();
    }
}
