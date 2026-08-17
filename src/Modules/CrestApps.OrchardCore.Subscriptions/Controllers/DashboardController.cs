using System.Security.Claims;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Receipts.Models;
using CrestApps.OrchardCore.Receipts.Services;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Subscriptions.Controllers;

/// <summary>
/// Displays the current subscriber's dashboard in the admin area.
/// </summary>
[Admin]
public class DashboardController : Controller
{
    private readonly IDisplayManager<SubscriberDashboard> _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISubscriptionSessionStore _subscriptionSessionStore;
    private readonly IContentManager _contentManager;
    private readonly UserManager<IUser> _userManager;
    private readonly IDisplayNameProvider _displayNameProvider;
    private readonly ILocalClock _localClock;
    private readonly IReceiptService _receiptService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardController"/> class.
    /// </summary>
    /// <param name="displayManager">The display manager used to build the subscriber dashboard shape.</param>
    /// <param name="updateModelAccessor">The accessor that provides the current model updater.</param>
    /// <param name="authorizationService">The authorization service used to check dashboard access.</param>
    /// <param name="subscriptionSessionStore">The store used to load the subscription session that recorded a transaction.</param>
    /// <param name="contentManager">The content manager used to load the service plan content item version.</param>
    /// <param name="userManager">The user manager used to load the current subscriber.</param>
    /// <param name="displayNameProvider">The display name provider used to format the subscriber display name.</param>
    /// <param name="localClock">The local clock used to convert the transaction date to local time.</param>
    /// <param name="receiptService">The receipt service used to build the printable receipt document.</param>
    public DashboardController(
        IDisplayManager<SubscriberDashboard> displayManager,
        IUpdateModelAccessor updateModelAccessor,
        IAuthorizationService authorizationService,
        ISubscriptionSessionStore subscriptionSessionStore,
        IContentManager contentManager,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        ILocalClock localClock,
        IReceiptService receiptService)
    {
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        _authorizationService = authorizationService;
        _subscriptionSessionStore = subscriptionSessionStore;
        _contentManager = contentManager;
        _userManager = userManager;
        _displayNameProvider = displayNameProvider;
        _localClock = localClock;
        _receiptService = receiptService;
    }

    /// <summary>
    /// Displays the subscriber dashboard for the current user.
    /// </summary>
    /// <returns>The dashboard view, or a forbidden result when access is denied.</returns>
    [Admin("subscription-dashboard")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(HttpContext.User, SubscriptionPermissions.ManageOwnSubscriptions))
        {
            return Forbid();
        }

        var model = await _displayManager.BuildDisplayAsync(_updateModelAccessor.ModelUpdater);

        return View(model);
    }

    /// <summary>
    /// Displays a printable receipt for one of the current subscriber's payment transactions.
    /// </summary>
    /// <param name="sessionId">The subscription session identifier that recorded the transaction.</param>
    /// <param name="transactionId">The transaction identifier of the payment to print.</param>
    /// <returns>The printable receipt view, a not found result when the transaction cannot be located, or a forbidden result when the transaction does not belong to the current user.</returns>
    [Admin("subscription-receipt/{sessionId}/{transactionId}")]
    public async Task<IActionResult> Receipt(string sessionId, string transactionId)
    {
        if (!await _authorizationService.AuthorizeAsync(HttpContext.User, SubscriptionPermissions.ManageOwnSubscriptions))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(transactionId))
        {
            return NotFound();
        }

        var session = await _subscriptionSessionStore.GetAsync(sessionId);

        if (session is null)
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId) || !string.Equals(session.OwnerId, userId, StringComparison.Ordinal))
        {
            return Forbid();
        }

        if (!session.TryGet<PaymentsMetadata>(out var metadata) ||
            metadata.Payments is null ||
            !metadata.Payments.TryGetValue(transactionId, out var payment))
        {
            return NotFound();
        }

        var user = await _userManager.GetUserAsync(HttpContext.User);
        var contentItem = await _contentManager.GetVersionAsync(session.ContentItemVersionId);
        var subtotal = payment.Amount - payment.TaxAmount;

        var request = new ReceiptRequest
        {
            Reference = payment.TransactionId,
            IssuedAt = (await _localClock.ConvertToLocalAsync(session.CreatedUtc)).DateTime,
            Currency = payment.Currency,
            LineItems =
            [
                new ReceiptLineItem
                {
                    Description = contentItem?.DisplayText,
                    Quantity = 1,
                    UnitAmount = subtotal,
                    Amount = subtotal,
                },
            ],
            TaxLines = BuildTaxLines(payment),
            TaxAmount = payment.TaxAmount,
            Total = payment.Amount,
            Status = MapStatus(payment.Status),
            IsTest = payment.GatewayMode != GatewayMode.Live,
            GatewayId = payment.GatewayId,
        };

        if (user is User u)
        {
            request.BilledToName = await _displayNameProvider.GetAsync(user);
            request.BilledToEmail = u.Email;
        }

        var document = await _receiptService.BuildAsync(request);

        return View(document);
    }

    private static List<ReceiptTaxLine> BuildTaxLines(PaymentInfo payment)
    {
        if (payment.TaxSnapshot?.Lines is not { Count: > 0 } lines)
        {
            return [];
        }

        var taxLines = new List<ReceiptTaxLine>(lines.Count);

        foreach (var line in lines)
        {
            var description = line.TaxName;

            if (!string.IsNullOrEmpty(line.JurisdictionName))
            {
                description = string.IsNullOrEmpty(description)
                    ? line.JurisdictionName
                    : $"{description} — {line.JurisdictionName}";
            }

            taxLines.Add(new ReceiptTaxLine
            {
                Description = description,
                Amount = line.TaxAmount,
            });
        }

        return taxLines;
    }

    private static ReceiptStatus MapStatus(PaymentStatus status)
        => status switch
        {
            PaymentStatus.Succeeded => ReceiptStatus.Paid,
            PaymentStatus.Failed => ReceiptStatus.Failed,
            _ => ReceiptStatus.Pending,
        };
}
