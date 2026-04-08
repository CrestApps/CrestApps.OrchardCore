namespace CrestApps.Core.Templates.Models;

/// <summary>
/// Describes an expected parameter for an template.
/// </summary>
public sealed class TemplateParameterDescriptor
{
    /// <summary>
    /// Gets or sets the parameter name (used as the Liquid variable name).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a human-readable description of the parameter,
    /// including its expected type and purpose.
    /// </summary>
    public string Description { get; set; }
}
