using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI;

public interface IAIToolSource
{
    /// <summary>
    /// Gets the unique name of the AI tool source.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the type of the AI tool source.
    /// </summary>
    AIToolSourceType Type { get; }

    /// <summary>
    /// Gets the localized display name of the AI tool source.
    /// </summary>
    LocalizedString DisplayName { get; }

    /// <summary>
    /// Gets the localized description of the AI tool source.
    /// </summary>
    LocalizedString Description { get; }

    /// <summary>
    /// Creates an instance of an AI tool asynchronously based on the given configuration.
    /// </summary>
    /// <param name="instance">The AI tool instance containing configuration details.</param>
    /// <returns>A task that represents the asynchronous operation, returning the created AI tool.</returns>
    Task<AITool> CreateAsync(AIToolInstance instance);
}
