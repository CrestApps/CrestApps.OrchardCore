using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Converts a lead with nobody at the screen to choose the contact: a subject action, a workflow or the AI. It merges
/// into an existing contact only when exactly one shares the lead's phone number or email; with more than one it
/// cannot tell which, so it creates a new contact rather than guess.
/// </summary>
public interface IUnattendedLeadConverter
{
    /// <summary>
    /// Converts the lead.
    /// </summary>
    /// <param name="lead">The lead to convert.</param>
    /// <param name="request">
    /// The account, opportunity and activity choices. Its lead and existing contact are filled in by the converter.
    /// </param>
    /// <returns>The result of the conversion.</returns>
    Task<LeadConversionResult> ConvertAsync(ContentItem lead, LeadConversionRequest request);
}
