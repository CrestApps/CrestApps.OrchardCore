using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Indexes;
using Dapper;
using YesSql;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// Searches the product index tables with SQL, on every database Orchard supports.
/// </summary>
/// <remarks>
/// <para>
/// Every filter is a sub-select over the attribute rows, so filters on different facets combine with AND and
/// the values of one facet combine with OR. A facet's options are counted with every <em>other</em> filter
/// applied but not its own, which is how large marketplaces keep showing the other makes after one is picked.
/// </para>
/// <para>
/// Values are always passed as parameters; only table and column names, which come from the store
/// configuration, are written into the SQL.
/// </para>
/// </remarks>
public sealed class SqlProductSearchService : IProductSearchService
{
    private readonly ISession _session;

    public SqlProductSearchService(ISession session)
    {
        _session = session;
    }

    public async Task<ProductSearchResult> SearchAsync(ProductSearchQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var configuration = _session.Store.Configuration;
        var dialect = configuration.SqlDialect;
        var sql = new SqlNames(
            dialect,
            Table(configuration, typeof(ProductSearchIndex)),
            Table(configuration, typeof(ProductAttributeIndex)));

        var transaction = await _session.BeginTransactionAsync(cancellationToken);
        var connection = transaction.Connection;

        var parameters = new DynamicParameters();
        var where = BuildWhere(query, sql, parameters, excludedFacetKey: null);

        var total = await connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM {sql.Products} p WHERE {where}",
            parameters,
            transaction);

        var builder = dialect.CreateBuilder(configuration.TablePrefix);
        builder.Select();
        builder.AddSelector($"p.{sql.Column(nameof(ProductSearchIndex.ContentItemId))}");
        builder.From($"{sql.Products} p");
        builder.WhereAnd(where);
        builder.OrderBy(BuildOrderBy(query.Sort, sql, parameters));
        builder.Skip(Math.Max(0, query.Skip).ToString(CultureInfo.InvariantCulture));
        builder.Take(Math.Clamp(query.Take, 1, 200).ToString(CultureInfo.InvariantCulture));

        var ids = (await connection.QueryAsync<string>(builder.ToSqlString(), parameters, transaction)).ToArray();

        var facets = new Dictionary<string, ProductFacetResult>(StringComparer.Ordinal);

        foreach (var facet in query.Facets ?? [])
        {
            if (string.IsNullOrEmpty(facet.Key) || string.IsNullOrEmpty(facet.AttributeKey))
            {
                continue;
            }

            facets[facet.Key] = await CountFacetAsync(query, facet, sql, connection, transaction);
        }

        return new ProductSearchResult
        {
            ContentItemIds = ids,
            Total = total,
            Facets = facets,
        };
    }

    private static async Task<ProductFacetResult> CountFacetAsync(
        ProductSearchQuery query,
        ProductFacetRequest facet,
        SqlNames sql,
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction)
    {
        var parameters = new DynamicParameters();
        var where = BuildWhere(query, sql, parameters, excludedFacetKey: facet.Key);

        parameters.Add("facetKey", facet.AttributeKey);

        var attributeKey = sql.Column(nameof(ProductAttributeIndex.AttributeKey));
        var documentId = sql.Column("DocumentId");

        if (facet.IsRange)
        {
            var number = sql.Column(nameof(ProductAttributeIndex.NumberValue));
            var bounds = await connection.QueryFirstOrDefaultAsync<RangeRow>(
                $"SELECT MIN(a.{number}) AS MinValue, MAX(a.{number}) AS MaxValue FROM {sql.Attributes} a " +
                $"WHERE a.{attributeKey} = @facetKey AND a.{documentId} IN (SELECT p.{documentId} FROM {sql.Products} p WHERE {where})",
                parameters,
                transaction);

            return new ProductFacetResult
            {
                Key = facet.Key,
                Min = ToDecimal(bounds?.MinValue),
                Max = ToDecimal(bounds?.MaxValue),
            };
        }

        var text = sql.Column(nameof(ProductAttributeIndex.TextValue));
        var label = sql.Column(nameof(ProductAttributeIndex.Label));
        var order = sql.Column(nameof(ProductAttributeIndex.SortOrder));
        var level = sql.Column(nameof(ProductAttributeIndex.Level));

        var levelFilter = string.Empty;

        if (facet.Level.HasValue)
        {
            parameters.Add("facetLevel", facet.Level.Value);
            levelFilter = $" AND a.{level} = @facetLevel";
        }

        var rows = await connection.QueryAsync<FacetRow>(
            $"SELECT a.{text} AS Value, MIN(a.{label}) AS Label, MIN(a.{order}) AS SortOrder, COUNT(DISTINCT a.{documentId}) AS Total FROM {sql.Attributes} a " +
            $"WHERE a.{attributeKey} = @facetKey{levelFilter} AND a.{text} IS NOT NULL " +
            $"AND a.{documentId} IN (SELECT p.{documentId} FROM {sql.Products} p WHERE {where}) " +
            $"GROUP BY a.{text}",
            parameters,
            transaction);

        return new ProductFacetResult
        {
            Key = facet.Key,
            Values = rows
                .Select(row => new ProductFacetValue
                {
                    Value = row.Value,
                    Label = string.IsNullOrEmpty(row.Label) ? row.Value : row.Label,
                    Order = (int)Math.Clamp(row.SortOrder, int.MinValue, int.MaxValue),
                    Count = (int)row.Total,
                })
                .ToArray(),
        };
    }

    private static string BuildWhere(ProductSearchQuery query, SqlNames sql, DynamicParameters parameters, string excludedFacetKey)
    {
        var conditions = new List<string>();
        var documentId = sql.Column("DocumentId");

        var types = query.ContentTypes?.Where(type => !string.IsNullOrEmpty(type)).Distinct(StringComparer.Ordinal).ToArray() ?? [];

        if (types.Length > 0)
        {
            parameters.Add("contentTypes", types);
            conditions.Add($"p.{sql.Column(nameof(ProductSearchIndex.ContentType))} IN @contentTypes");
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            // LOWER on both sides, because LIKE is case-insensitive on SQLite and SQL Server but not on PostgreSQL.
            parameters.Add("keyword", $"%{EscapeLike(query.Keyword.Trim().ToLowerInvariant())}%");
            parameters.Add("searchable", true);

            conditions.Add(
                $"(LOWER(p.{sql.Column(nameof(ProductSearchIndex.Title))}) LIKE @keyword ESCAPE '!' " +
                $"OR LOWER(p.{sql.Column(nameof(ProductSearchIndex.Sku))}) LIKE @keyword ESCAPE '!' " +
                $"OR p.{documentId} IN (SELECT k.{documentId} FROM {sql.Attributes} k " +
                $"WHERE k.{sql.Column(nameof(ProductAttributeIndex.Searchable))} = @searchable " +
                $"AND (LOWER(k.{sql.Column(nameof(ProductAttributeIndex.Label))}) LIKE @keyword ESCAPE '!' " +
                $"OR LOWER(k.{sql.Column(nameof(ProductAttributeIndex.TextValue))}) LIKE @keyword ESCAPE '!')))");
        }

        var index = 0;

        foreach (var filter in query.Filters ?? [])
        {
            if (filter is null ||
                string.IsNullOrEmpty(filter.AttributeKey) ||
                (excludedFacetKey is not null && string.Equals(filter.FacetKey, excludedFacetKey, StringComparison.Ordinal)))
            {
                continue;
            }

            var name = $"f{index++}";
            var match = new List<string>();

            if (filter.IsRange)
            {
                if (filter.Min.HasValue)
                {
                    parameters.Add($"{name}min", filter.Min.Value);
                    match.Add($"a.{sql.Column(nameof(ProductAttributeIndex.NumberValue))} >= @{name}min");
                }

                if (filter.Max.HasValue)
                {
                    parameters.Add($"{name}max", filter.Max.Value);
                    match.Add($"a.{sql.Column(nameof(ProductAttributeIndex.NumberValue))} <= @{name}max");
                }
            }
            else
            {
                var values = filter.Values?.Where(value => !string.IsNullOrEmpty(value)).Distinct(StringComparer.Ordinal).ToArray() ?? [];

                if (values.Length == 0)
                {
                    continue;
                }

                parameters.Add($"{name}values", values);
                match.Add($"a.{sql.Column(nameof(ProductAttributeIndex.TextValue))} IN @{name}values");
            }

            parameters.Add($"{name}key", filter.AttributeKey);

            conditions.Add(
                $"p.{documentId} IN (SELECT a.{documentId} FROM {sql.Attributes} a " +
                $"WHERE a.{sql.Column(nameof(ProductAttributeIndex.AttributeKey))} = @{name}key AND {string.Join(" AND ", match)})");
        }

        return conditions.Count == 0 ? "1 = 1" : string.Join(" AND ", conditions);
    }

    private static string BuildOrderBy(ProductListingSort sort, SqlNames sql, DynamicParameters parameters)
    {
        var documentId = $"p.{sql.Column("DocumentId")}";
        var direction = sort?.Descending == true ? " DESC" : string.Empty;

        string column = sort?.AttributeKey switch
        {
            null or "" => null,
            ProductAttributeKeys.Price => $"p.{sql.Column(nameof(ProductSearchIndex.Price))}",
            ProductAttributeKeys.Title => $"p.{sql.Column(nameof(ProductSearchIndex.Title))}",
            ProductAttributeKeys.Published => $"p.{sql.Column(nameof(ProductSearchIndex.PublishedUtc))}",
            _ => null,
        };

        if (sort is null || string.IsNullOrEmpty(sort.AttributeKey))
        {
            return $"p.{sql.Column(nameof(ProductSearchIndex.PublishedUtc))} DESC, {documentId} DESC";
        }

        var order = new StringBuilder();

        if (column is not null)
        {
            // Products without a value (a price that is not shown) sort last in either direction.
            order.Append($"CASE WHEN {column} IS NULL THEN 1 ELSE 0 END, {column}{direction}");
        }
        else
        {
            parameters.Add("sortKey", sort.AttributeKey);

            var number = SortValue(sql, nameof(ProductAttributeIndex.NumberValue));
            var date = SortValue(sql, nameof(ProductAttributeIndex.DateValue));

            order.Append($"CASE WHEN {number} IS NULL AND {date} IS NULL THEN 1 ELSE 0 END, {number}{direction}, {date}{direction}");
        }

        order.Append($", {documentId} DESC");

        return order.ToString();
    }

    private static string SortValue(SqlNames sql, string column)
        => $"(SELECT MIN(s.{sql.Column(column)}) FROM {sql.Attributes} s " +
           $"WHERE s.{sql.Column("DocumentId")} = p.{sql.Column("DocumentId")} " +
           $"AND s.{sql.Column(nameof(ProductAttributeIndex.AttributeKey))} = @sortKey)";

    private static string Table(IConfiguration configuration, Type indexType)
        => configuration.SqlDialect.QuoteForTableName(
            configuration.TablePrefix + configuration.TableNameConvention.GetIndexTable(indexType),
            configuration.Schema);

    // '!' is the escape character because a backslash is itself an escape inside MySQL string literals.
    private static string EscapeLike(string value)
        => value.Replace("!", "!!", StringComparison.Ordinal)
            .Replace("%", "!%", StringComparison.Ordinal)
            .Replace("_", "!_", StringComparison.Ordinal)
            .Replace("[", "![", StringComparison.Ordinal);

    // Databases disagree on the CLR type an aggregate comes back as (SQLite returns a double or a long for
    // MIN over a decimal column), so the bounds are read loosely and converted here.
    private static decimal? ToDecimal(object value)
        => value is null or DBNull ? null : Convert.ToDecimal(value, CultureInfo.InvariantCulture);

    private sealed class RangeRow
    {
        public object MinValue { get; set; }

        public object MaxValue { get; set; }
    }

    private sealed class FacetRow
    {
        public string Value { get; set; }

        public string Label { get; set; }

        public long SortOrder { get; set; }

        public long Total { get; set; }
    }

    private sealed class SqlNames
    {
        private readonly ISqlDialect _dialect;

        public SqlNames(ISqlDialect dialect, string products, string attributes)
        {
            _dialect = dialect;
            Products = products;
            Attributes = attributes;
        }

        public string Products { get; }

        public string Attributes { get; }

        public string Column(string name)
            => _dialect.QuoteForColumnName(name);
    }
}
