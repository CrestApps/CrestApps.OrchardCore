using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Sources;

internal sealed class ContactCenterBusinessHoursCalendarDeploymentSource : DeploymentSourceBase<ContactCenterBusinessHoursCalendarDeploymentStep>
{
    private readonly IBusinessHoursCalendarManager _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterBusinessHoursCalendarDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the business-hours calendars.</param>
    public ContactCenterBusinessHoursCalendarDeploymentSource(IBusinessHoursCalendarManager manager)
    {
        _manager = manager;
    }

    protected override async Task ProcessAsync(ContactCenterBusinessHoursCalendarDeploymentStep step, DeploymentPlanResult result)
    {
        var entries = await _manager.GetAllAsync();

        var data = new JsonArray();

        foreach (var entry in entries)
        {
            data.Add(JsonSerializer.SerializeToNode(entry, entry.GetType(), ContactCenterDeploymentSerializer.Options));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = ContactCenterDeploymentSteps.BusinessHoursCalendar,
            ["Calendars"] = data,
        });
    }
}
