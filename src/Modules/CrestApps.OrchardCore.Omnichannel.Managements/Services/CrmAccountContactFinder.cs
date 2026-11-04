using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using OrchardCore.ContentManagement;
using OrchardCore.Lists.Indexes;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Finds an account's contact by the primary email on its contact record.
/// </summary>
internal sealed class CrmAccountContactFinder : IAccountContactFinder
{
    private readonly ISession _session;

    public CrmAccountContactFinder(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc/>
    public async Task<string> FindContactByEmailAsync(string accountId, string email, CancellationToken cancellationToken = default)
    {
        var address = email?.Trim();

        if (string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(address))
        {
            return null;
        }

        var matches = await _session
            .Query<ContentItem, OmnichannelContactIndex>(index => index.Latest && index.PrimaryEmailAddress == address)
            .With<ContainedPartIndex>(index => index.ListContentItemId == accountId)
            .Take(2)
            .ListAsync(cancellationToken);

        var ids = matches.Select(item => item.ContentItemId).Distinct(StringComparer.Ordinal).ToArray();

        // Two contacts with one email can't be told apart, so neither is linked.
        return ids.Length == 1 ? ids[0] : null;
    }
}
