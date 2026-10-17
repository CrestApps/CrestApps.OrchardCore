using System.Globalization;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.Expressions;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Applies column transforms and reports which field types each transform supports.
/// </summary>
public static class ReportTransforms
{
    /// <summary>
    /// Determines whether a transform can be applied to a field type.
    /// </summary>
    /// <param name="transform">The transform.</param>
    /// <param name="fieldType">The field type.</param>
    /// <returns><see langword="true"/> when supported.</returns>
    public static bool Supports(ReportFieldTransform transform, ReportDataType fieldType)
    {
        return transform switch
        {
            ReportFieldTransform.None => true,
            ReportFieldTransform.Upper or ReportFieldTransform.Lower or ReportFieldTransform.Trim or ReportFieldTransform.Length => fieldType == ReportDataType.Text,
            ReportFieldTransform.Hour => fieldType == ReportDataType.DateTime,
            ReportFieldTransform.Round => ReportDataValues.IsNumeric(fieldType),
            _ => ReportDataValues.IsTemporal(fieldType),
        };
    }

    /// <summary>
    /// Infers the type of a transformed value.
    /// </summary>
    /// <param name="transform">The transform.</param>
    /// <param name="fieldType">The field type.</param>
    /// <returns>The type of the transformed values.</returns>
    public static ReportDataType GetResultType(ReportFieldTransform transform, ReportDataType fieldType)
    {
        return transform switch
        {
            ReportFieldTransform.Length or
            ReportFieldTransform.Year or
            ReportFieldTransform.DayOfWeek or
            ReportFieldTransform.Hour or
            ReportFieldTransform.MonthOfYear or
            ReportFieldTransform.Round => ReportDataType.Integer,
            ReportFieldTransform.Quarter => ReportDataType.Text,
            ReportFieldTransform.Month or ReportFieldTransform.Week or ReportFieldTransform.Day => ReportDataType.Date,
            _ => fieldType,
        };
    }

    /// <summary>
    /// Applies a transform to a value.
    /// </summary>
    /// <param name="transform">The transform.</param>
    /// <param name="value">The value.</param>
    /// <returns>The transformed value.</returns>
    public static object Apply(ReportFieldTransform transform, object value)
    {
        if (value is null || transform == ReportFieldTransform.None)
        {
            return value;
        }

        switch (transform)
        {
            case ReportFieldTransform.Upper:
                return ReportDataValues.ToText(value).ToUpper(CultureInfo.CurrentCulture);

            case ReportFieldTransform.Lower:
                return ReportDataValues.ToText(value).ToLower(CultureInfo.CurrentCulture);

            case ReportFieldTransform.Trim:
                return ReportDataValues.ToText(value).Trim();

            case ReportFieldTransform.Length:
                return (long)ReportDataValues.ToText(value).Length;

            case ReportFieldTransform.Round:
                return ExpressionOperations.ToNumber(value) is decimal number
                    ? ExpressionOperations.NarrowToInteger(Math.Round(number, MidpointRounding.AwayFromZero))
                    : null;
        }

        if (ExpressionOperations.ToDateTime(value) is not DateTime date)
        {
            return null;
        }

        return transform switch
        {
            ReportFieldTransform.Year => (long)date.Year,
            ReportFieldTransform.Quarter => string.Create(CultureInfo.InvariantCulture, $"{date.Year} Q{(date.Month - 1) / 3 + 1}"),
            ReportFieldTransform.Month => ExpressionFunctions.DateTrunc("month", date),
            ReportFieldTransform.Week => ExpressionFunctions.DateTrunc("week", date),
            ReportFieldTransform.Day => date.Date,
            ReportFieldTransform.DayOfWeek => (long)ExpressionFunctions.IsoDayOfWeek(date),
            ReportFieldTransform.Hour => (long)date.Hour,
            ReportFieldTransform.MonthOfYear => (long)date.Month,
            _ => value,
        };
    }
}
