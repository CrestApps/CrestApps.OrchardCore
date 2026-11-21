namespace CrestApps.OrchardCore.Surveys.Core.Controls.Matrix;

/// <summary>
/// Whether each row of a matrix takes one column or several.
/// </summary>
public enum MatrixMode
{
    /// <summary>
    /// One column per row.
    /// </summary>
    Single,

    /// <summary>
    /// Several columns per row.
    /// </summary>
    Multiple,
}
