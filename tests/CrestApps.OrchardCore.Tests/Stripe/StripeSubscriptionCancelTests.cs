using System.Net;
using System.Text;
using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Stripe.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
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
        var stripe = new StripeHandler { InvoiceStatus = "paid", SubscriptionStatus = "active" };

        await Assert.ThrowsAnyAsync<Exception>(() => Create(stripe).CancelAsync(Request()));

        Assert.DoesNotContain("DELETE /v1/subscriptions/sub_1", stripe.Calls);
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
            => $$"""{"id":"in_1","object":"invoice","status":"{{InvoiceStatus}}","currency":"usd","amount_paid":0}""";

        private static HttpResponseMessage Json(HttpStatusCode status, string body)
            => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
