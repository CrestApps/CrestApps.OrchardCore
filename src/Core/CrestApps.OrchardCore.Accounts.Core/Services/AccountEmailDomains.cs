using CrestApps.OrchardCore.Accounts.Indexes;
using YesSql;
using YesSql.Services;

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

    public async Task<IReadOnlyDictionary<string, string>> FindClaimsAsync(IEnumerable<string> domains, string exceptAccountId, CancellationToken cancellationToken = default)
    {
        var wanted = domains?.Where(domain => !string.IsNullOrEmpty(domain)).Distinct(StringComparer.Ordinal).ToArray() ?? [];

        if (wanted.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return (await _session.QueryIndex<AccountEmailDomainIndex>(index => index.Domain.IsIn(wanted) && index.AccountContentItemId != exceptAccountId).ListAsync(cancellationToken))
            .GroupBy(index => index.Domain, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().AccountContentItemId, StringComparer.Ordinal);
    }
}
