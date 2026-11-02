using CrestApps.OrchardCore.Assets.Core.Indexes;
using CrestApps.OrchardCore.Assets.Core.Services;
using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Assets.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using YesSql;

namespace CrestApps.OrchardCore.Assets.Controllers;

/// <summary>
/// Asset types: their category, attributes and meters.
/// </summary>
public sealed class AssetTypesController : Controller
{
    private const int BlankRows = 2;

    private readonly IAuthorizationService _authorizationService;
    private readonly AssetTypeManager _manager;
    private readonly ISession _session;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetTypesController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="manager">The asset type manager.</param>
    /// <param name="session">The session.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public AssetTypesController(IAuthorizationService authorizationService, AssetTypeManager manager, ISession session, INotifier notifier, IHtmlLocalizer<AssetTypesController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _manager = manager;
        _session = session;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the asset types.
    /// </summary>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The page.</returns>
    [Admin("assets/types", "AssetTypes")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AssetsPermissions.ManageAssets))
        {
            return Forbid();
        }

        var counts = (await _session.QueryIndex<AssetIndex>(collection: AssetsConstants.CollectionName).ListAsync(HttpContext.RequestAborted))
            .GroupBy(index => index.AssetTypeId ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var items = (await _manager.GetAllAsync(HttpContext.RequestAborted)).OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var pager = new Pager(pagerParameters, pagerOptions.Value);

        return View(new AssetsListViewModel
        {
            List = new AdminListContext<AssetTypeRow>
            {
                Source = items.Skip(pager.GetStartIndex()).Take(pager.PageSize).Select(item => new AssetTypeRow { Type = item, AssetCount = counts.GetValueOrDefault(item.ItemId) }).ToList(),
                Pager = await shapeFactory.PagerAsync(pager, items.Count),
            },
        });
    }

    /// <summary>
    /// Shows the editor.
    /// </summary>
    /// <param name="id">The type, or <see langword="null"/> for a new one.</param>
    /// <returns>The page.</returns>
    [Admin("assets/types/edit/{id?}", "AssetTypesEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AssetsPermissions.ManageAssets))
        {
            return Forbid();
        }

        var item = string.IsNullOrEmpty(id) ? await _manager.NewAsync() : await _manager.FindByIdAsync(id);

        if (item is null)
        {
            return NotFound();
        }

        var model = new AssetTypeEditViewModel
        {
            Id = id,
            Name = item.Name,
            Category = item.Category,
            Description = item.Description,
            Attributes = [.. item.Attributes.Select(attribute => new AssetAttributeEditModel { Name = attribute.Name, Kind = attribute.Kind })],
            Meters = [.. item.Meters.Select(meter => new MeterEditModel { Name = meter.Name, Unit = meter.Unit, Cumulative = meter.Cumulative })],
        };

        AddBlankRows(model);

        return View(model);
    }

    /// <summary>
    /// Saves the editor.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <returns>The list, or the editor with its errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("assets/types/edit/{id?}", "AssetTypesEdit")]
    public async Task<IActionResult> EditPost(AssetTypeEditViewModel form)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AssetsPermissions.ManageAssets))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(form.Id);
        var item = isNew ? await _manager.NewAsync() : await _manager.FindByIdAsync(form.Id);

        if (item is null)
        {
            return NotFound();
        }

        item.Name = form.Name?.Trim();
        item.Category = form.Category;
        item.Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim();
        item.Attributes = [.. (form.Attributes ?? []).Where(row => !string.IsNullOrWhiteSpace(row.Name)).Select(row => new AssetAttributeDefinition { Name = row.Name, Kind = row.Kind })];
        item.Meters = [.. (form.Meters ?? []).Where(row => !string.IsNullOrWhiteSpace(row.Name)).Select(row => new MeterDefinition { Name = row.Name, Unit = row.Unit, Cumulative = row.Cumulative })];

        var validation = await _manager.ValidateAsync(item);

        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.MemberNames.FirstOrDefault() ?? string.Empty, error.ErrorMessage);
        }

        if (!ModelState.IsValid)
        {
            AddBlankRows(form);

            return View(form);
        }

        if (isNew)
        {
            await _manager.CreateAsync(item);
        }
        else
        {
            await _manager.UpdateAsync(item);
        }

        await _notifier.SuccessAsync(H["The asset type was saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes an asset type no asset uses.
    /// </summary>
    /// <param name="id">The type.</param>
    /// <returns>The list.</returns>
    [HttpPost]
    [Admin("assets/types/delete/{id}", "AssetTypesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AssetsPermissions.ManageAssets))
        {
            return Forbid();
        }

        var item = await _manager.FindByIdAsync(id);

        if (item is null)
        {
            return NotFound();
        }

        if (await _session.QueryIndex<AssetIndex>(index => index.AssetTypeId == id, collection: AssetsConstants.CollectionName).FirstOrDefaultAsync(HttpContext.RequestAborted) is not null)
        {
            await _notifier.WarningAsync(H["{0} can't be deleted: assets use it.", item.Name]);

            return RedirectToAction(nameof(Index));
        }

        await _manager.DeleteAsync(item);
        await _notifier.SuccessAsync(H["The asset type was deleted."]);

        return RedirectToAction(nameof(Index));
    }

    private static void AddBlankRows(AssetTypeEditViewModel model)
    {
        while (model.Attributes.Count(row => string.IsNullOrWhiteSpace(row.Name)) < BlankRows)
        {
            model.Attributes.Add(new AssetAttributeEditModel());
        }

        while (model.Meters.Count(row => string.IsNullOrWhiteSpace(row.Name)) < BlankRows)
        {
            model.Meters.Add(new MeterEditModel());
        }
    }
}
