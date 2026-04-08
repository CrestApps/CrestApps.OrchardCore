namespace CrestApps.Core.Mvc.Web.Areas.Admin.ViewModels;

public sealed class ArticleIndexViewModel
{
    public IReadOnlyList<ArticleListEntry> Articles { get; set; } = [];

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;

    public int TotalCount { get; set; }

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 1;
}

public sealed class ArticleListEntry
{
    public string ItemId { get; set; }

    public string Title { get; set; }

    public DateTime CreatedUtc { get; set; }
}
