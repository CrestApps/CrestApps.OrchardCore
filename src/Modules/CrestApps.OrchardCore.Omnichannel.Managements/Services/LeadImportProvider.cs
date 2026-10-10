using System.Globalization;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using Dapper;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Reads the file imports leads arrived in, so the leads of one file, such as <c>July2020.csv</c>, can be loaded
/// together. An import is listed while at least one lead still records it, even after its entry is removed from the
/// import history.
/// </summary>
public sealed class LeadImportProvider
{
    private readonly ISession _session;
    private readonly ILocalClock _localClock;

    private IReadOnlyList<LeadImportSummary> _imports;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadImportProvider"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="localClock">The local clock, used to show when each file was imported.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LeadImportProvider(
        ISession session,
        ILocalClock localClock,
        IStringLocalizer<LeadImportProvider> stringLocalizer)
    {
        _session = session;
        _localClock = localClock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Returns the imports at least one lead records, newest first, with the number of leads each holds. The imports
    /// are read once per request.
    /// </summary>
    public async Task<IReadOnlyList<LeadImportSummary>> GetImportsAsync()
    {
        if (_imports is null)
        {
            var configuration = _session.Store.Configuration;
            var dialect = configuration.SqlDialect;
            var table = dialect.QuoteForTableName(
                $"{configuration.TablePrefix}{configuration.TableNameConvention.GetIndexTable(typeof(LeadImportIndex), null)}",
                configuration.Schema);
            var entryId = dialect.QuoteForColumnName(nameof(LeadImportIndex.EntryId));
            var fileName = dialect.QuoteForColumnName(nameof(LeadImportIndex.FileName));
            var importedUtc = dialect.QuoteForColumnName(nameof(LeadImportIndex.ImportedUtc));
            var contentItemId = dialect.QuoteForColumnName(nameof(LeadImportIndex.ContentItemId));
            var latest = dialect.QuoteForColumnName(nameof(LeadImportIndex.Latest));

            // Every row of an import carries the same file name and time, so grouping by them keeps one row per
            // import while the columns keep their own types.
            var sql =
                $"SELECT {entryId} AS EntryId, {fileName} AS FileName, {importedUtc} AS ImportedUtc, COUNT(DISTINCT {contentItemId}) AS LeadCount " +
                $"FROM {table} WHERE {latest} = @Latest AND {entryId} IS NOT NULL " +
                $"GROUP BY {entryId}, {fileName}, {importedUtc}";

            // The session's own transaction is used, so the read never opens a second connection beside it.
            var transaction = await _session.BeginTransactionAsync();
            var rows = await transaction.Connection.QueryAsync<LeadImportRow>(sql, new { Latest = true }, transaction);

            _imports = rows
                .GroupBy(row => row.EntryId, StringComparer.Ordinal)
                .Select(group => new LeadImportSummary(
                    group.Key,
                    group.Select(row => row.FileName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)),
                    DateTime.SpecifyKind(group.Min(row => row.ImportedUtc), DateTimeKind.Utc),
                    group.Sum(row => row.LeadCount)))
                .OrderByDescending(import => import.ImportedUtc)
                .ThenBy(import => import.FileName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
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
            var text = import.LeadCount == 1
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
public sealed record LeadImportSummary(string EntryId, string FileName, DateTime ImportedUtc, long LeadCount);
