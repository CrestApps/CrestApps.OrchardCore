using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Surveys.Core.Formulas;

/// <summary>
/// One field of the survey formula scope.
/// </summary>
public sealed class SurveyFormulaField
{
    /// <summary>
    /// Gets or sets the key formulas write between brackets.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the type of the values.
    /// </summary>
    public DataFieldType Type { get; set; }

    /// <summary>
    /// Gets or sets what the field reads.
    /// </summary>
    public SurveyFormulaFieldKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the question id, variable name or category id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the row id, or <see langword="null"/>.
    /// </summary>
    public string RowId { get; set; }

    /// <summary>
    /// Gets or sets the slot of the row that holds the value.
    /// </summary>
    public int Slot { get; set; }
}
