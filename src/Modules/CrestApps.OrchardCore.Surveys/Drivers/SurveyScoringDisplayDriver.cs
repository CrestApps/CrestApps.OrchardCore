using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Surveys.Drivers;

/// <summary>
/// Edits the scoring key and feedback of scorable questions when the survey is scored.
/// </summary>
internal sealed class SurveyScoringDisplayDriver : DisplayDriver<SurveyQuestion>
{
    private readonly SurveyDesignerContext _designer;
    private readonly ISurveyControlRegistry _registry;

    internal readonly IStringLocalizer S;

    public SurveyScoringDisplayDriver(
        SurveyDesignerContext designer,
        ISurveyControlRegistry registry,
        IStringLocalizer<SurveyScoringDisplayDriver> stringLocalizer)
    {
        _designer = designer;
        _registry = registry;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override bool CanHandleModel(SurveyQuestion model)
        => (_designer.AlwaysShowScoring || _designer.Definition?.Scoring?.Enabled == true)
            && _registry.GetControl(model?.ControlType)?.Has(SurveyControlCapabilities.Scorable) == true;

    /// <inheritdoc />
    public override IDisplayResult Edit(SurveyQuestion question, BuildEditorContext context)
        => Combine(
            Initialize<SurveyScoringEditViewModel>("SurveyScoring_Edit", model => Populate(question, model))
                .Location(l => l.Zone("Content", "8").Card(S["Scoring"], "80")),
            Initialize<SurveyScoringEditViewModel>("SurveyScoringFeedback_Edit", model => Populate(question, model))
                .Location(l => l.Zone("Content", "9").Card(S["Feedback"], "85")));

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(SurveyQuestion question, UpdateEditorContext context)
    {
        var model = new SurveyScoringEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var choiceIds = question.Choices.Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal);
        var rowIds = question.Rows.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
        var scoring = question.Scoring ??= new SurveyQuestionScoring();

        scoring.Points = model.Points is < 0 ? 0 : model.Points;
        scoring.PartialCredit = model.PartialCredit;
        scoring.PenaltyPerWrong = model.PenaltyPerWrong is > 0 ? model.PenaltyPerWrong : null;
        scoring.CategoryId = string.IsNullOrEmpty(model.CategoryId) ? null : model.CategoryId;
        scoring.CorrectChoiceIds = model.KeyChoices.Where(choice => choice.Correct && choiceIds.Contains(choice.Id)).Select(choice => choice.Id).ToList();
        scoring.ChoicePoints = model.KeyChoices.Where(choice => choice.Points is not null && choiceIds.Contains(choice.Id)).ToDictionary(choice => choice.Id, choice => choice.Points.Value, StringComparer.Ordinal);
        scoring.ChoiceFeedback = model.KeyChoices.Where(choice => !string.IsNullOrWhiteSpace(choice.Feedback) && choiceIds.Contains(choice.Id)).ToDictionary(choice => choice.Id, choice => choice.Feedback.Trim(), StringComparer.Ordinal);
        scoring.AcceptedAnswers = (model.AcceptedAnswers ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        scoring.CaseSensitive = model.CaseSensitive;
        scoring.NumberAnswer = model.NumberAnswer;
        scoring.Tolerance = Math.Max(0, model.Tolerance);
        scoring.Pairs = model.Pairs.Where(pair => rowIds.Contains(pair.RowId) && choiceIds.Contains(pair.ChoiceId ?? string.Empty)).ToDictionary(pair => pair.RowId, pair => pair.ChoiceId, StringComparer.Ordinal);
        scoring.GeneralFeedback = Trim(model.GeneralFeedback);
        scoring.CorrectFeedback = Trim(model.CorrectFeedback);
        scoring.IncorrectFeedback = Trim(model.IncorrectFeedback);
        scoring.GraderGuidance = Trim(model.GraderGuidance);

        return Edit(question, context);
    }

    private static string Trim(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void Populate(SurveyQuestion question, SurveyScoringEditViewModel model)
    {
        var scoring = question.Scoring ?? new SurveyQuestionScoring();
        var descriptor = _registry.GetControl(question.ControlType);

        model.Points = scoring.Points;
        model.PartialCredit = scoring.PartialCredit;
        model.PenaltyPerWrong = scoring.PenaltyPerWrong;
        model.CategoryId = scoring.CategoryId;
        model.KeyChoices = question.Choices.Select(choice => new SurveyChoiceKeyEditViewModel
        {
            Id = choice.Id,
            Text = choice.Text ?? choice.Value,
            Correct = scoring.CorrectChoiceIds.Contains(choice.Id),
            Points = scoring.ChoicePoints.TryGetValue(choice.Id, out var points) ? points : null,
            Feedback = scoring.ChoiceFeedback.GetValueOrDefault(choice.Id),
        }).ToList();
        model.AcceptedAnswers = string.Join(Environment.NewLine, scoring.AcceptedAnswers);
        model.CaseSensitive = scoring.CaseSensitive;
        model.NumberAnswer = scoring.NumberAnswer;
        model.Tolerance = scoring.Tolerance;
        model.HasChoices = question.Choices.Count > 0;
        model.HasPairs = question.Rows.Count > 0 && question.Choices.Count > 0;
        model.Pairs = question.Rows.Select(row => new SurveyPairEditViewModel
        {
            RowId = row.Id,
            RowText = row.Text,
            ChoiceId = scoring.Pairs.GetValueOrDefault(row.Id),
        }).ToList();
        model.PairChoices = question.Choices.Select(choice => new SelectListItem(choice.Text ?? choice.Value, choice.Id)).ToList();
        model.GeneralFeedback = scoring.GeneralFeedback;
        model.CorrectFeedback = scoring.CorrectFeedback;
        model.IncorrectFeedback = scoring.IncorrectFeedback;
        model.GraderGuidance = scoring.GraderGuidance;
        model.ManuallyGraded = descriptor?.Has(SurveyControlCapabilities.ManuallyGraded) == true;
        model.DefaultPoints = _designer.Definition?.Scoring?.DefaultPoints ?? 1;
        model.Categories = (_designer.Definition?.ScoreCategories ?? [])
            .Select(category => new SelectListItem(category.Title ?? category.Name, category.Id, category.Id == scoring.CategoryId))
            .ToList();
    }
}
