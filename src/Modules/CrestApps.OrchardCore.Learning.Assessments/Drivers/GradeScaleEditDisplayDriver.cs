using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Assessments.Drivers;

/// <summary>
/// The editor of a grade scale: one card for the name and one for the bands, padded with empty rows to add more.
/// </summary>
internal sealed class GradeScaleEditDisplayDriver : DisplayDriver<GradeScaleEditViewModel>
{
    internal readonly IStringLocalizer S;

    public GradeScaleEditDisplayDriver(IStringLocalizer<GradeScaleEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(GradeScaleEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<GradeScaleEditViewModel>("GradeScaleGeneral_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["General"], "10")),
            Initialize<GradeScaleEditViewModel>("GradeScaleBands_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Bands"], "20")));
    }

    public override async Task<IDisplayResult> UpdateAsync(GradeScaleEditViewModel model, UpdateEditorContext context)
    {
        // The bands are a collection, so the whole model is bound; the editor shape is never bound.
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates.
    protected override void BuildPrefix(GradeScaleEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(GradeScaleEditViewModel source, GradeScaleEditViewModel model)
    {
        model.Id = source.Id;
        model.Name = source.Name;
        model.Bands = [.. source.Bands];

        // Empty rows to add bands with.
        while (model.Bands.Count < Math.Max(6, model.Bands.Count(band => band.MinPercent is not null) + 2))
        {
            model.Bands.Add(new GradeBandRowViewModel());
        }
    }
}
