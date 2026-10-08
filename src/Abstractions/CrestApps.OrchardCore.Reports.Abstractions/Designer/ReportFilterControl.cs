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
}
