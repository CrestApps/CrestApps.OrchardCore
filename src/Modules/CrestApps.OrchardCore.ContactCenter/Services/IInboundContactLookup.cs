namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Resolves Omnichannel contact content items by a phone number. Used by the inbound voice flow to
/// attach a contact to the activity and by the incoming-call context provider to list customers
/// matched by the caller's number.
/// </summary>
public interface IInboundContactLookup
{
    /// <summary>
    /// Finds the identifiers of the contact content items whose primary cell or home phone number
    /// matches the supplied phone number.
    /// </summary>
    /// <param name="phoneNumber">The phone number to match, in any format.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching contact content item identifiers, or an empty list when none match.</returns>
    Task<IReadOnlyList<string>> FindContactItemIdsAsync(string phoneNumber, CancellationToken cancellationToken = default);
}
