using CrestApps.Core.AI.Chat.Privacy;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Documents;

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
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var profileManager = serviceProvider.GetRequiredService<IAIProfileManager>();
        var privacy = serviceProvider.GetRequiredService<AIConversationPrivacyService>();
        var stateManager = serviceProvider.GetRequiredService<IDocumentManager<AIConversationPrivacyState>>();
        var storeCommitter = serviceProvider.GetRequiredService<IStoreCommitter>();

        var state = await stateManager.GetOrCreateMutableAsync();
        var stateChanged = false;

        foreach (var profile in await profileManager.GetAsync(AIProfileType.Chat, cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            DateTime? cursor = state.Cursors.TryGetValue(profile.ItemId, out var saved) ? saved : null;

            var result = await privacy.ProcessAsync(profile, cursor, cancellationToken);

            if (result.Cursor is null)
            {
                // Turning a mode on later starts from then, not from the conversations kept before.
                stateChanged |= state.Cursors.Remove(profile.ItemId);
            }
            else if (result.Cursor != cursor)
            {
                state.Cursors[profile.ItemId] = result.Cursor.Value;
                stateChanged = true;
            }
        }

        if (stateChanged)
        {
            await stateManager.UpdateAsync(state);
        }

        await storeCommitter.CommitAsync(cancellationToken);
    }
}
