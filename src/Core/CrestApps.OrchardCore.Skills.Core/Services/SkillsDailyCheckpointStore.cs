using YesSql;

namespace CrestApps.OrchardCore.Skills.Core.Services;

/// <summary>
/// Reads and writes the <see cref="SkillsDailyCheckpoint"/>.
/// </summary>
internal sealed class SkillsDailyCheckpointStore
{
    private readonly ISession _session;

    public SkillsDailyCheckpointStore(ISession session)
    {
        _session = session;
    }

    public async Task<DateOnly?> GetLastRunAsync(CancellationToken cancellationToken = default)
    {
        var checkpoint = await _session.Query<SkillsDailyCheckpoint>(collection: SkillsConstants.CollectionName).FirstOrDefaultAsync(cancellationToken);

        return checkpoint?.LastRun;
    }

    public async Task SetLastRunAsync(DateOnly day, CancellationToken cancellationToken = default)
    {
        var checkpoint = await _session.Query<SkillsDailyCheckpoint>(collection: SkillsConstants.CollectionName).FirstOrDefaultAsync(cancellationToken)
            ?? new SkillsDailyCheckpoint();

        checkpoint.LastRun = day;

        await _session.SaveAsync(checkpoint, false, SkillsConstants.CollectionName, cancellationToken);
    }
}
