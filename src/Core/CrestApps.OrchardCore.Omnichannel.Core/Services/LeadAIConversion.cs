using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// The rules the automated SMS and voice conversations share for letting the AI convert a lead it qualified.
/// </summary>
public static class LeadAIConversion
{
    /// <summary>
    /// The qualification used when the load gives none and the subject has no goal.
    /// </summary>
    public const string DefaultQualification = "the customer is genuinely interested and agreed to the next step";

    /// <summary>
    /// Returns the conversion settings when the AI may convert this activity's record: the load allowed it and the
    /// record is a lead that is not converted yet.
    /// </summary>
    /// <param name="activity">The automated activity.</param>
    /// <param name="record">The activity's contact or lead.</param>
    /// <param name="settings">The conversion settings, when the AI may convert.</param>
    /// <returns><see langword="true"/> when the AI may convert the record.</returns>
    public static bool TryGetSettings(OmnichannelActivity activity, ContentItem record, out LeadAIConversionSettings settings)
    {
        settings = null;

        if (activity is null ||
            record is null ||
            !activity.TryGet<LeadAIConversionSettings>(out var stored) ||
            stored?.Enabled != true ||
            !record.TryGet<LeadPart>(out var lead) ||
            lead.IsConverted)
        {
            return false;
        }

        settings = stored;

        return true;
    }

    /// <summary>
    /// Returns what qualified means for the conversation: the load's own words, else the subject goal.
    /// </summary>
    /// <param name="settings">The conversion settings.</param>
    /// <param name="subjectGoal">The subject goal.</param>
    public static string GetQualification(LeadAIConversionSettings settings, string subjectGoal)
    {
        if (!string.IsNullOrWhiteSpace(settings?.QualificationGuidance))
        {
            return settings.QualificationGuidance.Trim();
        }

        return string.IsNullOrWhiteSpace(subjectGoal)
            ? DefaultQualification
            : $"the conversation achieved the subject goal ({subjectGoal.Trim()})";
    }

    /// <summary>
    /// Returns the instruction that asks the model whether to convert the lead.
    /// </summary>
    /// <param name="settings">The conversion settings.</param>
    /// <param name="subjectGoal">The subject goal.</param>
    public static string BuildInstruction(LeadAIConversionSettings settings, string subjectGoal)
        => $"The customer is a lead: a prospect who has not been qualified yet. Set ConvertLead to true if, and only if, the conversation clearly shows the lead qualified, meaning {GetQualification(settings, subjectGoal)}. Otherwise set ConvertLead to false. Never convert a customer who declined, was undecided, only asked for information, or asked not to be contacted.";

    /// <summary>
    /// Builds the conversion request for a lead the AI qualified: an account found or created from the lead's
    /// company, the load's opportunity choice, and the lead's other open activities moved to the contact.
    /// </summary>
    /// <param name="activity">The activity that concluded the conversation.</param>
    /// <param name="settings">The conversion settings.</param>
    public static LeadConversionRequest CreateRequest(OmnichannelActivity activity, LeadAIConversionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(activity);

        return new LeadConversionRequest
        {
            AccountMode = LeadConversionAccountMode.Automatic,
            CreateOpportunity = settings?.CreateOpportunity == true,
            OpportunityContentType = string.IsNullOrWhiteSpace(settings?.OpportunityContentType) ? null : settings.OpportunityContentType,
            CampaignId = activity.CampaignId,
            OpenActivities = LeadOpenActivityMode.Move,
            CompletingActivityId = activity.ItemId,
            UserId = activity.CompletedById,
            UserName = activity.CompletedByUsername,
        };
    }
}
