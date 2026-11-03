using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Scheduling.Deployments;

/// <summary>
/// Writes the Scheduling step: appointment types and resources, and the appointments and the waitlist when asked.
/// </summary>
internal sealed class SchedulingDeploymentSource : DeploymentSourceBase<SchedulingDeploymentStep>
{
    private const int PageSize = 500;
    private const int MaxRecords = 50_000;

    private readonly AppointmentTypeStore _typeStore;
    private readonly SchedulingResourceStore _resourceStore;
    private readonly AppointmentStore _appointmentStore;
    private readonly WaitlistStore _waitlistStore;

    public SchedulingDeploymentSource(
        AppointmentTypeStore typeStore,
        SchedulingResourceStore resourceStore,
        AppointmentStore appointmentStore,
        WaitlistStore waitlistStore)
    {
        _typeStore = typeStore;
        _resourceStore = resourceStore;
        _appointmentStore = appointmentStore;
        _waitlistStore = waitlistStore;
    }

    protected override async Task ProcessAsync(SchedulingDeploymentStep step, DeploymentPlanResult result)
    {
        var node = new JsonObject
        {
            ["name"] = step.Name,
            ["Resources"] = ToArray(await _resourceStore.ListAsync(), "bookingVersion"),
            ["AppointmentTypes"] = ToArray(await _typeStore.ListAsync()),
        };

        if (step.IncludeAppointments)
        {
            var appointments = new JsonArray();

            for (var skip = 0; skip < MaxRecords; skip += PageSize)
            {
                var (_, page) = await _appointmentStore.PageAsync(null, null, null, skip, PageSize);

                foreach (var appointment in page)
                {
                    // A manage link is a secret; the importing site makes its own.
                    appointments.Add(Serialize(appointment, "manageToken"));
                }

                if (page.Count < PageSize)
                {
                    break;
                }
            }

            node["Appointments"] = appointments;
        }

        if (step.IncludeWaitlist)
        {
            var waitlist = new JsonArray();

            for (var skip = 0; skip < MaxRecords; skip += PageSize)
            {
                var (_, page) = await _waitlistStore.PageAsync(skip, PageSize);

                foreach (var entry in page)
                {
                    waitlist.Add(Serialize(entry));
                }

                if (page.Count < PageSize)
                {
                    break;
                }
            }

            node["Waitlist"] = waitlist;
        }

        result.Steps.Add(node);
    }

    private static JsonArray ToArray<T>(IEnumerable<T> records, params string[] without)
    {
        return new JsonArray([.. records.Select(record => Serialize(record, without))]);
    }

    private static JsonObject Serialize<T>(T record, params string[] without)
    {
        var node = JsonSerializer.SerializeToNode(record, SchedulingRecipeStep.JsonOptions).AsObject();

        node.Remove("id");

        foreach (var name in without)
        {
            node.Remove(name);
        }

        return node;
    }
}
