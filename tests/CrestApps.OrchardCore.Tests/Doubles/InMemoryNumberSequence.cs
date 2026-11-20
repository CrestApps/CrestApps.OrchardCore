using CrestApps.OrchardCore.Numbering;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// A number sequence held in memory, for tests that create records without a store.
/// </summary>
internal sealed class InMemoryNumberSequence : INumberSequence
{
    private readonly Dictionary<string, long> _last = new(StringComparer.Ordinal);

    public async Task<long> NextAsync(string series, NumberSequenceOptions options = null, CancellationToken cancellationToken = default)
    {
        if (!_last.TryGetValue(series, out var last))
        {
            var seed = options?.SeedAsync is null ? 0 : await options.SeedAsync(cancellationToken);

            last = Math.Max((options?.FirstNumber ?? 1) - 1, seed);
        }

        _last[series] = ++last;

        return last;
    }

    public Task<long?> GetLastAsync(string series, CancellationToken cancellationToken = default)
        => Task.FromResult(_last.TryGetValue(series, out var last) ? last : (long?)null);

    public Task SetNextAsync(string series, long next, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(next, 1);

        _last[series] = next - 1;

        return Task.CompletedTask;
    }
}
