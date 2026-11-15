using CrestApps.OrchardCore.Accounts.Indexes;
using YesSql;

namespace CrestApps.OrchardCore.Accounts.Services;

/// <summary>
/// The default <see cref="IAccountEmailDomains"/>, from <see cref="AccountEmailDomainIndex"/>.
/// </summary>
public sealed class AccountEmailDomains : IAccountEmailDomains
{
    private readonly ISession _session;

    public AccountEmailDomains(ISession session)
    {
        _session = session;
    }

    public async Task<string> FindAccountIdAsync(string email, CancellationToken cancellationToken = default)
    {
        if (!email?.Contains('@') == true || AccountEmailDomainsNormalizer.Normalize(email) is not { } domain)
        {
            return null;
        }

        var accounts = (await _session.QueryIndex<AccountEmailDomainIndex>(index => index.Domain == domain).Take(2).ListAsync(cancellationToken))
            .Select(index => index.AccountContentItemId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return accounts.Count == 1 ? accounts[0] : null;
    }
}
