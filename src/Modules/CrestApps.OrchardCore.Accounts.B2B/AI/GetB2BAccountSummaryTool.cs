using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Accounts.B2B.AI;

/// <summary>
/// Returns an account's business buying setup and standing, as its Business buying card and Business tab show them: the
/// terms, what it owes on them (<see cref="AccountTermsService"/>), its open term invoices, its budget and what its paid
/// orders came to this month (<see cref="B2BApprovalService"/>), its price list, its buyers with their order limits, and
/// whether punchout is on. A punchout key is never returned; only whether one exists. It needs
/// <c>ManageB2BAccounts</c> and <c>ViewContent</c> on the account.
/// </summary>
internal sealed class GetB2BAccountSummaryTool : B2BAccountsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getB2BAccountSummary";

    private const int MaxInvoices = 20;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "contentItemId": {
          "type": "string",
          "description": "The account's id, from listAccounts."
        }
      },
      "required": ["contentItemId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns an account's business-buying setup and standing: enabled, terms, credit limit and available credit, amount owed, open term invoices, monthly budget and spend, price list, buyers with their order limits, and whether punchout is on. Amounts are in the store's currency.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (latest, error) = await LoadAccountAsync(services, ReadString(arguments, "contentItemId"));

        if (latest is null)
        {
            return error;
        }

        if (!await IsAuthorizedAsync(services, caller, CommonPermissions.ViewContent, latest))
        {
            return NotAllowedMessage;
        }

        // The standing is that of the published account, the one checkout uses; an account never published has only its draft.
        var published = latest.Published ? latest : await services.GetRequiredService<IContentManager>().GetAsync(latest.ContentItemId, VersionOptions.Published);
        var account = new B2BAccount(published ?? latest);
        var storeCurrency = (await services.GetRequiredService<ISiteService>().GetSettingsAsync<CheckoutSettings>()).Currency;

        decimal? owed = null;
        decimal? availableCredit = null;
        var openInvoices = new List<object>();
        var openInvoiceCount = 0;

        if (services.GetService<AccountTermsService>() is { } terms)
        {
            (owed, _) = await terms.GetOwedAsync(account, cancellationToken);

            if (account.Terms.CreditLimit is not null)
            {
                availableCredit = await terms.GetAvailableCreditAsync(account, cancellationToken);
            }

            var open = (await terms.ListInvoicesAsync(account, cancellationToken))
                .Where(invoice => invoice.Status is TransactionStatus.Outstanding or TransactionStatus.PartiallyPaid)
                .ToList();

            openInvoiceCount = open.Count;

            foreach (var invoice in open.Take(MaxInvoices))
            {
                openInvoices.Add(new
                {
                    id = invoice.ItemId,
                    number = invoice.InvoiceNumber,
                    status = invoice.Status.ToString(),
                    currency = invoice.Currency,
                    total = invoice.TotalAmount,
                    outstanding = invoice.OutstandingAmount,
                    created = await LocalTimeAsync(services, invoice.CreatedUtc),
                    due = await LocalTimeAsync(services, invoice.DueUtc),
                });
            }
        }

        decimal? spentThisMonth = null;

        if (account.MonthlyBudget is not null && services.GetService<B2BApprovalService>() is { } approvals)
        {
            spentThisMonth = await approvals.GetSpentThisMonthAsync(account, storeCurrency, cancellationToken: cancellationToken);
        }

        object priceList = null;

        if (!string.IsNullOrEmpty(account.PriceListId))
        {
            var list = await services.GetRequiredService<IPriceListStore>().FindByIdAsync(account.PriceListId, cancellationToken);

            priceList = list is null
                ? new { id = account.PriceListId, missing = true, note = "The price list no longer exists, so the account pays the store's prices." }
                : new { id = list.ItemId, name = list.Name, percentOff = list.PercentOff, currency = list.Currency, entryCount = list.Entries?.Count ?? 0 };
        }

        var punchout = account.Punchout;
        var punchoutBuyer = punchout?.UserId is { } punchoutUserId ? account.FindBuyer(punchoutUserId) : null;

        return Json(new
        {
            contentItemId = account.AccountId,
            account = account.Name,
            enabled = account.Part.Enabled,
            buysAsBusiness = account.IsEnabled,
            published = published is not null,
            hasUnpublishedChanges = published is not null && published.ContentItemVersionId != latest.ContentItemVersionId,
            taxId = account.TaxId,
            billingEmail = account.BillingEmail,
            currency = storeCurrency,
            terms = new
            {
                offersTerms = account.Terms.OffersTerms,
                netDays = account.Terms.NetDays,
                depositPercent = account.Terms.DepositPercent,
                creditLimit = account.Terms.CreditLimit,
                requirePurchaseOrder = account.Terms.RequirePurchaseOrder,
            },
            owed,
            availableCredit,
            noCreditLimit = account.Terms.CreditLimit is null,
            openInvoices = new
            {
                total = openInvoiceCount,
                items = openInvoices,
                truncated = openInvoiceCount > openInvoices.Count,
            },
            monthlyBudget = account.MonthlyBudget,
            spentThisMonth,
            priceList,
            buyers = account.Buyers.Select(buyer => new
            {
                userId = buyer.UserId,
                userName = buyer.UserName,
                email = buyer.Email,
                role = buyer.Role.ToString(),
                orderLimit = buyer.OrderLimit,
            }).ToList(),
            punchout = new
            {
                enabled = punchout?.Enabled == true,
                buyer = punchoutBuyer is null ? punchout?.UserId : punchoutBuyer.Email ?? punchoutBuyer.UserName,
                hasKey = !string.IsNullOrEmpty(punchout?.KeyHash),
                keyCreated = await LocalTimeAsync(services, punchout?.KeyCreatedUtc),
            },
            editorPath = GetAccountEditorPath(services, account.AccountId),
        });
    }
}
