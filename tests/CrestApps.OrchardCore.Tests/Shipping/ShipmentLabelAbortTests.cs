using System.Security.Claims;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Shipping;
using CrestApps.OrchardCore.Shipping.Controllers;
using CrestApps.OrchardCore.Shipping.Indexes;
using CrestApps.OrchardCore.Shipping.Migrations;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using CrestApps.OrchardCore.Shipping.ViewModels;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Shipping;

/// <summary>
/// The label Mild (fix verification): a label was bought and recorded with the request's token, so a request aborted
/// after the carrier sold the label failed to save it, and staff bought a second. The shipments are in a real SQLite store.
/// </summary>
public sealed class ShipmentLabelAbortTests : IAsyncLifetime
{
    private const string ShipmentId = "shipment-1";

    private static readonly Address _shipFrom = new() { Name = "Store", AddressLine1 = "1 Main St", City = "Portland", Region = "OR", PostalCode = "97201", Country = "US" };
    private static readonly Address _seattle = new() { Name = "Sam", AddressLine1 = "1 Pike Pl", City = "Seattle", Region = "WA", PostalCode = "98101", Country = "US" };

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-label-abort-{Guid.NewGuid():N}.db");
    private readonly Order _order = new() { ItemId = "order-1", OrderNumber = "261116-LABEL1", Status = OrderStatus.Paid, ShippingAddress = _seattle };
    private readonly SignedTokenService _tokens = SignedLinkDoubles.Tokens();
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new ShipmentIndexProvider()], ShippingConstants.CollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(ShippingConstants.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);

            await new ShippingMigrations(Mock.Of<IContentDefinitionManager>()) { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();
            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            await new ShipmentStore(session).CreateAsync(new Shipment
            {
                ItemId = ShipmentId,
                OrderId = _order.ItemId,
                Number = 1,
                Status = ShipmentStatus.Pending,
                Lines = [new ShipmentLine { LineId = "line-1", Quantity = 1 }],
            }, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Buy_WhenTheRequestIsAbortedAfterTheCarrierSoldTheLabel_StillRecordsTheLabel()
    {
        // Arrange
        using var aborted = new CancellationTokenSource();
        var carrier = new AbortingCarrier(aborted);
        var quoted = await QuoteAsync(carrier);

        // Act: the request is aborted while the carrier sells the label.
        await using (var session = _store.CreateSession())
        {
            var controller = CreateController(session, carrier, aborted.Token);

            try
            {
                await controller.Buy(ShipmentId, quoted);
            }
            catch (OperationCanceledException)
            {
                // What the request saw doesn't matter: what was recorded does.
            }
        }

        // Assert
        await using var check = _store.CreateSession();
        var shipment = await new ShipmentStore(check).FindByIdAsync(ShipmentId, TestContext.Current.CancellationToken);

        Assert.Equal(1, carrier.Purchases);
        Assert.Equal("https://labels.example.com/1.pdf", shipment.LabelUrl);
        Assert.Equal(ShipmentStatus.Shipped, shipment.Status);
        Assert.Equal("94001", shipment.TrackingNumber);
    }

    private async Task<ShipmentLabelViewModel> QuoteAsync(AbortingCarrier carrier)
    {
        await using var session = _store.CreateSession();
        var result = await CreateController(session, carrier, CancellationToken.None).Rates(ShipmentId, new ShipmentLabelViewModel { Weight = 2m });
        var model = Assert.IsType<ShipmentLabelViewModel>(Assert.IsType<ViewResult>(result).Model);

        model.RateId = model.Rates[0].RateId;

        return model;
    }

    private ShipmentLabelsAdminController CreateController(ISession session, AbortingCarrier carrier, CancellationToken requestAborted)
    {
        var shipments = new ShipmentStore(session);

        // Shipping a shipment saves it through the store, as the shipment service does.
        var shipmentService = new Mock<IShipmentService>();
        shipmentService.Setup(service => service.ShipAsync(It.IsAny<Shipment>(), It.IsAny<ShipmentDetails>(), It.IsAny<CancellationToken>()))
            .Returns(async (Shipment shipment, ShipmentDetails details, CancellationToken cancellationToken) =>
            {
                shipment.Status = ShipmentStatus.Shipped;
                shipment.Carrier = details.Carrier;
                shipment.TrackingNumber = details.TrackingNumber;
                await shipments.UpdateAsync(shipment, cancellationToken);

                return ShipmentResult.Success(shipment);
            });

        var orders = new Mock<IOrderStore>();
        orders.Setup(store => store.FindByIdAsync(_order.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(_order);

        var carriers = new Mock<ICarrierProviderResolver>();
        carriers.Setup(resolver => resolver.GetDefaultAsync()).ReturnsAsync(carrier);

        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

        var services = new ServiceCollection()
            .AddSingleton<IShipmentStore>(shipments)
            .AddSingleton(shipmentService.Object)
            .AddSingleton(orders.Object)
            .AddSingleton(carriers.Object)
            .AddSingleton(new ShippingOrderRateService(Mock.Of<IShippingRateService>(), Mock.Of<IContentManager>()))
            .AddSingleton(SiteServiceFactory.Create(new ShippingSettings { ShipFrom = _shipFrom, WeightUnit = WeightUnit.Pound }))
            .AddSingleton(authorization.Object)
            .AddSingleton(Mock.Of<INotifier>())
            .AddSingleton(session)
            .AddSingleton<IDistributedLock>(new LocalLock(NullLogger<LocalLock>.Instance))
            .AddSingleton(_tokens)
            .AddSingleton(typeof(IHtmlLocalizer<>), typeof(PassThroughHtmlLocalizer<>))
            .AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>))
            .BuildServiceProvider();

        var controller = ActivatorUtilities.CreateInstance<ShipmentLabelsAdminController>(services);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = services,
                RequestAborted = requestAborted,
                User = new ClaimsPrincipal(new ClaimsIdentity("Test")),
            },
        };
        controller.TempData = Mock.Of<ITempDataDictionary>();
        controller.Url = Mock.Of<IUrlHelper>();

        return controller;
    }

    /// <summary>
    /// A carrier that sells the label and, as it answers, the request is aborted.
    /// </summary>
    private sealed class AbortingCarrier : ICarrierProvider
    {
        private readonly CancellationTokenSource _aborted;
        private int _purchases;

        public AbortingCarrier(CancellationTokenSource aborted)
            => _aborted = aborted;

        public int Purchases
            => _purchases;

        public string Name
            => "TestCarrier";

        public LocalizedString DisplayName
            => new("TestCarrier", "Test carrier");

        public Task<bool> IsEnabledAsync()
            => Task.FromResult(true);

        public Task<CarrierQuote> QuoteAsync(CarrierShipmentRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new CarrierQuote
            {
                QuoteId = "shp_" + request.Reference,
                Rates = [new CarrierRate { RateId = "rate_" + request.Reference, Carrier = "USPS", Service = "Ground", Amount = 7.5m, Currency = "USD" }],
            });

        public async Task<CarrierLabel> BuyLabelAsync(string quoteId, string rateId, CancellationToken cancellationToken = default)
        {
            _purchases++;
            await _aborted.CancelAsync();

            return new CarrierLabel
            {
                Carrier = "USPS",
                Service = "Ground",
                TrackingNumber = "9400" + _purchases,
                LabelUrl = "https://labels.example.com/" + _purchases + ".pdf",
                Amount = 7.5m,
                Currency = "USD",
            };
        }
    }
}
