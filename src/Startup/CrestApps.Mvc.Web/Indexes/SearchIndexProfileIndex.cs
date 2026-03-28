using CrestApps.AI.Models;
using CrestApps.Data.YesSql.Indexes;
using YesSql.Indexes;

namespace CrestApps.Mvc.Web.Indexes;

public sealed class SearchIndexProfileIndex : CatalogItemIndex
{
    public string Name { get; set; }

    public string ProviderName { get; set; }

    public string Type { get; set; }
}

public sealed class SearchIndexProfileIndexProvider : IndexProvider<SearchIndexProfile>
{
    public override void Describe(DescribeContext<SearchIndexProfile> context)
    {
        context.For<SearchIndexProfileIndex>()
            .Map(profile => new SearchIndexProfileIndex
            {
                ItemId = profile.ItemId,
                Name = profile.Name,
                ProviderName = profile.ProviderName,
                Type = profile.Type,
            });
    }
}
