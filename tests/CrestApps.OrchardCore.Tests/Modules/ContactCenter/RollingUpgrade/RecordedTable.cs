namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.RollingUpgrade;

/// <summary>
/// The schema intent a migration declared for one index table.
/// </summary>
internal sealed class RecordedTable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecordedTable"/> class.
    /// </summary>
    /// <param name="indexType">The index type the table stores.</param>
    /// <param name="tableName">The resolved table name, including the collection.</param>
    public RecordedTable(Type indexType, string tableName)
    {
        IndexType = indexType;
        TableName = tableName;
    }

    /// <summary>
    /// Gets the index type the table stores.
    /// </summary>
    public Type IndexType { get; }

    /// <summary>
    /// Gets the resolved table name, including the collection.
    /// </summary>
    public string TableName { get; }

    /// <summary>
    /// Gets the columns the create step declares. These are the columns a fresh installation receives.
    /// </summary>
    public List<RecordedColumn> CreatedColumns { get; } = [];

    /// <summary>
    /// Gets the columns an update step adds. Subtracting these from <see cref="CreatedColumns"/> yields the
    /// schema a deployment on the previous version is running, which is the definition Orchard's migration
    /// versioning relies on: an update step exists precisely because the create step once produced the
    /// smaller table.
    /// </summary>
    public List<RecordedColumn> AddedColumns { get; } = [];
}
