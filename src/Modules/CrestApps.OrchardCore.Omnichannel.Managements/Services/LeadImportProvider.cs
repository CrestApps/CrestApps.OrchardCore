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
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Documents;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;
using YesSql.Sql;

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
/// The imports are therefore kept per tenant by <see cref="LeadImportListCache"/> and read again only when the import
/// history changes, or once their lifetime is over, so leads deleted since are reflected.
/// </remarks>
public sealed class LeadImportProvider
{
    private readonly ISession _session;
    private readonly ILocalClock _localClock;
    private readonly IDocumentManager<OmnichannelBackfillState> _backfillState;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly LeadImportListCache _importsCache;

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
    /// <param name="importsCache">Keeps the imports between requests, so they are not counted on every render.</param>
    public LeadImportProvider(
        ISession session,
        ILocalClock localClock,
        IStringLocalizer<LeadImportProvider> stringLocalizer,
        IDocumentManager<OmnichannelBackfillState> backfillState = null,
        IContentDefinitionManager contentDefinitionManager = null,
        LeadImportListCache importsCache = null)
    {
        _session = session;
        _localClock = localClock;
        _backfillState = backfillState;
        _contentDefinitionManager = contentDefinitionManager;
        _importsCache = importsCache;
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

            if (_importsCache is null)
            {
                _imports = await ReadImportsAsync();
            }
            else if (!_importsCache.TryGet(version, out _imports))
            {
                await _importsCache.ReadGate.WaitAsync();

                try
                {
                    // A request that waited here finds the imports the one ahead of it read.
                    if (!_importsCache.TryGet(version, out _imports))
                    {
                        _imports = await ReadImportsAsync();

                        _importsCache.Set(version, _imports);
                    }
                }
                finally
                {
                    _importsCache.ReadGate.Release();
                }
            }
        }

        return _imports;
    }

    /// <summary>
    /// Returns only the selected import as an option, named by its file and when it was imported, without counting the
    /// leads of any import. The editor renders this, so opening it never reads the lead import index; the full list
    /// is read by <see cref="GetOptionsAsync"/> once the operator asks for it.
    /// </summary>
    /// <param name="selectedEntryId">The identifier of the selected import, if any.</param>
    public async Task<IList<SelectListItem>> GetSelectedOptionsAsync(string selectedEntryId)
    {
        if (string.IsNullOrWhiteSpace(selectedEntryId))
        {
            return [];
        }

        var selected = selectedEntryId.Trim();

        // Without the content import feature there is no import history to read the file's name from.
        var entry = _backfillState is null
            ? null
            : await _session.Query<ContentTransferEntry, ContentTransferEntryIndex>(index => index.EntryId == selected).FirstOrDefaultAsync();

        if (entry is null)
        {
            return [new SelectListItem(S["The selected import"].Value, selected, true)];
        }

        var importedLocal = await _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(entry.CreatedUtc, DateTimeKind.Utc)));
        var fileName = string.IsNullOrWhiteSpace(entry.UploadedFileName) ? S["Unnamed file"].Value : entry.UploadedFileName.Trim();

        return [new SelectListItem(S["{0} (imported {1})", fileName, importedLocal.ToString("g", CultureInfo.CurrentCulture)].Value, selected, true)];
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
        var entryId = dialect.QuoteForColumnName(nameof(LeadImportIndex.EntryId));
        var latest = dialect.QuoteForColumnName(nameof(LeadImportIndex.Latest));

        // Only the lead count is grouped. EntryId and Latest are both columns of IDX_LeadImportIndex_EntryId, so the
        // counts are read from that index alone. The count used to group by the file name and the time as well, which
        // are not in the index, so the database read the table row of every lead to count them; on a large tenant that
        // random read outlasted the command timeout of a busy database on every render of the inventory load editor.
        // A lead has one latest version and records an import once, so each row is a distinct lead.
        var builder = new SqlBuilder(configuration.TablePrefix, dialect);
        builder.Select();
        builder.Selector($"{entryId} AS EntryId, COUNT(*) AS LeadCount");
        builder.Table(configuration.TableNameConvention.GetIndexTable(typeof(LeadImportIndex), null), alias: null, configuration.Schema);
        builder.WhereAnd($"{latest} = @Latest");
        builder.WhereAnd($"{entryId} IS NOT NULL");
        builder.GroupBy($"{entryId}");

        // The session's own transaction is used, so the read never opens a second connection beside it.
        var transaction = await _session.BeginTransactionAsync();
        var rows = (await transaction.Connection.QueryAsync<LeadImportRow>(builder.ToSqlString(), new { Latest = true }, transaction)).ToList();

        var imports = new List<LeadImportSummary>(rows.Count);

        // Every row of an import carries the same file name and time, so one row per import names it. Reading it by
        // the entry id seeks to the import in the index and reads a single table row.
        foreach (var row in rows)
        {
            var named = await _session
                .QueryIndex<LeadImportIndex>(index => index.EntryId == row.EntryId && index.Latest)
                .FirstOrDefaultAsync();

            imports.Add(new LeadImportSummary(
                row.EntryId,
                string.IsNullOrWhiteSpace(named?.FileName) ? null : named.FileName,
                DateTime.SpecifyKind(named?.ImportedUtc ?? DateTime.UtcNow, DateTimeKind.Utc),
                row.LeadCount));
        }

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
        if (_importsCache is null || _backfillState is null)
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

    private sealed class LeadImportRow
    {
        public string EntryId { get; set; }

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
