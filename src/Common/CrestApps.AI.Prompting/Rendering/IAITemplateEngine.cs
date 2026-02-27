namespace CrestApps.AI.Prompting.Rendering;

/// <summary>
/// Processes AI Liquid templates: renders them with arguments and validates their syntax.
/// </summary>
public interface IAITemplateEngine
{
    /// <summary>
    /// Renders a Liquid template string with the given arguments.
    /// </summary>
    /// <param name="template">The Liquid template content.</param>
    /// <param name="arguments">Key-value pairs to pass as template variables.</param>
    /// <returns>The rendered output string.</returns>
    Task<string> RenderAsync(string template, IDictionary<string, object> arguments = null);

    /// <summary>
    /// Validates that a Liquid template has valid syntax.
    /// </summary>
    /// <param name="template">The Liquid template content to validate.</param>
    /// <param name="errors">When validation fails, contains the error messages.</param>
    /// <returns><see langword="true"/> if the template syntax is valid; otherwise, <see langword="false"/>.</returns>
    bool TryValidate(string template, out IList<string> errors);
}
