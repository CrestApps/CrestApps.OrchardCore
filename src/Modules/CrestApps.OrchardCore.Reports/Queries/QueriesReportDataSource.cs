using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Queries;

namespace CrestApps.OrchardCore.Reports.Queries;

/// <summary>
/// Exposes the saved Orchard Core queries (SQL, Lucene, Elasticsearch, and any other query source) as report data sets.
/// A query is listed and read only for a principal allowed to execute it. Because a query declares no columns, its
/// fields are inferred from its results (see <see cref="QueryResultSchema"/>), and it runs without parameters.
/// </summary>
public sealed class QueriesReportDataSource : IReportDataSource
{
    /// <summary>
    /// The most result items sampled to infer a query's fields.
    /// </summary>
    public const int SampleSize = 200;

    private const string CachePrefix = "Queries:";

    private readonly IQueryManager _queryManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger _logger;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueriesReportDataSource"/> class.
    /// </summary>
    /// <param name="queryManager">The Orchard Core query manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public QueriesReportDataSource(
        IQueryManager queryManager,
        IAuthorizationService authorizationService,
        ILogger<QueriesReportDataSource> logger,
        IStringLocalizer<QueriesReportDataSource> stringLocalizer)
    {
        _queryManager = queryManager;
        _authorizationService = authorizationService;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Name => ReportsConstants.QueriesDataSource;

    /// <inheritdoc/>
    public LocalizedString DisplayName => S["Queries"];

    /// <inheritdoc/>
    public LocalizedString Description => S["The saved queries of the site, such as SQL or search index queries."];

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ReportDataSetDescriptor>> GetDataSetsAsync(ReportDataSourceContext context, CancellationToken cancellationToken = default)
    {
        if (context?.User is null)
        {
            return [];
        }

        var dataSets = new List<ReportDataSetDescriptor>();

        foreach (var query in await _queryManager.ListQueriesAsync(new QueryContext()))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (query is not null && await CanExecuteAsync(context, query))
            {
                dataSets.Add(Describe(query));
            }
        }

        return dataSets
            .OrderBy(dataSet => dataSet.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<ReportDataSetSchema> GetSchemaAsync(string dataSet, ReportDataSourceContext context, CancellationToken cancellationToken = default)
    {
        var query = await FindExecutableAsync(dataSet, context);

        if (query is null)
        {
            return null;
        }

        var rows = await ExecuteAsync(query, context, cancellationToken);

        return new ReportDataSetSchema
        {
            DataSet = Describe(query),
            Fields = QueryResultSchema.InferFields(rows.Take(SampleSize)),
        };
    }

    /// <inheritdoc/>
    public async Task<ReportDataTable> QueryAsync(ReportDataSourceQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var savedQuery = await FindExecutableAsync(query.DataSet, query.Context);

        if (savedQuery is null)
        {
            return new ReportDataTable();
        }

        var rows = await ExecuteAsync(savedQuery, query.Context, cancellationToken);
        var fields = QueryResultSchema.InferFields(rows.Take(SampleSize))
            .Where(field => query.Fields is null || query.Fields.Count == 0 || query.Fields.Contains(field.Name))
            .ToList();
        var maxRows = Math.Max(1, query.MaxRows);
        var table = new ReportDataTable
        {
            Fields = fields,
            Truncated = rows.Count > maxRows,
        };

        foreach (var row in rows.Take(maxRows))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var values = new Dictionary<string, JsonNode>(StringComparer.Ordinal);

            foreach (var (name, value) in row)
            {
                values.TryAdd(name, value);
            }

            var cells = new object[fields.Count];

            for (var index = 0; index < fields.Count; index++)
            {
                cells[index] = values.TryGetValue(fields[index].Name, out var value)
                    ? QueryResultSchema.ToValue(value, fields[index].DataType)
                    : null;
            }

            table.Rows.Add(cells);
        }

        return table;
    }

    private async Task<Query> FindExecutableAsync(string name, ReportDataSourceContext context)
    {
        if (string.IsNullOrEmpty(name) || context?.User is null)
        {
            return null;
        }

        var query = await _queryManager.GetQueryAsync(name);

        if (query is null || !string.Equals(query.Name, name, StringComparison.Ordinal) || !await CanExecuteAsync(context, query))
        {
            return null;
        }

        return query;
    }

    private Task<bool> CanExecuteAsync(ReportDataSourceContext context, Query query)
    {
        return _authorizationService.AuthorizeAsync(context.User, QueryPermissions.CreatePermissionForQuery(query.Name));
    }

    // Runs a query once per report run: the schema and the rows come from the same execution.
    private async Task<List<IReadOnlyList<KeyValuePair<string, JsonNode>>>> ExecuteAsync(Query query, ReportDataSourceContext context, CancellationToken cancellationToken)
    {
        var key = CachePrefix + query.Name;

        if (context.Properties.TryGetValue(key, out var cached) && cached is List<IReadOnlyList<KeyValuePair<string, JsonNode>>> rows)
        {
            return rows;
        }

        cancellationToken.ThrowIfCancellationRequested();

        IQueryResults results;

        try
        {
            results = await _queryManager.ExecuteQueryAsync(query, new Dictionary<string, object>());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "The query '{QueryName}' failed while a report read it.", query.Name);

            throw new ReportQueryException(S["The query '{0}' failed: {1}", query.Name, exception.Message], exception);
        }

        rows = (results?.Items ?? [])
            .Where(item => item is not null)
            .Select(QueryResultSchema.Flatten)
            .ToList();

        context.Properties[key] = rows;

        return rows;
    }

    private ReportDataSetDescriptor Describe(Query query)
    {
        return new ReportDataSetDescriptor(query.Name, query.Name, S["A {0} query.", query.Source].Value)
        {
            Group = query.Source,
        };
    }
}
