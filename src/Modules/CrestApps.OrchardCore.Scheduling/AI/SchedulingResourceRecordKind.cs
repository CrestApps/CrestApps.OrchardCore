using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Scheduling.AI;

/// <summary>
/// The operations of the <c>schedulingResource</c> record kind. Scheduling resources are kept in
/// <see cref="SchedulingResourceStore"/>, not behind a catalog manager, so each operation does what the Scheduling
/// Resources page does: a new resource starts with weekday hours, and a save gives the resource its Appointments calendar
/// before it stores it.
/// </summary>
internal static class SchedulingResourceRecordKind
{
    /// <summary>
    /// The name of the kind.
    /// </summary>
    public const string Name = "schedulingResource";

    /// <summary>
    /// Describes a resource from an explicit list of its fields. The hours are written in the editor's own text form, so
    /// they can be changed and sent back as they are.
    /// </summary>
    /// <param name="resource">The resource.</param>
    /// <returns>The fields the record tools return.</returns>
    public static object Project(SchedulingResource resource)
        => new
        {
            id = resource.ItemId,
            name = resource.Name,
            kind = resource.Kind.ToString(),
            userId = resource.UserId,
            calendarId = resource.CalendarId,
            timeZoneId = resource.TimeZoneId,
            availabilityMode = resource.AvailabilityMode.ToString(),

            // A resource saved before several shift calendars existed still has the single one, as the editor shows it.
            shiftCalendarIds = (resource.ShiftCalendarIds ?? [])
                .Append(resource.ShiftCalendarId)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct(StringComparer.Ordinal)
                .ToList(),

            // Wall-clock times in the resource's own time zone, and pure dates, as the editor shows them.
            workingHours = SchedulingFormat.WriteHours(resource.WorkingHours ?? []),
            dateOverrides = SchedulingFormat.WriteOverrides(resource.DateOverrides ?? []),
            enabled = resource.Enabled,
        };

    /// <summary>
    /// Finds a resource by its ID.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="id">The ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resource, or <see langword="null"/>.</returns>
    public static ValueTask<SchedulingResource> FindAsync(IServiceProvider services, string id, CancellationToken cancellationToken)
        => services.GetRequiredService<SchedulingResourceStore>().FindByIdAsync(id, cancellationToken);

    /// <summary>
    /// Lists the resources by name, as the Scheduling Resources page does, filtered by text in the name.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="query">The page and the text to filter by.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page of resources and how many match.</returns>
    public static async ValueTask<PageResult<SchedulingResource>> ListAsync(IServiceProvider services, AIRecordQuery query, CancellationToken cancellationToken)
    {
        var text = query.Text?.Trim();
        var resources = (await services.GetRequiredService<SchedulingResourceStore>().ListAsync())
            .Where(resource => string.IsNullOrEmpty(text) || (resource.Name ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Max(1, query.PageSize);

        cancellationToken.ThrowIfCancellationRequested();

        return new PageResult<SchedulingResource>
        {
            Count = resources.Count,
            Entries = resources.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
        };
    }

    /// <summary>
    /// Saves a resource as the Scheduling Resources page does: a new one gets its ID first, then the resource gets its
    /// Appointments calendar when it has none (or the one it names no longer exists), and only then is it stored.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="resource">The resource the editor bound.</param>
    /// <param name="isNew">Whether the resource is new.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the resource is saved.</returns>
    public static async ValueTask SaveAsync(IServiceProvider services, SchedulingResource resource, bool isNew, CancellationToken cancellationToken)
    {
        var store = services.GetRequiredService<SchedulingResourceStore>();

        // The calendar points back at the resource, so a new resource needs its ID before the calendar is made.
        if (isNew && string.IsNullOrEmpty(resource.ItemId))
        {
            resource.ItemId = CrestApps.Core.UniqueId.GenerateId();
        }

        await services.GetRequiredService<SchedulingResourceCalendars>().EnsureCalendarAsync(resource);

        if (isNew)
        {
            await store.CreateAsync(resource, cancellationToken);

            return;
        }

        await store.UpdateAsync(resource, cancellationToken);
    }

    /// <summary>
    /// Deletes a resource, as the page's Delete does. Its calendar and its appointments are kept.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="resource">The resource.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the resource was deleted.</returns>
    public static ValueTask<bool> DeleteAsync(IServiceProvider services, SchedulingResource resource, CancellationToken cancellationToken)
        => services.GetRequiredService<SchedulingResourceStore>().DeleteAsync(resource, cancellationToken);
}
