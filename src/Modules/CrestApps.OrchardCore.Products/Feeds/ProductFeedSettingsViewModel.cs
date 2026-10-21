using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Products.Feeds;

public class ProductFeedSettingsViewModel
{
    public string DefaultBrand { get; set; }

    public string DefaultCondition { get; set; } = "new";

    public string DefaultGoogleProductCategory { get; set; }

    public bool IncludeOutOfStock { get; set; } = true;

    /// <summary>
    /// Gets or sets <c>new</c> to make a new key, <c>remove</c> to make the feed public, or none to keep it.
    /// </summary>
    public string KeyAction { get; set; }

    [BindNever]
    public bool HasKey { get; set; }

    [BindNever]
    public bool HasBaseUrl { get; set; }

    [BindNever]
    public string XmlUrl { get; set; }

    [BindNever]
    public string CsvUrl { get; set; }

    [BindNever]
    public ProductFeed Feed { get; set; }
}
