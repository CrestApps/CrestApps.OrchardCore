using CrestApps.Core.Models;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.DataConnectors.Models;

/// <summary>
/// A data set read from a resource of a connection, exposed to reports and data pipelines live or as an import.
/// </summary>
/// <remarks>
/// The connector's resource settings, such as a query or a records path, are stored in
/// <see cref="CrestApps.Core.ExtensibleEntity.Properties"/>.
/// </remarks>
public sealed class ConnectedDataSet : CatalogItem
{
    /// <summary>
    /// Gets or sets the identifier of the stored document, so a record saved again after a commit updates its row.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the data set, which consumers store. It is unique on the tenant and stable.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the name shown to users.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets a description of the data set.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the connection the data set reads.
    /// </summary>
    public string ConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the resource the data set reads, such as a table, an index, a path or a file.
    /// </summary>
    public string Resource { get; set; }

    /// <summary>
    /// Gets or sets how consumers read the data set.
    /// </summary>
    public DataSetAccessMode AccessMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether consumers can read the data set and imports refresh it.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the fields of the data set.
    /// </summary>
    public List<ConnectedDataField> Fields { get; set; } = [];

    /// <summary>
    /// Gets or sets the name of the date field reports filter by default, or <see langword="null"/>.
    /// </summary>
    public string DefaultDateField { get; set; }

    /// <summary>
    /// Gets or sets how an imported data set is refreshed.
    /// </summary>
    public DataSetRefreshSettings Refresh { get; set; } = new();

    /// <summary>
    /// Gets or sets what the last refreshes of an imported data set left.
    /// </summary>
    public DataSetImportState Import { get; set; } = new();

    /// <summary>
    /// Gets or sets when the data set was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the data set was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the user name of whoever created the data set.
    /// </summary>
    public string Author { get; set; }
}

/// <summary>
/// A field of a connected data set, and the source field it is read from.
/// </summary>
public sealed class ConnectedDataField
{
    /// <summary>
    /// Gets or sets the field's name in the data set, which consumers store.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the name of the source field it is read from, as the connector returns it.
    /// </summary>
    public string SourceName { get; set; }

    /// <summary>
    /// Gets or sets the name shown to users.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets a description of the field.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the type values are read as.
    /// </summary>
    public DataFieldType Type { get; set; }

    /// <summary>
    /// Gets or sets the type the source last described the field with, to tell a change of the source from a type an
    /// administrator chose.
    /// </summary>
    public DataFieldType SourceType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the field is part of the row's identity.
    /// </summary>
    public bool IsIdentifier { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether consumers see the field.
    /// </summary>
    public bool IsIncluded { get; set; } = true;
}

/// <summary>
/// How consumers read a connected data set.
/// </summary>
public enum DataSetAccessMode
{
    /// <summary>
    /// Every read goes to the external system.
    /// </summary>
    Live,

    /// <summary>
    /// Reads are served from the last import that succeeded.
    /// </summary>
    Imported,
}

/// <summary>
/// How an imported data set is refreshed.
/// </summary>
public sealed class DataSetRefreshSettings
{
    /// <summary>
    /// Gets or sets whether a refresh reads every row or only the rows changed since the last one.
    /// </summary>
    public DataSetRefreshMode Mode { get; set; }

    /// <summary>
    /// Gets or sets the name of the data set field an incremental refresh reads after, such as a modification date.
    /// </summary>
    public string IncrementalField { get; set; }

    /// <summary>
    /// Gets or sets the minutes between scheduled refreshes, or <see langword="null"/> to refresh only on demand.
    /// </summary>
    public int? IntervalMinutes { get; set; }

    /// <summary>
    /// Gets or sets what a refresh does when the source's fields changed.
    /// </summary>
    public DataSetSchemaChangePolicy SchemaChangePolicy { get; set; }
}

/// <summary>
/// Whether a refresh reads every row or only the changed ones.
/// </summary>
public enum DataSetRefreshMode
{
    /// <summary>
    /// Every row is read and replaces the published rows.
    /// </summary>
    Full,

    /// <summary>
    /// Only the rows whose incremental field is after the last refresh's largest value are read, and replace the rows
    /// with the same identifier.
    /// </summary>
    Incremental,
}

/// <summary>
/// What a refresh does when the source's fields changed.
/// </summary>
public enum DataSetSchemaChangePolicy
{
    /// <summary>
    /// The data set takes the new fields: added fields join it, removed fields leave it, and changed types are updated.
    /// </summary>
    Accept,

    /// <summary>
    /// The refresh fails and the published rows are kept until an administrator reviews the fields.
    /// </summary>
    Fail,
}

/// <summary>
/// What the refreshes of an imported data set left.
/// </summary>
public sealed class DataSetImportState
{
    /// <summary>
    /// Gets or sets the version of the rows consumers read, or <see langword="null"/> before the first import.
    /// </summary>
    public string ActiveVersion { get; set; }

    /// <summary>
    /// Gets or sets the version published before the active one. It is kept until the next refresh, so a consumer that
    /// started reading it can finish.
    /// </summary>
    public string PreviousVersion { get; set; }

    /// <summary>
    /// Gets or sets the number of rows of the active version.
    /// </summary>
    public long RowCount { get; set; }

    /// <summary>
    /// Gets or sets the largest value of the incremental field the active version holds, as text.
    /// </summary>
    public string Watermark { get; set; }

    /// <summary>
    /// Gets or sets when the last refresh ended, whatever its result.
    /// </summary>
    public DateTime? LastRefreshUtc { get; set; }

    /// <summary>
    /// Gets or sets when the last refresh that succeeded ended.
    /// </summary>
    public DateTime? LastSuccessUtc { get; set; }

    /// <summary>
    /// Gets or sets the status of the last refresh.
    /// </summary>
    public DataSetRefreshStatus? LastStatus { get; set; }

    /// <summary>
    /// Gets or sets why the last refresh failed, or <see langword="null"/> when it succeeded.
    /// </summary>
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets when the next scheduled refresh is due.
    /// </summary>
    public DateTime? NextRefreshUtc { get; set; }

    /// <summary>
    /// Gets or sets when someone asked for a refresh that hasn't run yet.
    /// </summary>
    public DateTime? RefreshRequestedUtc { get; set; }
}
