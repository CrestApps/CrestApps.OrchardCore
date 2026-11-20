using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// What the line items editor offers beside the lines: the tax categories a line can take, and the product picker's
/// addresses where the Products feature is on (found by route name, so Invoicing uses none of its types).
/// </summary>
public sealed class DocumentLinesEditorContext
{
    // The Products feature's routes: its product search and a product as a document line.
    private const string ProductSearchRouteName = "CrestApps.Products.ProductSearch";
    private const string ProductLineRouteName = "CrestApps.Products.ProductLine";

    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DocumentLinesEditorContext(IServiceProvider serviceProvider, IHttpContextAccessor httpContextAccessor)
    {
        _serviceProvider = serviceProvider;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Returns the tax categories a line can take, by code, the selected one first kept even when it is no longer listed.
    /// </summary>
    /// <param name="selected">The codes the lines already have.</param>
    public async Task<IReadOnlyList<SelectListItem>> GetTaxCategoriesAsync(IEnumerable<string> selected)
    {
        var items = new List<SelectListItem>();

        if (_serviceProvider.GetService<INamedCatalog<TaxCategory>>() is { } categories)
        {
            foreach (var category in (await categories.GetAllAsync()).Where(category => !string.IsNullOrEmpty(category.Code)).OrderBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                items.Add(new SelectListItem(string.IsNullOrEmpty(category.Name) ? category.Code : category.Name, category.Code));
            }
        }

        foreach (var code in (selected ?? []).Where(code => !string.IsNullOrEmpty(code)).Distinct(StringComparer.Ordinal))
        {
            if (!items.Exists(item => string.Equals(item.Value, code, StringComparison.Ordinal)))
            {
                items.Add(new SelectListItem(code, code));
            }
        }

        return items;
    }

    /// <summary>
    /// Gets the address of the product search, or <see langword="null"/> without the Products feature.
    /// </summary>
    public string GetProductSearchUrl()
        => GetPath(ProductSearchRouteName, null);

    /// <summary>
    /// Gets the address of a product as a document line, with <c>__id__</c> for the product's id, or
    /// <see langword="null"/> without the Products feature.
    /// </summary>
    public string GetProductLineUrl()
        => GetPath(ProductLineRouteName, new { contentItemId = "__id__" });

    private string GetPath(string routeName, object values)
    {
        if (_httpContextAccessor.HttpContext is not { } httpContext ||
            _serviceProvider.GetService<LinkGenerator>() is not { } links)
        {
            return null;
        }

        try
        {
            return links.GetPathByName(httpContext, routeName, values);
        }
        catch (InvalidOperationException)
        {
            // No such route: the feature that maps it is off.
            return null;
        }
    }
}
