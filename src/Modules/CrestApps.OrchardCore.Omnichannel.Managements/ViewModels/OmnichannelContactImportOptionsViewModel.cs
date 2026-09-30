using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// View model for the omnichannel contact import options UI.
/// </summary>
public class OmnichannelContactImportOptionsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether to ignore duplicate contacts based on phone number.
    /// </summary>
    public bool IgnoreDuplicateByPhoneNumber { get; set; } = true;

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 country code used to normalize imported phone numbers.
    /// </summary>
    public string SelectedCountryCode { get; set; }

    /// <summary>
    /// Gets or sets which existing records a contact import compares phone numbers against.
    /// </summary>
    public Models.ContactImportDuplicateScope DuplicateScope { get; set; }

    /// <summary>
    /// Gets or sets whether a lead import skips numbers that belong to a contact.
    /// </summary>
    public bool SkipNumbersOfExistingContacts { get; set; } = true;

    /// <summary>
    /// Gets or sets whether a lead import skips numbers that belong to an open lead.
    /// </summary>
    public bool SkipNumbersOfOpenLeads { get; set; } = true;

    /// <summary>
    /// Gets or sets the lead source stamped on the leads of the file.
    /// </summary>
    public string LeadSource { get; set; }

    /// <summary>
    /// Gets or sets the list name stamped on the leads of the file.
    /// </summary>
    public string LeadListName { get; set; }

    /// <summary>
    /// Gets or sets the status of the leads of the file.
    /// </summary>
    public string LeadStatusId { get; set; }

    /// <summary>
    /// Gets or sets the owner of the leads of the file.
    /// </summary>
    public string LeadOwnerId { get; set; }

    /// <summary>
    /// Gets or sets whether the imported type is a lead type.
    /// </summary>
    [BindNever]
    public bool IsLeadType { get; set; }

    /// <summary>
    /// Gets or sets whether the CRM feature is enabled, which makes the duplicate scope meaningful.
    /// </summary>
    [BindNever]
    public bool CrmEnabled { get; set; }

    /// <summary>
    /// Gets or sets the lead statuses to choose from.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> LeadStatuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the available countries for phone-number normalization.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> AvailableCountries { get; set; } = [];
}
