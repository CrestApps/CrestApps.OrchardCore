using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Stores the call's turns and commits the call's document session, one at a time.
/// </summary>
public sealed partial class RealtimeVoiceConversationRunner
{
    // Set once the call's document session has refused a commit. A refused flush leaves what it could not write
    // queued, so every later commit on that session fails the same way: the turns after it go through a scope of
    // their own instead.
    private bool _callSessionBroken;

    /// <summary>
    /// Gets or sets how a turn is stored once the call's document session has refused one: the turn and when it was
    /// said in. Stores it through a scope of its own unless replaced.
    /// </summary>
    internal Func<AIChatSessionPrompt, DateTime, Task> StoreTurnElsewhere { get; set; }

    private bool CanStoreTurnElsewhere
        => StoreTurnElsewhere is not null || (_shellHost is not null && _shellSettings is not null);

    /// <summary>
    /// Stores one turn of the transcript and commits it.
    /// </summary>
    private async Task SaveTurnAsync(RealtimeVoiceConversationContext context, AIChatSessionPrompt prompt, DateTime now, CancellationToken cancellationToken)
    {
        await _callSessionLock.WaitAsync(cancellationToken);

        try
        {
            if (!_callSessionBroken)
            {
                try
                {
                    await _promptStore.CreateAsync(prompt, cancellationToken);

                    context.Session.LastActivityUtc = now;
                    await _chatSessionManager.SaveAsync(context.Session, cancellationToken);

                    // Commit the turn now. A realtime session holds the call for its entire duration inside a single
                    // scope, so without an explicit flush every turn's write sits uncommitted until the call ends and
                    // the scope disposes -- one write transaction held open for minutes. On SQLite that is a
                    // tenant-wide stall: a two-minute call blocked agent presence heartbeats and other webhook
                    // deliveries until they timed out. Flushing per turn keeps each write short, and also means a
                    // transcript survives a call that ends abruptly.
                    await _session.SaveChangesAsync(cancellationToken);

                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException && CanStoreTurnElsewhere)
                {
                    _callSessionBroken = true;

                    _logger.LogWarning(
                        ex,
                        "The call's document session refused to store a turn on activity '{ActivityId}'; this and later turns are stored on their own.",
                        context.Activity?.ItemId);
                }
            }

            if (!CanStoreTurnElsewhere)
            {
                return;
            }

            await (StoreTurnElsewhere ?? StoreTurnInOwnScopeAsync)(prompt, now);
        }
        finally
        {
            _callSessionLock.Release();
        }
    }

    /// <summary>
    /// Stores a turn through a scope of its own, which commits when it ends.
    /// </summary>
    private async Task StoreTurnInOwnScopeAsync(AIChatSessionPrompt prompt, DateTime now)
    {
        var scope = await _shellHost.GetScopeAsync(_shellSettings);

        await scope.UsingAsync(async childScope =>
        {
            var services = childScope.ServiceProvider;

            await services.GetRequiredService<IAIChatSessionPromptStore>().CreateAsync(new AIChatSessionPrompt
            {
                ItemId = prompt.ItemId,
                SessionId = prompt.SessionId,
                Role = prompt.Role,
                Content = prompt.Content,
                CreatedUtc = prompt.CreatedUtc,
            });

            var chatSessions = services.GetRequiredService<IAIChatSessionManager>();
            var chatSession = await chatSessions.FindByIdAsync(prompt.SessionId);

            if (chatSession is not null)
            {
                chatSession.LastActivityUtc = now;
                await chatSessions.SaveAsync(chatSession);
            }
        });
    }

    /// <summary>
    /// Commits what a finished response left on the call's document session: its usage, recorded on that session just
    /// before the response completed.
    /// </summary>
    /// <remarks>
    /// Recording usage flushes: the write lock is taken and held until something commits. The transcript commit comes
    /// before the response completes, so without this the lock was held through the caller's whole next turn and,
    /// after the last response, until the call ended -- live, a 37 second tenant-wide stall at hangup. Bookkeeping, so
    /// a commit the database refuses is reported and the conversation goes on.
    /// </remarks>
    private async Task CommitResponseUsageAsync(string activityId, CancellationToken cancellationToken)
    {
        await _callSessionLock.WaitAsync(cancellationToken);

        try
        {
            if (!_callSessionBroken)
            {
                await _session.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not commit the usage of a realtime response on activity '{ActivityId}'; the call carries on.", activityId);
        }
        finally
        {
            _callSessionLock.Release();
        }
    }
}
