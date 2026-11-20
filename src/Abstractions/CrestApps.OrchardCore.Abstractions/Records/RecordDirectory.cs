using System.Security.Claims;

namespace CrestApps.OrchardCore.Records;

/// <summary>
/// The default <see cref="IRecordDirectory"/>: the first source registered for a kind answers for it. Kinds compare
/// without case.
/// </summary>
public sealed class RecordDirectory : IRecordDirectory
{
    private readonly Dictionary<string, IRecordSource> _byKind = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IRecordSource> _sources = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordDirectory"/> class.
    /// </summary>
    /// <param name="sources">The record sources.</param>
    public RecordDirectory(IEnumerable<IRecordSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        foreach (var source in sources)
        {
            if (!string.IsNullOrEmpty(source?.Kind) && _byKind.TryAdd(source.Kind, source))
            {
                _sources.Add(source);
            }
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<IRecordSource> Sources => _sources;

    /// <inheritdoc/>
    public IRecordSource GetSource(string kind)
        => string.IsNullOrEmpty(kind) ? null : _byKind.GetValueOrDefault(kind);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IRecordSource>> GetSearchableSourcesAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var searchable = new List<IRecordSource>();

        if (user is null)
        {
            return searchable;
        }

        foreach (var source in _sources)
        {
            if (await source.CanSearchAsync(user, cancellationToken))
            {
                searchable.Add(source);
            }
        }

        return searchable;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RecordSummary>> SearchAsync(string kind, RecordSearch search, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);

        if (search.User is null || GetSource(kind) is not { } source || !await source.CanSearchAsync(search.User, cancellationToken))
        {
            return [];
        }

        return await source.SearchAsync(search, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<RecordSummary> GetSummaryAsync(RecordRef record, CancellationToken cancellationToken = default)
    {
        if (record.IsEmpty || GetSource(record.Kind) is not { } source)
        {
            return null;
        }

        return (await source.GetSummariesAsync([record.Id], cancellationToken)).GetValueOrDefault(record.Id);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<RecordRef, RecordSummary>> GetSummariesAsync(IEnumerable<RecordRef> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);

        var summaries = new Dictionary<RecordRef, RecordSummary>();

        foreach (var group in records.Where(record => !record.IsEmpty).GroupBy(record => record.Kind, StringComparer.OrdinalIgnoreCase))
        {
            if (GetSource(group.Key) is not { } source)
            {
                continue;
            }

            var found = await source.GetSummariesAsync([.. group.Select(record => record.Id).Distinct(StringComparer.Ordinal)], cancellationToken);

            foreach (var record in group)
            {
                if (found.TryGetValue(record.Id, out var summary))
                {
                    summaries[record] = summary;
                }
            }
        }

        return summaries;
    }
}
