using CrestApps.OrchardCore.Omnichannel.Core.Models;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Represents the context for bulk manage activity filtering.
/// Handlers use the <see cref="SqlBuilder"/> to add INNER JOIN / LEFT JOIN
/// and WHERE clauses that will be executed as a single server-side SQL query.
/// </summary>
public sealed class BulkManageActivityFilterContext
{
    /// <summary>
    /// Gets the filter criteria.
    /// </summary>
    public BulkManageActivityFilter Filter { get; }

    /// <summary>
    /// Gets the SQL query builder. Handlers add JOINs and WHERE clauses to this builder.
    /// The base query already selects from the activity index table with alias "a"
    /// and includes the base conditions (Status=NotStated, InteractionType=Manual).
    /// </summary>
    public ISqlBuilder SqlBuilder { get; }

    /// <summary>
    /// Gets the SQL dialect for quoting table/column names.
    /// </summary>
    public ISqlDialect Dialect { get; }

    /// <summary>
    /// Gets the table prefix for constructing full table names.
    /// </summary>
    public string TablePrefix { get; }

    /// <summary>
    /// Gets the table naming convention used to resolve collection-aware index table names.
    /// </summary>
    public ITableNameConvention TableNameConvention { get; }

    /// <summary>
    /// Gets the database schema, if any.
    /// </summary>
    public string Schema { get; }

    /// <summary>
    /// Gets the query parameters dictionary. Handlers add parameterized values here.
    /// </summary>
    public Dictionary<string, object> Parameters => SqlBuilder.Parameters;

    /// <summary>
    /// Gets the alias used for the activity index table in the query.
    /// </summary>
    public string ActivityTableAlias { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BulkManageActivityFilterContext"/> class.
    /// </summary>
    /// <param name="filter">The filter criteria.</param>
    /// <param name="sqlBuilder">The SQL builder to compose the query.</param>
    /// <param name="dialect">The SQL dialect.</param>
    /// <param name="tablePrefix">The table prefix.</param>
    /// <param name="tableNameConvention">The table naming convention.</param>
    /// <param name="schema">The database schema.</param>
    /// <param name="activityTableAlias">The alias for the activity index table.</param>
    public BulkManageActivityFilterContext(
        BulkManageActivityFilter filter,
        ISqlBuilder sqlBuilder,
        ISqlDialect dialect,
        string tablePrefix,
        ITableNameConvention tableNameConvention,
        string schema,
        string activityTableAlias)
    {
        Filter = filter;
        SqlBuilder = sqlBuilder;
        Dialect = dialect;
        TablePrefix = tablePrefix;
        TableNameConvention = tableNameConvention;
        Schema = schema;
        ActivityTableAlias = activityTableAlias;
    }
}
