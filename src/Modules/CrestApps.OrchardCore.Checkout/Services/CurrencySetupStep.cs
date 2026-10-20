using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// The setup guide's currency: what the store prices in and charges, unless a market says otherwise.
/// </summary>
internal sealed class CurrencySetupStep : IStoreSetupStep
{
    private const string CurrencyField = "currency";

    private static readonly string[] _commonCurrencies = ["USD", "EUR", "GBP", "CAD", "AUD", "JPY", "CHF", "SEK", "NOK", "DKK", "NZD", "INR", "BRL", "MXN"];

    private readonly ISiteService _siteService;
    private readonly IServiceProvider _serviceProvider;
    private readonly AdminOptions _adminOptions;

    internal readonly IStringLocalizer S;

    public CurrencySetupStep(ISiteService siteService, IServiceProvider serviceProvider, IOptions<AdminOptions> adminOptions, IStringLocalizer<CurrencySetupStep> stringLocalizer)
    {
        _siteService = siteService;
        _serviceProvider = serviceProvider;
        _adminOptions = adminOptions.Value;
        S = stringLocalizer;
    }

    public string Name
        => "currency";

    public int Order
        => 20;

    public async Task<StoreSetupStepDescription> DescribeAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _siteService.GetSettingsAsync<CheckoutSettings>();

        // The store's currency catalog, when products are on; else the usual ones.
        var currencies = _serviceProvider.GetService<IProductCurrencyProvider>() is { } provider
            ? await provider.GetCurrenciesAsync(cancellationToken)
            : [];
        var options = currencies.Count > 0
            ? currencies
                .OrderBy(currency => currency.DisplayName, StringComparer.CurrentCulture)
                .Select(currency => new KeyValuePair<string, string>(currency.CurrencyCode, $"{currency.DisplayName} ({currency.CurrencyCode})"))
                .ToList()
            : [.. _commonCurrencies.Select(code => new KeyValuePair<string, string>(code, code))];

        if (!string.IsNullOrEmpty(settings.Currency) && !options.Any(option => option.Key == settings.Currency))
        {
            options.Insert(0, new(settings.Currency, settings.Currency));
        }

        return new StoreSetupStepDescription
        {
            Title = S["Your currency"],
            Description = S["The currency your prices are in and customers pay in. Prices you enter on products are in it."],
            Status = S["Prices are in {0}.", settings.Currency],
            Url = $"~/{_adminOptions.AdminUrlPrefix}/Settings/{CheckoutConstants.SettingsGroupId}",
            UrlText = S["Checkout settings"],
            ActionText = S["Use this currency"],
            Fields =
            [
                new StoreSetupField { Name = CurrencyField, Label = S["Currency"], Value = settings.Currency, Type = "select", Options = options, Required = true },
            ],
        };
    }

    public async Task<StoreSetupActionResult> RunAsync(StoreSetupActionContext context, CancellationToken cancellationToken = default)
    {
        var code = context.GetValue(CurrencyField)?.ToUpperInvariant();

        if (code is null || code.Length != 3 || !code.All(char.IsAsciiLetter))
        {
            return StoreSetupActionResult.Failure(S["Choose a currency."]);
        }

        var site = await _siteService.LoadSiteSettingsAsync();
        var settings = site.GetOrCreate<CheckoutSettings>();

        settings.Currency = code;
        site.Put(settings);
        await _siteService.UpdateSiteSettingsAsync(site);

        return StoreSetupActionResult.Success(S["Prices are in {0}.", code]);
    }
}
