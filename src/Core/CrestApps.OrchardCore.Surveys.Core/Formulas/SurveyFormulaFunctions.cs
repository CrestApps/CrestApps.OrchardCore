using System.Globalization;
using CrestApps.OrchardCore.Surveys.Core.Services;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Expressions;

namespace CrestApps.OrchardCore.Surveys.Core.Formulas;

/// <summary>
/// The built-in survey formula functions: SELECTED, COUNTSELECTED, ANSWERED, SCORE and AGE.
/// </summary>
public sealed class SurveyFormulaFunctions : ISurveyExpressionFunctionProvider
{
    private static readonly char[] _separators = [',', ';', '\n'];

    /// <inheritdoc />
    public IEnumerable<ExpressionFunction> GetFunctions()
    {
        yield return ExpressionFunction.CreateScalar(
            "SELECTED",
            "Survey",
            "SELECTED([question], value)",
            "True when the answer of a choice question includes the value.",
            2,
            2,
            _ => DataFieldType.Boolean,
            args => args[0] is null || args[1] is null ? false : Split(args[0]).Contains(DataValues.ToText(args[1])?.Trim(), StringComparer.OrdinalIgnoreCase));

        yield return ExpressionFunction.CreateScalar(
            "COUNTSELECTED",
            "Survey",
            "COUNTSELECTED([question])",
            "The number of choices selected in an answer.",
            1,
            1,
            _ => DataFieldType.Integer,
            args => (long)Split(args[0]).Length);

        yield return ExpressionFunction.CreateScalar(
            "ANSWERED",
            "Survey",
            "ANSWERED([question])",
            "True when the question has an answer.",
            1,
            1,
            _ => DataFieldType.Boolean,
            args => !DataValues.IsEmpty(args[0]));

        yield return ExpressionFunction.CreateScalar(
            "SCORE",
            "Survey",
            "SCORE('question name')",
            "The score of one question of the response, by its name.",
            1,
            1,
            _ => DataFieldType.Decimal,
            args => SurveyFormulaContext.Current?.ScoreOf(DataValues.ToText(args[0])));

        yield return ExpressionFunction.CreateScalar(
            "AGE",
            "Survey",
            "AGE([date])",
            "The whole years from a date to today.",
            1,
            1,
            _ => DataFieldType.Integer,
            args => Age(args[0]));
    }

    private static string[] Split(object value)
    {
        var text = DataValues.ToText(value);

        return string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split(_separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static long? Age(object value)
    {
        DateTime date;

        if (value is DateTime typed)
        {
            date = typed;
        }
        else if (!DateTime.TryParse(DataValues.ToText(value), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out date))
        {
            return null;
        }

        var today = SurveyFormulaContext.Current?.Today ?? DateOnly.FromDateTime(DateTime.SpecifyKind(date, DateTimeKind.Utc));
        var years = today.Year - date.Year;

        if (today < DateOnly.FromDateTime(date).AddYears(years))
        {
            years--;
        }

        return Math.Max(years, 0);
    }
}
