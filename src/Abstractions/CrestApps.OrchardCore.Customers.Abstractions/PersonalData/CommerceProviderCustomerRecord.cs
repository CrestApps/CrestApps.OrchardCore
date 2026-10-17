namespace CrestApps.OrchardCore.Customers.PersonalData;

/// <summary>
/// A customer a payment provider keeps for the person.
/// </summary>
public sealed class CommerceProviderCustomerRecord
{
    /// <summary>
    /// Gets or sets the key of the payment provider.
    /// </summary>
    public string ProviderKey { get; set; }

    /// <summary>
    /// Gets or sets the provider's identifier of the customer.
    /// </summary>
    public string CustomerReference { get; set; }

    /// <summary>
    /// Gets or sets the name the provider holds.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the email the provider holds.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the phone number the provider holds.
    /// </summary>
    public string Phone { get; set; }
}
