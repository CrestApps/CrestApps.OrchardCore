namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Identifies a transform applied to a column's values before grouping.
/// </summary>
public enum ReportFieldTransform
{
    /// <summary>
    /// The value is used as is.
    /// </summary>
    None,

    /// <summary>
    /// Text in upper case.
    /// </summary>
    Upper,

    /// <summary>
    /// Text in lower case.
    /// </summary>
    Lower,

    /// <summary>
    /// Text without leading and trailing white space.
    /// </summary>
    Trim,

    /// <summary>
    /// The number of characters of the text.
    /// </summary>
    Length,

    /// <summary>
    /// The year of a date.
    /// </summary>
    Year,

    /// <summary>
    /// The quarter of a date, as <c>2026 Q1</c>.
    /// </summary>
    Quarter,

    /// <summary>
    /// The first day of the month of a date.
    /// </summary>
    Month,

    /// <summary>
    /// The first day of the week of a date (weeks start on Monday).
    /// </summary>
    Week,

    /// <summary>
    /// The date without its time of day.
    /// </summary>
    Day,

    /// <summary>
    /// The day of the week of a date.
    /// </summary>
    DayOfWeek,

    /// <summary>
    /// The hour of the day of a date-time.
    /// </summary>
    Hour,

    /// <summary>
    /// The month number of a date (1 to 12), for comparing months across years.
    /// </summary>
    MonthOfYear,

    /// <summary>
    /// A number rounded to a whole number.
    /// </summary>
    Round,
}
