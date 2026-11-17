using CrestApps.OrchardCore.Accounts.Models;
using OrchardCore.ContentManagement;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Accounts.Indexes;

/// <summary>
/// One row for each e-mail domain of each published account.
/// </summary>
public sealed class AccountEmailDomainIndex : MapIndex
{
    public string AccountContentItemId { get; set; }

    public string Domain { get; set; }
}

/// <summary>
/// Indexes the e-mail domains of published accounts.
/// </summary>
public sealed class AccountEmailDomainIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
        => context.For<AccountEmailDomainIndex>()
            .ExclusiveToDocumentType()
            .When(item => item.Published && item.Has<AccountEmailDomainsPart>())
            .Map(item => (item.GetOrCreate<AccountEmailDomainsPart>().Domains ?? [])
                .Select(AccountEmailDomainsNormalizer.Normalize)
                .Where(domain => domain is not null)
                .Distinct(StringComparer.Ordinal)
                .Select(domain => new AccountEmailDomainIndex { AccountContentItemId = item.ContentItemId, Domain = domain }));
}

/// <summary>
/// Puts a domain or an address's domain in the one form the index keeps.
/// </summary>
public static class AccountEmailDomainsNormalizer
{
    /// <summary>
    /// The longest domain kept.
    /// </summary>
    public const int MaxLength = 253;

    /// <summary>
    /// Returns the domain lower case, without spaces, a leading <c>@</c> or what comes before it, or <see langword="null"/>
    /// when it can't be a domain.
    /// </summary>
    /// <param name="value">A domain, or an address.</param>
    /// <returns>The domain, or <see langword="null"/>.</returns>
    public static string Normalize(string value)
    {
        var domain = value?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(domain))
        {
            return null;
        }

        var at = domain.LastIndexOf('@');
        domain = at >= 0 ? domain[(at + 1)..] : domain;

        return domain.Length is > 0 and <= MaxLength && domain.Contains('.') && !domain.Contains(' ') ? domain : null;
    }
}
