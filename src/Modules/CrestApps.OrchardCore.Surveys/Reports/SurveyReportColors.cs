using System.Globalization;
using CrestApps.OrchardCore.Reports.Designer;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Surveys.Reports;

/// <summary>
/// The built-in color rules of the survey reports, written as Report Builder <see cref="ReportColumnColors"/> and applied
/// with the shared <see cref="ReportColumnColorizer"/>, so a coded survey report colors its cells exactly like a designed
/// report would.
/// </summary>
internal static class SurveyReportColors
{
    /// <summary>
    /// The background of a bad value.
    /// </summary>
    public const string Bad = "#F8D7DA";

    /// <summary>
    /// The background of a middling value.
    /// </summary>
    public const string Middling = "#FFF3CD";

    /// <summary>
    /// The background of a good value.
    /// </summary>
    public const string Good = "#D1E7DD";

    /// <summary>
    /// The text color of a value below the pass mark.
    /// </summary>
    public const string BelowPassMark = "#B02A37";

    /// <summary>
    /// Gets the colors of a percent column: red text below the pass mark.
    /// </summary>
    /// <param name="passMarkPercent">The pass mark, from 0 to 100.</param>
    /// <returns>The colors.</returns>
    public static ReportColumnColors BelowPass(decimal passMarkPercent)
        => new()
        {
            Rules =
            [
                Rule(DataFilterOperator.LessThan, ReportColorTarget.Text, BelowPassMark, passMarkPercent),
            ],
        };

    /// <summary>
    /// Gets the colors of a pass or fail column: a red background on a failed response.
    /// </summary>
    /// <returns>The colors.</returns>
    public static ReportColumnColors Failed()
        => new()
        {
            Rules =
            [
                new ReportColorRule { Operator = DataFilterOperator.Equals, Values = ["false"], Color = Bad, Target = ReportColorTarget.Background },
            ],
        };

    /// <summary>
    /// Gets the colors of a Net Promoter Score question: detractors (0 to 6) red, passives (7 and 8) amber and promoters
    /// (9 and 10) green.
    /// </summary>
    /// <returns>The colors.</returns>
    public static ReportColumnColors Nps()
        => new()
        {
            Rules =
            [
                Rule(DataFilterOperator.LessThanOrEqual, ReportColorTarget.Background, Bad, 6),
                Rule(DataFilterOperator.Between, ReportColorTarget.Background, Middling, 7, 8),
                Rule(DataFilterOperator.GreaterThanOrEqual, ReportColorTarget.Background, Good, 9),
            ],
        };

    /// <summary>
    /// Gets the colors of a discrimination column: an amber cell below the threshold.
    /// </summary>
    /// <param name="threshold">The weakest discrimination that tells strong and weak respondents apart.</param>
    /// <returns>The colors.</returns>
    public static ReportColumnColors WeakBelow(decimal threshold)
        => new()
        {
            Rules =
            [
                Rule(DataFilterOperator.LessThan, ReportColorTarget.Background, Middling, threshold),
            ],
        };

    private static ReportColorRule Rule(DataFilterOperator filterOperator, ReportColorTarget target, string color, params decimal[] values)
        => new()
        {
            Operator = filterOperator,
            Values = values.Select(value => value.ToString(CultureInfo.InvariantCulture)).ToList(),
            Color = color,
            Target = target,
        };
}
