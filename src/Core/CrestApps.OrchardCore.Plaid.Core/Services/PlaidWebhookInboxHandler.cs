using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Plaid.Models;
using CrestApps.OrchardCore.Webhooks;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Plaid.Services;

/// <summary>
/// Acts on the Plaid webhooks the provider webhook inbox stored. A bank account whose number the bank made for Plaid
/// stops working when the person takes back Plaid's access, so <c>ITEM: USER_PERMISSION_REVOKED</c> and
/// <c>ITEM: USER_ACCOUNT_REVOKED</c> take it back to unverified and drop the item. Every other webhook is logged and
/// ignored. Replaying a webhook does nothing more: the item is gone after the first.
/// </summary>
public sealed class PlaidWebhookInboxHandler : IProviderWebhookInboxHandler
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    private readonly IPlaidItemStore _store;
    private readonly PlaidItemCleaner _cleaner;
    private readonly IBankAccountManager _manager;
    private readonly IBankAccountService _bankAccountService;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaidWebhookInboxHandler"/> class.
    /// </summary>
    public PlaidWebhookInboxHandler(
        IPlaidItemStore store,
        PlaidItemCleaner cleaner,
        IBankAccountManager manager,
        IBankAccountService bankAccountService,
        ILogger<PlaidWebhookInboxHandler> logger,
        IStringLocalizer<PlaidWebhookInboxHandler> stringLocalizer)
    {
        _store = store;
        _cleaner = cleaner;
        _manager = manager;
        _bankAccountService = bankAccountService;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public string TechnicalName
        => PlaidConstants.WebhookHandlerName;

    /// <inheritdoc />
    public WebhookHandlerReplaySafety ReplaySafety
        => WebhookHandlerReplaySafety.NaturallyIdempotent;

    /// <inheritdoc />
    public async Task HandleAsync(string payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(payload);

        var webhook = JsonSerializer.Deserialize<PlaidWebhook>(payload, _options)
            ?? throw new InvalidDataException("The Plaid webhook payload could not be read.");

        var revoked = webhook.WebhookType == "ITEM" && webhook.WebhookCode is "USER_PERMISSION_REVOKED" or "USER_ACCOUNT_REVOKED";

        if (!revoked)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Ignored the Plaid webhook {Type} {Code} for item {ItemId}.", webhook.WebhookType.SanitizeLogValue(), webhook.WebhookCode.SanitizeLogValue(), webhook.ItemId.SanitizeLogValue());
            }

            return;
        }

        var item = await _store.FindByItemIdAsync(webhook.ItemId, cancellationToken);

        if (item is null)
        {
            // Not kept: the account's number does not depend on Plaid, or it was already handled.
            return;
        }

        // USER_ACCOUNT_REVOKED names one account; only the linked one matters.
        if (webhook.WebhookCode == "USER_ACCOUNT_REVOKED"
            && !string.IsNullOrEmpty(webhook.AccountId)
            && !string.Equals(webhook.AccountId, item.PlaidAccountId, StringComparison.Ordinal))
        {
            return;
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
