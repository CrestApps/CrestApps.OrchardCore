using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.DncRegistry.BackgroundTasks;
using CrestApps.OrchardCore.DncRegistry.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.DncRegistry.AI;

/// <summary>
/// Starts the import or the deletion of a local do-not-call list once the AI turn that asked for it is committed. The
/// admin page queues the same work with <c>HttpBackgroundJob</c>, which waits for the request to end and, in a chat hub
/// call, is skipped; here a deferred task hands the work to a child scope of its own and returns without waiting, so a
/// long import does not hold the user's next message. When nothing could be queued, the Local DNC Import Processor
/// background task (every 10 minutes) picks up pending lists and resumes stalled ones and stalled deletions.
/// </summary>
internal static class LocalDncListJobs
{
    /// <summary>
    /// Queues the import of a list after the turn.
    /// </summary>
    /// <returns><see langword="true"/> when it was queued; otherwise the background task picks it up.</returns>
    public static bool QueueImport(string listId)
        => Queue("dnc_local_import", listId, static (services, id) => LocalDncImportBackgroundTask.ProcessEntriesAsync(services, id, CancellationToken.None));

    /// <summary>
    /// Queues the deletion of a list and its numbers after the turn.
    /// </summary>
    /// <returns><see langword="true"/> when it was queued; otherwise the background task picks it up.</returns>
    public static bool QueueDelete(string listId)
        => Queue("dnc_local_delete", listId, static (services, id) => services.GetRequiredService<ILocalDncListManager>().DeleteAsync(id));

    private static bool Queue(string jobName, string listId, Func<IServiceProvider, string, Task> job)
    {
        if (ShellScope.Current is null)
        {
            return false;
        }

        var traceParent = TraceContextPropagation.Capture();

        ShellScope.AddDeferredTask(deferredScope =>
        {
            _ = ShellScope.UsingChildScopeAsync(async jobScope =>
            {
                try
                {
                    await DeferredWork.RunAsync(jobName, traceParent, jobScope, scope => job(scope.ServiceProvider, listId));
                }
                catch (Exception ex)
                {
                    // The import and the deletion record their own failures on the list; this keeps a failure out of an
                    // unobserved task.
                    jobScope.ServiceProvider.GetService<ILogger<LocalDncImportBackgroundTask>>()?.LogError(ex, "The local DNC list job '{JobName}' for list '{ListId}' started from an AI tool failed.", jobName, listId);
                }
            });

            return Task.CompletedTask;
        });

        return true;
    }
}
