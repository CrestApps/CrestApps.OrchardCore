using CrestApps.OrchardCore.Learning.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Drivers;

/// <summary>
/// The editor of the form that enrolls a learner by hand: one card for the learner and the course, one for the dates and
/// one for the prerequisites.
/// </summary>
internal sealed class EnrollDisplayDriver : DisplayDriver<EnrollViewModel>
{
    internal readonly IStringLocalizer S;

    public EnrollDisplayDriver(IStringLocalizer<EnrollDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(EnrollViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<EnrollViewModel>("EnrollLearnerCourse_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Learner and Course"], "10")),
            Initialize<EnrollViewModel>("EnrollDates_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Dates"], "20")),
            Initialize<EnrollViewModel>("EnrollPrerequisites_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Prerequisites"], "30")));
    }

    public override async Task<IDisplayResult> UpdateAsync(EnrollViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(
            model,
            Prefix,
            m => m.UserId,
            m => m.CourseId,
            m => m.AccessEnds,
            m => m.Due,
            m => m.IgnorePrerequisites);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates.
    protected override void BuildPrefix(EnrollViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(EnrollViewModel source, EnrollViewModel model)
    {
        model.UserId = source.UserId;
        model.CourseId = source.CourseId;
        model.AccessEnds = source.AccessEnds;
        model.Due = source.Due;
        model.IgnorePrerequisites = source.IgnorePrerequisites;
        model.CourseOptions = source.CourseOptions;
    }
}
