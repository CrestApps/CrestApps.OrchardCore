using CrestApps.OrchardCore.Banking.Checks.Core.Services;

namespace CrestApps.OrchardCore.Tests.Checks;

/// <summary>
/// Nothing to commit, the stores are in memory: counts what the check service asked to commit.
/// </summary>
internal sealed class CountingCheckCommitter : ICheckCommitter
{
    public int Commits { get; private set; }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        Commits++;

        return Task.CompletedTask;
    }
}
