using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.Rating;

/// <summary>
/// Summarizes a rating question: average and distribution, plus the Net Promoter Score or customer satisfaction score
/// when the question reports one.
/// </summary>
public sealed class RatingAnalyzer : SurveyNumberAnalyzerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RatingAnalyzer"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public RatingAnalyzer(IStringLocalizer<RatingAnalyzer> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.Rating;

    /// <summary>
    /// Computes the Net Promoter Score of a distribution: percent of 9 and 10 minus percent of 0 to 6.
    /// </summary>
    /// <param name="distribution">How many responses gave each value.</param>
    /// <returns>The promoters, passives, detractors and score, or <see langword="null"/> without answers.</returns>
    public static (long Promoters, long Passives, long Detractors, decimal Score)? Nps(IReadOnlyDictionary<decimal, long> distribution)
    {
        var total = distribution?.Values.Sum() ?? 0;

        if (total == 0)
        {
            return null;
        }

        var promoters = distribution.Where(entry => entry.Key >= 9).Sum(entry => entry.Value);
        var detractors = distribution.Where(entry => entry.Key <= 6).Sum(entry => entry.Value);
        var passives = total - promoters - detractors;
        var score = Math.Round(100m * (promoters - detractors) / total, 1, MidpointRounding.AwayFromZero);

        return (promoters, passives, detractors, score);
    }

    /// <summary>
    /// Computes the customer satisfaction score: percent of the two highest values of the scale.
    /// </summary>
    /// <param name="distribution">How many responses gave each value.</param>
    /// <param name="max">The highest value of the scale.</param>
    /// <returns>The percent, or <see langword="null"/> without answers.</returns>
    public static decimal? Csat(IReadOnlyDictionary<decimal, long> distribution, int max)
    {
        var total = distribution?.Values.Sum() ?? 0;

        if (total == 0)
        {
            return null;
        }

        var satisfied = distribution.Where(entry => entry.Key >= max - 1).Sum(entry => entry.Value);

        return Math.Round(100m * satisfied / total, 1, MidpointRounding.AwayFromZero);
    }

    /// <inheritdoc />
    protected override IEnumerable<ReportSection> ExtraSections(SurveyQuestion question, string title, SurveyQuestionSummary summary, SurveyAnalysisContext context)
    {
        var settings = question.GetSettings<RatingSettings>();
        var distribution = summary.Numbers?.Distribution ?? [];

        if (settings.Metric == RatingMetric.Nps && Nps(distribution) is { } nps)
        {
            var total = nps.Promoters + nps.Passives + nps.Detractors;

            yield return ReportSection.ForMetrics(S["Net Promoter Score"],
            [
                new ReportMetric(S["Net Promoter Score"], Format(nps.Score, context.Culture)),
                new ReportMetric(S["Promoters (9-10)"], ReportFormat.Percent((double)nps.Promoters / total)),
                new ReportMetric(S["Passives (7-8)"], ReportFormat.Percent((double)nps.Passives / total)),
                new ReportMetric(S["Detractors (0-6)"], ReportFormat.Percent((double)nps.Detractors / total)),
            ]);
        }

        if (settings.Metric == RatingMetric.Csat && Csat(distribution, settings.Max) is { } csat)
        {
            yield return ReportSection.ForMetrics(S["Customer satisfaction"],
            [
                new ReportMetric(S["CSAT"], $"{Format(csat, context.Culture)}%", S["Share of the two highest ratings"]),
            ]);
        }
    }
}
