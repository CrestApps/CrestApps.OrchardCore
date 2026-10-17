namespace CrestApps.OrchardCore.Customers.PersonalData;

/// <summary>
/// A customer a payment provider keeps for a person, such as the Stripe customer their saved card is attached to.
/// </summary>
/// <param name="ProviderKey">The key of the payment provider, for example <c>Stripe</c>.</param>
/// <param name="CustomerReference">The provider's identifier of the customer.</param>
public sealed record CommerceProviderCustomer(string ProviderKey, string CustomerReference);
