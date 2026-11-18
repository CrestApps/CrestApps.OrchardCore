using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;

namespace CrestApps.OrchardCore.Accounts.B2B.Services;

/// <summary>
/// Adds what business buying keeps about an account (party kind <see cref="PartyKinds.Account"/>) to its billing
/// profile, after the Accounts source named it: its tax or VAT number, and its billing e-mail, as the contact's e-mail
/// when the account has none, else as a copy. While the account buys as a business, its terms add the days its invoices
/// are due in and the most it may owe.
/// </summary>
internal sealed class B2BAccountBillingProfileSource : IPartyBillingProfileSource
{
    private readonly IB2BAccountStore _accounts;

    public B2BAccountBillingProfileSource(IB2BAccountStore accounts)
    {
        _accounts = accounts;
    }

    public string Kind
        => PartyKinds.Account;

    public async Task ContributeAsync(PartyRef party, PartyBillingProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (party.IsEmpty || await _accounts.FindByIdAsync(party.Id, cancellationToken) is not { } account)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(account.TaxId))
        {
            profile.TaxId = account.TaxId.Trim();
        }

        var billingEmail = account.BillingEmail?.Trim();

        if (!string.IsNullOrEmpty(billingEmail))
        {
            var contact = profile.Contact ?? new ContactInfo();

            if (string.IsNullOrWhiteSpace(contact.Email))
            {
                profile.Contact = contact with { Email = billingEmail };
            }
            else
            {
                profile.AddCopyTo(billingEmail);
            }
        }

        if (!account.IsEnabled)
        {
            return;
        }

        var terms = account.Terms;

        if (terms.OffersTerms)
        {
            profile.NetDays = terms.NetDays;
        }

        if (terms.CreditLimit is { } creditLimit)
        {
            profile.CreditLimit = creditLimit;
        }
    }
}
