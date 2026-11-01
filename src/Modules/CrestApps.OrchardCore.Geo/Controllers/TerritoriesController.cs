using CrestApps.OrchardCore.Geo.Core.Services;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using CrestApps.OrchardCore.Geo.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Geo.Controllers;

/// <summary>
/// Service territories: the list with its map and finder, and the editor with drawing.
/// </summary>
public sealed class TerritoriesController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ServiceTerritoryManager _manager;
    private readonly ITerritoryResolver _resolver;
    private readonly IGeoSettingsService _settingsService;
    private readonly IClock _clock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="TerritoriesController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="manager">The territory manager.</param>
    /// <param name="resolver">The territory resolver.</param>
    /// <param name="settingsService">The settings service.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public TerritoriesController(
        IAuthorizationService authorizationService,
        ServiceTerritoryManager manager,
        ITerritoryResolver resolver,
        IGeoSettingsService settingsService,
        IClock clock,
        INotifier notifier,
        IHtmlLocalizer<TerritoriesController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _manager = manager;
        _resolver = resolver;
        _settingsService = settingsService;
        _clock = clock;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the territories, draws them on a map, and finds the territory of a point or postal code.
    /// </summary>
    /// <param name="form">The finder.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The page.</returns>
    [Admin("geo/territories", "GeoTerritories")]
    public async Task<IActionResult> Index(TerritoryListViewModel form, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, GeoPermissions.ManageTerritories))
        {
            return Forbid();
        }

        var all = (await _manager.GetAllAsync(HttpContext.RequestAborted)).OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var names = all.ToDictionary(item => item.ItemId, item => item.Name, StringComparer.Ordinal);
        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var model = new TerritoryListViewModel
        {
            Latitude = form.Latitude,
            Longitude = form.Longitude,
            PostalCode = form.PostalCode,
            Map = await _settingsService.GetAsync(),
            Shapes = [.. all.Where(item => !string.IsNullOrEmpty(item.Shape)).Select(item => new TerritoryShape { Label = item.Name, GeoJson = item.Shape })],
            List = new AdminListContext<TerritoryRow>
            {
                Source = all
                    .Skip(pager.GetStartIndex())
                    .Take(pager.PageSize)
                    .Select(item => new TerritoryRow { Territory = item, ParentName = names.GetValueOrDefault(item.ParentId ?? string.Empty) })
                    .ToList(),
                Pager = await shapeFactory.PagerAsync(pager, all.Count),
            },
        };

        var point = GeoPoint.From(form.Latitude, form.Longitude);

        if (point.HasValue || !string.IsNullOrWhiteSpace(form.PostalCode))
        {
            model.Searched = true;
            model.Match = TerritoryMatcher.Match(all, point, form.PostalCode);
        }

        return View(model);
    }

    /// <summary>
    /// Shows the editor.
    /// </summary>
    /// <param name="id">The territory, or <see langword="null"/> for a new one.</param>
    /// <returns>The page.</returns>
    [Admin("geo/territories/edit/{id?}", "GeoTerritoriesEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, GeoPermissions.ManageTerritories))
        {
            return Forbid();
        }

        var item = string.IsNullOrEmpty(id) ? await _manager.NewAsync() : await _manager.FindByIdAsync(id);

        if (item is null)
        {
            return NotFound();
        }

        var model = new TerritoryEditViewModel
        {
            Id = id,
            Name = item.Name,
            ParentId = item.ParentId,
            Shape = item.Shape,
            PostalCodes = string.Join(", ", item.PostalCodes),
            TimeZoneId = item.TimeZoneId,
            TravelSpeedKmh = item.TravelSpeedKmh,
        };

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Saves the editor.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <returns>The list, or the editor with its errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("geo/territories/edit/{id?}", "GeoTerritoriesEdit")]
    public async Task<IActionResult> EditPost(TerritoryEditViewModel form)
    {
        if (!await _authorizationService.AuthorizeAsync(User, GeoPermissions.ManageTerritories))
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
        item.ParentId = string.IsNullOrEmpty(form.ParentId) ? null : form.ParentId;
        item.Shape = form.Shape;
        item.PostalCodes = [.. (form.PostalCodes ?? string.Empty).Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
        item.TimeZoneId = string.IsNullOrEmpty(form.TimeZoneId) ? null : form.TimeZoneId;
        item.TravelSpeedKmh = form.TravelSpeedKmh;

        var validation = await _manager.ValidateAsync(item);

        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.MemberNames.FirstOrDefault() ?? string.Empty, error.ErrorMessage);
        }

        if (!ModelState.IsValid)
        {
            await PopulateAsync(form);

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

        await _notifier.SuccessAsync(H["The territory was saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes a territory.
    /// </summary>
    /// <param name="id">The territory.</param>
    /// <returns>The list.</returns>
    [HttpPost]
    [Admin("geo/territories/delete/{id}", "GeoTerritoriesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, GeoPermissions.ManageTerritories))
        {
            return Forbid();
        }

        var item = await _manager.FindByIdAsync(id);

        if (item is null)
        {
            return NotFound();
        }

        var children = (await _manager.GetAllAsync(HttpContext.RequestAborted)).Where(other => other.ParentId == item.ItemId).ToList();

        foreach (var child in children)
        {
            child.ParentId = item.ParentId;
            await _manager.UpdateAsync(child);
        }

        await _manager.DeleteAsync(item);
        await _notifier.SuccessAsync(H["The territory was deleted."]);

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateAsync(TerritoryEditViewModel model)
    {
        var all = (await _manager.GetAllAsync(HttpContext.RequestAborted)).Where(item => item.ItemId != model.Id).OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

        model.Map = await _settingsService.GetAsync();
        model.ParentOptions = [.. all.Select(item => new SelectListItem(item.Name, item.ItemId, item.ItemId == model.ParentId))];
        model.OtherShapes = [.. all.Where(item => !string.IsNullOrEmpty(item.Shape)).Select(item => new TerritoryShape { Label = item.Name, GeoJson = item.Shape })];
        model.TimeZoneOptions = [.. _clock.GetTimeZones().Select(zone => new SelectListItem(zone.TimeZoneId, zone.TimeZoneId, zone.TimeZoneId == model.TimeZoneId))];
    }
}
