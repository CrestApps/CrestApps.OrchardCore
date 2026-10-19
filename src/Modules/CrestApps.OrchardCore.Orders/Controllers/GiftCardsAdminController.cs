using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Orders.ViewModels;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders.Controllers;

/// <summary>
/// Commerce › Gift cards: issue cards, see what's left on them and how they were spent, and change their balances.
/// </summary>
[Feature(OrderConstants.Features.GiftCards)]
[Admin("gift-cards/{action}/{itemId?}", "GiftCards{action}")]
public sealed class GiftCardsAdminController : Controller
{
    private const string _optionsSearch = "Options.Search";
    private const string _optionsStatus = "Options.Status";

    private readonly IGiftCardStore _store;
    private readonly GiftCardService _giftCards;
    private readonly GiftCardEmailSender _emailSender;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISiteService _siteService;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public GiftCardsAdminController(
        IGiftCardStore store,
        GiftCardService giftCards,
        GiftCardEmailSender emailSender,
        IAuthorizationService authorizationService,
        ISiteService siteService,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<GiftCardsAdminController> htmlLocalizer,
        IStringLocalizer<GiftCardsAdminController> stringLocalizer)
    {
        _store = store;
        _giftCards = giftCards;
        _emailSender = emailSender;
        _authorizationService = authorizationService;
        _siteService = siteService;
        _notifier = notifier;
        _clock = clock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("gift-cards", "GiftCardsIndex")]
    public async Task<IActionResult> Index(
        GiftCardsIndexOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OrderPermissions.ManageGiftCards))
        {
            return Forbid();
        }

        options ??= new GiftCardsIndexOptions();

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, cards) = await _store.PageAsync(new GiftCardQuery { Search = options.Search, Status = options.Status }, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);

        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        if (options.Status != GiftCardStatusFilter.All)
        {
            routeData.Values.TryAdd(_optionsStatus, options.Status);
        }

        options.Statuses =
        [
            new SelectListItem(S["Any status"], nameof(GiftCardStatusFilter.All), options.Status == GiftCardStatusFilter.All),
            new SelectListItem(S["Active"], nameof(GiftCardStatusFilter.Active), options.Status == GiftCardStatusFilter.Active),
            new SelectListItem(S["Used up"], nameof(GiftCardStatusFilter.UsedUp), options.Status == GiftCardStatusFilter.UsedUp),
            new SelectListItem(S["Expired"], nameof(GiftCardStatusFilter.Expired), options.Status == GiftCardStatusFilter.Expired),
            new SelectListItem(S["Disabled"], nameof(GiftCardStatusFilter.Disabled), options.Status == GiftCardStatusFilter.Disabled),
        ];

        var pagerShape = await shapeFactory.PagerAsync(pager, count, routeData);

        var model = new GiftCardsIndexViewModel
        {
            Options = options,
            Pager = pagerShape,
            List = new AdminListContext<GiftCard>
            {
                Source = cards,
                Pager = (IShape)pagerShape,
                ToolbarActions = await shapeFactory.CreateAsync("GiftCardsAdminListFilters", Arguments.From(new { Options = options })),
            },
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("gift-cards", "GiftCardsIndex")]
    public async Task<IActionResult> IndexFilterPost(GiftCardsIndexOptions options, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OrderPermissions.ManageGiftCards))
        {
            return Forbid();
        }

        var routeValues = new RouteValueDictionary();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeValues.TryAdd(_optionsSearch, options.Search);
        }

        if (options.Status != GiftCardStatusFilter.All)
        {
            routeValues.TryAdd(_optionsStatus, options.Status);
        }

        if (pagerParameters.PageSize.HasValue)
        {
            routeValues.TryAdd("pageSize", pagerParameters.PageSize.Value);
        }

        return RedirectToAction(nameof(Index), routeValues);
    }

    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, OrderPermissions.ManageGiftCards))
        {
            return Forbid();
        }

        var settings = await _siteService.GetSettingsAsync<CheckoutSettings>();

        return View(new GiftCardCreateViewModel { Currency = settings.Currency });
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    public async Task<IActionResult> CreatePost(GiftCardCreateViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OrderPermissions.ManageGiftCards))
        {
            return Forbid();
        }

        if (model.Value is not > 0m)
        {
            ModelState.AddModelError(nameof(model.Value), S["Enter an amount greater than zero."]);
        }

        if (string.IsNullOrWhiteSpace(model.Currency) || model.Currency.Trim().Length != 3)
        {
            ModelState.AddModelError(nameof(model.Currency), S["Enter the three-letter currency code, such as USD."]);
        }

        if (!string.IsNullOrWhiteSpace(model.Code) && GiftCardCodes.Normalize(model.Code)?.Length is < 8 or > 32)
        {
            ModelState.AddModelError(nameof(model.Code), S["A code is 8 to 32 letters and digits. Leave it blank to have one made up."]);
        }

        if (model.ExpiresUtc.HasValue && model.ExpiresUtc.Value <= _clock.UtcNow)
        {
            ModelState.AddModelError(nameof(model.ExpiresUtc), S["The expiry date must be in the future."]);
        }

        if (model.SendEmail && string.IsNullOrWhiteSpace(model.RecipientEmail))
        {
            ModelState.AddModelError(nameof(model.RecipientEmail), S["Enter the email to send the card to, or clear Email the card."]);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var card = await _giftCards.IssueAsync(
            model.Value.Value,
            model.Currency,
            S["Issued"],
            card =>
            {
                card.ExpiresUtc = model.ExpiresUtc;
                card.RecipientName = TrimToNull(model.RecipientName);
                card.RecipientEmail = TrimToNull(model.RecipientEmail);
                card.Note = TrimToNull(model.Note);
            },
            model.Code,
            User.Identity?.Name,
            HttpContext.RequestAborted);

        if (card is null)
        {
            ModelState.AddModelError(nameof(model.Code), S["That code is already in use."]);

            return View(model);
        }

        if (model.SendEmail)
        {
            if (await _emailSender.SendAsync(card, TrimToNull(model.Message), HttpContext.RequestAborted))
            {
                await _notifier.SuccessAsync(H["The gift card was created and emailed to {0}.", card.RecipientEmail]);
            }
            else
            {
                await _notifier.WarningAsync(H["The gift card was created, but it couldn't be emailed. Check the email settings, then email it from its page."]);
            }
        }
        else
        {
            await _notifier.SuccessAsync(H["The gift card was created."]);
        }

        return RedirectToAction(nameof(Edit), new { itemId = card.ItemId });
    }

    public async Task<IActionResult> Edit(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OrderPermissions.ManageGiftCards))
        {
            return Forbid();
        }

        var card = string.IsNullOrEmpty(itemId) ? null : await _store.FindByIdAsync(itemId);

        if (card is null)
        {
            return NotFound();
        }

        return View(ToModel(card));
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    public async Task<IActionResult> EditPost(string itemId, GiftCardEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OrderPermissions.ManageGiftCards))
        {
            return Forbid();
        }

        var card = string.IsNullOrEmpty(itemId) ? null : await _store.FindByIdAsync(itemId);

        if (card is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            var invalid = ToModel(card);
            invalid.IsEnabled = model.IsEnabled;
            invalid.ExpiresUtc = model.ExpiresUtc;
            invalid.RecipientName = model.RecipientName;
            invalid.RecipientEmail = model.RecipientEmail;
            invalid.Note = model.Note;

            return View(invalid);
        }

        card.IsEnabled = model.IsEnabled;
        card.ExpiresUtc = model.ExpiresUtc;
        card.RecipientName = TrimToNull(model.RecipientName);
        card.RecipientEmail = TrimToNull(model.RecipientEmail);
        card.Note = TrimToNull(model.Note);

        await _store.UpdateAsync(card);
        await _notifier.SuccessAsync(H["The gift card was updated."]);

        return RedirectToAction(nameof(Edit), new { itemId });
    }

    /// <summary>
    /// Adds to the card's balance, or takes off it with a negative amount.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Adjust(string itemId, decimal? amount, string reason)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OrderPermissions.ManageGiftCards))
        {
            return Forbid();
        }

        var card = string.IsNullOrEmpty(itemId) ? null : await _store.FindByIdAsync(itemId);

        if (card is null)
        {
            return NotFound();
        }

        if (amount is null or 0m)
        {
            await _notifier.ErrorAsync(H["Enter an amount to add, or a negative amount to take off."]);
        }
        else if (string.IsNullOrWhiteSpace(reason))
        {
            await _notifier.ErrorAsync(H["Say why the balance changes. It's kept with the card's activity."]);
        }
        else if (await _giftCards.AdjustAsync(itemId, amount.Value, reason.Trim(), User.Identity?.Name, HttpContext.RequestAborted) is { } adjusted)
        {
            await _notifier.SuccessAsync(H["The balance is now {0}.", ProductPriceFormatter.Format(adjusted.Balance, adjusted.Currency)]);
        }
        else
        {
            await _notifier.ErrorAsync(H["The card is being used at a checkout right now. Try again in a moment."]);
        }

        return RedirectToAction(nameof(Edit), new { itemId });
    }

    /// <summary>
    /// Emails the card to its recipient again.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Email(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OrderPermissions.ManageGiftCards))
        {
            return Forbid();
        }

        var card = string.IsNullOrEmpty(itemId) ? null : await _store.FindByIdAsync(itemId);

        if (card is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(card.RecipientEmail))
        {
            await _notifier.ErrorAsync(H["Enter the recipient's email and save first."]);
        }
        else if (await _emailSender.SendAsync(card, message: null, HttpContext.RequestAborted))
        {
            await _notifier.SuccessAsync(H["The gift card was emailed to {0}.", card.RecipientEmail]);
        }
        else
        {
            await _notifier.ErrorAsync(H["The gift card couldn't be emailed. Check the email settings."]);
        }

        return RedirectToAction(nameof(Edit), new { itemId });
    }

    private static GiftCardEditViewModel ToModel(GiftCard card)
        => new()
        {
            ItemId = card.ItemId,
            Code = card.Code,
            InitialValue = card.InitialValue,
            Balance = card.Balance,
            Currency = card.Currency,
            OrderId = card.OrderId,
            OrderNumber = card.OrderNumber,
            CreatedUtc = card.CreatedUtc,
            Activity = [.. (card.Activity ?? []).OrderByDescending(activity => activity.CreatedUtc)],
            IsEnabled = card.IsEnabled,
            ExpiresUtc = card.ExpiresUtc,
            RecipientName = card.RecipientName,
            RecipientEmail = card.RecipientEmail,
            Note = card.Note,
        };

    private static string TrimToNull(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
