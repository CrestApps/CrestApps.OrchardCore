using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Integrations;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Integrations;

/// <summary>
/// The editor of a profile's integrations.
/// </summary>
public class AIProfileIntegrationViewModel
{
    /// <summary>Gets or sets the calendar connection.</summary>
    public string CalendarConnectionId { get; set; }

    /// <summary>Gets or sets the CRM connection.</summary>
    public string CrmConnectionId { get; set; }

    /// <summary>Gets or sets how long an appointment is.</summary>
    public int AppointmentMinutes { get; set; }

    /// <summary>Gets or sets the calendar connections.</summary>
    public IList<SelectListItem> Calendars { get; set; } = [];

    /// <summary>Gets or sets the CRM connections.</summary>
    public IList<SelectListItem> Crms { get; set; } = [];
}

/// <summary>
/// Picks the calendar and CRM a profile's integration tools use, on its Capabilities tab.
/// </summary>
public sealed class AIProfileIntegrationDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly IAIIntegrationConnectionStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileIntegrationDisplayDriver"/> class.
    /// </summary>
    /// <param name="store">Lists the connections.</param>
    public AIProfileIntegrationDisplayDriver(IAIIntegrationConnectionStore store)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<AIProfileIntegrationViewModel>("AIProfileIntegration_Edit", async model =>
        {
            var settings = profile.GetSettings<AIProfileIntegrationSettings>();
            var connections = await _store.GetAllAsync();

            model.CalendarConnectionId = settings.CalendarConnectionId;
            model.CrmConnectionId = settings.CrmConnectionId;
            model.AppointmentMinutes = settings.AppointmentMinutes;
            model.Calendars = connections.Where(connection => AIIntegrationProviders.HasCalendar(connection.Provider))
                .Select(connection => new SelectListItem(connection.Name, connection.ItemId, connection.ItemId == settings.CalendarConnectionId)).ToList();
            model.Crms = connections.Where(connection => AIIntegrationProviders.IsCrm(connection.Provider))
                .Select(connection => new SelectListItem(connection.Name, connection.ItemId, connection.ItemId == settings.CrmConnectionId)).ToList();
        }).Location("Content:8#Capabilities;10");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new AIProfileIntegrationViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        profile.AlterSettings<AIProfileIntegrationSettings>(settings =>
        {
            settings.CalendarConnectionId = string.IsNullOrWhiteSpace(model.CalendarConnectionId) ? null : model.CalendarConnectionId;
            settings.CrmConnectionId = string.IsNullOrWhiteSpace(model.CrmConnectionId) ? null : model.CrmConnectionId;
            settings.AppointmentMinutes = Math.Clamp(model.AppointmentMinutes <= 0 ? 30 : model.AppointmentMinutes, 10, 240);
        });

        return Edit(profile, context);
    }
}
