namespace CrestApps.OrchardCore.Surveys.Core.Controls.Matrix;

/// <summary>
/// The settings of a matrix question. Its columns are the question's choices and its rows the question's rows.
/// </summary>
public sealed class MatrixSettings
{
    /// <summary>
    /// Gets or sets whether each row takes one column or several.
    /// </summary>
    public MatrixMode Mode { get; set; }

    /// <summary>
    /// Gets or sets whether every row must be answered when the question is required.
    /// </summary>
    public bool AllRowsRequired { get; set; } = true;
}
