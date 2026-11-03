using CrestApps.OrchardCore.DataConnectors.Models;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.DataConnectors;

/// <summary>
/// The connection a connector works with.
/// </summary>
public class DataConnectorContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataConnectorContext"/> class.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="secrets">The reader of the connection's secrets.</param>
    public DataConnectorContext(DataConnection connection, IDataConnectorSecrets secrets)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(secrets);

        Connection = connection;
        Secrets = secrets;
    }

    /// <summary>
    /// Gets the connection, with the connector's settings in its properties.
    /// </summary>
    public DataConnection Connection { get; }

    /// <summary>
    /// Gets the reader of the connection's secrets, such as a password or an API key.
    /// </summary>
    public IDataConnectorSecrets Secrets { get; }
}

/// <summary>
/// Why a connector is asked to read rows.
/// </summary>
public enum DataConnectorReadPurpose
{
    /// <summary>
    /// A consumer reads a live data set.
    /// </summary>
    Live,

    /// <summary>
    /// A refresh imports the data set into local storage.
    /// </summary>
    Import,

    /// <summary>
    /// An administrator looks at the first rows, or the fields are inferred from a sample.
    /// </summary>
    Preview,
}

/// <summary>
/// One read of a data set's resource.
/// </summary>
public sealed class DataConnectorReadContext : DataConnectorContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataConnectorReadContext"/> class.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="secrets">The reader of the connection's secrets.</param>
    /// <param name="dataSet">The data set read.</param>
    public DataConnectorReadContext(DataConnection connection, IDataConnectorSecrets secrets, ConnectedDataSet dataSet)
        : base(connection, secrets)
    {
        ArgumentNullException.ThrowIfNull(dataSet);

        DataSet = dataSet;
    }

    /// <summary>
    /// Gets the data set read, with the connector's resource settings in its properties.
    /// </summary>
    public ConnectedDataSet DataSet { get; }

    /// <summary>
    /// Gets or sets why the rows are read.
    /// </summary>
    public DataConnectorReadPurpose Purpose { get; set; }

    /// <summary>
    /// Gets the source fields wanted, by source name. Empty means every field.
    /// </summary>
    public ISet<string> Fields { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets the read's conditions, on source field names. They are hints the connector may apply.
    /// </summary>
    public IList<DataCondition> Conditions { get; } = [];

    /// <summary>
    /// Gets or sets the most rows to read, or zero for no limit.
    /// </summary>
    public int MaxRows { get; set; }

    /// <summary>
    /// Gets or sets the number of rows in a batch.
    /// </summary>
    public int BatchSize { get; set; } = DataSourceQuery.DefaultBatchSize;

    /// <summary>
    /// Gets the parameters of the read, such as values a pipeline passes to a query.
    /// </summary>
    public IDictionary<string, object> Parameters { get; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the source name of the field an incremental import reads after, or <see langword="null"/>.
    /// </summary>
    public string WatermarkField { get; set; }

    /// <summary>
    /// Gets or sets the largest value of <see cref="WatermarkField"/> the last import read. Only the rows after it
    /// are read.
    /// </summary>
    public object Watermark { get; set; }
}
