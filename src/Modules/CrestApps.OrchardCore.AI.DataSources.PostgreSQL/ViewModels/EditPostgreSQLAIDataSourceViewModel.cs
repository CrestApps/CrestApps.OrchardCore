using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.DataSources.PostgreSQL.ViewModels;

/// <summary>
/// View model for editing PostgreSQL AI data source settings.
/// </summary>
public class EditPostgreSQLAIDataSourceViewModel
{
    /// <summary>
    /// Gets or sets the PostgreSQL connection string.
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the PostgreSQL table name.
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the globally configured connection string should be used
    /// instead of one stored on this data source.
    /// </summary>
    public bool UseDefaultConnectionString { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a connection string is already stored.
    /// </summary>
    [BindNever]
    public bool HasConnectionString { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a connection string is configured globally.
    /// </summary>
    [BindNever]
    public bool HasDefaultConnectionString { get; set; }
}
