using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using Dapper;
using Microsoft.AspNetCore.Mvc.Rendering;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Reads the lists leads arrived in. A list is not a record of its own: it is the free-text list name each lead
/// carries, set by an import or typed on the lead, so the lists are the distinct names found on the lead index.
/// </summary>
public sealed class LeadListProvider
{
    private readonly ISession _session;

    private IReadOnlyList<string> _names;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadListProvider"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public LeadListProvider(ISession session)
    {
        _session = session;
    }

    /// <summary>
    /// Returns the names of the lists at least one lead carries, ordered by name. The names are read once per request.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetNamesAsync()
    {
        if (_names is null)
        {
            var configuration = _session.Store.Configuration;
            var dialect = configuration.SqlDialect;
            var table = dialect.QuoteForTableName(
                $"{configuration.TablePrefix}{configuration.TableNameConvention.GetIndexTable(typeof(LeadIndex), null)}",
                configuration.Schema);
            var column = dialect.QuoteForColumnName(nameof(LeadIndex.ListName));

            // The session's own transaction is used, so the read never opens a second connection beside it.
            var transaction = await _session.BeginTransactionAsync();
            var names = await transaction.Connection.QueryAsync<string>(
                $"SELECT DISTINCT {column} FROM {table} WHERE {column} IS NOT NULL",
                transaction: transaction);

            _names = names
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return _names;
    }

    /// <summary>
    /// Returns the lists as options for a select list. A selected list that no lead carries any longer is kept as an
    /// option, so a saved filter still shows what it loads instead of quietly reading as no list.
    /// </summary>
    /// <param name="selectedName">The name of the selected list, if any.</param>
    public async Task<IList<SelectListItem>> GetOptionsAsync(string selectedName)
    {
        var names = (await GetNamesAsync()).ToList();
        var selected = string.IsNullOrWhiteSpace(selectedName) ? null : selectedName.Trim();

        if (selected is not null && !names.Contains(selected, StringComparer.Ordinal))
        {
            names.Add(selected);
            names.Sort(StringComparer.OrdinalIgnoreCase);
        }

        return names
            .Select(name => new SelectListItem(name, name, string.Equals(name, selected, StringComparison.Ordinal)))
            .ToList();
    }
}
