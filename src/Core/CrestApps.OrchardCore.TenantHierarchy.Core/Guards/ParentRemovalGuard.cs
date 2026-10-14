using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Removing;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Guards;

/// <summary>
/// Refuses to remove a parent tenant that still has child tenants, so removing a parent never leaves orphans.
/// </summary>
/// <remarks>
/// Orchard Core runs the tenant-level removing handlers first, and they drop the tenant tables, then the host-level
/// handlers. So the check is registered at both levels: the tenant-level handler stops the removal of a disabled parent
/// before any table is dropped, and the host-level handler stops the removal of an uninitialized parent, for which no
/// tenant-level handler runs.
/// </remarks>
internal static class ParentRemovalGuard
{
    /// <summary>
    /// The message set on the removing context when a parent still has child tenants.
    /// </summary>
    public const string ErrorMessage = "The tenant is a parent tenant that still has child tenants. Remove or move its child tenants first.";

    /// <summary>
    /// Sets an error on the context when the tenant being removed is a parent that still has child tenants.
    /// </summary>
    /// <param name="context">The removing context.</param>
    /// <param name="shellHost">The shell host.</param>
    public static void Check(ShellRemovingContext context, IShellHost shellHost)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(shellHost);

        // When a removal is only synchronized on another node, the tenant is already gone from the shared settings.
        if (context.LocalResourcesOnly || !context.Success)
        {
            return;
        }

        var settings = context.ShellSettings;

        if (settings is null || !settings.IsParentTenant() || string.IsNullOrEmpty(settings.TenantId))
        {
            return;
        }

        var allSettings = shellHost is GuardedShellHost guarded
            ? guarded.Inner.GetAllSettings()
            : shellHost.GetAllSettings();

        if (allSettings.Any(candidate => candidate.IsChildOf(settings)))
        {
            context.ErrorMessage = ErrorMessage;
        }
    }
}
