using CrestApps.OrchardCore.Learning.Classes.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Classes.Drivers;

/// <summary>
/// The editor of a class session: one card for the session and its meetings, one for where it is held and one for its
/// registration.
/// </summary>
internal sealed class ClassSessionEditDisplayDriver : DisplayDriver<ClassSessionEditViewModel>
{
    internal readonly IStringLocalizer S;

    public ClassSessionEditDisplayDriver(IStringLocalizer<ClassSessionEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ClassSessionEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<ClassSessionEditViewModel>("ClassSessionGeneral_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Session"], "10")),
            Initialize<ClassSessionEditViewModel>("ClassSessionWhere_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Where"], "20")),
            Initialize<ClassSessionEditViewModel>("ClassSessionRegistration_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Registration"], "30")));
    }

    public override async Task<IDisplayResult> UpdateAsync(ClassSessionEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(
            model,
            Prefix,
            m => m.Title,
            m => m.Meetings,
            m => m.Capacity,
            m => m.Location,
            m => m.MeetingUrl,
            m => m.RegistrationOpens,
            m => m.RegistrationCloses,
            m => m.WaitlistMode,
            m => m.OfferHours);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates.
    protected override void BuildPrefix(ClassSessionEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(ClassSessionEditViewModel source, ClassSessionEditViewModel model)
    {
        model.Title = source.Title;
        model.Meetings = source.Meetings;
        model.Capacity = source.Capacity;
        model.Location = source.Location;
        model.MeetingUrl = source.MeetingUrl;
        model.RegistrationOpens = source.RegistrationOpens;
        model.RegistrationCloses = source.RegistrationCloses;
        model.WaitlistMode = source.WaitlistMode;
        model.OfferHours = source.OfferHours;
        model.WaitlistModeOptions = source.WaitlistModeOptions;
    }
}
