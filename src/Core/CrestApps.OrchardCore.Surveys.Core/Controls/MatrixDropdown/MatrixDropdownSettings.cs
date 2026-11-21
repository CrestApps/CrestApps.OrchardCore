using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.MatrixDropdown;

/// <summary>
/// The settings of a drop-down matrix: its columns. Its rows are the question's rows and the options of every cell are
/// the question's choices.
/// </summary>
public sealed class MatrixDropdownSettings
{
    /// <summary>
    /// Gets or sets the columns.
    /// </summary>
    public List<SurveySubQuestion> Columns { get; set; } = [];

    /// <summary>
    /// Gets or sets whether every cell must be answered when the question is required.
    /// </summary>
    public bool AllCellsRequired { get; set; }
}
