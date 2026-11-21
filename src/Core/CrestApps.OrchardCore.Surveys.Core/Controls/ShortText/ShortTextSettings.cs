namespace CrestApps.OrchardCore.Surveys.Core.Controls.ShortText;

/// <summary>
/// The settings of a short text question.
/// </summary>
public sealed class ShortTextSettings
{
    /// <summary>
    /// Gets or sets the kind of input.
    /// </summary>
    public ShortTextInputType InputType { get; set; }

    /// <summary>
    /// Gets or sets the most characters, or <see langword="null"/> for 500.
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Gets or sets a pattern the browser checks as the respondent types (HTML <c>pattern</c>), or <see langword="null"/>.
    /// The server checks the question's validation rules.
    /// </summary>
    public string Mask { get; set; }
}
