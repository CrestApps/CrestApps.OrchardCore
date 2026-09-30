namespace CrestApps.OrchardCore.Omnichannel.Managements.Models;

/// <summary>
/// Options for omnichannel contact import behavior.
/// Stored in the <see cref="ContentTransfer.ContentTransferEntry"/> Properties bag.
/// </summary>
public sealed class OmnichannelContactImportOptionsPart
{
    /// <summary>
    /// Gets or sets a value indicating whether to ignore duplicate contacts based on phone number.
    /// When enabled, only the first row with a given phone number is imported.
    /// </summary>
    public bool IgnoreDuplicateByPhoneNumber { get; set; } = true;

    /// <summary>
    /// Gets or sets which existing records a contact import compares phone numbers against. It applies to contact
    /// types only; a lead import uses <see cref="SkipNumbersOfExistingContacts"/> and
    /// <see cref="SkipNumbersOfOpenLeads"/> instead.
    /// </summary>
    public ContactImportDuplicateScope DuplicateScope { get; set; } = ContactImportDuplicateScope.AllContacts;

    /// <summary>
    /// Gets or sets whether a lead import skips rows whose phone number already belongs to a contact, so a customer
    /// is not prospected again as a stranger.
    /// </summary>
    public bool SkipNumbersOfExistingContacts { get; set; } = true;

    /// <summary>
    /// Gets or sets whether a lead import skips rows whose phone number already belongs to a lead that is still open.
    /// </summary>
    public bool SkipNumbersOfOpenLeads { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to ignore phone numbers listed on a national do-not-call registry.
    /// </summary>
    public bool IgnoreDoNotCallNumbers { get; set; }

    /// <summary>
    /// Gets or sets whether a row whose number is on a selected registry is imported marked Do not call instead of
    /// being skipped. The record is kept, so the number is recognised if it is bought or imported again, and the Do
    /// not call flag keeps it out of every call.
    /// </summary>
    public bool MarkRegistryNumbersDoNotCall { get; set; }

    /// <summary>
    /// Gets or sets the registry keys selected for DNC checking during this import.
    /// </summary>
    public string[] SelectedRegistryKeys { get; set; } = [];

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 country code used to normalize imported phone numbers.
    /// </summary>
    public string SelectedCountryCode { get; set; }

    /// <summary>
    /// Gets or sets the content item identifier of the lead source stamped on every lead the file creates, unless the
    /// row has its own.
    /// </summary>
    public string LeadSourceId { get; set; }

    /// <summary>
    /// Gets or sets the list name stamped on every lead the file creates, unless the row has its own, so a purchased
    /// list can be loaded and reported on as one unit.
    /// </summary>
    public string LeadListName { get; set; }

    /// <summary>
    /// Gets or sets the status of every lead the file creates, unless the row has its own.
    /// </summary>
    public string LeadStatusId { get; set; }

    /// <summary>
    /// Gets or sets the owner of every lead the file creates, unless the row has its own.
    /// </summary>
    public string LeadOwnerId { get; set; }
}

/// <summary>
/// Which existing records a contact import compares phone numbers against.
/// </summary>
public enum ContactImportDuplicateScope
{
    /// <summary>
    /// Every contact of any contact type. Leads are not compared, so importing a customer who was once a lead is not
    /// refused. Without the CRM feature there are no leads, so this is every record, as it always was.
    /// </summary>
    AllContacts,

    /// <summary>
    /// Only records of the type being imported.
    /// </summary>
    SameType,

    /// <summary>
    /// Every record that has a phone number, leads included.
    /// </summary>
    AllRecords,
}
