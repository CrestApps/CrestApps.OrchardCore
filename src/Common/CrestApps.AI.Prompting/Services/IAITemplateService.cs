using CrestApps.AI.Prompting.Models;

namespace CrestApps.AI.Prompting.Services;

/// <summary>
/// Service for discovering, listing, rendering, and composing AI prompt templates.
/// </summary>
public interface IAITemplateService
{
    /// <summary>
    /// Lists all available prompt templates.
    /// </summary>
    /// <returns>All registered prompt templates.</returns>
    Task<IReadOnlyList<AITemplate>> ListAsync();

    /// <summary>
    /// Gets a prompt template by its unique identifier.
    /// </summary>
    /// <param name="id">The prompt template identifier.</param>
    /// <returns>The prompt template, or <see langword="null"/> if not found.</returns>
    Task<AITemplate> GetAsync(string id);

    /// <summary>
    /// Renders a prompt template with the provided arguments.
    /// Processes Liquid syntax in the template body.
    /// </summary>
    /// <param name="id">The prompt template identifier.</param>
    /// <param name="arguments">Key-value pairs to pass as template variables.</param>
    /// <returns>The rendered prompt text.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the template with the specified <paramref name="id"/> is not found.</exception>
    Task<string> RenderAsync(string id, IDictionary<string, object> arguments = null);

    /// <summary>
    /// Renders and merges multiple prompt templates into a single output.
    /// Each template is rendered independently and the results are concatenated.
    /// </summary>
    /// <param name="ids">The prompt template identifiers to merge.</param>
    /// <param name="arguments">Key-value pairs to pass as template variables.</param>
    /// <param name="separator">The separator between merged prompts. Defaults to double newline.</param>
    /// <returns>The merged rendered output.</returns>
    Task<string> MergeAsync(IEnumerable<string> ids, IDictionary<string, object> arguments = null, string separator = "\n\n");
}
