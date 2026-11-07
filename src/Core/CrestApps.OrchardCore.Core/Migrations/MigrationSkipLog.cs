using OrchardCore.Documents;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Core.Migrations;

/// <summary>
/// Keeps the migration skip summaries in one small document.
/// </summary>
public sealed class MigrationSkipLog : IMigrationSkipLog
{
    /// <summary>
    /// The most summaries kept; older ones drop off, since each is also in the log.
    /// </summary>
    public const int MaxEntries = 50;

    private readonly IDocumentManager<MigrationSkipLogDocument> _documentManager;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="MigrationSkipLog"/> class.
    /// </summary>
    /// <param name="documentManager">The document manager.</param>
    /// <param name="clock">The clock.</param>
    public MigrationSkipLog(
        IDocumentManager<MigrationSkipLogDocument> documentManager,
        IClock clock)
    {
        _documentManager = documentManager;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task RecordAsync(MigrationSkipReport report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (report.Count == 0)
        {
            return;
        }

        var document = await _documentManager.GetOrCreateMutableAsync();

        document.Entries ??= [];
        document.Entries.Add(new MigrationSkipEntry
        {
            MigrationName = report.MigrationName,
            SkippedCount = report.Count,
            SkippedIds = [.. report.SkippedIds.Take(MigrationSkipReport.MaxListedIds)],
            RecordedUtc = _clock.UtcNow,
        });

        if (document.Entries.Count > MaxEntries)
        {
            document.Entries.RemoveRange(0, document.Entries.Count - MaxEntries);
        }

        await _documentManager.UpdateAsync(document);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MigrationSkipEntry>> ListAsync(CancellationToken cancellationToken = default)
    {
        var document = await _documentManager.GetOrCreateImmutableAsync();

        return [.. document.Entries ?? []];
    }

    /// <inheritdoc/>
    public async Task DismissAllAsync(CancellationToken cancellationToken = default)
    {
        var document = await _documentManager.GetOrCreateMutableAsync();

        if (document.Entries is not { Count: > 0 })
        {
            return;
        }

        document.Entries.Clear();

        await _documentManager.UpdateAsync(document);
    }
}
