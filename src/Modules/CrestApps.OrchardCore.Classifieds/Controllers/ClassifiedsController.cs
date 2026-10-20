using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Classifieds.Models;
using CrestApps.OrchardCore.Classifieds.Services;
using CrestApps.OrchardCore.Classifieds.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Records;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Classifieds.Controllers;

/// <summary>
/// The storefront's listing pages: posting a listing, editing, renewing and upgrading one's own, and finding listings,
/// near a place when one is given.
/// </summary>
public sealed class ClassifiedsController : Controller
{
    private readonly ClassifiedService _classifieds;
    private readonly IContentManager _contentManager;
    private readonly IContentItemDisplayManager _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShapeFactory _shapeFactory;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public ClassifiedsController(
        ClassifiedService classifieds,
        IContentManager contentManager,
        IContentItemDisplayManager displayManager,
        IUpdateModelAccessor updateModelAccessor,
        IAuthorizationService authorizationService,
        IShapeFactory shapeFactory,
        INotifier notifier,
        IHtmlLocalizer<ClassifiedsController> htmlLocalizer)
    {
        _classifieds = classifieds;
        _contentManager = contentManager;
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        _authorizationService = authorizationService;
        _shapeFactory = shapeFactory;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    [HttpGet("listings/post/{contentType}", Name = "ClassifiedPost")]
    public async Task<IActionResult> Post(string contentType)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        if (!await _authorizationService.AuthorizeAsync(User, ClassifiedsPermissions.PostClassifieds))
        {
            return Forbid();
        }

        if (await _classifieds.GetSettingsAsync(contentType) is not { } settings)
        {
            return NotFound();
        }

        var item = await _contentManager.NewAsync(contentType);

        return View("Post", await BuildFormAsync(item, settings, await _displayManager.BuildEditorAsync(item, _updateModelAccessor.ModelUpdater, isNew: true)));
    }

    [HttpPost("listings/post/{contentType}")]
    [ValidateAntiForgeryToken]
    [ActionName(nameof(Post))]
    public async Task<IActionResult> PostPost(string contentType)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        if (!await _authorizationService.AuthorizeAsync(User, ClassifiedsPermissions.PostClassifieds))
        {
            return Forbid();
        }

        if (await _classifieds.GetSettingsAsync(contentType) is not { } settings)
        {
            return NotFound();
        }

        var item = await _contentManager.NewAsync(contentType);
        var editor = await _displayManager.UpdateEditorAsync(item, _updateModelAccessor.ModelUpdater, isNew: true);

        if (!ModelState.IsValid)
        {
            return View("Post", await BuildFormAsync(item, settings, editor));
        }

        item.Owner = User.FindFirstValue(ClaimTypes.NameIdentifier);
        item.Author = User.Identity.Name;

        await _classifieds.PostAsync(item);
        await _notifier.SuccessAsync(settings.RequireApproval
            ? H["Thanks: your listing was sent for approval. You'll see it under My listings."]
            : H["Your listing is up."]);

        return RedirectToRoute("ClassifiedsOwn");
    }

    [HttpGet("listings/edit/{contentItemId}", Name = "ClassifiedEdit")]
    public async Task<IActionResult> Edit(string contentItemId)
    {
        if (await GetOwnAsync(contentItemId) is not { } item)
        {
            return NotFound();
        }

        var settings = await _classifieds.GetSettingsAsync(item.ContentType) ?? new ClassifiedPartSettings();

        return View("Post", await BuildFormAsync(item, settings, await _displayManager.BuildEditorAsync(item, _updateModelAccessor.ModelUpdater, isNew: false)));
    }

    [HttpPost("listings/edit/{contentItemId}")]
    [ValidateAntiForgeryToken]
    [ActionName(nameof(Edit))]
    public async Task<IActionResult> EditPost(string contentItemId)
    {
        if (await GetOwnAsync(contentItemId) is not { } item)
        {
            return NotFound();
        }

        var settings = await _classifieds.GetSettingsAsync(item.ContentType) ?? new ClassifiedPartSettings();
        var editor = await _displayManager.UpdateEditorAsync(item, _updateModelAccessor.ModelUpdater, isNew: false);

        if (!ModelState.IsValid)
        {
            return View("Post", await BuildFormAsync(item, settings, editor));
        }

        await _classifieds.ChangedAsync(item);
        await _notifier.SuccessAsync(settings.RequireApproval ? H["Your changes were sent for approval."] : H["Your listing was saved."]);

        return RedirectToRoute("ClassifiedsOwn");
    }

    [Authorize]
    [HttpGet("account/listings", Name = "ClassifiedsOwn")]
    public async Task<IActionResult> Own([FromServices] ISession session, [FromServices] IContentDefinitionManager contentDefinitionManager)
    {
        var listings = await _classifieds.ListOwnAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.RequestAborted);
        var types = await _classifieds.ListTypesAsync();
        var upgradeTypes = (await contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(type => type.Parts.Any(part => part.PartDefinition.Name == nameof(ClassifiedUpgradePart)))
            .Select(type => type.Name)
            .ToArray();
        var upgrades = upgradeTypes.Length == 0
            ? []
            : await session.Query<ContentItem, ContentItemIndex>(index => index.Published && index.ContentType.IsIn(upgradeTypes)).Take(20).ListAsync(HttpContext.RequestAborted);

        return View("Own", await _shapeFactory.CreateAsync<ClassifiedOwnViewModel>("ClassifiedsOwn", model =>
        {
            model.Listings = [.. listings];
            model.Types = [.. types.Select(type => (type.ContentType, type.DisplayName))];
            model.Upgrades = [.. upgrades.Select(item => (item.ContentItemId, item.DisplayText, item.TryGet<ClassifiedUpgradePart>(out var upgrade) ? upgrade : new ClassifiedUpgradePart()))];
        }));
    }

    [Authorize]
    [HttpPost("account/listings/{contentItemId}/renew", Name = "ClassifiedRenew")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Renew(string contentItemId)
    {
        if (await GetOwnAsync(contentItemId) is not { } item)
        {
            return NotFound();
        }

        await _notifier.SuccessAsync(await _classifieds.RenewAsync(item) ? H["Your listing runs for another term."] : H["Only a listing that's up or expired can be renewed."]);

        return RedirectToRoute("ClassifiedsOwn");
    }

    [Authorize]
    [HttpPost("account/listings/{contentItemId}/upgrade/{productId}", Name = "ClassifiedUpgrade")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upgrade(string contentItemId, string productId, [FromServices] ICartService cartService)
    {
        if (await GetOwnAsync(contentItemId) is null)
        {
            return NotFound();
        }

        var result = await cartService.AddAsync(productId, priceId: null, variantId: null, quantity: 1, addOns: null, details: new Dictionary<string, string> { [ClassifiedsConstants.ListingDetail] = contentItemId }, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            await _notifier.ErrorAsync(H["{0}", result.Message.Value]);

            return RedirectToRoute("ClassifiedsOwn");
        }

        return RedirectToRoute("Cart");
    }

    [HttpGet("listings", Name = "ClassifiedsSearch")]
    public async Task<IActionResult> Search(string type, string q, string lat, string lng, int km = 25)
    {
        var types = await _classifieds.ListTypesAsync();
        double? latitude = double.TryParse(lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLatitude) ? parsedLatitude : null;
        double? longitude = double.TryParse(lng, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLongitude) ? parsedLongitude : null;
        var radius = Math.Clamp(km, 1, 500);
        var results = await _classifieds.SearchAsync(types.Any(entry => entry.ContentType == type) ? type : null, q, latitude, longitude, radius, 50, HttpContext.RequestAborted);
        var rows = new List<(ContentItem, double?, string)>();

        foreach (var (item, kilometers) in results)
        {
            var metadata = await _contentManager.PopulateAspectAsync<ContentItemMetadata>(item);

            rows.Add((item, kilometers, metadata.DisplayRouteValues is null ? null : Url.RouteUrl(new RouteValueDictionary(metadata.DisplayRouteValues))));
        }

        return View(await _shapeFactory.CreateAsync<ClassifiedSearchViewModel>("ClassifiedsSearch", model =>
        {
            model.ContentType = type;
            model.Text = q;
            model.Latitude = lat;
            model.Longitude = lng;
            model.Kilometers = radius;
            model.Types = [.. types.Select(entry => (entry.ContentType, entry.DisplayName))];
            model.Results = rows;
            model.Searched = true;
        }));
    }

    private async Task<IShape> BuildFormAsync(ContentItem item, ClassifiedPartSettings settings, IShape editor)
        => await _shapeFactory.CreateAsync<ClassifiedPostViewModel>("ClassifiedPost", model =>
        {
            model.ContentType = item.ContentType;
            model.DisplayName = item.ContentType;
            model.ContentItemId = item.Id == 0 ? null : item.ContentItemId;
            model.Editor = editor;
            model.RequireApproval = settings.RequireApproval;
        });

    // The latest version of one of the signed-in user's listings.
    private async Task<ContentItem> GetOwnAsync(string contentItemId)
    {
        if (User.Identity?.IsAuthenticated != true || string.IsNullOrEmpty(contentItemId))
        {
            return null;
        }

        var item = await _contentManager.GetAsync(contentItemId, VersionOptions.Latest);

        return item is not null && item.Has<ClassifiedPart>() && string.Equals(item.Owner, User.FindFirstValue(ClaimTypes.NameIdentifier), StringComparison.Ordinal) ? item : null;
    }
}
