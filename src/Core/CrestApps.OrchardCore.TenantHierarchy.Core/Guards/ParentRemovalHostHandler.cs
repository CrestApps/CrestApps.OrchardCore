using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Removing;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Guards;

/// <summary>
/// The host-level part of <see cref="ParentRemovalGuard"/>. It is registered after Orchard Core's handlers, so it runs
/// before them on removal.
/// </summary>
internal sealed class ParentRemovalHostHandler : IShellRemovingHandler
{
    private readonly IShellHost _shellHost;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParentRemovalHostHandler"/> class.
    /// </summary>
    /// <param name="shellHost">The shell host.</param>
    public ParentRemovalHostHandler(IShellHost shellHost)
    {
        _shellHost = shellHost;
    }

    /// <inheritdoc/>
    public Task RemovingAsync(ShellRemovingContext context)
    {
        ParentRemovalGuard.Check(context, _shellHost);

        return Task.CompletedTask;
    }
}
