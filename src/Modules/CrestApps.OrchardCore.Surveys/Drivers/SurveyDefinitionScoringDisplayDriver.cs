using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Surveys.Drivers;

/// <summary>
/// Edits the scoring, review and timing settings of a survey definition.
/// </summary>
internal sealed class SurveyDefinitionScoringDisplayDriver : DisplayDriver<SurveyDefinition>
{
    internal readonly IStringLocalizer S;

    public SurveyDefinitionScoringDisplayDriver(IStringLocalizer<SurveyDefinitionScoringDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(SurveyDefinition definition, BuildEditorContext context)
        => Combine(
            Initialize<SurveyDefinitionScoringEditViewModel>("SurveyDefinitionScoring_Edit", model => Populate(definition, model))
                .Location(l => l.Zone("Content", "1").Tab(S["Scoring"], "2").Card(S["Scoring"], "10")),
            Initialize<SurveyDefinitionScoringEditViewModel>("SurveyDefinitionReview_Edit", model => Populate(definition, model))
                .Location(l => l.Zone("Content", "2").Tab(S["Scoring"], "2").Card(S["What Respondents See"], "20")),
            Initialize<SurveyDefinitionScoringEditViewModel>("SurveyDefinitionTimeLimit_Edit", model => Populate(definition, model))
                .Location(l => l.Zone("Content", "3").Tab(S["Scoring"], "2").Card(S["Time Limit"], "30")));

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(SurveyDefinition definition, UpdateEditorContext context)
    {
        var model = new SurveyDefinitionScoringEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.Scoring?.PassMarkPercent is { } passMark && (passMark < 0 || passMark > 100))
        {
            context.Updater.ModelState.AddModelError(Prefix, "Scoring.PassMarkPercent", S["The pass mark must be between 0 and 100."]);
        }

        definition.Scoring = model.Scoring ?? new SurveyScoringSettings();
        definition.Scoring.RoundingDecimals = Math.Clamp(definition.Scoring.RoundingDecimals, 0, 6);
        definition.Timing = model.Timing ?? new SurveyTimingSettings();
        definition.Timing.TimeLimitMinutes = definition.Timing.TimeLimitMinutes is > 0 ? definition.Timing.TimeLimitMinutes : null;
        definition.Timing.GraceSeconds = Math.Clamp(definition.Timing.GraceSeconds, 0, SurveyAvailability.MaxGraceSeconds);
        definition.Review = new SurveyReviewSettings
        {
            ResultsVisibility = model.ResultsVisibility,
            During = CombineFlags(model.ReviewDuring),
            ImmediatelyAfter = CombineFlags(model.ReviewImmediatelyAfter),
            LaterWhileOpen = CombineFlags(model.ReviewLaterWhileOpen),
            AfterClose = CombineFlags(model.ReviewAfterClose),
        };

        return Edit(definition, context);
    }

    private static List<ReviewShows> Flags(ReviewShows value)
        =>
        [
            .. new[] { ReviewShows.Score, ReviewShows.Responses, ReviewShows.RightAnswers, ReviewShows.Feedback, ReviewShows.GeneralFeedback }
                .Where(flag => value.HasFlag(flag)),
        ];

    private static ReviewShows CombineFlags(IEnumerable<ReviewShows> flags)
        => (flags ?? []).Aggregate(ReviewShows.None, (total, flag) => total | flag);

    private static void Populate(SurveyDefinition definition, SurveyDefinitionScoringEditViewModel model)
    {
        var review = definition.Review ?? new SurveyReviewSettings();

        model.Scoring = definition.Scoring ?? new SurveyScoringSettings();
        model.Timing = definition.Timing ?? new SurveyTimingSettings();
        model.ResultsVisibility = review.ResultsVisibility;
        model.ReviewDuring = Flags(review.During);
        model.ReviewImmediatelyAfter = Flags(review.ImmediatelyAfter);
        model.ReviewLaterWhileOpen = Flags(review.LaterWhileOpen);
        model.ReviewAfterClose = Flags(review.AfterClose);
    }
}
