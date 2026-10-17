namespace CrestApps.OrchardCore.Customers.PersonalData;

/// <summary>
/// What an <see cref="ICommercePersonalDataContributor"/> is given to export a person's records.
/// </summary>
public sealed class CommercePersonalDataExportContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CommercePersonalDataExportContext"/> class.
    /// </summary>
    /// <param name="subject">The identified person.</param>
    /// <param name="export">The export the contributors add to.</param>
    public CommercePersonalDataExportContext(CommercePersonalDataSubject subject, CommercePersonalDataExport export)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(export);

        Subject = subject;
        Export = export;
    }

    /// <summary>
    /// Gets the identified person.
    /// </summary>
    public CommercePersonalDataSubject Subject { get; }

    /// <summary>
    /// Gets the export the contributors add to.
    /// </summary>
    public CommercePersonalDataExport Export { get; }
}
