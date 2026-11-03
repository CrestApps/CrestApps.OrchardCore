using CrestApps.OrchardCore.Accounts.B2B.Indexes;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Contents.Services;
using YesSql.Filters.Query;
using YesSql.Services;

namespace CrestApps.OrchardCore.Accounts.B2B.Services;

/// <summary>
/// Adds <c>b2b:</c> to the content list search box: <c>b2b:true</c> lists the accounts that buy as a business, and
/// <c>b2b:false</c> the items that don't, including accounts saved before the B2B part was added.
/// </summary>
internal sealed class B2BAccountsContentsAdminListFilterProvider : IContentsAdminListFilterProvider
{
    public const string TermName = "b2b";

    public void Build(QueryEngineBuilder<ContentItem> builder)
        => builder.WithNamedTerm(TermName, term => term
            .OneCondition((value, query) =>
            {
                if (!string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
                {
                    return query.With<B2BAccountIndex>(index => index.Enabled && index.Latest);
                }

                // An account saved before the B2B part was added has no B2B index row, so "not buying as a business" is
                // read as "not among those that do".
                return query.With<ContentItemIndex>(index => index.ContentItemId.IsNotIn<B2BAccountIndex>(
                    b2b => b2b.ContentItemId,
                    b2b => b2b.Enabled && b2b.Latest));
            }));
}
