using System.Text;
using CrestApps.OrchardCore.Products.Core.Transfer;
using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Products.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Contents;
using OrchardCore.Modules;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Products.Controllers;

/// <summary>
/// Imports products from a Shopify or WooCommerce product export, and exports a product type's products in
/// Shopify's format.
/// </summary>
[Admin]
public sealed class ProductTransferController : Controller
{
    // A product export with thousands of products is a few megabytes.
    private const long MaxFileBytes = 20 * 1024 * 1024;

    private readonly ProductCatalogTransfer _transfer;
    private readonly ProductContentTypes _contentTypes;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISession _session;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public ProductTransferController(
        ProductCatalogTransfer transfer,
        ProductContentTypes contentTypes,
        IContentDefinitionManager contentDefinitionManager,
        IAuthorizationService authorizationService,
        ISession session,
        IClock clock,
        IStringLocalizer<ProductTransferController> stringLocalizer)
    {
        _transfer = transfer;
        _contentTypes = contentTypes;
        _contentDefinitionManager = contentDefinitionManager;
        _authorizationService = authorizationService;
        _session = session;
        _clock = clock;
        S = stringLocalizer;
    }

    [Admin("products/transfer", "ProductTransfer")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent))
        {
            return Forbid();
        }

        return View(await BuildAsync(new ProductTransferViewModel()));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxFileBytes + 1024 * 1024)]
    [Admin("products/transfer", "ProductTransfer")]
    public async Task<IActionResult> Index(ProductTransferViewModel model, IFormFile file)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent))
        {
            return Forbid();
        }

        model = await BuildAsync(model ?? new ProductTransferViewModel());

        if (!model.ContentTypes.Any(type => type.Value == model.ContentType))
        {
            ModelState.AddModelError(nameof(model.ContentType), S["Choose the product type to import into."]);
        }

        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError(nameof(file), S["Choose a CSV file to import."]);
        }
        else if (file.Length > MaxFileBytes)
        {
            ModelState.AddModelError(nameof(file), S["The file is larger than 20 MB. Split it, then import each part."]);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string text;

        using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            text = await reader.ReadToEndAsync(HttpContext.RequestAborted);
        }

        var read = ProductCsv.Read(text);
        var result = read.Format is null
            ? new ProductImportResult()
            : await _transfer.ImportAsync(read.Products, new ProductImportOptions
            {
                ContentType = model.ContentType,
                UpdateExisting = model.UpdateExisting,
                Publish = model.Publish,
            }, HttpContext.RequestAborted);

        model.Format = read.Format;
        model.Result = result;
        model.Problems = [.. read.Problems.Concat(result.Problems)];

        return View(model);
    }

    [Admin("products/transfer/export", "ProductTransferExport")]
    public async Task<IActionResult> Export(string contentType)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent))
        {
            return Forbid();
        }

        var types = await _contentTypes.GetProductTypesAsync(_contentDefinitionManager);

        if (!types.Contains(contentType))
        {
            return NotFound();
        }

        var csv = await _transfer.ExportAsync(contentType, HttpContext.RequestAborted);

        // A byte order mark, so a spreadsheet reads the names and descriptions as UTF-8.
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();

        return File(bytes, "text/csv", $"products-{contentType.ToLowerInvariant()}-{_clock.UtcNow:yyyy-MM-dd}.csv");
    }

    private async Task<ProductTransferViewModel> BuildAsync(ProductTransferViewModel model)
    {
        var types = await _contentTypes.GetProductTypesAsync(_contentDefinitionManager);
        var options = new List<SelectListItem>();

        foreach (var name in types)
        {
            var type = await _contentDefinitionManager.GetTypeDefinitionAsync(name);
            options.Add(new SelectListItem(type?.DisplayName ?? name, name));
        }

        model.ContentTypes = [.. options.OrderBy(option => option.Text, StringComparer.CurrentCultureIgnoreCase)];
        model.ContentType ??= model.ContentTypes.FirstOrDefault()?.Value;

        return model;
    }
}
