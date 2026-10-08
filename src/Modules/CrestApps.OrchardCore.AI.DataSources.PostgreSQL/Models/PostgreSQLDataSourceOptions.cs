namespace CrestApps.OrchardCore.AI.DataSources.PostgreSQL.Models;

/// <summary>
/// Provides the global configuration for PostgreSQL AI data sources.
/// </summary>
public sealed class PostgreSQLDataSourceOptions
{
    /// <summary>
    /// Gets or sets the connection string used by PostgreSQL data sources that do not define their own.
    /// </summary>
    public string ConnectionString { get; set; }
}
