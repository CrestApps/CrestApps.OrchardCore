using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Shipping.Controllers;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using CrestApps.OrchardCore.Shipping.ViewModels;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
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
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Shipping;

/// <summary>
/// SEC-C17q: a shipment gets at most one label, however many times Buy is posted at once (a double click, two staff
/// members), and a label is bought only from rates quoted for that shipment, to its addresses, a moment ago.
/// </summary>
public sealed class ShipmentLabelPurchaseTests
{
    private static readonly Address _shipFrom = new() { Name = "Store", AddressLine1 = "1 Main St", City = "Portland", Region = "OR", PostalCode = "97201", Country = "US" };
    private static readonly Address _seattle = new() { Name = "Sam", AddressLine1 = "1 Pike Pl", City = "Seattle", Region = "WA", PostalCode = "98101", Country = "US" };

    [Fact]
    public async Task Buy_PostedTwiceAtOnce_BuysOneLabel()
    {
        // Arrange
        var harness = new Harness();
        var quoted = await harness.QuoteAsync("shipment-1");

        harness.Lock.Arm();

        // Act: the first purchase waits at the carrier until the second request has arrived.
        var first = harness.CreateController().Buy("shipment-1", quoted);
        var second = harness.CreateController().Buy("shipment-1", quoted);

        await Task.WhenAll(first, second);

        // Assert
        Assert.Equal(1, harness.Carrier.Purchases);
        Assert.Equal(ShipmentStatus.Shipped, harness.Saved("shipment-1").Status);
    }

    [Fact]
    public async Task Buy_WithTheRatesJustQuotedForTheShipment_BuysTheLabel()
    {
        // Arrange
        var harness = new Harness();
        var quoted = await harness.QuoteAsync("shipment-1");

        // Act
        var result = await harness.CreateController().Buy("shipment-1", quoted);

        // Assert
        Assert.Equal(1, harness.Carrier.Purchases);
        Assert.Equal("OrdersDetail", Assert.IsType<RedirectToRouteResult>(result).RouteName);
        Assert.Equal("https://labels.example.com/1.pdf", harness.Saved("shipment-1").LabelUrl);
        Assert.Equal(ShipmentStatus.Shipped, harness.Saved("shipment-1").Status);
    }

    [Fact]
    public async Task Buy_WithAnotherShipmentsQuote_IsRefused()
    {
        // Arrange: the rates were quoted for the order's second shipment, and posted to the first one's Buy.
        var harness = new Harness();
        var quoted = await harness.QuoteAsync("shipment-2");

        // Act
        await harness.CreateController().Buy("shipment-1", quoted);

        // Assert
        Assert.Equal(0, harness.Carrier.Purchases);
        Assert.Equal(ShipmentStatus.Pending, harness.Saved("shipment-1").Status);
        Assert.Null(harness.Saved("shipment-1").LabelUrl);
    }

    [Fact]
    public async Task Buy_WithAQuoteIdTheRatesDidntGive_IsRefused()
    {
        // Arrange: a quote id typed into the form, such as another shipment's EasyPost id.
        var harness = new Harness();
        var quoted = await harness.QuoteAsync("shipment-1");

        quoted.QuoteId = "shp_someone-elses";

        // Act
        await harness.CreateController().Buy("shipment-1", quoted);

        // Assert
        Assert.Equal(0, harness.Carrier.Purchases);
    }

    [Fact]
    public async Task Buy_WithARateTheQuoteDidntOffer_IsRefused()
    {
        // Arrange
        var harness = new Harness();
        var quoted = await harness.QuoteAsync("shipment-1");

        quoted.RateId = "rate_from_another_quote";

        // Act
        await harness.CreateController().Buy("shipment-1", quoted);

        // Assert
        Assert.Equal(0, harness.Carrier.Purchases);
    }

    [Fact]
    public async Task Buy_AfterTheOrdersAddressChanged_IsRefused()
    {
        // Arrange: the rates were for the old address, so the label would send the parcel there.
        var harness = new Harness();
        var quoted = await harness.QuoteAsync("shipment-1");

        harness.Order.ShippingAddress = new Address { Name = "Sam", AddressLine1 = "9 Elm St", City = "Tacoma", Region = "WA", PostalCode = "98402", Country = "US" };

        // Act
        await harness.CreateController().Buy("shipment-1", quoted);

        // Assert
        Assert.Equal(0, harness.Carrier.Purchases);
    }

    [Fact]
    public async Task Buy_ForAShipmentThatHasALabel_IsRefused()
    {
        // Arrange: a label was bought, but the shipment didn't move on (its order couldn't be read at that moment).
        var harness = new Harness();
        var quoted = await harness.QuoteAsync("shipment-1");
        var saved = harness.Saved("shipment-1");

        saved.LabelUrl = "https://labels.example.com/earlier.pdf";
        harness.Save(saved);

        // Act
        await harness.CreateController().Buy("shipment-1", quoted);

        // Assert
        Assert.Equal(0, harness.Carrier.Purchases);
    }

    /// <summary>
    /// The shipments as the database holds them, each request reading its own copy, with a carrier that counts the
    /// labels it sells.
    /// </summary>
    private sealed class Harness
    {
        private readonly Dictionary<string, string> _shipments = new(StringComparer.Ordinal);
        private readonly ServiceProvider _services;

        public Harness()
        {
            Order = new Order
            {
                ItemId = "order-1",
                OrderNumber = "261116-LABEL1",
                Status = OrderStatus.Paid,
                ShippingAddress = _seattle,
                Lines = [new OrderLine { LineId = "line-1", Title = "Kettle", Quantity = 2 }],
            };

            Save(new Shipment { ItemId = "shipment-1", OrderId = Order.ItemId, Number = 1, Status = ShipmentStatus.Pending, Lines = [new ShipmentLine { LineId = "line-1", Quantity = 1 }] });
            Save(new Shipment { ItemId = "shipment-2", OrderId = Order.ItemId, Number = 2, Status = ShipmentStatus.Pending, Lines = [new ShipmentLine { LineId = "line-1", Quantity = 1 }] });

            Carrier = new CountingCarrier();
            Lock = new SignalingLock(Carrier);

            var shipments = new Mock<IShipmentStore>();
            shipments.Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string id, CancellationToken _) => ValueTask.FromResult(Saved(id)));
            shipments.Setup(store => store.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()))
                .Returns((Shipment shipment, CancellationToken _) =>
                {
                    Save(shipment);

                    return ValueTask.CompletedTask;
                });

            var shipmentService = new Mock<IShipmentService>();
            shipmentService.Setup(service => service.ShipAsync(It.IsAny<Shipment>(), It.IsAny<ShipmentDetails>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Shipment shipment, ShipmentDetails details, CancellationToken _) =>
                {
                    shipment.Status = ShipmentStatus.Shipped;
                    shipment.Carrier = details.Carrier;
                    shipment.TrackingNumber = details.TrackingNumber;
                    Save(shipment);

                    return ShipmentResult.Success(shipment);
                });

            var orders = new Mock<IOrderStore>();
            orders.Setup(store => store.FindByIdAsync(Order.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(() => Order);

            var carriers = new Mock<ICarrierProviderResolver>();
            carriers.Setup(resolver => resolver.GetDefaultAsync()).ReturnsAsync(Carrier);

            var authorization = new Mock<IAuthorizationService>();
            authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
                .ReturnsAsync(AuthorizationResult.Success());

            _services = new ServiceCollection()
                .AddSingleton(shipments.Object)
                .AddSingleton(shipmentService.Object)
                .AddSingleton(orders.Object)
                .AddSingleton(carriers.Object)
                .AddSingleton(new ShippingOrderRateService(Mock.Of<IShippingRateService>(), Mock.Of<IContentManager>()))
                .AddSingleton(SiteServiceFactory.Create(new ShippingSettings { ShipFrom = _shipFrom, WeightUnit = WeightUnit.Pound }))
                .AddSingleton(authorization.Object)
                .AddSingleton(Mock.Of<INotifier>())
                .AddSingleton(Mock.Of<global::YesSql.ISession>())
                .AddSingleton<IDistributedLock>(Lock)
                .AddSingleton<SignedTokenService>(SignedLinkDoubles.Tokens())
                .AddSingleton(typeof(IHtmlLocalizer<>), typeof(PassThroughHtmlLocalizer<>))
                .AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>))
                .BuildServiceProvider();
        }

        public Order Order { get; }

        public CountingCarrier Carrier { get; }

        public SignalingLock Lock { get; }

        public Shipment Saved(string id)
        {
            lock (_shipments)
            {
                return _shipments.TryGetValue(id, out var json) ? JsonSerializer.Deserialize<Shipment>(json) : null;
            }
        }

        public void Save(Shipment shipment)
        {
            lock (_shipments)
            {
                _shipments[shipment.ItemId] = JsonSerializer.Serialize(shipment);
            }
        }

        public async Task<ShipmentLabelViewModel> QuoteAsync(string shipmentId)
        {
            var result = await CreateController().Rates(shipmentId, new ShipmentLabelViewModel { Weight = 2m });
            var model = Assert.IsType<ShipmentLabelViewModel>(Assert.IsType<ViewResult>(result).Model);

            Assert.NotEmpty(model.Rates);
            model.RateId = model.Rates[0].RateId;

            return model;
        }

        public ShipmentLabelsAdminController CreateController()
        {
            var controller = ActivatorUtilities.CreateInstance<ShipmentLabelsAdminController>(_services);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = _services, User = new ClaimsPrincipal(new ClaimsIdentity("Test")) },
            };
            controller.TempData = Mock.Of<ITempDataDictionary>();
            controller.Url = Mock.Of<IUrlHelper>();

            return controller;
        }
    }

    /// <summary>
    /// A carrier account whose quotes name the shipment they were asked for. The first label waits until a second
    /// purchase has arrived (at the carrier, or at the shipment's lock), so two purchases overlap.
    /// </summary>
    private sealed class CountingCarrier : ICarrierProvider
    {
        private readonly TaskCompletionSource _secondArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _purchases;

        public int Purchases
            => Volatile.Read(ref _purchases);

        public string Name
            => "TestCarrier";

        public LocalizedString DisplayName
            => new("TestCarrier", "Test carrier");

        public void SignalSecondArrived()
            => _secondArrived.TrySetResult();

        public Task<bool> IsEnabledAsync()
            => Task.FromResult(true);

        public Task<CarrierQuote> QuoteAsync(CarrierShipmentRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new CarrierQuote
            {
                QuoteId = "shp_" + request.Reference,
                Rates =
                [
                    new CarrierRate { RateId = "rate_" + request.Reference, Carrier = "USPS", Service = "Ground", Amount = 7.5m, Currency = "USD" },
                ],
            });

        public async Task<CarrierLabel> BuyLabelAsync(string quoteId, string rateId, CancellationToken cancellationToken = default)
        {
            var purchase = Interlocked.Increment(ref _purchases);

            if (purchase == 1)
            {
                await _secondArrived.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
            else
            {
                _secondArrived.TrySetResult();
            }

            return new CarrierLabel
            {
                Carrier = "USPS",
                Service = "Ground",
                TrackingNumber = "9400" + purchase,
                LabelUrl = "https://labels.example.com/" + purchase + ".pdf",
                Amount = 7.5m,
                Currency = "USD",
            };
        }
    }

    /// <summary>
    /// The site's lock, which tells the carrier when a second request asks for a key that's already asked for, once
    /// armed.
    /// </summary>
    private sealed class SignalingLock : IDistributedLock
    {
        private readonly LocalLock _inner = new(NullLogger<LocalLock>.Instance);
        private readonly CountingCarrier _carrier;
        private readonly Dictionary<string, int> _requests = new(StringComparer.Ordinal);
        private bool _armed;

        public SignalingLock(CountingCarrier carrier)
            => _carrier = carrier;

        public void Arm()
        {
            lock (_requests)
            {
                _requests.Clear();
                _armed = true;
            }
        }

        public Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
        {
            Count(key);

            return _inner.AcquireLockAsync(key, expiration);
        }

        public Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
        {
            Count(key);

            return _inner.TryAcquireLockAsync(key, timeout, expiration);
        }

        public Task<bool> IsLockAcquiredAsync(string key)
            => _inner.IsLockAcquiredAsync(key);

        private void Count(string key)
        {
            lock (_requests)
            {
                if (_armed && (_requests[key] = _requests.GetValueOrDefault(key) + 1) >= 2)
                {
                    _carrier.SignalSecondArrived();
                }
            }
        }
    }
}
