using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Certifications.Drivers;

/// <summary>
/// The editor of the review of external learning: the note that is kept with the review. It is only there while the
/// submission waits for another reviewer.
/// </summary>
internal sealed class ExternalLearningReviewDisplayDriver : DisplayDriver<ExternalLearningReviewViewModel>
{
    internal readonly IStringLocalizer S;

    public ExternalLearningReviewDisplayDriver(IStringLocalizer<ExternalLearningReviewDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ExternalLearningReviewViewModel model, BuildEditorContext context)
    {
        if (model.Submission is not { State: ExternalLearningState.Submitted } || model.IsOwnLearning)
        {
            return null;
        }

        return Initialize<ExternalLearningReviewViewModel>("ExternalLearningReview_Edit", m => m.Note = model.Note)
            .Location(l => l.Zone("Content", "1").Card(S["Review"], "10"));
    }

    public override async Task<IDisplayResult> UpdateAsync(ExternalLearningReviewViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Note);

        return Edit(model, context);
    }

    // The fields keep the names the page posts.
    protected override void BuildPrefix(ExternalLearningReviewViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }
}
