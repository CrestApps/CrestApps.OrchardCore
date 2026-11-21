using System.Globalization;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.Ranking;

/// <summary>
/// Items put in order. The answer holds the item ids from first to last. The drag list posts the ids in order under the
/// question's prefix; the select mode posts a rank for each item under <c>q.{questionId}.{itemId}</c>.
/// </summary>
public sealed class RankingControlHandler : SurveyChoiceControlHandlerBase<RankingSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RankingControlHandler"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public RankingControlHandler(IStringLocalizer<RankingControlHandler> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.Ranking;

    /// <inheritdoc />
    protected override bool AllowsMultiple => true;

    /// <inheritdoc />
    public override void PreparePresentation(SurveyQuestion question, SurveyPresentationContext context)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(context);

        // The order shown must not give the right order away, so items always start shuffled.
        context.Plan.ChoiceOrder[question.Id] = SurveyChoiceSource.OrderChoices(SurveyChoiceSource.GetChoices(question, context.Definition), shuffle: true, context.Random);
    }

    /// <inheritdoc />
    public override Task ReadAnswerAsync(SurveyAnswerReadContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var items = SurveyChoiceSource.GetChoices(context.Question, context.Definition).Where(choice => !choice.Retired).Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal);
        var ordered = ReadValues(context, context.Prefix).Where(items.Contains).Distinct(StringComparer.Ordinal).ToList();

        if (ordered.Count == 0)
        {
            var ranks = new List<(string Id, int Rank)>();

            foreach (var id in items)
            {
                if (int.TryParse(ReadValue(context, $"{context.Prefix}.{id}"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank) && rank > 0)
                {
                    ranks.Add((id, rank));
                }
            }

            ordered = ranks.OrderBy(entry => entry.Rank).Select(entry => entry.Id).ToList();
        }

        var settings = context.Question.GetSettings<RankingSettings>();

        if (settings.MaxRanked is > 0 && ordered.Count > settings.MaxRanked)
        {
            ordered = ordered.Take(settings.MaxRanked.Value).ToList();
        }

        context.Answer = ordered.Count == 0 ? null : new SurveyAnswer { Values = ordered };

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task ValidateAnswerAsync(SurveyAnswerValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.IsRequired)
        {
            return Task.CompletedTask;
        }

        var settings = context.Question.GetSettings<RankingSettings>();
        var items = context.Question.Choices.Count(choice => choice is not null && !choice.Retired);
        var needed = settings.MaxRanked is > 0 ? Math.Min(settings.MaxRanked.Value, items) : items;

        if ((context.Answer?.Values?.Count ?? 0) < needed)
        {
            context.Errors.Add(S["Rank {0} items.", needed]);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override string FormatAnswer(SurveyQuestion question, SurveyAnswer answer, SurveyFormatContext context)
        => answer?.Values is { Count: > 0 } values
            ? string.Join(", ", values.Select((id, index) => $"{index + 1}. {(FindChoice(question, id) is { } choice ? (context?.UseValues == true ? choice.Value ?? choice.Id : choice.Text) : id)}"))
            : string.Empty;

    /// <inheritdoc />
    public override IEnumerable<SurveyAnswerIndexRow> GetIndexRows(SurveyQuestion question, SurveyAnswer answer)
    {
        var values = answer?.Values ?? [];

        for (var i = 0; i < values.Count; i++)
        {
            yield return new SurveyAnswerIndexRow { RowId = values[i], NumberValue = i + 1 };
        }
    }
}
