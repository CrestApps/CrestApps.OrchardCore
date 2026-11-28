using CrestApps.OrchardCore.WorkOrders.Core;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.WorkOrders.AI;

/// <summary>
/// Builds the work order AI tools' results from explicit lists of fields, never by serializing a stored record.
/// </summary>
internal static class WorkOrderToolResults
{
    /// <summary>
    /// Gives the names of the work order types and sub-statuses, by id.
    /// </summary>
    public static async Task<(Dictionary<string, string> Types, Dictionary<string, WorkOrderSubStatus> SubStatuses)> NamesAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var types = (await services.GetRequiredService<WorkOrderSettingManager<WorkOrderType>>().GetAllAsync(cancellationToken))
            .ToDictionary(type => type.ItemId, type => type.Name, StringComparer.Ordinal);
        var subStatuses = (await services.GetRequiredService<WorkOrderSettingManager<WorkOrderSubStatus>>().GetAllAsync(cancellationToken))
            .ToDictionary(item => item.ItemId, StringComparer.Ordinal);

        return (types, subStatuses);
    }

    /// <summary>
    /// Gives a work order's row of a list.
    /// </summary>
    public static async Task<object> SummaryAsync(IServiceProvider services, WorkOrder workOrder, IReadOnlyDictionary<string, string> types, IReadOnlyDictionary<string, WorkOrderSubStatus> subStatuses)
        => new
        {
            id = workOrder.ItemId,
            number = workOrder.Number,
            summary = workOrder.Summary,
            type = types.GetValueOrDefault(workOrder.TypeId ?? string.Empty),
            status = workOrder.Status.ToString(),
            subStatus = subStatuses.GetValueOrDefault(workOrder.SubStatusId ?? string.Empty)?.Name,
            priority = workOrder.Priority.ToString(),
            contactName = workOrder.Contact?.Name,
            city = workOrder.ServiceAddress?.City,
            created = await WorkOrderToolSupport.LocalAsync(services, workOrder.CreatedUtc),
            respondBy = await WorkOrderToolSupport.LocalAsync(services, workOrder.Sla?.RespondByUtc),
            resolveBy = await WorkOrderToolSupport.LocalAsync(services, workOrder.Sla?.ResolveByUtc),
            adminPath = WorkOrderToolSupport.WorkOrderPath(services, workOrder.ItemId),
        };

    /// <summary>
    /// Gives what a write tool reports about the work order it changed: where it stands and what it plans.
    /// </summary>
    public static async Task<object> DetailAsync(IServiceProvider services, WorkOrder workOrder, CancellationToken cancellationToken)
    {
        var (types, subStatuses) = await NamesAsync(services, cancellationToken);
        var address = workOrder.ServiceAddress;

        return new
        {
            id = workOrder.ItemId,
            number = workOrder.Number,
            summary = workOrder.Summary,
            description = workOrder.Description,
            type = types.GetValueOrDefault(workOrder.TypeId ?? string.Empty),
            status = workOrder.Status.ToString(),
            subStatus = subStatuses.GetValueOrDefault(workOrder.SubStatusId ?? string.Empty)?.Name,
            nextStatuses = WorkOrderStatusRules.Next(workOrder.Status).Select(status => status.ToString()),
            priority = workOrder.Priority.ToString(),
            customerId = workOrder.CustomerId,
            accountId = workOrder.AccountId,
            contactId = workOrder.ContactId,
            contact = new
            {
                name = workOrder.Contact?.Name,
                phone = workOrder.Contact?.Phone,
                email = workOrder.Contact?.Email,
            },
            serviceAddress = address is null
                ? null
                : new
                {
                    addressLine1 = address.AddressLine1,
                    addressLine2 = address.AddressLine2,
                    city = address.City,
                    region = address.Region,
                    postalCode = address.PostalCode,
                    country = address.Country,
                },
            accessNotes = workOrder.AccessNotes,
            assetIds = workOrder.AssetIds,
            earliest = await WorkOrderToolSupport.LocalAsync(services, workOrder.EarliestUtc),
            latest = await WorkOrderToolSupport.LocalAsync(services, workOrder.LatestUtc),
            estimatedMinutes = workOrder.EstimatedMinutes,
            respondBy = await WorkOrderToolSupport.LocalAsync(services, workOrder.Sla?.RespondByUtc),
            resolveBy = await WorkOrderToolSupport.LocalAsync(services, workOrder.Sla?.ResolveByUtc),
            lines = Lines(workOrder),
            timeZoneId = await WorkOrderToolSupport.GetTimeZoneIdAsync(services),
            adminPath = WorkOrderToolSupport.WorkOrderPath(services, workOrder.ItemId),
        };
    }

    /// <summary>
    /// Gives a work order's lines, with their ids.
    /// </summary>
    public static IEnumerable<object> Lines(WorkOrder workOrder)
        => workOrder.Lines.Select(line => new
        {
            id = line.Id,
            kind = line.Kind.ToString(),
            description = line.Description,
            stockItemId = line.StockItemId,
            quantityEstimated = line.QuantityEstimated,
            quantityUsed = line.QuantityUsed,
            unitPrice = line.UnitPrice,
            billable = line.Billable,
            locationId = line.LocationId,
        });

    /// <summary>
    /// Gives a work order's checklists, each with its position, its questions and their answers.
    /// </summary>
    public static IEnumerable<object> Checklists(WorkOrder workOrder)
        => workOrder.Checklists.Select((checklist, index) => Checklist(checklist, index));

    /// <summary>
    /// Gives one checklist with its position, its questions and their answers.
    /// </summary>
    public static object Checklist(WorkOrderChecklist checklist, int index)
        => new
        {
            position = index + 1,
            name = checklist.Name,
            questions = checklist.Questions.Select(question => new
            {
                id = question.Id,
                section = question.Section,
                text = question.Text,
                kind = question.Kind.ToString(),
                required = question.Required,
                shown = WorkOrderService.IsShown(checklist, question),
                choices = question.Choices.Count == 0 ? null : question.Choices,
                min = question.Min,
                max = question.Max,
                unit = question.Unit ?? question.Meter,
                answer = checklist.Answers.FirstOrDefault(answer => answer.QuestionId == question.Id)?.Value,
            }),
        };
}
