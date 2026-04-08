namespace CrestApps.Core.Mvc.Web.Areas.Admin.ViewModels;

public sealed class PagerViewModel
{
    public int Page { get; set; } = 1;

    public int TotalPages { get; set; } = 1;

    public int TotalCount { get; set; }

    public string Action { get; set; } = "Index";

    public string Controller { get; set; }

    public string Area { get; set; }
}
