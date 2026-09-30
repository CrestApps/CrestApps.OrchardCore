using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Classifies content types into the CRM record kinds by the parts they carry. A type is never named in code: a
/// contact is anything with <c>OmnichannelContactPart</c>, a lead is a contact-capable type that also carries
/// <c>LeadPart</c>, an account carries <c>AccountPart</c> and an opportunity carries <c>OpportunityPart</c>.
/// </summary>
public static class OmnichannelRecordKinds
{
    /// <summary>
    /// Determines whether the content type carries the Omnichannel contact part, which makes its items reachable
    /// by phone, SMS and email. Both contacts and leads are reachable.
    /// </summary>
    /// <param name="definition">The content type definition.</param>
    public static bool IsReachable(ContentTypeDefinition definition)
        => HasPart(definition, OmnichannelConstants.ContentParts.OmnichannelContact);

    /// <summary>
    /// Determines whether the content type is a lead type: reachable, and marked with the lead part.
    /// </summary>
    /// <param name="definition">The content type definition.</param>
    public static bool IsLead(ContentTypeDefinition definition)
        => IsReachable(definition) && HasPart(definition, OmnichannelConstants.ContentParts.Lead);

    /// <summary>
    /// Determines whether the content type is a contact type: reachable, and not a lead type.
    /// </summary>
    /// <param name="definition">The content type definition.</param>
    public static bool IsContact(ContentTypeDefinition definition)
        => IsReachable(definition) && !HasPart(definition, OmnichannelConstants.ContentParts.Lead);

    /// <summary>
    /// Determines whether the content type is an account type.
    /// </summary>
    /// <param name="definition">The content type definition.</param>
    public static bool IsAccount(ContentTypeDefinition definition)
        => HasPart(definition, OmnichannelConstants.ContentParts.Account);

    /// <summary>
    /// Determines whether the content type is an opportunity type.
    /// </summary>
    /// <param name="definition">The content type definition.</param>
    public static bool IsOpportunity(ContentTypeDefinition definition)
        => HasPart(definition, OmnichannelConstants.ContentParts.Opportunity);

    /// <summary>
    /// Determines whether items of the content type can belong to an account: contacts and opportunities. Leads
    /// never can, because a lead is not yet anyone's contact.
    /// </summary>
    /// <param name="definition">The content type definition.</param>
    public static bool IsAccountChild(ContentTypeDefinition definition)
        => definition is not null &&
        !HasPart(definition, OmnichannelConstants.ContentParts.Lead) &&
        !IsAccount(definition) &&
        (IsReachable(definition) || IsOpportunity(definition));

    /// <summary>
    /// Determines whether the content type carries the named part.
    /// </summary>
    /// <param name="definition">The content type definition.</param>
    /// <param name="partName">The technical name of the part definition.</param>
    public static bool HasPart(ContentTypeDefinition definition, string partName)
    {
        if (definition?.Parts is null)
        {
            return false;
        }

        foreach (var part in definition.Parts)
        {
            if (string.Equals(part.PartDefinition?.Name, partName, StringComparison.Ordinal) ||
                string.Equals(part.Name, partName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
