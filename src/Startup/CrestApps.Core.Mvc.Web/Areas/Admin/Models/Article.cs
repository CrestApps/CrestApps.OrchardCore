using CrestApps.Core.Models;

namespace CrestApps.Core.Mvc.Web.Areas.Admin.Models;

public sealed class Article : CatalogItem
{
    public string Title { get; set; }

    public string Description { get; set; }

    public DateTime CreatedUtc { get; set; }
}
