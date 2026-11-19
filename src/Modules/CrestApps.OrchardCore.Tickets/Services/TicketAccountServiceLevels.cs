using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.ViewModels;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.Services;

/// <summary>
/// Says which service level an account's tickets get, for its page: the policy of each agreement in force that names
/// one (for the tickets that agreement covers), then the account's own policy (<see cref="TicketAccountPart"/>).
/// </summary>
internal sealed class TicketAccountServiceLevels
{
    private readonly IServiceLevelPolicyManager _policies;
    private readonly ITicketAgreementResolver _agreements;

    public TicketAccountServiceLevels(IServiceLevelPolicyManager policies, IEnumerable<ITicketAgreementResolver> agreements)
    {
        _policies = policies;

        // Registered only with the Agreements feature.
        _agreements = agreements.FirstOrDefault();
    }

    /// <summary>
    /// Describes an account's service level.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The card's content.</returns>
    public async Task<AccountServiceLevelViewModel> DescribeAsync(ContentItem account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        var model = new AccountServiceLevelViewModel { AccountId = account.ContentItemId };

        if (account.TryGet<TicketAccountPart>(out var part) && !string.IsNullOrEmpty(part.ServiceLevelPolicyId)
            && await _policies.FindByIdAsync(part.ServiceLevelPolicyId, cancellationToken) is { } own)
        {
            model.AccountPolicyId = own.ItemId;
            model.AccountPolicyName = own.Name;
        }

        if (_agreements is null)
        {
            return model;
        }

        foreach (var agreement in await _agreements.ListForAccountAsync(account.ContentItemId, cancellationToken))
        {
            if (!string.IsNullOrEmpty(agreement.ServiceLevelPolicyId)
                && await _policies.FindByIdAsync(agreement.ServiceLevelPolicyId, cancellationToken) is { } policy)
            {
                model.Agreements.Add(new AccountServiceLevelAgreement(agreement.AgreementId, agreement.Number, agreement.Name, policy.ItemId, policy.Name));
            }
        }

        return model;
    }
}
