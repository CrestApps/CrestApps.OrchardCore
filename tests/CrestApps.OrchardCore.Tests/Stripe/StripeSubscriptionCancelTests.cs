using System.Net;
using System.Text;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Stripe.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Stripe;

namespace CrestApps.OrchardCore.Tests.Stripe;

/// <summary>
/// L-checkout-subscription-cancel (final review): "cancel only if unpaid" for a subscription read the subscription, then
/// canceled it, then voided its open first invoice. A buyer whose next card paid that invoice in between had an active
/// subscription canceled, with nothing refunded. The open invoice is now voided first, which Stripe refuses for a paid
/// one, and the subscription is canceled only after that. The Stripe API is answered by a handler that behaves like it:
/// only an open or uncollectible invoice can be voided.
/// </summary>
public sealed class StripeSubscriptionCancelTests
{
    [Fact]
    public async Task AFirstInvoicePaidJustBeforeTheCancel_LeavesTheSubscriptionActive()
    {
        var stripe = new StripeHandler { InvoiceStatus = "paid", SubscriptionStatus = "active", AmountPaid = 2500 };

        await Assert.ThrowsAnyAsync<Exception>(() => Create(stripe).CancelAsync(Request()));

        Assert.DoesNotContain("DELETE /v1/subscriptions/sub_1", stripe.Calls);
    }

    /// <summary>
    /// A trial, or a first cycle a coupon made free, has a first invoice Stripe marks paid for 0.00. Nothing was collected,
    /// so a checkout canceled before it finished still cancels the subscription, or Stripe would bill the buyer when the
    /// trial ends.
    /// </summary>
    [Fact]
    public async Task AFirstInvoicePaidForNothing_StillLetsTheSubscriptionBeCanceled()
    {
        // Arrange
        var stripe = new StripeHandler { InvoiceStatus = "paid", SubscriptionStatus = "trialing", AmountPaid = 0 };

        // Act
        var canceled = await Create(stripe).CancelAsync(Request());

        // Assert
        Assert.Equal("canceled", canceled.Status);
        Assert.Contains("DELETE /v1/subscriptions/sub_1", stripe.Calls);
        Assert.DoesNotContain("POST /v1/invoices/in_1/void", stripe.Calls);
    }

    [Fact]
    public async Task AnOpenFirstInvoice_IsVoidedBeforeTheSubscriptionIsCanceled()
    {
        var stripe = new StripeHandler { InvoiceStatus = "open", SubscriptionStatus = "incomplete" };

        await Create(stripe).CancelAsync(Request());

        var voided = stripe.Calls.IndexOf("POST /v1/invoices/in_1/void");
        var canceled = stripe.Calls.IndexOf("DELETE /v1/subscriptions/sub_1");

        Assert.True(voided >= 0, string.Join(", ", stripe.Calls));
        Assert.True(canceled > voided, string.Join(", ", stripe.Calls));
    }

    /// <summary>
    /// The supersede Mild (fix verification): an attempt set aside because the total changed went through the plain
    /// cancel, which canceled the subscription without voiding its open first invoice, and canceled it even when the buyer
    /// had just paid that invoice. Every cancel of an attempt that isn't paid now voids first.
    /// </summary>
    [Fact]
    public async Task SupersedingAnUnpaidSubscriptionAttempt_VoidsItsFirstInvoiceBeforeCancelingIt()
    {
        var stripe = new StripeHandler { InvoiceStatus = "open", SubscriptionStatus = "incomplete" };

        var result = await CreateProvider(stripe).CancelAsync(Superseding(), TestContext.Current.CancellationToken);

        var voided = stripe.Calls.IndexOf("POST /v1/invoices/in_1/void");
        var canceled = stripe.Calls.IndexOf("DELETE /v1/subscriptions/sub_1");

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(voided >= 0, string.Join(", ", stripe.Calls));
        Assert.True(canceled > voided, string.Join(", ", stripe.Calls));
    }

    [Fact]
    public async Task SupersedingASubscriptionAttempt_WhoseFirstInvoiceWasJustPaid_LeavesTheSubscriptionActive()
    {
        var stripe = new StripeHandler { InvoiceStatus = "paid", SubscriptionStatus = "active", AmountPaid = 2500 };

        var result = await CreateProvider(stripe).CancelAsync(Superseding(), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.DoesNotContain("DELETE /v1/subscriptions/sub_1", stripe.Calls);
    }

    // What TrySupersedeAsync sends once its verify said the attempt isn't paid: no OnlyIfUnpaid.
    private static CancelPaymentContext Superseding()
        => new()
        {
            Attempt = new PaymentAttempt { ItemId = "attempt-1", ProviderReference = "sub_1", State = PaymentAttemptState.Pending },
            Reason = "The total changed before it was paid.",
        };

    private static StripeCheckoutPaymentProvider CreateProvider(StripeHandler handler)
        => new(
            Mock.Of<IStripePaymentIntentService>(),
            Mock.Of<IStripePaymentMethodService>(),
            Create(handler),
            Mock.Of<IStripeRefundService>(),
            Mock.Of<IStripeCheckoutCustomerResolver>(),
            Mock.Of<IStripeWalletService>(),
            Mock.Of<IPaymentRefundStore>(),
            new TestClock(DateTime.UtcNow),
            NullLogger<StripeCheckoutPaymentProvider>.Instance,
            new PassThroughStringLocalizer<StripeCheckoutPaymentProvider>());

    private static CancelSubscriptionRequest Request()
        => new()
        {
            SubscriptionId = "sub_1",
            Reason = "Too many cards were declined for the first payment of this subscription.",
            IdempotencyKey = "cancel_attempt-1",
            VoidOpenInvoice = true,
        };

    private static StripeSubscriptionService Create(StripeHandler handler)
        => new(new StripeClient("sk_test_cancel", httpClient: new SystemNetHttpClient(new HttpClient(handler), maxNetworkRetries: 0)), new TestClock(DateTime.UtcNow));

    // Answers the subscription and invoice calls as Stripe does.
    private sealed class StripeHandler : HttpMessageHandler
    {
        public string InvoiceStatus { get; set; }

        public string SubscriptionStatus { get; set; }

        public long AmountPaid { get; set; }

        public List<string> Calls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri.AbsolutePath;

            lock (Calls)
            {
                Calls.Add($"{request.Method.Method} {path}");
            }

            if (path == "/v1/invoices/in_1/void")
            {
                if (InvoiceStatus is not ("open" or "uncollectible"))
                {
                    return Task.FromResult(Json(HttpStatusCode.BadRequest, """{"error":{"type":"invalid_request_error","message":"You can only void an open or uncollectible invoice."}}"""));
                }

                InvoiceStatus = "void";

                return Task.FromResult(Json(HttpStatusCode.OK, Invoice()));
            }

            if (path == "/v1/subscriptions/sub_1")
            {
                if (request.Method == HttpMethod.Delete)
                {
                    SubscriptionStatus = "canceled";
                }

                return Task.FromResult(Json(HttpStatusCode.OK, $$$"""{"id":"sub_1","object":"subscription","status":"{{{SubscriptionStatus}}}","latest_invoice":{{{Invoice()}}},"items":{"object":"list","data":[]}}"""));
            }

            return Task.FromResult(Json(HttpStatusCode.NotFound, """{"error":{"type":"invalid_request_error","message":"Unexpected call."}}"""));
        }

        private string Invoice()
            => $$"""{"id":"in_1","object":"invoice","status":"{{InvoiceStatus}}","currency":"usd","amount_paid":{{AmountPaid}}}""";

        private static HttpResponseMessage Json(HttpStatusCode status, string body)
            => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
