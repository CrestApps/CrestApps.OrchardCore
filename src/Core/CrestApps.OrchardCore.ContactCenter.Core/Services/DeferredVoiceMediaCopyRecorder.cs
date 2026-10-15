using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The default <see cref="IVoiceMediaCopyRecorder"/>: each task is deferred to the end of the current shell scope
/// and runs in a scope of its own after the unit of work has committed. Every task reloads the record it acts on,
/// and logs rather than throws when its save fails; a copy that could not be recorded is made once more the next
/// time it is needed, which is acceptable.
/// </summary>
public sealed class DeferredVoiceMediaCopyRecorder : IVoiceMediaCopyRecorder
{
    /// <inheritdoc/>
    public void RecordLibraryCopy(string itemId, string masterStorageReference, string providerName, string mediaReference, string replacedReference)
    {
        ArgumentException.ThrowIfNullOrEmpty(itemId);
        ArgumentException.ThrowIfNullOrEmpty(masterStorageReference);
        ArgumentException.ThrowIfNullOrEmpty(providerName);
        ArgumentException.ThrowIfNullOrEmpty(mediaReference);

        Defer(scope => RecordLibraryCopyAsync(scope.ServiceProvider, itemId, masterStorageReference, providerName, mediaReference, replacedReference));
    }

    /// <inheritdoc/>
    public void RecordGreetingCopy(string agentId, string masterStorageReference, string providerName, string mediaReference, string replacedReference)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);
        ArgumentException.ThrowIfNullOrEmpty(masterStorageReference);
        ArgumentException.ThrowIfNullOrEmpty(providerName);
        ArgumentException.ThrowIfNullOrEmpty(mediaReference);

        Defer(scope => RecordGreetingCopyAsync(scope.ServiceProvider, agentId, masterStorageReference, providerName, mediaReference, replacedReference));
    }

    /// <inheritdoc/>
    public void ReleaseReplacedGreetingCopies(string agentId, VoiceMediaAsset saved, VoiceMediaAsset replaced)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        Defer(scope => ReleaseReplacedGreetingCopiesAsync(scope.ServiceProvider, agentId, saved, replaced));
    }

    /// <summary>
    /// Records a provider's copy on a freshly loaded library entry, or deletes the copy when the entry's audio changed
    /// meanwhile. Exposed for the deferred task and for tests.
    /// </summary>
    /// <param name="serviceProvider">The service provider of the scope the task runs in.</param>
    /// <param name="itemId">The library entry identifier.</param>
    /// <param name="masterStorageReference">The master copy the provider copy was made from.</param>
    /// <param name="providerName">The technical name of the provider that holds the copy.</param>
    /// <param name="mediaReference">The reference the provider plays the clip by.</param>
    /// <param name="replacedReference">The reference of the copy the new one replaces, or <see langword="null"/>.</param>
    internal static async Task RecordLibraryCopyAsync(IServiceProvider serviceProvider, string itemId, string masterStorageReference, string providerName, string mediaReference, string replacedReference)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<DeferredVoiceMediaCopyRecorder>>();

        try
        {
            var store = serviceProvider.GetRequiredService<IVoiceMediaItemStore>();
            var item = await store.FindByIdAsync(itemId);

            if (item is null || !string.Equals(item.MasterStorageReference, masterStorageReference, StringComparison.Ordinal))
            {
                // The entry is gone, or an operator uploaded new audio while the call was still copying the old: the
                // copy just made plays audio nobody wants any more, and must not be recorded against the new master.
                await DeleteOrphanCopyAsync(serviceProvider, masterStorageReference, providerName, mediaReference, logger, "the audio changed meanwhile", itemId);

                return;
            }

            switch (Record(item, providerName, mediaReference, replacedReference))
            {
                case RecordOutcome.AlreadyRecorded:
                    return;
                case RecordOutcome.AnotherCopyRecorded:
                    await DeleteOrphanCopyAsync(serviceProvider, masterStorageReference, providerName, mediaReference, logger, "another copy was recorded meanwhile", itemId);

                    return;
            }

            // The single provider copy an entry recorded before copies were tracked per provider follows the copy a
            // provider was last given, so a reader of those members still finds a copy that plays.
            item.ProviderName = providerName;
            item.MediaReference = mediaReference;

            await store.UpdateAsync(item);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to record the voice media copy {Reference} the telephony provider {Provider} was given for the entry {ItemId}; the next play uploads the clip again.",
                mediaReference.SanitizeLogValue(),
                providerName.SanitizeLogValue(),
                itemId.SanitizeLogValue());
        }
    }

    /// <summary>
    /// Records a provider's copy on a freshly loaded agent profile, or deletes the copy when the greeting changed
    /// meanwhile. Exposed for the deferred task and for tests.
    /// </summary>
    /// <param name="serviceProvider">The service provider of the scope the task runs in.</param>
    /// <param name="agentId">The identifier of the agent whose greeting was copied.</param>
    /// <param name="masterStorageReference">The master copy the provider copy was made from.</param>
    /// <param name="providerName">The technical name of the provider that holds the copy.</param>
    /// <param name="mediaReference">The reference the provider plays the greeting by.</param>
    /// <param name="replacedReference">The reference of the copy the new one replaces, or <see langword="null"/>.</param>
    internal static async Task RecordGreetingCopyAsync(IServiceProvider serviceProvider, string agentId, string masterStorageReference, string providerName, string mediaReference, string replacedReference)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<DeferredVoiceMediaCopyRecorder>>();

        try
        {
            var agents = serviceProvider.GetRequiredService<IAgentProfileManager>();
            var agent = await agents.FindByIdAsync(agentId);
            var media = agent?.VoicemailGreetingMedia;

            if (media is null || !string.Equals(media.MasterStorageReference, masterStorageReference, StringComparison.Ordinal))
            {
                // The greeting was removed or replaced between the call and now: the copy plays the old greeting and
                // must not be recorded against the new one.
                await DeleteOrphanCopyAsync(serviceProvider, masterStorageReference, providerName, mediaReference, logger, "the greeting changed meanwhile", agentId);

                return;
            }

            switch (Record(media, providerName, mediaReference, replacedReference))
            {
                case RecordOutcome.AlreadyRecorded:
                    return;
                case RecordOutcome.AnotherCopyRecorded:
                    await DeleteOrphanCopyAsync(serviceProvider, masterStorageReference, providerName, mediaReference, logger, "another copy was recorded meanwhile", agentId);

                    return;
            }

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

    /// <summary>
    /// Deletes the copies a saved greeting replaced once the save has landed, or the copies made for the save when it
    /// did not. Exposed for the deferred task and for tests.
    /// </summary>
    /// <param name="serviceProvider">The service provider of the scope the task runs in.</param>
    /// <param name="agentId">The identifier of the agent.</param>
    /// <param name="saved">The copies the saved greeting is held as, or <see langword="null"/> when the greeting was removed.</param>
    /// <param name="replaced">The copies the previous greeting was held as, or <see langword="null"/> when there were none.</param>
    internal static async Task ReleaseReplacedGreetingCopiesAsync(IServiceProvider serviceProvider, string agentId, VoiceMediaAsset saved, VoiceMediaAsset replaced)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<DeferredVoiceMediaCopyRecorder>>();

        try
        {
            var agents = serviceProvider.GetRequiredService<IAgentProfileManager>();
            var provisioning = serviceProvider.GetRequiredService<IVoiceMediaProvisioningService>();
            var agent = await agents.FindByIdAsync(agentId);

            var landed = saved is null
                ? agent is not null && agent.VoicemailGreetingMedia is null && string.IsNullOrWhiteSpace(agent.VoicemailGreetingMediaName)
                : VoiceMediaAssetCopies.HoldsSameAudio(agent?.VoicemailGreetingMedia, saved);

            if (landed)
            {
                if (replaced is not null && VoiceMediaAssetCopies.HasAnyCopy(replaced))
                {
                    await provisioning.DeleteEverywhereAsync(replaced);
                }

                return;
            }

            // The profile was not saved with the new greeting, so the copies made for it would never be played.
            if (saved is not null && VoiceMediaAssetCopies.HasAnyCopy(saved))
            {
                logger.LogWarning("The voicemail greeting of agent {AgentId} was not saved, so the copies made for it are deleted and the previous greeting is kept.", agentId.SanitizeLogValue());

                await provisioning.DeleteEverywhereAsync(saved);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete the voicemail greeting copies replaced for agent {AgentId}.", agentId.SanitizeLogValue());
        }
    }

    private static void Defer(Func<ShellScope, Task> task)
    {
        if (ShellScope.Current is null)
        {
            return;
        }

        ShellScope.AddDeferredTask(task);
    }

    // Puts the copy in the record's map under the provider's name. A copy already recorded for the provider is
    // replaced only when it is the one the new copy was made to replace (the provider reported it missing); a
    // different one was recorded by another path meanwhile and is kept, so the new copy is a duplicate.
    private static RecordOutcome Record(IVoiceMediaAsset media, string providerName, string mediaReference, string replacedReference)
    {
        media.ProviderReferences ??= new Dictionary<string, string>();

        var recorded = media.ProviderReferences
            .Where(copy => string.Equals(copy.Key, providerName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (recorded.Any(copy => string.Equals(copy.Value, mediaReference, StringComparison.Ordinal)))
        {
            return RecordOutcome.AlreadyRecorded;
        }

        if (recorded.Any(copy => !string.IsNullOrWhiteSpace(copy.Value) && !string.Equals(copy.Value, replacedReference, StringComparison.Ordinal)))
        {
            return RecordOutcome.AnotherCopyRecorded;
        }

        foreach (var copy in recorded)
        {
            media.ProviderReferences.Remove(copy.Key);
        }

        media.ProviderReferences[providerName] = mediaReference;

        return RecordOutcome.Recorded;
    }

    private static async Task DeleteOrphanCopyAsync(IServiceProvider serviceProvider, string masterStorageReference, string providerName, string mediaReference, ILogger logger, string reason, string ownerId)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "The voice media copy {Reference} the telephony provider {Provider} was given for {OwnerId} is deleted rather than recorded: {Reason}.",
                mediaReference.SanitizeLogValue(),
                providerName.SanitizeLogValue(),
                ownerId.SanitizeLogValue(),
                reason);
        }

        // A caller arriving within the reuse window must not be handed the copy that is being deleted.
        serviceProvider.GetRequiredService<IVoiceMediaUploadCoordinator>().Forget(providerName, masterStorageReference);

        var provisioning = serviceProvider.GetRequiredService<IVoiceMediaProvisioningService>();

        await provisioning.DeleteEverywhereAsync(new VoiceMediaAsset
        {
            ProviderReferences = new Dictionary<string, string> { [providerName] = mediaReference },
        });
    }

    private enum RecordOutcome
    {
        Recorded,
        AlreadyRecorded,
        AnotherCopyRecorded,
    }
}
