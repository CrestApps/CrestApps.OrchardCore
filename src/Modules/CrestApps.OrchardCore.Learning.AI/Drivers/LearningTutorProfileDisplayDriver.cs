using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Learning.AI.Models;
using CrestApps.OrchardCore.Learning.AI.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.AI.Drivers;

/// <summary>
/// Adds the learning tutor switch to the AI profile editor. A profile built from the learning tutor template starts with
/// it on.
/// </summary>
internal sealed class LearningTutorProfileDisplayDriver : DisplayDriver<AIProfile>
{
    internal readonly IStringLocalizer S;

    public LearningTutorProfileDisplayDriver(IStringLocalizer<LearningTutorProfileDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<LearningTutorProfileViewModel>("LearningTutorProfile_Edit", model =>
        {
            model.IsTutor = profile.TryGet<LearningTutorProfileMetadata>(out var metadata) && metadata.IsTutor;
        }).Location(l => l.Zone("Content", "7").Tab(S["Knowledge"], "3").Card(S["Learning Tutor"], "10"));
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new LearningTutorProfileViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        profile.Put(new LearningTutorProfileMetadata { IsTutor = model.IsTutor });

        return Edit(profile, context);
    }
}
