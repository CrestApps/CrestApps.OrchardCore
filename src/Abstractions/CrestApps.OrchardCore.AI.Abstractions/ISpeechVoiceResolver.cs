using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Resolves the available speech voices for a deployment by delegating to the matching AI client provider.
/// </summary>
public interface ISpeechVoiceResolver
{
    /// <summary>
    /// Gets the available speech voices for the specified deployment.
    /// </summary>
    /// <param name="deployment">The AI deployment containing provider, connection, and model information.</param>
    /// <returns>An array of available <see cref="SpeechVoice"/> instances.</returns>
    Task<SpeechVoice[]> GetSpeechVoicesAsync(AIDeployment deployment);
}
