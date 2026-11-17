using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// Reads the pay inputs of every source, and tells each source what was taken of its own records.
/// </summary>
public sealed class DefaultPayInputService : IPayInputService
{
    private readonly IPayInputSource[] _sources;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultPayInputService"/> class.
    /// </summary>
    /// <param name="sources">The sources.</param>
    public DefaultPayInputService(IEnumerable<IPayInputSource> sources)
    {
        _sources = sources.ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PayInput>> GetInputsAsync(PayInputQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var inputs = new List<PayInput>();

        foreach (var source in _sources)
        {
            foreach (var input in await source.GetInputsAsync(query, cancellationToken))
            {
                input.SourceKind ??= source.SourceKind;
                inputs.Add(input);
            }
        }

        return inputs;
    }

    /// <inheritdoc />
    public Task MarkConsumedAsync(PayInputConsumption consumption, CancellationToken cancellationToken = default)
        => ForEachSourceAsync(consumption, (source, ids) => source.MarkConsumedAsync(consumption.ConsumerId, ids, cancellationToken));

    /// <inheritdoc />
    public Task ReleaseAsync(PayInputConsumption consumption, CancellationToken cancellationToken = default)
        => ForEachSourceAsync(consumption, (source, ids) => source.ReleaseAsync(consumption.ConsumerId, ids, cancellationToken));

    private async Task ForEachSourceAsync(PayInputConsumption consumption, Func<IPayInputSource, IReadOnlyCollection<string>, Task> action)
    {
        ArgumentNullException.ThrowIfNull(consumption);
        ArgumentException.ThrowIfNullOrEmpty(consumption.ConsumerId);

        foreach (var group in (consumption.Sources ?? []).Where(source => !string.IsNullOrEmpty(source?.SourceId)).GroupBy(source => source.SourceKind, StringComparer.Ordinal))
        {
            // A record of a source no longer turned on is left as it is.
            if (_sources.FirstOrDefault(source => string.Equals(source.SourceKind, group.Key, StringComparison.Ordinal)) is not { } source)
            {
                continue;
            }

            await action(source, group.Select(item => item.SourceId).Distinct(StringComparer.Ordinal).ToArray());
        }
    }
}
