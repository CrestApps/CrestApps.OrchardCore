using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.ContactCenter.SharedSkills;

/// <summary>
/// Writes the proficiencies of agents saved before Skills was turned on to the users' shared skills, a batch at a time.
/// </summary>
internal sealed class SharedAgentSkillsBackfill
{
    private const int PageSize = 200;
    private const int MaxScanned = 100_000;

    private readonly IAgentProfileStore _agentProfileStore;
    private readonly AgentSkillsSyncRunner _runner;

    public SharedAgentSkillsBackfill(
        IAgentProfileStore agentProfileStore,
        AgentSkillsSyncRunner runner)
    {
        _agentProfileStore = agentProfileStore;
        _runner = runner;
    }

    /// <summary>
    /// Pushes up to <paramref name="batchSize"/> agents that were never matched with the catalog.
    /// </summary>
    /// <returns>How many agents were pushed; 0 when every agent is done.</returns>
    public async Task<int> RunAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var pushed = 0;

        for (var page = 1; (page - 1) * PageSize < MaxScanned && pushed < batchSize; page++)
        {
            var result = await _agentProfileStore.PageAsync(page, PageSize, new QueryContext(), cancellationToken);

            foreach (var profile in result.Entries)
            {
                if (pushed >= batchSize)
                {
                    break;
                }

                if (string.IsNullOrEmpty(profile.UserId) || AgentSkillsSync.HasSynced(profile))
                {
                    continue;
                }

                // The runner pushes the profile's skills and saves it marked as synced.
                await _runner.RunAsync(profile, cancellationToken);
                pushed++;
            }

            if (result.Entries.Count < PageSize)
            {
                break;
            }
        }

        return pushed;
    }
}
