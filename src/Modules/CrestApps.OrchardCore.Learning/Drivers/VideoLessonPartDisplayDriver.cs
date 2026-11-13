using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Drivers;

/// <summary>
/// The editor of the video lesson part: how much of the video the learner must watch, and the transcript.
/// </summary>
internal sealed class VideoLessonPartDisplayDriver : ContentPartDisplayDriver<VideoLessonPart>
{
    internal readonly IStringLocalizer S;

    public VideoLessonPartDisplayDriver(IStringLocalizer<VideoLessonPartDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(VideoLessonPart part, BuildPartEditorContext context)
    {
        return Initialize<LearningPartTextViewModel>(GetEditorShapeType(context), model =>
        {
            model.RequiredPercent = part.RequiredPercent;
            model.Transcript = part.Transcript;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(VideoLessonPart part, UpdatePartEditorContext context)
    {
        var model = new LearningPartTextViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.RequiredPercent is < 1 or > 100)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.RequiredPercent), S["The share to watch is from 1 to 100."]);
        }

        part.RequiredPercent = Math.Clamp(model.RequiredPercent, 1, 100);
        part.Transcript = model.Transcript?.Trim();

        return Edit(part, context);
    }
}
