using CrestApps.OrchardCore.Orders.Core.Services;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders.Dashboard;


/// <summary>
/// Reads the sales the widget shows, in the store's currency; orders in other currencies count in Orders only.
/// </summary>
public sealed class SalesSummaryPartDisplayDriver : ContentPartDisplayDriver<SalesSummaryPart>
{
    private readonly OrderReportReader _reader;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;

    public SalesSummaryPartDisplayDriver(OrderReportReader reader, ISiteService siteService, IClock clock)
    {
        _reader = reader;
        _siteService = siteService;
        _clock = clock;
    }

    public override IDisplayResult Display(SalesSummaryPart part, BuildPartDisplayContext context)
        => Initialize<SalesSummaryViewModel>(GetDisplayShapeType(context), async model =>
        {
            var now = _clock.UtcNow;
            var site = await _siteService.GetSiteSettingsAsync();
            var timeZone = !string.IsNullOrEmpty(site.TimeZoneId) && TimeZoneInfo.TryFindSystemTimeZoneById(site.TimeZoneId, out var zone) ? zone : TimeZoneInfo.Utc;
            var today = TimeZoneInfo.ConvertTimeToUtc(TimeZoneInfo.ConvertTimeFromUtc(now, timeZone).Date, timeZone);
            var currency = (await _siteService.GetSettingsAsync<Checkout.Models.CheckoutSettings>()).Currency;

            model.Currency = currency;

            await foreach (var order in _reader.ReadPaidAsync(now.AddDays(-30), now))
            {
                var paid = order.PaidUtc ?? order.CreatedUtc;
                var inCurrency = string.Equals(order.Currency, currency, StringComparison.OrdinalIgnoreCase);
                var amount = inCurrency ? (order.Total ?? order.Subtotal) - order.RefundedAmount : 0m;

                model.Last30Days.Add(amount);

                if (paid >= now.AddDays(-7))
                {
                    model.Last7Days.Add(amount);
                }

                if (paid >= today)
                {
                    model.Today.Add(amount);
                }
            }

            await foreach (var _ in _reader.ReadUnfulfilledAsync())
            {
                model.ToShip++;
            }
        }).Location("Detail", "Content").Location("DetailAdmin", "Content");
}

public class SalesSummaryViewModel
{
    public string Currency { get; set; }

    public SalesFigure Today { get; set; } = new();

    public SalesFigure Last7Days { get; set; } = new();

    public SalesFigure Last30Days { get; set; } = new();

    public int ToShip { get; set; }
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
