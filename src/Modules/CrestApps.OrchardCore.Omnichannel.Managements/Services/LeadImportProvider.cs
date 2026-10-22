using System.Globalization;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.BackgroundTasks;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using Dapper;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Documents;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Reads the file imports leads arrived in, so the leads of one file, such as <c>July2020.csv</c>, can be loaded
/// together. An import is listed while at least one lead still records it, even after its entry is removed from the
/// import history. While the leads imported before leads recorded their imports are still being read, the imports no
/// lead read so far records are listed too, marked as still being indexed.
/// </summary>
/// <remarks>
/// Counting the leads of every import reads the whole lead import index, which on a large tenant saturated the
/// database's reads for a minute on every render of the inventory load editor and timed out the queries around it.
/// The imports are therefore kept per tenant and read again only when the import history changes, or after ten
/// minutes, so leads deleted since are reflected.
/// </remarks>
public sealed class LeadImportProvider
{
    private const string CacheKey = "CrestApps.Omnichannel.LeadImports";

    private static readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(10);

    private readonly ISession _session;
    private readonly ILocalClock _localClock;
    private readonly IDocumentManager<OmnichannelBackfillState> _backfillState;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IMemoryCache _memoryCache;

    private IReadOnlyList<LeadImportSummary> _imports;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadImportProvider"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="localClock">The local clock, used to show when each file was imported.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="backfillState">Tells whether the past imports have been recorded on their leads.</param>
    /// <param name="contentDefinitionManager">Names the lead content types.</param>
    /// <param name="memoryCache">Keeps the imports between requests, so they are not counted on every render.</param>
    public LeadImportProvider(
        ISession session,
        ILocalClock localClock,
        IStringLocalizer<LeadImportProvider> stringLocalizer,
        IDocumentManager<OmnichannelBackfillState> backfillState = null,
        IContentDefinitionManager contentDefinitionManager = null,
        IMemoryCache memoryCache = null)
    {
        _session = session;
        _localClock = localClock;
        _backfillState = backfillState;
        _contentDefinitionManager = contentDefinitionManager;
        _memoryCache = memoryCache;
        S = stringLocalizer;
    }

    /// <summary>
    /// Returns the imports at least one lead records, newest first, with the number of leads each holds. The imports
    /// are read once per request, and are kept between requests until the import history changes.
    /// </summary>
    public async Task<IReadOnlyList<LeadImportSummary>> GetImportsAsync()
    {
        if (_imports is null)
        {
            var version = await GetHistoryVersionAsync();

            if (_memoryCache is not null &&
                _memoryCache.TryGetValue(CacheKey, out CachedImports cached) &&
                cached.Version == version)
            {
                _imports = cached.Imports;
            }
            else
            {
                _imports = await ReadImportsAsync();

                _memoryCache?.Set(CacheKey, new CachedImports(version, _imports), _cacheLifetime);
            }
        }

        return _imports;
    }

    /// <summary>
    /// Returns the imports as options for a select list, each named by its file, when it was imported and how many
    /// leads it holds. A selected import that no lead records any longer is kept as an option, so a saved filter still
    /// shows that it loads nothing instead of quietly reading as any import.
    /// </summary>
    /// <param name="selectedEntryId">The identifier of the selected import, if any.</param>
    public async Task<IList<SelectListItem>> GetOptionsAsync(string selectedEntryId)
    {
        var selected = string.IsNullOrWhiteSpace(selectedEntryId) ? null : selectedEntryId.Trim();
        var options = new List<SelectListItem>();

        foreach (var import in await GetImportsAsync())
        {
            var importedLocal = await _localClock.ConvertToLocalAsync(new DateTimeOffset(import.ImportedUtc));
            var fileName = import.FileName ?? S["Unnamed file"].Value;
            var imported = importedLocal.ToString("g", CultureInfo.CurrentCulture);
            var text = import.IsIndexing
                ? S["{0} (imported {1}, leads still being indexed)", fileName, imported].Value
                : import.LeadCount == 1
                ? S["{0} (imported {1}, 1 lead)", fileName, imported].Value
                : S["{0} (imported {1}, {2} leads)", fileName, imported, import.LeadCount.ToString("N0", CultureInfo.CurrentCulture)].Value;

            options.Add(new SelectListItem(text, import.EntryId, string.Equals(import.EntryId, selected, StringComparison.Ordinal)));
        }

        if (selected is not null && !options.Any(option => option.Selected))
        {
            options.Insert(0, new SelectListItem(S["An import with no leads left"].Value, selected, true));
        }

        return options;
    }

    private async Task<IReadOnlyList<LeadImportSummary>> ReadImportsAsync()
    {
        var configuration = _session.Store.Configuration;
        var dialect = configuration.SqlDialect;
        var table = dialect.QuoteForTableName(
            $"{configuration.TablePrefix}{configuration.TableNameConvention.GetIndexTable(typeof(LeadImportIndex), null)}",
            configuration.Schema);
        var entryId = dialect.QuoteForColumnName(nameof(LeadImportIndex.EntryId));
        var fileName = dialect.QuoteForColumnName(nameof(LeadImportIndex.FileName));
        var importedUtc = dialect.QuoteForColumnName(nameof(LeadImportIndex.ImportedUtc));
        var latest = dialect.QuoteForColumnName(nameof(LeadImportIndex.Latest));

        // Every row of an import carries the same file name and time, so grouping by them keeps one row per
        // import while the columns keep their own types. A lead has one latest version and records an import once,
        // so each row is a distinct lead and the rows are counted without the sort a distinct count needs.
        var sql =
            $"SELECT {entryId} AS EntryId, {fileName} AS FileName, {importedUtc} AS ImportedUtc, COUNT(*) AS LeadCount " +
            $"FROM {table} WHERE {latest} = @Latest AND {entryId} IS NOT NULL " +
            $"GROUP BY {entryId}, {fileName}, {importedUtc}";

        // The session's own transaction is used, so the read never opens a second connection beside it.
        var transaction = await _session.BeginTransactionAsync();
        var rows = await transaction.Connection.QueryAsync<LeadImportRow>(sql, new { Latest = true }, transaction);

        var imports = rows
            .GroupBy(row => row.EntryId, StringComparer.Ordinal)
            .Select(group => new LeadImportSummary(
                group.Key,
                group.Select(row => row.FileName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)),
                DateTime.SpecifyKind(group.Min(row => row.ImportedUtc), DateTimeKind.Utc),
                group.Sum(row => row.LeadCount)))
            .ToList();

        imports.AddRange(await GetImportsStillBeingIndexedAsync(imports.Select(import => import.EntryId)));

        return imports
            .OrderByDescending(import => import.ImportedUtc)
            .ThenBy(import => import.FileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    // Tells kept imports apart from current ones by the import history alone, which holds one row per import: an
    // import that starts, finishes or is removed changes it, while the leads an import is still writing do not.
    // Without the import feature there is no history to read, so kept imports last their lifetime.
    private async Task<string> GetHistoryVersionAsync()
    {
        if (_memoryCache is null || _backfillState is null)
        {
            return string.Empty;
        }

        var state = await _backfillState.GetOrCreateImmutableAsync();
        var entries = await _session.QueryIndex<ContentTransferEntryIndex>(index =>
                index.Direction == ContentTransferDirection.Import)
            .ListAsync();

        var hash = new HashCode();
        var count = 0;

        foreach (var entry in entries.OrderBy(entry => entry.EntryId, StringComparer.Ordinal))
        {
            hash.Add(entry.EntryId, StringComparer.Ordinal);
            hash.Add(entry.Status);
            count++;
        }

        var backfilled = state.Completed.Contains(LeadImportBackfillBackgroundTask.BackfillName);

        return string.Create(CultureInfo.InvariantCulture, $"{backfilled}:{count}:{hash.ToHashCode()}");
    }

    // After an upgrade the leads imported before leads recorded their imports are read a few thousand a minute. Until
    // every lead is read, an import no lead read so far records is listed anyway, so the files imported before the
    // upgrade can be found from the start; its lead count is not known until its leads are read.
    private async Task<IEnumerable<LeadImportSummary>> GetImportsStillBeingIndexedAsync(IEnumerable<string> listedEntryIds)
    {
        if (_backfillState is null || _contentDefinitionManager is null)
        {
            return [];
        }

        var state = await _backfillState.GetOrCreateImmutableAsync();

        if (state.Completed.Contains(LeadImportBackfillBackgroundTask.BackfillName))
        {
            return [];
        }

        var leadTypes = (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(definition => OmnichannelRecordKinds.HasPart(definition, OmnichannelConstants.ContentParts.Lead))
            .Select(definition => definition.Name)
            .ToArray();

        if (leadTypes.Length == 0)
        {
            return [];
        }

        var listed = listedEntryIds.ToHashSet(StringComparer.Ordinal);

        // Only index rows are read to find the missing imports. An import's record holds every row error it recorded,
        // so only the records of the imports not listed yet are loaded.
        var missingEntryIds = (await _session.QueryIndex<ContentTransferEntryIndex>(index =>
                index.Direction == ContentTransferDirection.Import && index.ContentType.IsIn(leadTypes))
            .ListAsync())
            .Select(index => index.EntryId)
            .Where(entryId => !string.IsNullOrEmpty(entryId) && !listed.Contains(entryId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (missingEntryIds.Length == 0)
        {
            return [];
        }

        var entries = await _session.Query<ContentTransferEntry, ContentTransferEntryIndex>(index => index.EntryId.IsIn(missingEntryIds))
            .ListAsync();

        // An import that never saved a batch wrote no leads.
        return entries
            .Where(entry => entry.ProcessSaveUtc is not null || entry.CompletedUtc is not null)
            .Select(entry => new LeadImportSummary(
                entry.EntryId,
                string.IsNullOrWhiteSpace(entry.UploadedFileName) ? null : entry.UploadedFileName.Trim(),
                DateTime.SpecifyKind(entry.CreatedUtc, DateTimeKind.Utc),
                0)
            {
                IsIndexing = true,
            })
            .ToArray();
    }

    private sealed record CachedImports(string Version, IReadOnlyList<LeadImportSummary> Imports);

    private sealed class LeadImportRow
    {
        public string EntryId { get; set; }

        public string FileName { get; set; }

        public DateTime ImportedUtc { get; set; }

        public long LeadCount { get; set; }
    }
}

/// <summary>
/// A file import and the number of leads that record it.
/// </summary>
/// <param name="EntryId">The identifier of the import entry.</param>
/// <param name="FileName">The name of the imported file.</param>
/// <param name="ImportedUtc">When the file was uploaded, in UTC.</param>
/// <param name="LeadCount">The number of leads that record the import.</param>
public sealed record LeadImportSummary(string EntryId, string FileName, DateTime ImportedUtc, long LeadCount)
{
    /// <summary>
    /// Gets whether the import was made before leads recorded their imports and none of its leads has been read yet,
    /// so <see cref="LeadCount"/> is not known.
    /// </summary>
    public bool IsIndexing { get; init; }
}
