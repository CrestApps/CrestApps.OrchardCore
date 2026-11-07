namespace CrestApps.OrchardCore.PersonalData;

/// <summary>
/// One record a module keeps about a person, as it is exported: its kind, a title and the fields that hold the
/// person's data.
/// </summary>
public sealed class PersonalDataRecord
{
    /// <summary>
    /// Gets or sets the kind of record, such as <c>Booking</c> or <c>WorkerProfile</c>. Each module names its own kinds.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets a short title a person reading the export recognizes the record by.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the record in its module.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets when the record was made, when it is known.
    /// </summary>
    public DateTime? CreatedUtc { get; set; }

    /// <summary>
    /// Gets the fields of the record, by name.
    /// </summary>
    public IDictionary<string, string> Fields { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
