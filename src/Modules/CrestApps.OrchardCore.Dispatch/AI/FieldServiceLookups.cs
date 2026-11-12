using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Dispatch.Core;
using CrestApps.OrchardCore.Dispatch.Core.Indexes;
using CrestApps.OrchardCore.Dispatch.Core.Services;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.Dispatch.AI;

/// <summary>
/// Finds the work orders, visits and technicians the field service AI tools are asked about, by the numbers and names
/// people say.
/// </summary>
internal static class FieldServiceLookups
{
    public static Task<WorkOrder> WorkOrderAsync(IServiceProvider services, string number, CancellationToken cancellationToken)
    {
        var text = number?.Trim().ToUpperInvariant();

        return string.IsNullOrEmpty(text)
            ? Task.FromResult<WorkOrder>(null)
            : services.GetRequiredService<ISession>().Query<WorkOrder, WorkOrderIndex>(index => index.Number == text, collection: WorkOrdersConstants.CollectionName).FirstOrDefaultAsync(cancellationToken);
    }

    // A visit by its number, or the open one of a work order by the work order's number.
    public static async Task<ServiceVisit> VisitAsync(IServiceProvider services, string number, CancellationToken cancellationToken)
    {
        var text = number?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var session = services.GetRequiredService<ISession>();
        var visit = await session.Query<ServiceVisit, ServiceVisitIndex>(index => index.Number == text, collection: DispatchConstants.CollectionName).FirstOrDefaultAsync(cancellationToken);

        if (visit is not null)
        {
            return visit;
        }

        var visits = await session.Query<ServiceVisit, ServiceVisitIndex>(index => index.WorkOrderNumber == text, collection: DispatchConstants.CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .ListAsync(cancellationToken);

        return visits.FirstOrDefault(item => VisitStatusRules.IsOpen(item.Status) && item.Status != VisitStatus.Unscheduled)
            ?? visits.FirstOrDefault(item => VisitStatusRules.IsOpen(item.Status))
            ?? (visits.Count > 0 ? visits[0] : null);
    }

    public static async Task<StaffMember> WorkerAsync(IServiceProvider services, string name, CancellationToken cancellationToken)
    {
        var text = Normalize(name);

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var workers = await services.GetRequiredService<DispatchResources>().ListWorkersAsync(cancellationToken);

        return workers.FirstOrDefault(worker => Normalize(worker.DisplayName) == text)
            ?? workers.FirstOrDefault(worker => Normalize(worker.DisplayName).StartsWith(text, StringComparison.Ordinal));
    }

    public static async Task<string> NamesAsync(IServiceProvider services, IEnumerable<string> workerIds, CancellationToken cancellationToken)
    {
        var resources = services.GetRequiredService<DispatchResources>();
        var names = new List<string>();

        foreach (var id in workerIds ?? [])
        {
            names.Add((await resources.FindWorkerAsync(id, cancellationToken))?.DisplayName ?? id);
        }

        return string.Join(", ", names);
    }

    public static async Task<string> TimeZoneAsync(IServiceProvider services)
    {
        var zone = await services.GetRequiredService<IDispatchSettingsProvider>().GetTimeZoneIdAsync();

        return CalendarTime.IsValidZone(zone) ? zone : "UTC";
    }

    public static string Local(DateTime? utc, string zone)
        => utc is { } value ? CalendarTime.ToLocal(value, zone).ToString("yyyy-MM-ddTHH:mm", System.Globalization.CultureInfo.InvariantCulture) : null;

    public static string Digits(string phone)
        => string.IsNullOrEmpty(phone) ? string.Empty : string.Concat(phone.Where(char.IsAsciiDigit));

    private static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : string.Join(' ', value.Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
