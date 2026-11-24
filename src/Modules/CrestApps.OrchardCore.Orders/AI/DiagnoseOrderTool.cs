using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Orders.AI;

/// <summary>
/// Explains why an order is stuck, unpaid or not shipped: its money (<see cref="IOrderOperationsService.GetMoneyAsync"/>),
/// its fulfillment status, open disputes and payment risk, and, for a caller who may also see the payment ledgers
/// (<c>ManageRefunds</c>), the payment attempts of its checkout, its refunds waiting to be settled by hand and the open
/// payment issues about it. It changes nothing.
/// </summary>
internal sealed class DiagnoseOrderTool : OrdersAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "diagnoseOrder";

    // The ledgers' permission is declared in Transactions.Core, which this module does not reference; it is checked by
    // its name, as every Orchard Core permission is.
    private static readonly Permission _manageRefunds = new(
        "ManageRefunds",
        LocalizationSource.Create<DiagnoseOrderTool>("Manage payments and refunds"),
        isSecurityCritical: true);

    private const int IssuesScanned = 100;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "orderId": {
          "type": "string",
          "description": "The order's id, from listOrders."
        },
        "orderNumber": {
          "type": "string",
          "description": "The order's number, when the id is not known."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Explains why an order is stuck, unpaid or not shipped: what is owed and refundable, its fulfillment status, open disputes and payment risk and, for people who may see the payment ledgers, its payment attempts, refunds waiting to be settled by hand and open payment issues, with a list of findings. It changes nothing.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (order, error) = await FindOrderAsync(services, arguments, cancellationToken);

        if (order is null)
        {
            return error;
        }

        var money = await services.GetRequiredService<IOrderOperationsService>().GetMoneyAsync(order, cancellationToken);
        var findings = new List<string>();

        switch (order.Status)
        {
            case OrderStatus.Draft:
                findings.Add("The order is a draft: it was never placed. Send the buyer its invoice with sendOrderInvoice, or change it with saveDraftOrder.");
                break;
            case OrderStatus.Canceled:
                findings.Add("The order is canceled.");
                break;
            case OrderStatus.AwaitingPayment when money.Outstanding > 0m:
                findings.Add($"The order waits for payment: {Format(money.Outstanding, order.Currency)} is still owed.");
                break;
        }

        if (order.Status == OrderStatus.Paid && order.FulfillmentStatus is null or OrderFulfillmentStatus.Unfulfilled or OrderFulfillmentStatus.PartiallyFulfilled)
        {
            findings.Add(order.FulfillmentStatus == OrderFulfillmentStatus.PartiallyFulfilled
                ? "The order is paid and only part of it has shipped."
                : "The order is paid and nothing has shipped yet.");
        }

        if (order.HasOpenDispute)
        {
            findings.Add("The buyer has an open dispute (chargeback) on a payment of this order.");
        }

        if (order.PaymentRisk?.IsElevated == true)
        {
            findings.Add($"The payment provider rated the payment's risk {order.PaymentRisk.Level}.");
        }

        object ledger = null;
        var authorizationService = services.GetRequiredService<IAuthorizationService>();

        if (!await authorizationService.AuthorizeAsync(caller, _manageRefunds))
        {
            ledger = new { available = false, reason = "Payment attempts, refunds and payment issues need Manage payments and refunds." };
        }
        else if (services.GetService<IPaymentAttemptStore>() is { } attemptStore &&
            services.GetService<IPaymentRefundStore>() is { } refundStore &&
            services.GetService<IPaymentIssueStore>() is { } issueStore)
        {
            ledger = await ReadLedgerAsync(services, order, attemptStore, refundStore, issueStore, findings, cancellationToken);
        }
        else
        {
            ledger = new { available = false, reason = "The payment ledgers are not turned on for this site (the Transactions feature)." };
        }

        if (findings.Count == 0)
        {
            findings.Add("Nothing looks wrong with this order.");
        }

        return Json(new
        {
            orderId = order.ItemId,
            number = order.OrderNumber,
            status = ToCamel(order.Status.ToString()),
            fulfillmentStatus = order.FulfillmentStatus is { } fulfillment ? ToCamel(fulfillment.ToString()) : null,
            refundStatus = ToCamel(order.RefundStatus.ToString()),
            currency = order.Currency,
            total = order.Total,
            outstanding = money.Outstanding,
            refundable = money.Refundable,
            refunded = order.RefundedAmount,
            placedAt = await LocalTimeAsync(services, order.CreatedUtc),
            paidAt = await LocalTimeAsync(services, order.PaidUtc),
            canceledAt = await LocalTimeAsync(services, order.CanceledUtc),
            openDispute = order.HasOpenDispute,
            paymentRisk = order.PaymentRisk is { } risk ? new { level = risk.Level, score = risk.Score, elevated = risk.IsElevated } : null,
            ledger,
            findings,
            adminPath = GetAdminPath(services, "OrdersDetail", new { orderId = order.ItemId }),
        });
    }

    private static async Task<object> ReadLedgerAsync(
        IServiceProvider services,
        Order order,
        IPaymentAttemptStore attemptStore,
        IPaymentRefundStore refundStore,
        IPaymentIssueStore issueStore,
        List<string> findings,
        CancellationToken cancellationToken)
    {
        var attempts = string.IsNullOrEmpty(order.CheckoutSessionId)
            ? new List<PaymentAttempt>()
            : (await attemptStore.GetBySessionAsync(order.CheckoutSessionId, cancellationToken)).OrderByDescending(attempt => attempt.CreatedUtc).ToList();

        var refunds = string.IsNullOrEmpty(order.CheckoutSessionId)
            ? new List<PaymentRefund>()
            : (await refundStore.GetBySessionAsync(order.CheckoutSessionId, cancellationToken)).ToList();

        var openIssues = await issueStore.PageAsync(1, IssuesScanned, PaymentIssueStatus.Open, cancellationToken);
        var references = new HashSet<string>(StringComparer.Ordinal) { order.ItemId };

        if (!string.IsNullOrEmpty(order.CheckoutSessionId))
        {
            references.Add(order.CheckoutSessionId);
        }

        foreach (var attempt in attempts.Where(attempt => !string.IsNullOrEmpty(attempt.ProviderReference)))
        {
            references.Add(attempt.ProviderReference);
        }

        var issues = openIssues.Entries
            .Where(issue => (issue.ReferenceId is not null && references.Contains(issue.ReferenceId)) || (issue.ProviderReference is not null && references.Contains(issue.ProviderReference)))
            .ToList();

        var pendingByHand = refunds.Where(refund => refund.Status == RefundStatus.PendingManualReview).ToList();

        if (attempts.FirstOrDefault() is { } latest)
        {
            if (latest.State == PaymentAttemptState.Failed && order.Status == OrderStatus.AwaitingPayment)
            {
                findings.Add(string.IsNullOrEmpty(latest.FailureReason)
                    ? "The latest payment attempt failed."
                    : $"The latest payment attempt failed: {latest.FailureReason}");
            }
            else if (latest.State is PaymentAttemptState.Created or PaymentAttemptState.Pending)
            {
                findings.Add("A payment attempt is still pending at the payment provider.");
            }
        }
        else if (order.Status == OrderStatus.AwaitingPayment)
        {
            findings.Add("No payment was attempted for this order's checkout.");
        }

        if (pendingByHand.Count > 0)
        {
            findings.Add($"{pendingByHand.Count} refund(s) wait to be settled by hand on the Refunds page (resolveRefund once the money was returned).");
        }

        if (issues.Count > 0)
        {
            findings.Add($"{issues.Count} open payment issue(s) are about this order (listPaymentIssues).");
        }

        var attemptItems = new List<object>();

        foreach (var attempt in attempts)
        {
            attemptItems.Add(new
            {
                id = attempt.ItemId,
                at = await LocalTimeAsync(services, attempt.CreatedUtc),
                method = attempt.ProviderKey,
                state = ToCamel(attempt.State.ToString()),
                expected = attempt.ExpectedAmount + attempt.ExpectedTaxAmount,
                confirmed = attempt.ConfirmedAmount + attempt.ConfirmedTaxAmount,
                currency = attempt.Currency,
                failureReason = attempt.FailureReason,
                superseded = attempt.IsSuperseded,
            });
        }

        var issueItems = new List<object>();

        foreach (var issue in issues)
        {
            issueItems.Add(new
            {
                id = issue.ItemId,
                at = await LocalTimeAsync(services, issue.CreatedUtc),
                kind = issue.Kind,
                message = issue.Message,
                amount = issue.Amount,
                currency = issue.Currency,
            });
        }

        return new
        {
            available = true,
            attempts = attemptItems,
            refundsWaitingToBeSettledByHand = pendingByHand.Select(refund => new { id = refund.ItemId, amount = refund.RefundGrossAmount, currency = refund.Currency, reason = refund.Reason }),
            openPaymentIssues = issueItems,
            openIssuesScanned = Math.Min(openIssues.Count, IssuesScanned),
            openIssuesTruncated = openIssues.Count > IssuesScanned,
        };
    }
}
