using CrestApps.OrchardCore.Calendars.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Calendars.Drivers;

/// <summary>
/// The editor of a calendar category: its name and color, and who can use it. The fields keep the names the page
/// posts, so the controller's validation keys stay as they are.
/// </summary>
internal sealed class CalendarCategoryEditDisplayDriver : DisplayDriver<CalendarCategoryEditViewModel>
{
    internal readonly IStringLocalizer S;

    public CalendarCategoryEditDisplayDriver(IStringLocalizer<CalendarCategoryEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(CalendarCategoryEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<CalendarCategoryEditViewModel>("CalendarCategoryGeneral_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["General"], "10")),
            Initialize<CalendarCategoryEditViewModel>("CalendarCategorySharing_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Sharing"], "20"))
                .RenderWhen(() => Task.FromResult(model.CanShare)));
    }

    public override async Task<IDisplayResult> UpdateAsync(CalendarCategoryEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Name, m => m.Color, m => m.Shared);

        return Edit(model, context);
    }

    // The fields keep the names the page posts.
    protected override void BuildPrefix(CalendarCategoryEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(CalendarCategoryEditViewModel source, CalendarCategoryEditViewModel model)
    {
        // The id is not copied: a shape reserves a member of that name, and the page keeps the id in its own hidden field.
        model.Name = source.Name;
        model.Color = source.Color;
        model.Shared = source.Shared;
        model.CanShare = source.CanShare;
    }
}
