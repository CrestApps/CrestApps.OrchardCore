using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.Marketplace;
using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Marketplace.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Stripe;

namespace CrestApps.OrchardCore.Tests.Marketplace;

/// <summary>
/// SEC-C11: a pending payout (its transfer was asked for, and the run stopped before the answer was saved) was sent again,
/// and any failure answer was taken as proof that no transfer existed: the payout was marked failed, its key rotated, and
/// the next run transferred again. These run the real Stripe Connect provider through Stripe.net against a fake Stripe
/// that keeps idempotency keys the way Stripe documents it: for 24 hours, and refusing a key reused with other parameters.
/// </summary>
public sealed partial class MarketplaceServiceTests
{
    /// <summary>
    /// The first transfer went through and the process died; the next run is more than 24 hours later (payouts are manual,
    /// or the vendor was suspended meanwhile), so Stripe no longer holds the key.
    /// </summary>
    [Fact]
    public async Task APendingPayout_WhoseKeyStripeNoLongerHolds_IsFoundAtStripe_NotTransferredAgain()
    {
        // Arrange
        var stripe = new FakeStripe();
        var service = Create(new MarketplaceSettings { CommissionPercent = 10m, HoldDays = 0, ReleaseOnFulfillment = false, PayoutProvider = MarketplaceConstants.StripeConnectPayoutProvider }, CreateStripeProvider(stripe));

        await service.RecordSaleAsync(_order, TestContext.Current.CancellationToken);
        await service.ReleaseDueAsync(10, TestContext.Current.CancellationToken);

        _failNextPayoutUpdate = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PayOutAsync(_vendor, TestContext.Current.CancellationToken));
        Assert.Single(stripe.Transfers);

        stripe.Now = stripe.Now.AddHours(25);

        // Act
        await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        // Assert
        var transfer = Assert.Single(stripe.Transfers);
        var payout = Assert.Single(_payouts);

        Assert.Equal(VendorPayoutStatus.Paid, payout.Status);
        Assert.Equal(transfer.Id, payout.TransferId);
    }

    /// <summary>
    /// On the resend, reading the order's charge fails for a moment, so the request differs from the first one. Stripe
    /// refuses the reused key; that was taken as a refusal, the payout failed, the key rotated, and the next run paid again.
    /// </summary>
    [Fact]
    public async Task APendingPayout_WhoseResendDiffersFromTheFirstRequest_IsNotFailedAndPaidAgain()
    {
        // Arrange
        var stripe = new FakeStripe();
        var service = Create(new MarketplaceSettings { CommissionPercent = 10m, HoldDays = 0, ReleaseOnFulfillment = false, PayoutProvider = MarketplaceConstants.StripeConnectPayoutProvider }, CreateStripeProvider(stripe));

        _order.CheckoutSessionId = "session-1";

        await service.RecordSaleAsync(_order, TestContext.Current.CancellationToken);
        await service.ReleaseDueAsync(10, TestContext.Current.CancellationToken);

        _failNextPayoutUpdate = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PayOutAsync(_vendor, TestContext.Current.CancellationToken));
        Assert.Equal("ch_1", Assert.Single(stripe.Transfers).SourceTransaction);

        stripe.FailPaymentIntentReads = true;

        // Act: two more runs.
        await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);
        await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        // Assert
        var transfer = Assert.Single(stripe.Transfers);
        var payout = Assert.Single(_payouts);

        Assert.Equal(VendorPayoutStatus.Paid, payout.Status);
        Assert.Equal(transfer.Id, payout.TransferId);
    }

    /// <summary>
    /// A pending payout is finished by the provider it was recorded with, not by whichever provider is selected now.
    /// </summary>
    [Fact]
    public async Task APendingPayout_IsFinishedByTheProviderItWasRecordedWith()
    {
        // Arrange
        var other = new FakePayoutProvider("Other");
        var settings = new MarketplaceSettings { CommissionPercent = 10m, HoldDays = 0, ReleaseOnFulfillment = false, PayoutProvider = "Test" };
        var service = Create(settings, other);

        await service.RecordSaleAsync(_order, TestContext.Current.CancellationToken);
        await service.ReleaseDueAsync(10, TestContext.Current.CancellationToken);

        _failNextPayoutUpdate = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PayOutAsync(_vendor, TestContext.Current.CancellationToken));

        settings.PayoutProvider = "Other";

        // Act
        await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, other.Calls);
        Assert.Equal(VendorPayoutStatus.Paid, Assert.Single(_payouts).Status);
    }

    /// <summary>
    /// Only Stripe saying no to the request proves no transfer exists. A rate limit or a server error leave the
    /// disbursement submitted (its outcome unknown), a refusal fails it, and a transfer Stripe already has for the
    /// disbursement's key is found before anything is sent.
    /// </summary>
    [Fact]
    public async Task TheStripeProvider_ReportsATemporaryErrorAsSubmitted_ARefusalAsFailed_AndFindsATransferItAlreadyMade()
    {
        // Arrange
        var stripe = new FakeStripe();
        var provider = CreateStripeProvider(stripe);

        var made = await provider.SubmitAsync(new(), [StripeDisbursement("d1", "key-1")], TestContext.Current.CancellationToken);

        // Act
        var again = await provider.SubmitAsync(new(), [StripeDisbursement("d1", "key-1")], TestContext.Current.CancellationToken);

        stripe.TransferStatus = HttpStatusCode.TooManyRequests;
        var limited = await provider.SubmitAsync(new(), [StripeDisbursement("d2", "key-2")], TestContext.Current.CancellationToken);

        stripe.TransferStatus = HttpStatusCode.BadRequest;
        var refused = await provider.SubmitAsync(new(), [StripeDisbursement("d3", "key-3")], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DisbursementStatus.Settled, made.Statuses["d1"]);
        Assert.Equal(DisbursementStatus.Settled, again.Statuses["d1"]);
        Assert.Equal(made.TraceNumbers["d1"], again.TraceNumbers["d1"]);
        Assert.Equal(DisbursementStatus.Submitted, limited.Statuses["d2"]);
        Assert.Equal(DisbursementStatus.Failed, refused.Statuses["d3"]);
        Assert.False(string.IsNullOrEmpty(refused.FailureReasons["d3"]));

        var transfer = Assert.Single(stripe.Transfers);
        Assert.Equal("key-1", transfer.Metadata[StripeConnectPayoutProvider.PayoutKeyMetadata]);
        Assert.Equal("d1", transfer.Metadata["disbursementId"]);
    }

    /// <summary>
    /// SEC-C11 on disbursements: when the order's charge can't be read from Stripe, the transfer isn't sent without it.
    /// The batch is answered as not sent and stays open, the payout stays pending with its key, and the next run sends the
    /// same disbursement funded by the charge.
    /// </summary>
    [Fact]
    public async Task APayout_WhoseChargeCantBeRead_IsNotSent_AndIsSentFromTheChargeByTheNextRun()
    {
        // Arrange
        var stripe = new FakeStripe { FailPaymentIntentReads = true };
        var service = Create(new MarketplaceSettings { CommissionPercent = 10m, HoldDays = 0, ReleaseOnFulfillment = false, PayoutProvider = MarketplaceConstants.StripeConnectPayoutProvider }, CreateStripeProvider(stripe));

        _order.CheckoutSessionId = "session-1";

        await service.RecordSaleAsync(_order, TestContext.Current.CancellationToken);
        await service.ReleaseDueAsync(10, TestContext.Current.CancellationToken);

        var (firstPaid, firstFailures) = await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        Assert.Equal(0m, firstPaid);
        Assert.Single(firstFailures);
        Assert.Empty(stripe.Transfers);
        Assert.Equal(VendorPayoutStatus.Pending, Assert.Single(_payouts).Status);
        Assert.Equal(DisbursementStatus.Batched, Assert.Single(_disbursements.Store.Entries).Status);

        stripe.FailPaymentIntentReads = false;

        // Act
        var (paid, failures) = await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(failures);
        Assert.Equal(48.6m, paid);

        var transfer = Assert.Single(stripe.Transfers);
        var payout = Assert.Single(_payouts);

        Assert.Equal("ch_1", transfer.SourceTransaction);
        Assert.Equal(VendorPayoutStatus.Paid, payout.Status);
        Assert.Equal(transfer.Id, payout.TransferId);
        Assert.Equal(DisbursementStatus.Settled, Assert.Single(_disbursements.Store.Entries).Status);
    }

    /// <summary>
    /// Stripe made the transfer but its answer was lost (a server error): the disbursement is submitted, and a run more
    /// than 24 hours later, when Stripe no longer holds the key, finds the transfer by the key in its metadata instead of
    /// giving up on it and paying again.
    /// </summary>
    [Fact]
    public async Task APayout_WhoseAnswerWasLost_IsFoundAtStripe_AfterItsKeyExpired()
    {
        // Arrange
        var stripe = new FakeStripe { LoseNextTransferAnswer = true };
        var service = Create(new MarketplaceSettings { CommissionPercent = 10m, HoldDays = 0, ReleaseOnFulfillment = false, PayoutProvider = MarketplaceConstants.StripeConnectPayoutProvider }, CreateStripeProvider(stripe));

        await service.RecordSaleAsync(_order, TestContext.Current.CancellationToken);
        await service.ReleaseDueAsync(10, TestContext.Current.CancellationToken);

        await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        Assert.Single(stripe.Transfers);
        Assert.Equal(VendorPayoutStatus.Pending, Assert.Single(_payouts).Status);
        Assert.Equal(DisbursementStatus.Submitted, Assert.Single(_disbursements.Store.Entries).Status);

        stripe.Now = stripe.Now.AddHours(25);

        // Act: two more runs.
        await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);
        await service.PayOutAsync(_vendor, TestContext.Current.CancellationToken);

        // Assert
        var transfer = Assert.Single(stripe.Transfers);
        var payout = Assert.Single(_payouts);

        Assert.Equal(VendorPayoutStatus.Paid, payout.Status);
        Assert.Equal(transfer.Id, payout.TransferId);
        Assert.Equal(DisbursementStatus.Settled, Assert.Single(_disbursements.Store.Entries).Status);
    }

    private Disbursement StripeDisbursement(string id, string key)
        => new()
        {
            ItemId = id,
            IdempotencyKey = key,
            Payee = new(MarketplaceConstants.VendorPartyKind, _vendor.ItemId),
            Amount = 10m,
            Currency = "EUR",
            CreatedUtc = _now,
        };

    private StripeConnectPayoutProvider CreateStripeProvider(FakeStripe stripe)
    {
        var client = new StripeClient("sk_test_payouts", httpClient: new SystemNetHttpClient(new HttpClient(stripe), maxNetworkRetries: 0));
        var attempts = new InMemoryPaymentAttemptStore(new PaymentAttempt
        {
            ItemId = "attempt-1",
            SessionId = "session-1",
            ProviderKey = StripeConstants.ProcessorKey,
            ProviderReference = "pi_1",
            State = PaymentAttemptState.Succeeded,
        });

        // The vendor and the order the payouts are for, read when they are sent.
        var vendors = new Mock<IVendorStore>();
        vendors.Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string id, CancellationToken _) => id == _vendor.ItemId ? _vendor : null);

        var orders = new Mock<IOrderStore>();
        orders.Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string id, CancellationToken _) => id == _order?.ItemId ? _order : null);

        return new StripeConnectPayoutProvider(client, attempts, vendors.Object, orders.Object, NullLogger<StripeConnectPayoutProvider>.Instance, new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<StripeConnectPayoutProvider>());
    }

    /// <summary>
    /// Stripe's transfers, payment intents and idempotency keys, as its API reference describes them: a key is kept for
    /// 24 hours and answers a repeat of the same request with the first answer; the same key with other parameters is
    /// refused with an <c>idempotency_error</c>.
    /// </summary>
    private sealed class FakeStripe : HttpMessageHandler
    {
        private readonly Dictionary<string, (string Body, DateTime UsedUtc, string Response)> _keys = new(StringComparer.Ordinal);

        public DateTime Now { get; set; } = _now;

        public bool FailPaymentIntentReads { get; set; }

        // Makes the next transfer, then answers with a server error, as when Stripe's answer is lost.
        public bool LoseNextTransferAnswer { get; set; }

        // Anything but OK refuses new transfers with this status: 400 as Stripe's invalid_request_error, 429 as a rate limit.
        public HttpStatusCode TransferStatus { get; set; } = HttpStatusCode.OK;

        public List<(string Id, string Destination, string TransferGroup, string SourceTransaction, Dictionary<string, string> Metadata, string Json)> Transfers { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri.AbsolutePath;

            if (path == "/v1/transfers" && request.Method == HttpMethod.Post)
            {
                return CreateTransfer(request, await request.Content.ReadAsStringAsync(cancellationToken));
            }

            if (path == "/v1/transfers" && request.Method == HttpMethod.Get)
            {
                var query = HttpUtility.ParseQueryString(request.RequestUri.Query);
                var data = Transfers
                    .Where(transfer => transfer.Destination == query["destination"] && (query["transfer_group"] is null || transfer.TransferGroup == query["transfer_group"]))
                    .Select(transfer => transfer.Json);

                return Json(HttpStatusCode.OK, $$"""{"object":"list","url":"/v1/transfers","has_more":false,"data":[{{string.Join(",", data)}}]}""");
            }

            if (path == "/v1/payment_intents/pi_1")
            {
                return FailPaymentIntentReads
                    ? Json(HttpStatusCode.InternalServerError, """{"error":{"type":"api_error","message":"An unknown error occurred."}}""")
                    : Json(HttpStatusCode.OK, """{"id":"pi_1","object":"payment_intent","latest_charge":"ch_1"}""");
            }

            return Json(HttpStatusCode.NotFound, """{"error":{"type":"invalid_request_error","message":"Unexpected call."}}""");
        }

        private HttpResponseMessage CreateTransfer(HttpRequestMessage request, string body)
        {
            var key = request.Headers.TryGetValues("Idempotency-Key", out var values) ? values.First() : null;

            if (key is not null && _keys.TryGetValue(key, out var used) && Now - used.UsedUtc < TimeSpan.FromHours(24))
            {
                return used.Body == body
                    ? Json(HttpStatusCode.OK, used.Response)
                    : Json(HttpStatusCode.BadRequest, """{"error":{"type":"idempotency_error","message":"Keys for idempotent requests can only be used with the same parameters they were first used with."}}""");
            }

            if (TransferStatus != HttpStatusCode.OK)
            {
                return Json(TransferStatus, TransferStatus == HttpStatusCode.TooManyRequests
                    ? """{"error":{"type":"invalid_request_error","code":"rate_limit","message":"Too many requests hit the API too quickly."}}"""
                    : """{"error":{"type":"invalid_request_error","code":"balance_insufficient","message":"You have insufficient funds in your Stripe account."}}""");
            }

            var form = HttpUtility.ParseQueryString(body);
            var metadata = form.AllKeys
                .Where(name => name?.StartsWith("metadata[", StringComparison.Ordinal) == true)
                .ToDictionary(name => name["metadata[".Length..^1], name => form[name]);
            var id = "tr_" + (Transfers.Count + 1);
            var json = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["id"] = id,
                ["object"] = "transfer",
                ["amount"] = long.Parse(form["amount"], System.Globalization.CultureInfo.InvariantCulture),
                ["currency"] = form["currency"],
                ["destination"] = form["destination"],
                ["transfer_group"] = form["transfer_group"],
                ["source_transaction"] = form["source_transaction"],
                ["metadata"] = metadata,
                ["created"] = new DateTimeOffset(Now).ToUnixTimeSeconds(),
            });

            Transfers.Add((id, form["destination"], form["transfer_group"], form["source_transaction"], metadata, json));

            if (key is not null)
            {
                _keys[key] = (body, Now, json);
            }

            if (LoseNextTransferAnswer)
            {
                LoseNextTransferAnswer = false;

                return Json(HttpStatusCode.InternalServerError, """{"error":{"type":"api_error","message":"An unknown error occurred."}}""");
            }

            return Json(HttpStatusCode.OK, json);
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string body)
            => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
