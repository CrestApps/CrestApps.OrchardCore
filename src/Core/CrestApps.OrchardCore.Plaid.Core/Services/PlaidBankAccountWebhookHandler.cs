using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Plaid.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Plaid.Services;

/// <summary>
/// Acts on the Plaid webhooks that matter to linked bank accounts. A bank account whose number the bank made for Plaid
/// stops working when the person takes back Plaid's access, so <c>ITEM: USER_PERMISSION_REVOKED</c> and
/// <c>ITEM: USER_ACCOUNT_REVOKED</c> take it back to unverified and drop the kept item. An item can be kept for several
/// bank accounts (one per account of the item); each is handled. Every other webhook is ignored here. Replaying a
/// webhook does nothing more: the kept items are gone after the first.
/// </summary>
public sealed class PlaidBankAccountWebhookHandler : IPlaidWebhookHandler
{
    private readonly IPlaidItemStore _store;
    private readonly PlaidItemCleaner _cleaner;
    private readonly IBankAccountManager _manager;
    private readonly IBankAccountService _bankAccountService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaidBankAccountWebhookHandler"/> class.
    /// </summary>
    /// <param name="store">The kept items.</param>
    /// <param name="cleaner">The item cleaner.</param>
    /// <param name="manager">The bank account manager.</param>
    /// <param name="bankAccountService">The bank account service.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public PlaidBankAccountWebhookHandler(
        IPlaidItemStore store,
        PlaidItemCleaner cleaner,
        IBankAccountManager manager,
        IBankAccountService bankAccountService,
        IStringLocalizer<PlaidBankAccountWebhookHandler> stringLocalizer)
    {
        _store = store;
        _cleaner = cleaner;
        _manager = manager;
        _bankAccountService = bankAccountService;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task HandleAsync(PlaidWebhookContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Is("ITEM", "USER_PERMISSION_REVOKED") && !context.Is("ITEM", "USER_ACCOUNT_REVOKED"))
        {
            return;
        }

        var webhook = context.Webhook;

        foreach (var item in await _store.ListByItemIdAsync(webhook.ItemId, cancellationToken))
        {
            // USER_ACCOUNT_REVOKED names one account; only the bank account linked to it matters.
            if (webhook.WebhookCode == "USER_ACCOUNT_REVOKED"
                && !string.IsNullOrEmpty(webhook.AccountId)
                && !string.Equals(webhook.AccountId, item.PlaidAccountId, StringComparison.Ordinal))
            {
                continue;
            }

            var account = await _manager.FindByIdAsync(item.BankAccountId, cancellationToken);

            if (account is not null
                && account.Status is BankAccountStatus.Verified or BankAccountStatus.VerificationSent
                && string.Equals(account.VerificationMethod, PlaidConstants.ProviderName, StringComparison.Ordinal))
            {
                await _bankAccountService.ChangeStatusAsync(
                    account,
                    BankAccountStatus.Unverified,
                    S["The account holder took back Plaid's access at the bank, so this account number no longer works. Link the bank again or enter the numbers."],
                    cancellationToken: cancellationToken);
            }

            await _cleaner.RemoveAsync(item, cancellationToken);
        }
    }
}
