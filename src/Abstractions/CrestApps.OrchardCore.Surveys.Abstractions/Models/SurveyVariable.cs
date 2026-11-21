using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// A value a survey keeps besides answers: hidden (from the link, a collector or a consumer) or calculated by a formula.
/// </summary>
public sealed class SurveyVariable
{
    /// <summary>
    /// Gets or sets the name, unique across questions and variables.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets whether the value is hidden or calculated.
    /// </summary>
    public VariableKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the formula of a calculated variable.
    /// </summary>
    public string Formula { get; set; }

    /// <summary>
    /// Gets or sets the default value.
    /// </summary>
    public string DefaultValue { get; set; }

    /// <summary>
    /// Gets or sets whether the value may come from the query string of the link.
    /// </summary>
    public bool AcceptFromUrl { get; set; }

    /// <summary>
    /// Gets or sets the type of the value.
    /// </summary>
    public DataFieldType DataType { get; set; } = DataFieldType.Text;
}
