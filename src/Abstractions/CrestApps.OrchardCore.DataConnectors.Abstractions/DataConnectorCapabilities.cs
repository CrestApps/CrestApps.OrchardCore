namespace CrestApps.OrchardCore.DataConnectors;

/// <summary>
/// What a data connector can do.
/// </summary>
[Flags]
public enum DataConnectorCapabilities
{
    /// <summary>
    /// The connector can do nothing beyond its contract.
    /// </summary>
    None = 0,

    /// <summary>
    /// A data set can be read live: every read goes to the external system.
    /// </summary>
    Live = 1,

    /// <summary>
    /// A data set can be imported: its rows are copied into local storage and read from there.
    /// </summary>
    Import = 2,

    /// <summary>
    /// An import can read only the rows changed since the last one, by the largest value of an incremental field.
    /// </summary>
    IncrementalImport = 4,

    /// <summary>
    /// The connector can list the resources of a connection.
    /// </summary>
    Discovery = 8,

    /// <summary>
    /// The connector applies some read conditions at the source.
    /// </summary>
    ConditionPushdown = 16,
}
