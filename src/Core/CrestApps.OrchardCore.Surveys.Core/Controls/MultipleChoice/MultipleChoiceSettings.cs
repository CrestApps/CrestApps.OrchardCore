namespace CrestApps.OrchardCore.Surveys.Core.Controls.MultipleChoice;

/// <summary>
/// The settings of a multiple choice question.
/// </summary>
public sealed class MultipleChoiceSettings
{
    /// <summary>
    /// Gets or sets how the choices show.
    /// </summary>
    public MultipleChoiceDisplay Display { get; set; }

    /// <summary>
    /// Gets or sets the fewest choices a respondent must pick, or <see langword="null"/>.
    /// </summary>
    public int? MinSelected { get; set; }

    /// <summary>
    /// Gets or sets the most choices a respondent may pick, or <see langword="null"/>.
    /// </summary>
    public int? MaxSelected { get; set; }

    /// <summary>
    /// Gets or sets how many columns the choices show in.
    /// </summary>
    public int Columns { get; set; } = 1;
}
