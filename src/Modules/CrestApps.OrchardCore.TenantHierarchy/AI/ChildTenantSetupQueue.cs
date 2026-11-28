using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.TenantHierarchy.AI;

/// <summary>
/// Runs the setup of a child tenant created from a chat after the chat's turn. The child tenants page queues the setup
/// with <c>HttpBackgroundJob.ExecuteAfterEndOfRequestAsync</c>, which waits for the request to end; a chat hub call
/// belongs to a connection whose request lasts as long as the connection, so that job would be skipped. Each hub call
/// runs in a shell scope of its own, and work deferred on it runs when the call ends.
/// </summary>
internal static class ChildTenantSetupQueue
{
    /// <summary>
    /// The name of the deferred work, for its telemetry.
    /// </summary>
    public const string WorkName = "tenanthierarchy_child_setup";

    /// <summary>
    /// Gets a value indicating whether work can be queued here. Check it before anything is created: a tenant created
    /// with no setup to follow stays in the Setting up state.
    /// </summary>
    public static bool CanQueue => ShellScope.Current is not null;

    /// <summary>
    /// Queues the setup of a child tenant.
    /// </summary>
    /// <param name="entryId">The registry entry identifier.</param>
    public static void Queue(string entryId)
    {
        ArgumentException.ThrowIfNullOrEmpty(entryId);

        DeferredWork.Add(WorkName, scope => scope.ServiceProvider.GetRequiredService<ChildTenantManager>().SetupAsync(entryId));
    }
}
