namespace CrestApps.OrchardCore.PersonalData;

/// <summary>
/// Everything the modules keep about a person, as returned for a privacy export.
/// </summary>
public sealed class PersonalDataExport
{
    /// <summary>
    /// Gets or sets when the export was made.
    /// </summary>
    public DateTime ExportedUtc { get; set; }

    /// <summary>
    /// Gets or sets the user IDs the person was identified by.
    /// </summary>
    public IList<string> UserIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the emails the person was identified by.
    /// </summary>
    public IList<string> Emails { get; set; } = [];

    /// <summary>
    /// Gets or sets the phone numbers the person was identified by.
    /// </summary>
    public IList<string> PhoneNumbers { get; set; } = [];

    /// <summary>
    /// Gets the records the modules keep about the person.
    /// </summary>
    public IList<PersonalDataRecord> Records { get; init; } = [];

    /// <summary>
    /// Gets what could not be exported, for the person handling the request.
    /// </summary>
    public IList<string> Warnings { get; init; } = [];
}
