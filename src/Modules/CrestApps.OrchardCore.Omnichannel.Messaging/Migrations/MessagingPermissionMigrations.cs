using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Migrations;

/// <summary>
/// Using the workspace used to imply seeing every conversation. Which conversations an agent sees is now its own
/// permission, so every role that could use the workspace is granted the agent's own conversations and their queues'
/// unclaimed ones: its agents keep the inbox they had, and lose only the conversations their colleagues have claimed.
/// </summary>
internal sealed class MessagingPermissionMigrations : DataMigration
{
    private static readonly Dictionary<string, string[]> _grants = new(StringComparer.Ordinal)
    {
        [MessagingPermissions.UseMessagingWorkspace.Name] = [MessagingPermissions.ViewQueueConversations.Name],
    };

    /// <summary>
    /// Schedules the grants.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public static int Create()
    {
        // The roles are documents; they are written once this step's own transaction has committed.
        ShellScope.AddDeferredTask(scope => MessagingRolePermissions.GrantAsync(
            scope.ServiceProvider,
            _grants,
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<MessagingPermissionMigrations>()));

        return 1;
    }
}
