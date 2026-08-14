using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Core.Services;

public sealed class ClockTimeProviderAdapter : TimeProvider
{
    private readonly IClock _clock;

    public ClockTimeProviderAdapter(IClock clock)
    {
        _clock = clock;
    }

    public override DateTimeOffset GetUtcNow()
        => new(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc));
}
