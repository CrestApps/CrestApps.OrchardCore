namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// A <see cref="TimeProvider"/> whose current instant is produced by a delegate, so a test can
/// move time forward between calls without holding a reference to the provider.
/// </summary>
internal sealed class DelegateTimeProvider : TimeProvider
{
    private readonly Func<DateTime> _utcNow;

    public DelegateTimeProvider(Func<DateTime> utcNow)
    {
        _utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow()
        => new(DateTime.SpecifyKind(_utcNow(), DateTimeKind.Utc));
}
