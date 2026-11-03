using CrestApps.OrchardCore.Accounts.Models;
using OrchardCore.ContentManagement;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Accounts.Indexes;

/// <summary>
/// One row for each member of each version of an account, so the account a signed-in user belongs to is found by
/// their user id.
/// </summary>
public sealed class AccountMemberIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the account's content item id.
    /// </summary>
    public string AccountContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the member's user id.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets whether the account version is published.
    /// </summary>
    public bool Published { get; set; }

    /// <summary>
    /// Gets or sets whether the account version is the latest.
    /// </summary>
    public bool Latest { get; set; }
}

/// <summary>
/// Indexes the members of the accounts that have the <see cref="AccountMembersPart"/>.
/// </summary>
public sealed class AccountMemberIndexProvider : IndexProvider<ContentItem>
{
    /// <inheritdoc/>
    public override void Describe(DescribeContext<ContentItem> context)
        => context.For<AccountMemberIndex>()
            .When(contentItem => contentItem.Has<AccountMembersPart>())
            .Map(contentItem => ((contentItem.TryGet<AccountMembersPart>(out var part) ? part.Members : null) ?? [])
                .Where(member => !string.IsNullOrEmpty(member?.UserId))
                .Select(member => member.UserId)
                .Distinct(StringComparer.Ordinal)
                .Select(userId => new AccountMemberIndex
                {
                    AccountContentItemId = contentItem.ContentItemId,
                    UserId = userId,
                    Published = contentItem.Published,
                    Latest = contentItem.Latest,
                }));
}
