using CrestApps.Core.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The default <see cref="IVoicemailGreetingCopyRecorder"/>: records the copy in a deferred task that runs in its own
/// scope after the call command's unit of work has committed. It reloads the agent, records the copy only when it is
/// not already recorded, and logs rather than throws when the save fails; the next call that reaches the voicemail
/// then uploads the greeting once more, which is acceptable.
/// </summary>
public sealed class DeferredVoicemailGreetingCopyRecorder : IVoicemailGreetingCopyRecorder
{
    /// <inheritdoc/>
    public void RecordProviderCopy(string agentId, string providerName, string mediaReference)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);
        ArgumentException.ThrowIfNullOrEmpty(providerName);
        ArgumentException.ThrowIfNullOrEmpty(mediaReference);

        if (ShellScope.Current is null)
        {
            return;
        }

        ShellScope.AddDeferredTask(scope => RecordAsync(scope.ServiceProvider, agentId, providerName, mediaReference));
    }

    /// <summary>
    /// Records the copy on a freshly loaded agent profile. Exposed for the deferred task and for tests.
    /// </summary>
    /// <param name="serviceProvider">The service provider of the scope the task runs in.</param>
    /// <param name="agentId">The identifier of the agent whose greeting was copied.</param>
    /// <param name="providerName">The technical name of the provider that holds the copy.</param>
    /// <param name="mediaReference">The reference the provider plays the greeting by.</param>
    internal static async Task RecordAsync(IServiceProvider serviceProvider, string agentId, string providerName, string mediaReference)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<DeferredVoicemailGreetingCopyRecorder>>();

        try
        {
            var agents = serviceProvider.GetRequiredService<IAgentProfileManager>();
            var agent = await agents.FindByIdAsync(agentId);
            var media = agent?.VoicemailGreetingMedia;

            // The greeting was removed or replaced between the call and now; the copy belongs to nothing any more.
            if (media is null)
            {
                return;
            }

            media.ProviderReferences ??= new Dictionary<string, string>();

            var recorded = media.ProviderReferences
                .Where(copy => string.Equals(copy.Key, providerName, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (recorded.Any(copy => string.Equals(copy.Value, mediaReference, StringComparison.Ordinal)))
            {
                return;
            }

            foreach (var copy in recorded)
            {
                media.ProviderReferences.Remove(copy.Key);
            }

            media.ProviderReferences[providerName] = mediaReference;

            await agents.UpdateAsync(agent);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to record the voicemail greeting copy {Reference} the telephony provider {Provider} was given for agent {AgentId}; the next call to the voicemail uploads the greeting again.",
                mediaReference.SanitizeLogValue(),
                providerName.SanitizeLogValue(),
                agentId.SanitizeLogValue());
        }
    }
}
