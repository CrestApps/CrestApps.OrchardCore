namespace CrestApps.OrchardCore.Surveys.Core.Controls.Boolean;

/// <summary>
/// The settings of a yes or no question.
/// </summary>
public sealed class BooleanSettings
{
    /// <summary>
    /// Gets or sets how the question shows.
    /// </summary>
    public BooleanDisplay Display { get; set; }

    /// <summary>
    /// Gets or sets the label of yes, or <see langword="null"/> for the default.
    /// </summary>
    public string TrueLabel { get; set; }

    /// <summary>
    /// Gets or sets the label of no, or <see langword="null"/> for the default.
    /// </summary>
    public string FalseLabel { get; set; }
}
