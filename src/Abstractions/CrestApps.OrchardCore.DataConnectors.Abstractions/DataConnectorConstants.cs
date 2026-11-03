namespace CrestApps.OrchardCore.DataConnectors;

/// <summary>
/// The names the data connectors share.
/// </summary>
public static class DataConnectorConstants
{
    /// <summary>
    /// The YesSql collection that holds connections, data sets and refreshes.
    /// </summary>
    public const string CollectionName = "DataConnectors";

    /// <summary>
    /// The purpose the secrets of connections are protected for.
    /// </summary>
    public const string SecretsProtectionPurpose = "CrestApps.DataConnectors.Secrets";

    /// <summary>
    /// The name of the HTTP client that HTTP-based connectors use.
    /// </summary>
    public const string HttpClientName = "CrestApps.DataConnectors";

    /// <summary>
    /// The name of the table that holds the rows of imported data sets, before the tenant's table prefix.
    /// </summary>
    public const string RowsTableName = "DataConnectors_Rows";

    /// <summary>
    /// The name of the data source that reads live data sets from their external systems.
    /// </summary>
    public const string LiveDataSourceName = "ConnectedData";

    /// <summary>
    /// The name of the data source that reads imported data sets from local storage.
    /// </summary>
    public const string ImportedDataSourceName = "ImportedData";

    /// <summary>
    /// The feature identifiers.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The base feature: connections, data sets, live and imported data sources.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.DataConnectors";

        /// <summary>
        /// The HTTP API connector.
        /// </summary>
        public const string Http = "CrestApps.OrchardCore.DataConnectors.Http";

        /// <summary>
        /// The Elasticsearch connector.
        /// </summary>
        public const string Elasticsearch = "CrestApps.OrchardCore.DataConnectors.Elasticsearch";

        /// <summary>
        /// The media files connector.
        /// </summary>
        public const string Files = "CrestApps.OrchardCore.DataConnectors.Files";

        /// <summary>
        /// The SQL Server connector.
        /// </summary>
        public const string SqlServer = "CrestApps.OrchardCore.DataConnectors.SqlServer";
    }
}
