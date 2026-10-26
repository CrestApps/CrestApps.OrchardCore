using System.Security.Claims;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Services;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.Products.Reports.DataSources;

/// <summary>
/// Decides which products a principal may read in reports, the way the content screens decide it: a product type is
/// readable when the principal may view its content items, and the latest version of its products, drafts included,
/// is read when the principal may also edit them. The answer is kept for the rest of the request.
/// </summary>
public sealed class ProductReportAccess
{
    private readonly ProductContentTypes _contentTypes;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly Dictionary<ClaimsPrincipal, ProductTypeAccess> _cache = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductReportAccess"/> class.
    /// </summary>
    /// <param name="contentTypes">The content types that are products.</param>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public ProductReportAccess(
        ProductContentTypes contentTypes,
        IContentDefinitionManager contentDefinitionManager,
        IAuthorizationService authorizationService)
    {
        _contentTypes = contentTypes;
        _contentDefinitionManager = contentDefinitionManager;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Gets the product types a principal may read, and which version of their products it reads.
    /// </summary>
    /// <param name="user">The principal, or <see langword="null"/>.</param>
    /// <returns>The access of the principal; <see cref="ProductTypeAccess.None"/> without a principal.</returns>
    public async Task<ProductTypeAccess> GetAsync(ClaimsPrincipal user)
    {
        if (user is null)
        {
            return ProductTypeAccess.None;
        }

        if (_cache.TryGetValue(user, out var cached))
        {
            return cached;
        }

        var latest = new List<string>();
        var published = new List<string>();
        var settings = new Dictionary<string, ProductPartSettings>(StringComparer.Ordinal);

        foreach (var contentType in await _contentTypes.GetProductTypesAsync(_contentDefinitionManager))
        {
            var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentType);

            if (definition is null || !await _authorizationService.AuthorizeContentTypeAsync(user, CommonPermissions.ViewContent, definition))
            {
                continue;
            }

            if (await _authorizationService.AuthorizeContentTypeAsync(user, CommonPermissions.EditContent, definition))
            {
                latest.Add(contentType);
            }
            else
            {
                published.Add(contentType);
            }

            settings[contentType] = definition.Parts
                .FirstOrDefault(part => part.PartDefinition.Name == nameof(ProductPart))?
                .GetSettings<ProductPartSettings>();
        }

        var access = new ProductTypeAccess(latest, published, settings);

        _cache[user] = access;

        return access;
    }
}
