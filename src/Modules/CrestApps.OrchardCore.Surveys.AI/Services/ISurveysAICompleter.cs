namespace CrestApps.OrchardCore.Surveys.AI.Services;

/// <summary>
/// Sends one prompt template and one input to the site's utility AI model.
/// </summary>
public interface ISurveysAICompleter
{
    /// <summary>
    /// Gets whether an AI model is set up.
    /// </summary>
    /// <returns><see langword="true"/> when one is.</returns>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Renders a prompt template as the system message and sends the input as the user message.
    /// </summary>
    /// <param name="templateId">The prompt template id.</param>
    /// <param name="arguments">The template arguments.</param>
    /// <param name="input">The user message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The model's answer, or <see langword="null"/> when no model is set up.</returns>
    Task<string> CompleteAsync(string templateId, IDictionary<string, object> arguments, string input, CancellationToken cancellationToken);
}
