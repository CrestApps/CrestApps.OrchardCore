namespace CrestApps.OrchardCore.PersonalData;

/// <summary>
/// What a contributor exports a person's records into.
/// </summary>
public sealed class PersonalDataExportContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PersonalDataExportContext"/> class.
    /// </summary>
    /// <param name="subject">The person the request is about.</param>
    /// <param name="export">The export the records are added to.</param>
    public PersonalDataExportContext(PersonalDataSubject subject, PersonalDataExport export)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(export);

        Subject = subject;
        Export = export;
    }

    /// <summary>
    /// Gets the person the request is about.
    /// </summary>
    public PersonalDataSubject Subject { get; }

    /// <summary>
    /// Gets the export the records are added to.
    /// </summary>
    public PersonalDataExport Export { get; }

    /// <summary>
    /// Adds a record to the export.
    /// </summary>
    /// <param name="kind">The kind of record.</param>
    /// <param name="id">The identifier of the record.</param>
    /// <param name="title">The title of the record.</param>
    /// <param name="createdUtc">When the record was made, when it is known.</param>
    /// <returns>The record, to add its fields to.</returns>
    public PersonalDataRecord Add(string kind, string id, string title, DateTime? createdUtc = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(kind);

        var record = new PersonalDataRecord
        {
            Kind = kind,
            Id = id,
            Title = title,
            CreatedUtc = createdUtc,
        };

        Export.Records.Add(record);

        return record;
    }
}
