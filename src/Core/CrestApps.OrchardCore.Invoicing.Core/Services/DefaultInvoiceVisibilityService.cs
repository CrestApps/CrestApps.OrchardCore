using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Invoice visibility through <see cref="IPartyMembershipService"/> with <see cref="PartyAccess.Billing"/>, the same rule
/// the ledger applies to transactions: an account's admins see its invoices, a plain member does not.
/// </summary>
public sealed class DefaultInvoiceVisibilityService : IInvoiceVisibilityService
{
    private readonly IPartyMembershipService _membership;

    public DefaultInvoiceVisibilityService(IPartyMembershipService membership)
    {
        _membership = membership;
    }

    /// <inheritdoc/>
    public async Task<bool> CanViewAsync(Invoice invoice, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        if (string.IsNullOrEmpty(userId) || !invoice.IssuedUtc.HasValue || invoice.BillTo is null)
        {
            return false;
        }

        var party = invoice.BillTo.Party;

        return !party.IsEmpty &&
            !string.Equals(party.Kind, PartyKinds.Guest, StringComparison.Ordinal) &&
            await _membership.IsMemberAsync(party, userId, PartyAccess.Billing, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<InvoiceQuery> CreateQueryAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var parties = await _membership.GetPartiesAsync(userId, PartyAccess.Billing, cancellationToken);

        return new InvoiceQuery
        {
            IssuedOnly = true,
            OnlyBillTo = true,
            BillTo = [.. (parties ?? []).Where(party => !party.IsEmpty && !string.Equals(party.Kind, PartyKinds.Guest, StringComparison.Ordinal))],
        };
    }
}
