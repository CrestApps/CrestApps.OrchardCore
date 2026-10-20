using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Taxation.TaxJar;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Taxation;

/// <summary>
/// TaxJar works out tax for the countries it's set up for: it's asked with the order's addresses, lines and shipping,
/// and its breakdown becomes a tax line for each line and for shipping. An order isn't taxed by a guess when it can't
/// answer.
/// </summary>
public sealed class TaxJarProviderTests
{
    private const string Answer = """
        {
          "tax": {
            "taxable_amount": 115,
            "amount_to_collect": 10.35,
            "jurisdictions": { "country": "US", "state": "NV", "county": "CLARK", "city": "LAS VEGAS" },
            "breakdown": {
              "shipping": { "taxable_amount": 15, "tax_collectable": 1.35, "combined_tax_rate": 0.09 },
              "line_items": [
                { "id": "line-1", "taxable_amount": 100, "tax_collectable": 9, "combined_tax_rate": 0.09 }
              ]
            }
          }
        }
        """;

    [Fact]
    public async Task TaxJarsBreakdown_BecomesATaxLineForEachLineAndForShipping()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, Answer);
        var provider = CreateProvider(handler);

        var result = await provider.DetermineAsync(Context("US"), TestContext.Current.CancellationToken);

        Assert.Equal(10.35m, result.TaxAmount);
        Assert.Equal(125.35m, result.TotalAmount);
        Assert.Equal(
            [("line-1", 9m, 0.09m), ("shipping", 1.35m, 0.09m)],
            result.Lines.Select(line => (line.ItemId, line.TaxAmount, line.Rate)));
        Assert.All(result.Lines, line =>
        {
            Assert.Equal(TaxTypeNames.SalesTax, line.TaxType);
            Assert.Equal("LAS VEGAS, CLARK, NV, US", line.JurisdictionName);
        });

        // What TaxJar was asked, in its own names.
        var sent = JsonNode.Parse(handler.Body);
        Assert.Equal("https://api.sandbox.taxjar.com/v2/taxes", handler.Url);
        Assert.Equal("Bearer test-token", handler.Authorization);
        Assert.Equal("89101", sent["to_zip"]?.GetValue<string>());
        Assert.Equal("NV", sent["to_state"]?.GetValue<string>());
        Assert.Equal(100m, sent["amount"]?.GetValue<decimal>());
        Assert.Equal(15m, sent["shipping"]?.GetValue<decimal>());
        Assert.Equal("line-1", sent["line_items"]?[0]?["id"]?.GetValue<string>());
        Assert.Equal("20010", sent["line_items"]?[0]?["product_tax_code"]?.GetValue<string>());
    }

    [Fact]
    public async Task WhenTaxJarRefuses_ItSaysWhy()
    {
        var provider = CreateProvider(new RecordingHandler(HttpStatusCode.BadRequest, """{"error":"Bad Request","detail":"to_zip is required"}"""));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.DetermineAsync(Context("US"), TestContext.Current.CancellationToken));

        Assert.Contains("to_zip is required", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("US", TaxPriceType.Exclusive, true)]
    [InlineData("CA", TaxPriceType.Exclusive, false)]
    [InlineData("US", TaxPriceType.Inclusive, false)]
    public void TaxJar_HandlesOnlyItsCountries_AndPricesWithoutTax(string country, TaxPriceType priceType, bool handles)
    {
        var context = Context(country);
        context.DefaultPriceType = priceType;

        Assert.Equal(handles, CreateProvider(new RecordingHandler(HttpStatusCode.OK, Answer)).CanHandle(context));
        Assert.False(CreateProvider(new RecordingHandler(HttpStatusCode.OK, Answer), apiKey: null).CanHandle(Context("US")));
    }

    [Fact]
    public async Task WhenATaxServiceCantAnswer_TheStoresOwnRulesWorkTheTaxOut()
    {
        var harness = new TaxTestHarness(new TestClock(TaxTestData.TransactionDate), services => services.AddScoped<ITaxDeterminationProvider, FailingProvider>());
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "CA Sales Tax",
            TaxType = TaxTypeNames.SalesTax,
            JurisdictionId = jurisdictionId,
            Source = TaxCalculationMethodNames.Percentage,
            Rate = 0.075m,
        });

        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(200m), TestContext.Current.CancellationToken);

        Assert.Equal(15m, result.TaxAmount);
    }

    private sealed class FailingProvider : ITaxDeterminationProvider
    {
        public int Order
            => 0;

        public bool CanHandle(TaxCalculationContext context)
            => true;

        public Task<TaxCalculationResult> DetermineAsync(TaxCalculationContext context, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The tax service is down.");
    }

    private static TaxJarTaxDeterminationProvider CreateProvider(RecordingHandler handler, string apiKey = "test-token")
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, disposeHandler: false));

        return new TaxJarTaxDeterminationProvider(
            Options.Create(new TaxJarOptions { ApiKey = apiKey, UseSandbox = true, Countries = ["US"] }),
            factory.Object,
            NullLogger<TaxJarTaxDeterminationProvider>.Instance);
    }

    private static TaxCalculationContext Context(string country)
        => new()
        {
            Currency = "USD",
            Origin = new Address { Country = "US", Region = "CA", PostalCode = "94107", City = "San Francisco" },
            Destination = new Address { Country = country, Region = "NV", PostalCode = "89101", City = "Las Vegas", AddressLine1 = "100 Main St" },
            Items =
            [
                new TaxableItem { Id = "line-1", Kind = TaxableItemKind.Physical, Quantity = 2m, UnitPrice = 50m, Currency = "USD", TaxClassificationCode = "20010" },
                new TaxableItem { Id = "shipping", Kind = TaxableItemKind.Shipping, Quantity = 1m, UnitPrice = 15m, Currency = "USD" },
            ],
        };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _answer;

        public RecordingHandler(HttpStatusCode status, string answer)
        {
            _status = status;
            _answer = answer;
        }

        public string Url { get; private set; }

        public string Authorization { get; private set; }

        public string Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Url = request.RequestUri?.ToString();
            Authorization = request.Headers.Authorization?.ToString();
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_status) { Content = new StringContent(_answer, System.Text.Encoding.UTF8, "application/json") };
        }
    }
}
