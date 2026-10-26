using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders.Dashboard;

/// <summary>
/// Reads the sales the widget shows, in the store's currency; orders in other currencies count in Orders only. The
/// widget keeps its figures for the minutes it is set to (30 by default), counts the orders of one channel or all of
/// them, and covers today and two windows of days it is set to.
/// </summary>
public sealed class SalesSummaryPartDisplayDriver : ContentPartDisplayDriver<SalesSummaryPart>
{
    private readonly OrderReportReader _reader;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;
    private readonly IMemoryCache _memoryCache;

    internal readonly IStringLocalizer S;

    public SalesSummaryPartDisplayDriver(
        OrderReportReader reader,
        ISiteService siteService,
        IClock clock,
        IMemoryCache memoryCache,
        IStringLocalizer<SalesSummaryPartDisplayDriver> stringLocalizer)
    {
        _reader = reader;
        _siteService = siteService;
        _clock = clock;
        _memoryCache = memoryCache;
        S = stringLocalizer;
    }

    public override IDisplayResult Display(SalesSummaryPart part, BuildPartDisplayContext context)
        => Initialize<SalesSummaryViewModel>(GetDisplayShapeType(context), async model =>
        {
            var shortDays = Math.Clamp(part.ShortDays, 1, 365);
            var longDays = Math.Clamp(Math.Max(part.LongDays, shortDays), 1, 365);
            var minutes = Math.Clamp(part.CacheMinutes, 0, 24 * 60);
            var key = $"CrestApps.SalesSummary:{part.ContentItem.ContentItemId}:{part.ContentItem.ModifiedUtc?.Ticks}";

            if (minutes > 0 && _memoryCache.TryGetValue(key, out SalesSummaryViewModel cached))
            {
                cached.CopyTo(model);

                return;
            }

            await FillAsync(part, model, shortDays, longDays);

            if (minutes > 0)
            {
                _memoryCache.Set(key, model.Clone(), TimeSpan.FromMinutes(minutes));
            }
        }).Location("Detail", "Content").Location("DetailAdmin", "Content");

    public override IDisplayResult Edit(SalesSummaryPart part, BuildPartEditorContext context)
        => Initialize<SalesSummaryEditViewModel>(GetEditorShapeType(context), model =>
        {
            model.CacheMinutes = part.CacheMinutes;
            model.ShortDays = part.ShortDays;
            model.LongDays = part.LongDays;
            model.Channel = part.Channel;
            model.Channels =
            [
                new SelectListItem(S["All channels"], string.Empty),
                new SelectListItem(S["Online orders"], OrderChannels.Online),
                new SelectListItem(S["Point of sale"], OrderChannels.PointOfSale),
                new SelectListItem(S["Orders made by staff"], OrderChannels.Draft),
            ];
        });

    public override async Task<IDisplayResult> UpdateAsync(SalesSummaryPart part, UpdatePartEditorContext context)
    {
        var model = new SalesSummaryEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.ShortDays is < 1 or > 365 || model.LongDays is < 1 or > 365)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ShortDays), S["Cover between 1 and 365 days."]);
        }

        if (model.CacheMinutes is < 0 or > 24 * 60)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.CacheMinutes), S["Keep the figures between 0 (don't) and 1440 minutes (a day)."]);
        }

        part.ShortDays = Math.Clamp(model.ShortDays, 1, 365);
        part.LongDays = Math.Clamp(model.LongDays, 1, 365);
        part.CacheMinutes = Math.Clamp(model.CacheMinutes, 0, 24 * 60);
        part.Channel = model.Channel is OrderChannels.Online or OrderChannels.PointOfSale or OrderChannels.Draft ? model.Channel : null;

        return await EditAsync(part, context);
    }

    private async Task FillAsync(SalesSummaryPart part, SalesSummaryViewModel model, int shortDays, int longDays)
    {
        var now = _clock.UtcNow;
        var site = await _siteService.GetSiteSettingsAsync();
        var timeZone = !string.IsNullOrEmpty(site.TimeZoneId) && TimeZoneInfo.TryFindSystemTimeZoneById(site.TimeZoneId, out var zone) ? zone : TimeZoneInfo.Utc;
        var today = TimeZoneInfo.ConvertTimeToUtc(TimeZoneInfo.ConvertTimeFromUtc(now, timeZone).Date, timeZone);
        var currency = (await _siteService.GetSettingsAsync<Checkout.Models.CheckoutSettings>()).Currency;

        model.Currency = currency;
        model.ShortDays = shortDays;
        model.LongDays = longDays;

        await foreach (var order in _reader.ReadPaidAsync(now.AddDays(-longDays), now))
        {
            if (!InChannel(order, part.Channel))
            {
                continue;
            }

            var paid = order.PaidUtc ?? order.CreatedUtc;
            var inCurrency = string.Equals(order.Currency, currency, StringComparison.OrdinalIgnoreCase);
            var amount = inCurrency ? (order.Total ?? order.Subtotal) - order.RefundedAmount : 0m;

            model.LongWindow.Add(amount);

            if (paid >= now.AddDays(-shortDays))
            {
                model.ShortWindow.Add(amount);
            }

            if (paid >= today)
            {
                model.Today.Add(amount);
            }
        }

        await foreach (var order in _reader.ReadUnfulfilledAsync())
        {
            if (InChannel(order, part.Channel))
            {
                model.ToShip++;
            }
        }
    }

    // An order with no channel was made online, before orders recorded where they came from.
    internal static bool InChannel(Order order, string channel)
        => string.IsNullOrEmpty(channel) || string.Equals(string.IsNullOrEmpty(order.Channel) ? OrderChannels.Online : order.Channel, channel, StringComparison.Ordinal);
}

public class SalesSummaryViewModel
{
    public string Currency { get; set; }

    public int ShortDays { get; set; } = 7;

    public int LongDays { get; set; } = 30;

    public SalesFigure Today { get; set; } = new();

    public SalesFigure ShortWindow { get; set; } = new();

    public SalesFigure LongWindow { get; set; } = new();

    public int ToShip { get; set; }

    internal SalesSummaryViewModel Clone()
    {
        var copy = new SalesSummaryViewModel();

        CopyTo(copy);

        return copy;
    }

    internal void CopyTo(SalesSummaryViewModel to)
    {
        to.Currency = Currency;
        to.ShortDays = ShortDays;
        to.LongDays = LongDays;
        to.Today = Today;
        to.ShortWindow = ShortWindow;
        to.LongWindow = LongWindow;
        to.ToShip = ToShip;
    }
}

public class SalesFigure
{
    public int Orders { get; set; }

    public decimal Sales { get; set; }

    public void Add(decimal amount)
    {
        Orders++;
        Sales += amount;
    }
}

public class SalesSummaryEditViewModel
{
    public int CacheMinutes { get; set; }

    public int ShortDays { get; set; }

    public int LongDays { get; set; }

    public string Channel { get; set; }

    public IList<SelectListItem> Channels { get; set; } = [];
}
