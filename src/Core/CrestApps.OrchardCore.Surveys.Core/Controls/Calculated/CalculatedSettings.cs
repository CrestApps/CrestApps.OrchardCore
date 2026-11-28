namespace CrestApps.OrchardCore.Surveys.Core.Controls.Calculated;

/// <summary>
/// The settings of a calculated value question.
/// </summary>
public sealed class CalculatedSettings
{
    /// <summary>
    /// Gets or sets the formula, in the Data Pipelines formula language over the survey's questions and variables.
    /// </summary>
    public string Formula { get; set; }

    /// <summary>
    /// Gets or sets whether respondents see the value.
    /// </summary>
    public CalculatedDisplay Display { get; set; }

    /// <summary>
    /// Gets or sets a .NET format of the value shown, such as N2, or <see langword="null"/>.
    /// </summary>
    public string Format { get; set; }
}
