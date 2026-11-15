using CrestApps.OrchardCore.ExchangeRates.Core.Services;
using CrestApps.OrchardCore.ExchangeRates.Models;
using CrestApps.OrchardCore.ExchangeRates.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// Exchange rates hold from their day until a later one of the pair, turn around for the opposite pair, are one per pair
/// and day, can be fetched by a provider without replacing what staff entered, and are copied onto an invoice and its
/// transaction at issue.
/// </summary>
public sealed class ExchangeRateTests
{
    [Fact]
    public async Task TheRateOfADay_IsTheLatestOnOrBeforeIt_OrTheInverseOfTheOppositePair()
    {
        // Arrange: EUR at 1.05 from November 1 and 1.08 from November 10; USD in GBP at 0.8 from November 5.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        await AddRateAsync(harness, "EUR", "USD", 1.05m, new DateOnly(2026, 11, 1));
        await AddRateAsync(harness, "eur", "usd", 1.08m, new DateOnly(2026, 11, 10));
        await AddRateAsync(harness, "USD", "GBP", 0.8m, new DateOnly(2026, 11, 5));

        // Act
        var quotes = await harness.InScopeAsync(async services =>
        {
            var rates = services.GetRequiredService<IExchangeRateService>();

            return new[]
            {
                await rates.GetRateAsync("EUR", "USD", new DateOnly(2026, 11, 9), TestContext.Current.CancellationToken),
                await rates.GetRateAsync("EUR", "USD", new DateOnly(2026, 11, 14), TestContext.Current.CancellationToken),
                await rates.GetRateAsync("EUR", "USD", new DateOnly(2026, 10, 31), TestContext.Current.CancellationToken),
                await rates.GetRateAsync("GBP", "USD", new DateOnly(2026, 11, 14), TestContext.Current.CancellationToken),
                await rates.GetRateAsync("USD", "USD", new DateOnly(2026, 11, 14), TestContext.Current.CancellationToken),
            };
        });

        // Assert
        Assert.Equal((1.05m, new DateOnly(2026, 11, 1)), (quotes[0].Rate, quotes[0].Date));
        Assert.Equal(1.08m, quotes[1].Rate);
        Assert.Null(quotes[2]);
        Assert.Equal(1.25m, quotes[3].Rate);
        Assert.Equal(1m, quotes[4].Rate);
    }

    [Fact]
    public async Task ASecondRateOfAPairForADay_OrARateThatCouldNotConvert_IsRefused()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        await AddRateAsync(harness, "EUR", "USD", 1.05m, new DateOnly(2026, 11, 1));

        // Act
        var errors = await harness.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<IExchangeRateManager>();
            var found = new List<string>();

            foreach (var (currency, baseCurrency, value) in new[] { ("EUR", "USD", 1.1m), ("EUR", "EUR", 1m), ("EURO", "USD", 1m), ("GBP", "USD", 0m) })
            {
                var rate = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
                rate.Currency = currency;
                rate.BaseCurrency = baseCurrency;
                rate.Rate = value;
                rate.Date = new DateOnly(2026, 11, 1);
                found.Add(string.Join(" ", (await manager.ValidateAsync(rate, TestContext.Current.CancellationToken)).Errors.Select(error => error.ErrorMessage)));
            }

            return found;
        });

        // Assert
        Assert.All(errors, error => Assert.False(string.IsNullOrEmpty(error)));
        Assert.Contains("already has a rate", errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task AProvidersRates_AreKept_ButARateEnteredForTheDayIsNotReplaced()
    {
        // Arrange: EUR entered by hand for the day; the provider sends EUR, GBP and a currency nobody asked for.
        await using var harness = await InvoicingTestHarness.CreateAsync(reportingCurrency: "USD");
        await AddRateAsync(harness, "EUR", "USD", 1.07m, new DateOnly(2026, 11, 14));
        var provider = new Mock<IExchangeRateProvider>();
        provider.SetupGet(item => item.Name).Returns("bank");
        provider.Setup(item => item.GetRatesAsync("USD", It.IsAny<IReadOnlyCollection<string>>(), new DateOnly(2026, 11, 14), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, decimal> { ["EUR"] = 1.09m, ["GBP"] = 1.27m, ["JPY"] = 0.0067m });

        // Act
        var added = await harness.InScopeAsync(async services =>
        {
            var site = new SiteSettings();
            site.Put(new ExchangeRateSettings { BaseCurrency = "USD", Currencies = ["EUR", "GBP"], ProviderName = "bank" });
            var updater = new ExchangeRateUpdater(
                [provider.Object],
                services.GetRequiredService<IExchangeRateManager>(),
                services.GetRequiredService<IExchangeRateStore>(),
                Mock.Of<ISiteService>(service => service.GetSiteSettingsAsync() == Task.FromResult<ISite>(site)),
                NullLogger<ExchangeRateUpdater>.Instance);

            return await updater.UpdateAsync(new DateOnly(2026, 11, 14), TestContext.Current.CancellationToken);
        });
        var (eur, gbp) = await harness.InScopeAsync(async services =>
        {
            var rates = services.GetRequiredService<IExchangeRateService>();

            return (await rates.GetRateAsync("EUR", "USD", new DateOnly(2026, 11, 14), TestContext.Current.CancellationToken),
                await rates.GetRateAsync("GBP", "USD", new DateOnly(2026, 11, 14), TestContext.Current.CancellationToken));
        });

        // Assert
        Assert.Equal(1, added);
        Assert.Equal((1.07m, "manual"), (eur.Rate, eur.Source));
        Assert.Equal((1.27m, "bank"), (gbp.Rate, gbp.Source));
    }

    [Fact]
    public async Task AnInvoiceIssuedInAnotherCurrency_RecordsTheRateOfItsDay_OnItAndItsTransaction()
    {
        // Arrange: the store reports in USD; a EUR invoice and a USD invoice are issued on November 13.
        await using var harness = await InvoicingTestHarness.CreateAsync(reportingCurrency: "USD");
        await AddRateAsync(harness, "EUR", "USD", 1.08m, new DateOnly(2026, 11, 10));
        var euros = await harness.CreateDraftAsync(invoice => invoice.Currency = "EUR");
        var dollars = await harness.CreateDraftAsync();
        var unknown = await harness.CreateDraftAsync(invoice => invoice.Currency = "JPY");

        // Act
        var issuedEuros = await harness.RunAsync(commands => commands.IssueAsync(euros.ItemId, TestContext.Current.CancellationToken));
        var issuedDollars = await harness.RunAsync(commands => commands.IssueAsync(dollars.ItemId, TestContext.Current.CancellationToken));
        var issuedUnknown = await harness.RunAsync(commands => commands.IssueAsync(unknown.ItemId, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(issuedEuros.Succeeded && issuedDollars.Succeeded && issuedUnknown.Succeeded);
        Assert.Equal((1.08m, "USD"), (issuedEuros.Invoice.ExchangeRate, issuedEuros.Invoice.BaseCurrency));
        Assert.Equal((1m, "USD"), (issuedDollars.Invoice.ExchangeRate, issuedDollars.Invoice.BaseCurrency));
        Assert.Null(issuedUnknown.Invoice.ExchangeRate);

        var transaction = await harness.FindTransactionAsync(issuedEuros.Invoice.TransactionId);
        Assert.Equal((1.08m, "USD"), (transaction.ExchangeRate, transaction.BaseCurrency));
    }

    private static Task<ExchangeRate> AddRateAsync(InvoicingTestHarness harness, string currency, string baseCurrency, decimal value, DateOnly date)
        => harness.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<IExchangeRateManager>();
            var rate = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
            rate.Currency = currency;
            rate.BaseCurrency = baseCurrency;
            rate.Rate = value;
            rate.Date = date;
            var validation = await manager.ValidateAsync(rate, TestContext.Current.CancellationToken);
            Assert.True(validation.Succeeded, string.Join(" ", validation.Errors.Select(error => error.ErrorMessage)));
            await manager.CreateAsync(rate, TestContext.Current.CancellationToken);

            return rate;
        });
}
