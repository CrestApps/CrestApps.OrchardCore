using CrestApps.Models;

namespace CrestApps.Mvc.Web.Models;

public sealed class Article : CatalogItem
{
    public string Title { get; set; }

    public string Description { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Author { get; set; }
}
