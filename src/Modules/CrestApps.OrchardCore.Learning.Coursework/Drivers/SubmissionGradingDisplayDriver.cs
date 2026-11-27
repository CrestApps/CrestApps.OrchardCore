using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Coursework.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Coursework.Drivers;

/// <summary>
/// The editor of the grading page of a submission: the grade (the rubric levels or the points), the feedback and, when
/// graded work is graded again, why. It is only there while the submission can be graded.
/// </summary>
internal sealed class SubmissionGradingDisplayDriver : DisplayDriver<SubmissionGradingViewModel>
{
    internal readonly IStringLocalizer S;

    public SubmissionGradingDisplayDriver(IStringLocalizer<SubmissionGradingDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(SubmissionGradingViewModel model, BuildEditorContext context)
    {
        if (!model.CanGrade || model.Submission is null)
        {
            return null;
        }

        var title = model.Submission.Status == SubmissionStatus.Graded ? S["Grade Again"] : S["Grade"];

        return Initialize<SubmissionGradingViewModel>("SubmissionGrade_Edit", m => Populate(model, m))
            .Location(l => l.Zone("Content", "1").Card(title, "10"));
    }

    public override async Task<IDisplayResult> UpdateAsync(SubmissionGradingViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(
            model,
            Prefix,
            m => m.Choices,
            m => m.Points,
            m => m.Feedback,
            m => m.Reason,
            m => m.AIAssisted);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scoring help fills.
    protected override void BuildPrefix(SubmissionGradingViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(SubmissionGradingViewModel source, SubmissionGradingViewModel model)
    {
        model.Submission = source.Submission;
        model.Coursework = source.Coursework;
        model.Part = source.Part;
        model.Rubric = source.Rubric;
        model.CanGrade = source.CanGrade;
        model.DaysLate = source.DaysLate;
        model.LatePenaltyPercent = source.LatePenaltyPercent;
        model.Choices = source.Choices;
        model.Points = source.Points;
        model.Feedback = source.Feedback;
        model.Reason = source.Reason;
        model.AIAssisted = source.AIAssisted;
    }
}
