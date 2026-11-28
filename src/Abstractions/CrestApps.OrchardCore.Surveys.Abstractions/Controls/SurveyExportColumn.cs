using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Surveys.Controls;

/// <summary>
/// One export column of a question.
/// </summary>
public sealed class SurveyExportColumn
{
    /// <summary>
    /// Gets or sets the stable key: <c>{name}</c>, <c>{name}.{row}</c> or <c>{name}.other</c>.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the column title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the type of its values.
    /// </summary>
    public DataFieldType Type { get; set; } = DataFieldType.Text;

    /// <summary>
    /// Gets or sets the id of the question.
    /// </summary>
    public string QuestionId { get; set; }

    /// <summary>
    /// Gets or sets the id of the row, or <see langword="null"/>.
    /// </summary>
    public string RowId { get; set; }
}
