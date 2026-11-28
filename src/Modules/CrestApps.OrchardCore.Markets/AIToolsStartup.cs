using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Markets.Models;
using CrestApps.OrchardCore.Markets.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Markets;

/// <summary>
/// Exposes the markets to the generic record tools (<c>listRecords</c>, <c>getRecord</c>) as the read-only
/// <c>market</c> kind when the AI feature is on, under <c>ManageMarkets</c> as Commerce › Markets. The markets page
/// binds its own view model instead of a display driver, so the editor replay cannot write a market yet; markets are
/// changed on the page or with the Markets recipe step.
/// </summary>
[Feature(MarketsConstants.Feature)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIRecordKind<Market>("market", kind => kind
            .WithTitle(S["Market"])
            .WithDescription(S["a market the store sells to: its two-letter countries (each in one market only), the currency and exchange rate its shoppers see and pay prices in, the price rounding, the language, whether prices include tax, and whether it is enabled. Read-only here; markets are changed on Commerce › Markets"])
            .WithPermission(MarketsPermissions.ManageMarkets)
            .FindUsing((services, id, cancellationToken) => services.GetRequiredService<IMarketStore>().FindByIdAsync(id, cancellationToken))
            .ListUsing(ListAsync)
            .WithProjection(market => new
            {
                id = market.ItemId,
                name = market.Name,
                countries = market.Countries,
                currency = market.Currency,
                exchangeRate = market.ExchangeRate,
                rounding = market.Rounding.ToString(),
                culture = market.Culture,
                pricesIncludeTax = market.PricesIncludeTax,
                isEnabled = market.IsEnabled,
            })
            .ReadOnly());
    }

    // Markets are a short list: filtered by name and paged in memory, in name order.
    private static async ValueTask<PageResult<Market>> ListAsync(IServiceProvider services, AIRecordQuery query, CancellationToken cancellationToken)
    {
        var matches = (await services.GetRequiredService<IMarketStore>().GetAllAsync(cancellationToken))
            .Where(market => string.IsNullOrWhiteSpace(query.Text) || (market.Name?.Contains(query.Text.Trim(), StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(market => market.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new PageResult<Market>
        {
            Count = matches.Count,
            Entries = matches.Skip((Math.Max(1, query.Page) - 1) * query.PageSize).Take(query.PageSize).ToList(),
        };
    }
}
