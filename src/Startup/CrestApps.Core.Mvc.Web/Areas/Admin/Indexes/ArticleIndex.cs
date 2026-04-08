using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Mvc.Web.Areas.Admin.Models;
using YesSql.Indexes;

namespace CrestApps.Core.Mvc.Web.Areas.Admin.Indexes;

public sealed class ArticleIndex : CatalogItemIndex
{
    public string Title { get; set; }
}

public sealed class ArticleIndexProvider : IndexProvider<Article>
{
    public override void Describe(DescribeContext<Article> context)
    {
        context.For<ArticleIndex>()
            .Map(article => new ArticleIndex
            {
                ItemId = article.ItemId,
                Title = article.Title?[..Math.Min(article.Title.Length, 255)],
            });
    }
}
