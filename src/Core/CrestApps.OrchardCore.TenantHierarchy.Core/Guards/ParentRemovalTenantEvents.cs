using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Removing;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Guards;

/// <summary>
/// The tenant-level part of <see cref="ParentRemovalGuard"/>. It is registered last, so it runs first on removal.
/// </summary>
internal sealed class ParentRemovalTenantEvents : ModularTenantEvents
{
    private readonly IShellHost _shellHost;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParentRemovalTenantEvents"/> class.
    /// </summary>
    /// <param name="shellHost">The shell host.</param>
    public ParentRemovalTenantEvents(IShellHost shellHost)
    {
        _shellHost = shellHost;
    }

    /// <inheritdoc/>
    public override Task RemovingAsync(ShellRemovingContext context)
    {
        ParentRemovalGuard.Check(context, _shellHost);

        return Task.CompletedTask;
    }
}
