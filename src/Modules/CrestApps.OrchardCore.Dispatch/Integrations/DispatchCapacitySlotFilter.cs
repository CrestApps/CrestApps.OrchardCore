using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Dispatch.Integrations;

/// <summary>
/// Offers a booking slot of an appointment type that makes work orders only while a technician can take the job
/// then: one with the type's skills and certifications, on a published shift and free long enough. Bookings already
/// made into the slot that no one has been given yet use up a technician each.
/// </summary>
internal sealed class DispatchCapacitySlotFilter : IAppointmentSlotFilter
{
    private static readonly string[] _waitingStatuses = [nameof(WorkOrderStatus.New), nameof(WorkOrderStatus.Ready)];

    private readonly WorkOrderSettingManager<WorkOrderType> _types;
    private readonly IDispatchCandidateFinder _finder;
    private readonly ISession _session;

    public DispatchCapacitySlotFilter(WorkOrderSettingManager<WorkOrderType> types, IDispatchCandidateFinder finder, ISession session)
    {
        _types = types;
        _finder = finder;
        _session = session;
    }

    public async Task<IReadOnlyList<AppointmentSlot>> FilterAsync(AppointmentSlotFilterContext context, IReadOnlyList<AppointmentSlot> slots, CancellationToken cancellationToken = default)
    {
        var appointmentTypeId = context.AppointmentType?.ItemId;
        var type = string.IsNullOrEmpty(appointmentTypeId) ? null : (await _types.GetAllAsync(cancellationToken)).FirstOrDefault(item => item.AppointmentTypeIds.Contains(appointmentTypeId));

        if (type is null || slots.Count == 0)
        {
            return slots;
        }

        var from = slots.Min(slot => slot.StartUtc);
        var to = slots.Max(slot => slot.EndUtc);
        var typeId = type.ItemId;
        var source = nameof(WorkOrderSource.Appointment);
        var waiting = (await _session.Query<WorkOrder, WorkOrderIndex>(index => index.TypeId == typeId && index.Source == source && index.Status.IsIn(_waitingStatuses), collection: WorkOrdersConstants.CollectionName)
            .ListAsync(cancellationToken))
            .Where(workOrder => workOrder.SourceId != context.ExcludeAppointmentId && workOrder.EarliestUtc is not null && workOrder.EarliestUtc < to && workOrder.LatestUtc > from)
            .ToList();
        var crew = Math.Max(1, type.CrewSize);
        var job = TimeSpan.FromMinutes(Math.Max(5, type.DefaultDurationMinutes));
        var kept = new List<AppointmentSlot>();

        foreach (var slot in slots)
        {
            // The job a booking of this slot would make: its type's skills, arriving in the slot and then working its
            // length, as the booking's work order is given.
            var workOrder = new WorkOrder
            {
                TypeId = typeId,
                RequiredSkills = [.. type.RequiredSkills],
                EarliestUtc = slot.StartUtc,
                LatestUtc = slot.EndUtc + job,
            };
            var visit = new ServiceVisit
            {
                TypeId = typeId,
                DurationMinutes = (int)job.TotalMinutes,
                CrewSize = crew,
            };
            var found = await _finder.FindAsync(visit, workOrder, slot.StartUtc, slot.EndUtc + job, cancellationToken: cancellationToken);

            // A waiting booking takes a technician when its job, from its arrival, would overlap this one's.
            var taken = waiting.Count(item => item.EarliestUtc < slot.StartUtc + job && item.EarliestUtc + job > slot.StartUtc);
            var free = (found.Candidates.Count / crew) - taken;

            if (free <= 0)
            {
                continue;
            }

            slot.Remaining = Math.Min(Math.Max(1, slot.Remaining), free);
            kept.Add(slot);
        }

        return kept;
    }
}
