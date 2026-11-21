namespace CrestApps.OrchardCore.Surveys.Core.Controls.SingleChoice;

/// <summary>
/// The settings of a single choice question.
/// </summary>
public sealed class SingleChoiceSettings
{
    /// <summary>
    /// Gets or sets how the choices show.
    /// </summary>
    public SingleChoiceDisplay Display { get; set; }

    /// <summary>
    /// Gets or sets how many columns the choices show in; 0 or 1 for one column.
    /// </summary>
    public int Columns { get; set; } = 1;
}
