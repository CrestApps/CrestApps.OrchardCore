namespace CrestApps.OrchardCore.AI.Agent.RecordTools;

/// <summary>
/// What a generic record tool does with the records of a kind, which decides the permission it needs and, for a
/// read-only kind, the sentence it refuses with.
/// </summary>
internal enum AIRecordAccess
{
    /// <summary>
    /// Lists or reads records (<c>listRecords</c>, <c>getRecord</c>).
    /// </summary>
    Read,

    /// <summary>
    /// Creates or changes a record (<c>saveRecord</c>).
    /// </summary>
    Save,

    /// <summary>
    /// Deletes records (<c>deleteRecord</c>).
    /// </summary>
    Delete,
}
