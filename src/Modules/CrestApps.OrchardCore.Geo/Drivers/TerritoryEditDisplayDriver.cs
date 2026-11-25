using CrestApps.OrchardCore.Geo.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Geo.Drivers;

/// <summary>
/// Builds the editor of the territory page: its details, area, time and travel, and who serves it, each in its card.
/// </summary>
internal sealed class TerritoryEditDisplayDriver : DisplayDriver<TerritoryEditViewModel>
{
    internal readonly IStringLocalizer S;

    public TerritoryEditDisplayDriver(IStringLocalizer<TerritoryEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(TerritoryEditViewModel model, BuildEditorContext context)
        => Combine(
            Initialize<TerritoryEditViewModel>("TerritoryDetails_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Details"], "10")),
            Initialize<TerritoryEditViewModel>("TerritoryArea_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Area"], "20")),
            Initialize<TerritoryEditViewModel>("TerritoryTravel_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Time and Travel"], "30")),
            Initialize<TerritoryEditViewModel>("TerritoryMembers_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "4").Card(S["Who Serves It"], "40")));

    public override async Task<IDisplayResult> UpdateAsync(TerritoryEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Name, m => m.ParentId, m => m.Shape, m => m.PostalCodes, m => m.TimeZoneId, m => m.TravelSpeedKmh, m => m.Members);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the map script reads.
    protected override void BuildPrefix(TerritoryEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(TerritoryEditViewModel source, TerritoryEditViewModel model)
    {
        model.Id = source.Id;
        model.Name = source.Name;
        model.ParentId = source.ParentId;
        model.Shape = source.Shape;
        model.PostalCodes = source.PostalCodes;
        model.TimeZoneId = source.TimeZoneId;
        model.TravelSpeedKmh = source.TravelSpeedKmh;
        model.Members = source.Members;
        model.OtherShapes = source.OtherShapes;
        model.ParentOptions = source.ParentOptions;
        model.TimeZoneOptions = source.TimeZoneOptions;
        model.Map = source.Map;
        model.MemberGroups = source.MemberGroups;
    }
}
