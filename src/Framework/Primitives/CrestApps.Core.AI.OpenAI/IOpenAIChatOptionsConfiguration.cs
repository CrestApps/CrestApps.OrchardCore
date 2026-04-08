using CrestApps.Core.AI.Models;
using OpenAI.Chat;

namespace CrestApps.Core.AI.OpenAI;

/// <summary>
/// Configures OpenAI-specific chat completion options during the AI completion pipeline.
/// Implementations can apply provider-specific settings (e.g., response format, reasoning effort)
/// that are not available through the generic <see cref="CompletionServiceConfigureContext"/>.
/// </summary>
public interface IOpenAIChatOptionsConfiguration
{
    /// <summary>
    /// Asynchronously initializes any provider-specific state before the completion request.
    /// Called once per request before <see cref="Configure"/> to set up any required context.
    /// </summary>
    /// <param name="context">The completion service context containing request options and settings.</param>
    Task InitializeConfigurationAsync(CompletionServiceConfigureContext context);

    /// <summary>
    /// Applies OpenAI-specific settings to the <see cref="ChatCompletionOptions"/> object.
    /// Called after <see cref="InitializeConfigurationAsync"/> to finalize provider-specific options.
    /// </summary>
    /// <param name="context">The completion service context containing request options and settings.</param>
    /// <param name="chatCompletionOptions">The OpenAI chat completion options to configure.</param>
    void Configure(CompletionServiceConfigureContext context, ChatCompletionOptions chatCompletionOptions);
}
