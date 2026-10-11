using System.Globalization;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.Expressions;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Formats designed report values for display with the current culture. Each data type has a default format, a column
/// can set its own .NET format string, and day-of-week and month-of-year numbers are shown as names.
/// </summary>
public sealed class ReportValueFormatter
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportValueFormatter"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReportValueFormatter(IStringLocalizer<ReportValueFormatter> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Formats a value of a result column.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="column">The column.</param>
    /// <returns>The formatted text; empty when the value is missing.</returns>
    public string Format(object value, ReportResultColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);

        if (value is not null && !column.IsMeasure)
        {
            if (column.Transform == ReportFieldTransform.DayOfWeek && ExpressionOperations.ToInteger(value) is long day && day is >= 1 and <= 7)
            {
                return CultureInfo.CurrentCulture.DateTimeFormat.GetDayName((DayOfWeek)(day % 7));
            }

            if (column.Transform == ReportFieldTransform.MonthOfYear && ExpressionOperations.ToInteger(value) is long month && month is >= 1 and <= 12)
            {
                return CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName((int)month);
            }

            if (column.Transform == ReportFieldTransform.Month && string.IsNullOrEmpty(column.Format))
            {
                return Format(value, column.DataType, "Y");
            }
        }

        return Format(value, column.DataType, column.Format);
    }

    /// <summary>
    /// Formats a value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="dataType">The type of the value.</param>
    /// <param name="format">The .NET format string, or <see langword="null"/> for the default of the type.</param>
    /// <returns>The formatted text; empty when the value is missing.</returns>
    public string Format(object value, ReportDataType dataType, string format = null)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is bool flag)
        {
            return flag ? S["Yes"] : S["No"];
        }

        var effectiveFormat = string.IsNullOrWhiteSpace(format)
            ? DefaultFormat(dataType, value)
            : format.Trim();

        if (value is IFormattable formattable && effectiveFormat is not null)
        {
            try
            {
                return formattable.ToString(effectiveFormat, CultureInfo.CurrentCulture);
            }
            catch (FormatException)
            {
                return formattable.ToString(DefaultFormat(dataType, value), CultureInfo.CurrentCulture);
            }
        }

        return ReportDataValues.ToText(value);
    }

    private static string DefaultFormat(ReportDataType dataType, object value)
    {
        return value switch
        {
            DateTime => dataType == ReportDataType.Date || ((DateTime)value).TimeOfDay == TimeSpan.Zero ? "d" : "g",
            long or int => "N0",
            decimal or double or float => dataType == ReportDataType.Integer ? "N0" : "#,##0.##",
            _ => null,
        };
    }
}
