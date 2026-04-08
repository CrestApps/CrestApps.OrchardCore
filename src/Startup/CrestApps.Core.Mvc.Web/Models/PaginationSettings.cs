namespace CrestApps.Core.Mvc.Web.Models;

public sealed class PaginationSettings
{
    public const int DefaultPageSize = 25;

    public int AdminPageSize { get; set; } = DefaultPageSize;
}
