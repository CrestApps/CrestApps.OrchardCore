using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Converts a lead into a contact: it creates the contact or merges the lead into an existing one, places the
/// contact in an account, optionally creates an opportunity, moves the lead's history to the contact, and closes
/// the lead as converted. Converting a lead that is already converted changes nothing and returns the contact it
/// became.
/// </summary>
public interface ILeadConversionService
{
    /// <summary>
    /// Converts the lead described by the request.
    /// </summary>
    /// <param name="request">How to convert the lead.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The outcome, whose errors say why a lead could not be converted.</returns>
    Task<LeadConversionResult> ConvertAsync(LeadConversionRequest request, CancellationToken cancellationToken = default);
}
