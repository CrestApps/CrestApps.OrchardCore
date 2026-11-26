using CrestApps.OrchardCore.Scheduling.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Scheduling.Drivers;

/// <summary>
/// The editor of the page where staff book an appointment: the open time to choose, and the customer. The fields keep
/// the names the page posts. The public booking page doesn't use this editor.
/// </summary>
internal sealed class NewAppointmentDisplayDriver : DisplayDriver<NewAppointmentViewModel>
{
    internal readonly IStringLocalizer S;

    public NewAppointmentDisplayDriver(IStringLocalizer<NewAppointmentDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(NewAppointmentViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<NewAppointmentViewModel>("AppointmentNewTimes_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Open Times"], "10")),
            Initialize<NewAppointmentViewModel>("AppointmentNewCustomer_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Customer"], "20"))
                .RenderWhen(() => Task.FromResult(model.Slots.Count > 0)));
    }

    public override async Task<IDisplayResult> UpdateAsync(NewAppointmentViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Start, m => m.CustomerName, m => m.CustomerEmail, m => m.CustomerPhone, m => m.Notes);

        return Edit(model, context);
    }

    // The fields keep the names the page posts.
    protected override void BuildPrefix(NewAppointmentViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(NewAppointmentViewModel source, NewAppointmentViewModel model)
    {
        model.AppointmentTypeId = source.AppointmentTypeId;
        model.Date = source.Date;
        model.Start = source.Start;
        model.CustomerName = source.CustomerName;
        model.CustomerEmail = source.CustomerEmail;
        model.CustomerPhone = source.CustomerPhone;
        model.Notes = source.Notes;
        model.AppointmentType = source.AppointmentType;
        model.TimeZoneId = source.TimeZoneId;
        model.Slots = source.Slots;
        model.TypeOptions = source.TypeOptions;
        model.Errors = source.Errors;
    }
}
