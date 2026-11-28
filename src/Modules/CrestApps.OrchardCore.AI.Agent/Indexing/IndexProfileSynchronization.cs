using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.Indexing;

/// <summary>
/// Synchronizes an index profile after an AI tool created, rebuilt or synchronized it: the provider fills the index.
/// </summary>
/// <remarks>
/// <see cref="IIndexProfileManager.SynchronizeAsync(IndexProfile)"/> runs the work as an HTTP background job, which waits
/// up to 60 seconds for the request's HTTP context to be released and skips the job otherwise. A tool called from the
/// admin chat runs in a SignalR call whose HTTP context lasts as long as the connection, so the job was always skipped
/// and a rebuilt index stayed empty; a tool called from a workflow has no HTTP context and the job didn't run at all.
/// The work is queued to run when the tool call's scope ends instead, after its changes are saved, and it runs the
/// same handlers.
/// </remarks>
internal static class IndexProfileSynchronization
{
    /// <summary>
    /// Queues the synchronization of an index profile to run when the current scope ends.
    /// </summary>
    /// <param name="indexProfile">The index profile to synchronize.</param>
    public static void Queue(IndexProfile indexProfile)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);

        var id = indexProfile.Id;

        DeferredWork.Add("ai_index_profile_sync", async scope =>
        {
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(IndexProfileSynchronization));

            // Read the profile again in this scope, as it was saved, rather than the instance of the ended one.
            var current = await services.GetRequiredService<IIndexProfileManager>().FindByIdAsync(id);

            if (current is null)
            {
                logger.LogWarning("The index profile '{IndexProfileId}' was not synchronized: it no longer exists.", id);

                return;
            }

            try
            {
                var handlers = services.GetServices<IIndexProfileHandler>();

                await handlers.InvokeAsync((handler, context) => handler.SynchronizedAsync(context), new IndexProfileSynchronizedContext(current), logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error synchronizing the index profile '{IndexName}'. The synchronization will be retried on the next background task run.", current.Name);
            }
        });
    }
}
