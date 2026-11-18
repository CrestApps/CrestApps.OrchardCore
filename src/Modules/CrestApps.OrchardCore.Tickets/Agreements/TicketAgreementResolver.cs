using CrestApps.Core;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;

namespace CrestApps.OrchardCore.Tickets.Agreements;

/// <summary>
/// Finds the agreements tickets work under: those in force with ticket terms.
/// </summary>
internal sealed class TicketAgreementResolver : ITicketAgreementResolver
{
    private readonly IAgreementBalanceService _balances;
    private readonly IAgreementManager _agreements;

    public TicketAgreementResolver(IAgreementBalanceService balances, IAgreementManager agreements)
    {
        _balances = balances;
        _agreements = agreements;
    }

    /// <inheritdoc/>
    public async Task<TicketAgreementInfo> FindForNewTicketAsync(string accountId, string deskId, string contentType, CancellationToken cancellationToken = default)
    {
        foreach (var agreement in await _balances.ListInForceAsync(accountId, cancellationToken: cancellationToken))
        {
            if (TermsOf(agreement) is { } terms && terms.Covers(deskId, contentType))
            {
                return Info(agreement, terms);
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<TicketAgreementInfo> FindAsync(string agreementId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(agreementId) || await _agreements.FindByIdAsync(agreementId, cancellationToken) is not { } agreement || TermsOf(agreement) is not { } terms
            ? null
            : Info(agreement, terms);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TicketAgreementInfo>> ListForAccountAsync(string accountId, CancellationToken cancellationToken = default)
        => [.. (await _balances.ListInForceAsync(accountId, cancellationToken: cancellationToken))
            .Select(agreement => (agreement, terms: TermsOf(agreement)))
            .Where(pair => pair.terms is not null)
            .Select(pair => Info(pair.agreement, pair.terms))];

    private static TicketAgreementTerms TermsOf(Agreement agreement)
        => agreement.TryGet<TicketAgreementTerms>(out var terms) ? terms : null;

    private static TicketAgreementInfo Info(Agreement agreement, TicketAgreementTerms terms)
        => new(agreement.ItemId, agreement.Number, agreement.Name, agreement.AccountId, string.IsNullOrEmpty(terms.ServiceLevelPolicyId) ? null : terms.ServiceLevelPolicyId);
}
