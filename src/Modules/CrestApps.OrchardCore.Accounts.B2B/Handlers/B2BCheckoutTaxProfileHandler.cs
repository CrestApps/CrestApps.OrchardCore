using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Taxation.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Accounts.B2B.Handlers;

/// <summary>
/// Tells checkout tax that a buyer who buys for a business account is a business, with the account's tax or VAT
/// number, so reverse charge and business rules apply to it. It keeps what other handlers know about the buyer, such as
/// the exemption on their customer record.
/// </summary>
public sealed class B2BCheckoutTaxProfileHandler : ICheckoutTaxProfileHandler
{
    private readonly B2BAccountService _accounts;

    /// <summary>
    /// Initializes a new instance of the <see cref="B2BCheckoutTaxProfileHandler"/> class.
    /// </summary>
    /// <param name="accounts">The service that finds the account a buyer buys for.</param>
    public B2BCheckoutTaxProfileHandler(B2BAccountService accounts)
    {
        _accounts = accounts;
    }

    /// <inheritdoc/>
    public async Task BuildingAsync(CheckoutTaxProfile profile, ICheckoutFlowSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        // A sale at a register is the store's, even when the cashier buys for an account.
        if (session is not CheckoutSession { OwnerId.Length: > 0 } checkout ||
            checkout.TryGet<CheckoutCounterSale>(out _) ||
            await _accounts.GetForUserAsync(checkout.OwnerId, cancellationToken) is not { } account)
        {
            return;
        }

        profile.Customer ??= new CustomerTaxProfile();
        profile.Customer.CustomerType = CustomerTaxType.B2B;

        if (!string.IsNullOrWhiteSpace(account.Part.TaxId))
        {
            var taxId = account.Part.TaxId.Trim();

            profile.Customer.TaxRegistrationNumber = taxId;
            profile.Customer.VatNumber = taxId;

            // A B2B account and its tax number are set up by the store, not by the buyer, so the number counts as verified.
            profile.Customer.TaxRegistrationVerified = true;
        }
    }
}
