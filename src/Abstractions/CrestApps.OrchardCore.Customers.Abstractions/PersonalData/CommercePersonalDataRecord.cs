namespace CrestApps.OrchardCore.Customers.PersonalData;

/// <summary>
/// One record of a person's data that a module keeps, in a shape any module can fill: what kind of record it is, its
/// ID, when it was made, and its personal values by name. Modules that have no typed list on
/// <see cref="CommercePersonalDataExport"/> export through <see cref="CommercePersonalDataExport.Records"/>.
/// </summary>
public sealed class CommercePersonalDataRecord
{
    /// <summary>
    /// Gets or sets the kind of record, such as <c>Donation</c> or <c>Membership</c>.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the record's ID.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets when the record was made, in UTC.
    /// </summary>
    public DateTime? CreatedUtc { get; set; }

    /// <summary>
    /// Gets the record's values by name, such as the donor's name and email.
    /// </summary>
    public IDictionary<string, string> Values { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
