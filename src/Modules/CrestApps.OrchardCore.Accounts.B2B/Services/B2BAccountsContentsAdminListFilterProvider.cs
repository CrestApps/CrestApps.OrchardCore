using CrestApps.OrchardCore.Accounts.B2B.Indexes;
using OrchardCore.ContentManagement;
using OrchardCore.Contents.Services;
using YesSql.Filters.Query;

namespace CrestApps.OrchardCore.Accounts.B2B.Services;

/// <summary>
/// Adds <c>b2b:</c> to the content list search box: <c>b2b:true</c> lists the accounts that buy as a business, and
/// <c>b2b:false</c> the accounts with the B2B part turned off.
/// </summary>
internal sealed class B2BAccountsContentsAdminListFilterProvider : IContentsAdminListFilterProvider
{
    public const string TermName = "b2b";

    public void Build(QueryEngineBuilder<ContentItem> builder)
        => builder.WithNamedTerm(TermName, term => term
            .OneCondition((value, query) =>
            {
                var enabled = !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);

                return query.With<B2BAccountIndex>(index => index.Enabled == enabled && index.Latest);
            }));
}
