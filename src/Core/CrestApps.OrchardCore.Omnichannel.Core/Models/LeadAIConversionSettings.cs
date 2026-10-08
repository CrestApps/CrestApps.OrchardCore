namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Lets the AI convert a lead it qualified in an automated conversation. It is chosen on an automatic inventory load
/// of leads and snapshotted onto each loaded activity, like the load's other AI options, so a conversation follows the
/// rules it started with even if the load is edited afterwards.
/// </summary>
public sealed class LeadAIConversionSettings
{
    /// <summary>
    /// Gets or sets whether the AI may convert the lead when it judges the lead qualified.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets whether an AI conversion also creates an opportunity.
    /// </summary>
    public bool CreateOpportunity { get; set; }

    /// <summary>
    /// Gets or sets the opportunity type an AI conversion creates; empty uses the lead type's default.
    /// </summary>
    public string OpportunityContentType { get; set; }

    /// <summary>
    /// Gets or sets what qualified means for this load, in plain words, for example "has a budget and wants to buy
    /// within three months". Empty uses the subject goal.
    /// </summary>
    public string QualificationGuidance { get; set; }
}
