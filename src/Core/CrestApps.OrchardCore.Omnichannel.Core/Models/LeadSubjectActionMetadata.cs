namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// The lead status a subject action moves a lead to when the activity it runs for belongs to a lead. It is stored
/// on the action, so any action type can advance a lead, for example a no-answer retry that marks the lead as
/// contacted.
/// </summary>
public sealed class SetLeadStatusActionMetadata
{
    /// <summary>
    /// Gets or sets the identifier of the status the lead moves to, or <see langword="null"/> to leave it as it is.
    /// </summary>
    public string StatusId { get; set; }
}

/// <summary>
/// The settings of a subject action that converts the activity's lead into a contact.
/// </summary>
public sealed class ConvertLeadActionMetadata
{
    /// <summary>
    /// Gets or sets how the account is chosen. <see cref="LeadConversionAccountMode.Automatic"/> uses the account
    /// named after the lead's company, creating it when none exists.
    /// </summary>
    public LeadConversionAccountMode AccountMode { get; set; } = LeadConversionAccountMode.Automatic;

    /// <summary>
    /// Gets or sets whether an opportunity is created with the contact.
    /// </summary>
    public bool CreateOpportunity { get; set; }

    /// <summary>
    /// Gets or sets the opportunity type, or <see langword="null"/> to use the lead type's default.
    /// </summary>
    public string OpportunityContentType { get; set; }

    /// <summary>
    /// Gets or sets what happens to the lead's other open activities.
    /// </summary>
    public LeadOpenActivityMode OpenActivities { get; set; } = LeadOpenActivityMode.Move;
}
