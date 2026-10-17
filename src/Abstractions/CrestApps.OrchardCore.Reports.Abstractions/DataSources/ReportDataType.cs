namespace CrestApps.OrchardCore.Reports.DataSources;

/// <summary>
/// Identifies the logical type of a report field. Every value a data source returns must use the CLR type that
/// matches its field's data type: <see cref="string"/> for <see cref="Text"/>, <see cref="long"/> for
/// <see cref="Integer"/>, <see cref="decimal"/> for <see cref="Decimal"/>, <see cref="bool"/> for
/// <see cref="Boolean"/>, and <see cref="DateTime"/> for <see cref="Date"/> and <see cref="DateTime"/>. Use
/// <see cref="ReportDataValues.Coerce(object, ReportDataType)"/> to convert raw values.
/// </summary>
public enum ReportDataType
{
    /// <summary>
    /// A text value.
    /// </summary>
    Text,

    /// <summary>
    /// A whole number.
    /// </summary>
    Integer,

    /// <summary>
    /// A decimal number.
    /// </summary>
    Decimal,

    /// <summary>
    /// A true or false value.
    /// </summary>
    Boolean,

    /// <summary>
    /// A calendar date with no time of day and no time zone. Date values are never shifted to the tenant time zone.
    /// </summary>
    Date,

    /// <summary>
    /// A point in time. A data source returns date-time values in UTC; the report engine shows them in the tenant
    /// time zone.
    /// </summary>
    DateTime,
}
