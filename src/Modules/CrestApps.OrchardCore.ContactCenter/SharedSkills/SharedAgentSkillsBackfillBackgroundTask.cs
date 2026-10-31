using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.SharedSkills;

/// <summary>
/// Writes the proficiencies of agents saved before Skills was turned on to the users' shared skills, every five
/// minutes, until every agent is done.
/// </summary>
[BackgroundTask(
    Title = "Contact Center shared skills backfill",
    Schedule = "*/5 * * * *",
    Description = "Copies the proficiencies of agents saved before Skills was turned on to the users' skills in the shared catalog.",
    LockTimeout = 1_000,
    LockExpiration = 600_000)]
internal sealed class SharedAgentSkillsBackfillBackgroundTask : IBackgroundTask
{
    private const int BatchSize = 100;

    private volatile bool _done;

    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        if (_done)
        {
            return;
        }

        var pushed = await serviceProvider.GetRequiredService<SharedAgentSkillsBackfill>().RunAsync(BatchSize, cancellationToken);

        _done = pushed == 0;
    }
}
