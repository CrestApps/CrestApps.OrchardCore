using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// The default <see cref="IPartyTaxProfileProvider"/>: it reads the party's <see cref="PartyBillingProfile"/> and fills
/// what the profile doesn't know yet. An account is a business, taxed at its billing address as its business address;
/// another party's billing address is its residence. The tax number fills both the tax registration number and the VAT
/// number, and an exempt party stays exempt. A customer record's id is the profile's customer id, which exemption
/// certificates are kept by.
/// </summary>
public sealed class BillingProfilePartyTaxProfileProvider : IPartyTaxProfileProvider
{
    private readonly IPartyBillingProfileService _billingProfiles;

    /// <summary>
    /// Initializes a new instance of the <see cref="BillingProfilePartyTaxProfileProvider"/> class.
    /// </summary>
    /// <param name="billingProfiles">The service that reads a party's billing profile.</param>
    public BillingProfilePartyTaxProfileProvider(IPartyBillingProfileService billingProfiles)
    {
        _billingProfiles = billingProfiles;
    }

    /// <inheritdoc/>
    public async Task BuildAsync(CustomerTaxProfile profile, PartyRef party, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (party.IsEmpty)
        {
            return;
        }

        var isBusiness = string.Equals(party.Kind, PartyKinds.Account, StringComparison.OrdinalIgnoreCase);

        if (isBusiness)
        {
            profile.CustomerType = CustomerTaxType.B2B;
        }

        if (string.Equals(party.Kind, PartyKinds.Customer, StringComparison.OrdinalIgnoreCase))
        {
            profile.CustomerId ??= party.Id;
        }

        if (await _billingProfiles.GetAsync(party, cancellationToken) is not { } billing)
        {
            return;
        }

        if (string.IsNullOrEmpty(profile.TaxRegistrationNumber) && string.IsNullOrEmpty(profile.VatNumber))
        {
            profile.SetTaxNumber(billing.TaxId);
        }

        profile.IsTaxExempt |= billing.TaxExempt;

        if (billing.Contact?.Address is not { } address)
        {
            return;
        }

        if (isBusiness)
        {
            profile.BusinessAddress ??= address.Clone();
        }
        else
        {
            profile.ResidenceAddress ??= address.Clone();
        }
    }
}
