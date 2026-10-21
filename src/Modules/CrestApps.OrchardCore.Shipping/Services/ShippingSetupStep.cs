using System.Globalization;
using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Shipping.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Shipping.Services;

/// <summary>
/// The setup guide's shipping: at least one zone with a way to ship to it. The quick action ships everywhere at one
/// flat rate, which zones by country can replace later.
/// </summary>
internal sealed class ShippingSetupStep : IStoreSetupStep
{
    private const string AmountField = "amount";

    private readonly INamedCatalog<ShippingZone> _catalog;
    private readonly INamedCatalogManager<ShippingZone> _manager;
    private readonly ISiteService _siteService;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public ShippingSetupStep(
        INamedCatalog<ShippingZone> catalog,
        INamedCatalogManager<ShippingZone> manager,
        ISiteService siteService,
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<ShippingSetupStep> stringLocalizer)
    {
        _catalog = catalog;
        _manager = manager;
        _siteService = siteService;
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public string Name
        => "shipping";

    public int Order
        => 40;

    public async Task<StoreSetupStepDescription> DescribeAsync(CancellationToken cancellationToken = default)
    {
        var zones = (await _catalog.GetAllAsync(cancellationToken)).Where(zone => zone.Methods.Any(method => method.Enabled)).ToList();

        return new StoreSetupStepDescription
        {
            Title = S["Ship your products"],
            Description = S["Where you ship and what it costs. Start with one flat rate for every order, wherever it goes; later you can add zones by country, rates by weight, free shipping over an amount, or pickup. Skip it if you sell only services or downloads."],
            IsDone = zones.Count > 0,
            Status = zones.Count == 0 ? null : S["You ship to {0}.", string.Join(", ", zones.Select(zone => zone.Name))],
            Url = _linkGenerator.GetPathByName(_httpContextAccessor.HttpContext, "ShippingZonesIndex"),
            UrlText = S["Shipping zones"],
            ActionText = zones.Count == 0 ? S["Ship everywhere at this rate"] : null,
            Fields = zones.Count > 0
                ? []
                :
                [
                    new StoreSetupField { Name = AmountField, Label = S["Shipping per order"], Value = "5", Type = "number", Required = true, Hint = S["In your store's currency. Enter 0 to ship for free."] },
                ],
        };
    }

    public async Task<StoreSetupActionResult> RunAsync(StoreSetupActionContext context, CancellationToken cancellationToken = default)
    {
        if (!decimal.TryParse(context.GetValue(AmountField), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount < 0m)
        {
            return StoreSetupActionResult.Failure(S["Enter what shipping costs per order, such as 5, or 0 for free."]);
        }

        var name = S["Everywhere"].Value;

        if (await _catalog.FindByNameAsync(name, cancellationToken) is not null)
        {
            return StoreSetupActionResult.Failure(S["There's a shipping zone named {0} already. Add a rate to it under Shipping zones.", name]);
        }

        var currency = (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency;
        var zone = await _manager.NewAsync(cancellationToken: cancellationToken);

        zone.Name = name;
        zone.RestOfWorld = true;

        var method = new ShippingMethod
        {
            MethodId = UniqueId.GenerateId(),
            ProviderName = ShippingConstants.Providers.Flat,
            Name = amount == 0m ? S["Free shipping"] : S["Standard shipping"],
            Currency = currency,
        };

        method.Put(new FlatRateSettings { Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero) });
        zone.Methods.Add(method);

        await _manager.CreateAsync(zone, cancellationToken);

        return StoreSetupActionResult.Success(S["Orders ship anywhere for {0} {1}.", amount.ToString("0.00", CultureInfo.CurrentCulture), currency]);
    }
}
