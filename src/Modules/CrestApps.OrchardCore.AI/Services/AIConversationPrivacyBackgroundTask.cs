using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.Documents;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Applies each AI profile's storage mode to its conversations once they have ended, and deletes conversations past
/// the profile's retention.
/// </summary>
[BackgroundTask(
    Title = "AI Conversation Privacy",
    Schedule = "*/10 * * * *",
    Description = "Redacts or strips ended AI conversations as their profile's storage mode says, and deletes conversations past its retention.",
    LockTimeout = 5_000,
    LockExpiration = 300_000)]
public sealed class AIConversationPrivacyBackgroundTask : IBackgroundTask
{
    internal const int BatchSize = 50;

    // A conversation is left alone this long after its last activity, so the close task has closed it and the
    // workflows on its end and its post-session analysis have read the transcript first.
    private static readonly TimeSpan _settleTime = TimeSpan.FromMinutes(15);

    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var clock = serviceProvider.GetRequiredService<IClock>();
        var profileManager = serviceProvider.GetRequiredService<IAIProfileManager>();
        var privacy = serviceProvider.GetRequiredService<AIConversationPrivacyService>();
        var session = serviceProvider.GetRequiredService<ISession>();
        var collection = serviceProvider.GetRequiredService<IOptions<YesSqlStoreOptions>>().Value.AICollectionName;
        var stateManager = serviceProvider.GetRequiredService<IDocumentManager<AIConversationPrivacyState>>();
        var storeCommitter = serviceProvider.GetRequiredService<IStoreCommitter>();
        var logger = serviceProvider.GetRequiredService<ILogger<AIConversationPrivacyBackgroundTask>>();

        var utcNow = clock.UtcNow;
        var state = await stateManager.GetOrCreateMutableAsync();
        var stateChanged = false;

        foreach (var profile in await profileManager.GetAsync(AIProfileType.Chat, cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var settings = profile.GetSettings<AIProfileDataPrivacySettings>();

            if (settings.StorageMode == AIDataStorageMode.Everything)
            {
                // Turning a mode on later starts from then, not from the conversations kept before.
                stateChanged |= state.Cursors.Remove(profile.ItemId);
            }
            else
            {
                // A mode applies to the conversations active since it was chosen, not to those kept before.
                if (!state.Cursors.TryGetValue(profile.ItemId, out var cursor))
                {
                    cursor = settings.StorageModeSinceUtc ?? utcNow;
                    state.Cursors[profile.ItemId] = cursor;
                    stateChanged = true;
                }

                var advancedTo = await ApplyStorageModeAsync(session, collection, privacy, profile, settings.StorageMode, cursor, utcNow - _settleTime, logger, cancellationToken);

                if (advancedTo > cursor)
                {
                    state.Cursors[profile.ItemId] = advancedTo;
                    stateChanged = true;
                }
            }

            if (settings.RetentionDays > 0)
            {
                await DeleteExpiredAsync(session, collection, privacy, profile, utcNow.AddDays(-settings.RetentionDays.Value), logger, cancellationToken);
            }
        }

        if (stateChanged)
        {
            await stateManager.UpdateAsync(state);
        }

        await storeCommitter.CommitAsync(cancellationToken);
    }

    // Walks the profile's conversations in the order of their last activity from the cursor, and stops at the first
    // one that is still going on or still being analyzed, so none is passed over: that one is tried again next time.
    private static async Task<DateTime> ApplyStorageModeAsync(
        ISession session,
        string collection,
        AIConversationPrivacyService privacy,
        AIProfile profile,
        AIDataStorageMode mode,
        DateTime cursor,
        DateTime settledBeforeUtc,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var profileId = profile.ItemId;
        var conversations = await session
            .Query<AIChatSession, AIChatSessionIndex>(index => index.ProfileId == profileId && index.LastActivityUtc > cursor && index.LastActivityUtc <= settledBeforeUtc, collection)
            .OrderBy(index => index.LastActivityUtc)
            .Take(BatchSize)
            .ListAsync(cancellationToken);

        foreach (var conversation in conversations)
        {
            if (cancellationToken.IsCancellationRequested || !IsFinished(conversation))
            {
                break;
            }

            try
            {
                await privacy.ApplyAsync(conversation, mode, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Most likely the utility model is unavailable. The cursor stays, so the conversation is redacted on
                // a later run rather than kept as it is.
                logger.LogError(ex, "Applying the storage mode of AI profile '{ProfileId}' to session '{SessionId}' failed.", profileId, conversation.SessionId);

                break;
            }

            cursor = conversation.LastActivityUtc;
        }

        return cursor;
    }

    private static async Task DeleteExpiredAsync(
        ISession session,
        string collection,
        AIConversationPrivacyService privacy,
        AIProfile profile,
        DateTime expiredBeforeUtc,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var profileId = profile.ItemId;
        var sessionIds = (await session
            .QueryIndex<AIChatSessionIndex>(index => index.ProfileId == profileId && index.LastActivityUtc < expiredBeforeUtc, collection)
            .OrderBy(index => index.LastActivityUtc)
            .Take(BatchSize)
            .ListAsync(cancellationToken))
            .Select(index => index.SessionId)
            .ToList();

        foreach (var sessionId in sessionIds)
        {
            await privacy.DeleteAsync(sessionId, cancellationToken);
        }

        if (sessionIds.Count > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Deleted {Count} AI sessions of profile '{ProfileId}' past its retention.", sessionIds.Count, profileId);
        }
    }

    internal static bool IsFinished(AIChatSession conversation)
        => conversation.Status != ChatSessionStatus.Active
            && conversation.PostSessionProcessingStatus != PostSessionProcessingStatus.Pending;
}
