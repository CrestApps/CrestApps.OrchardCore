namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Identifies the control that renders an exposed filter.
/// </summary>
public enum ReportFilterControl
{
    /// <summary>
    /// Picks a control from the field's data type and the filter operator.
    /// </summary>
    Auto,

    /// <summary>
    /// A text box.
    /// </summary>
    Text,

    /// <summary>
    /// A drop-down list of the values found in the data; the person picks one.
    /// </summary>
    Select,

    /// <summary>
    /// A list of the values found in the data; the person picks any number.
    /// </summary>
    MultiSelect,

    /// <summary>
    /// A date range picker.
    /// </summary>
    DateRange,

    /// <summary>
    /// A pair of minimum and maximum number boxes.
    /// </summary>
    NumberRange,

    /// <summary>
    /// A yes, no, or any choice.
    /// </summary>
    Boolean,

    /// <summary>
    /// A choice of recent periods (today, the last 7, 30, or 90 days, the last year, or all time) for a date field. The
    /// filter keeps the rows of the last number of days its value names, and keeps every row when it has no value.
    /// </summary>
    RelativeDate,
}
