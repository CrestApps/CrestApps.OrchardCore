---
sidebar_label: Plaid
title: Plaid
description: "Instant bank account verification with Plaid Auth for the Banking building block: the link provider, the Plaid Link shape, settings with the secret in Orchard Secrets, kept items with protected access tokens, the signed webhook through the provider webhook inbox, plain HTTP with resilience, and the seams other modules use to share one Plaid setup per tenant (IPlaidClient, IPlaidWebhookHandler, IPlaidCredentialsProvider)."
user_manual:
  - user-manual/finance/bank-accounts
---

The **Plaid** module links a person's bank with [Plaid](https://plaid.com) so a [Banking](./banking) account is saved with the bank's own routing and account numbers and verified at once, instead of by hand or by a prenote. It is Banking's first link provider (`IBankAccountLinkProvider`); Banking never names Plaid.

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Plaid` | Plaid Bank Account Linking | Bank Accounts (Banking), Secrets (`OrchardCore.Secrets`), Provider Webhook Inbox |

Projects: `CrestApps.OrchardCore.Plaid.Abstractions` (the contracts other modules use: `IPlaidClient`, `IPlaidWebhookHandler`, `IPlaidCredentialsProvider`, `IPlaidLinkSite`, the Plaid models, `PlaidApiException` and `PlaidConstants`), `CrestApps.OrchardCore.Plaid.Core` (the HTTP client, the webhook check, the webhook fan-out, the link provider, the item store, the observer and the bank account webhook handler) and `CrestApps.OrchardCore.Plaid` (settings, the Plaid Link shape, the webhook endpoint, the migration). There is no new NuGet package: Plaid publishes no official .NET library, so the module calls Plaid's JSON API with a named `HttpClient`.

## Set it up

1. In the Plaid dashboard, copy the client id and the secret of the environment (**Developers** → **Keys**). Ask Plaid for Auth in Production.
2. Under **Security** → **Secrets**, add a text secret, such as `Plaid.Secret`, holding the Plaid secret.
3. Under **Configuration** → **Settings** → **Plaid**, choose the environment, enter the client id, choose the secret and save. **Test the connection** asks Plaid for a link token with the saved settings.

When the host supplies the keys (an `IPlaidCredentialsProvider` registered with `AddPlaidCredentialsProvider<T>()`), the settings page says so instead of showing the fields, saving it changes nothing, and **Test the connection** tries the host's keys.

The settings (`PlaidSettings`: `Environment`, `ClientId`, `SecretName`) hold no secret, only the secret's name, so they can go in a recipe:

```json
{
  "steps": [
    { "name": "Feature", "enable": [ "OrchardCore.Secrets", "CrestApps.OrchardCore.Plaid" ] },
    { "name": "Secrets", "Secrets": [ { "Name": "Plaid.Secret", "Store": "Database" } ] },
    {
      "name": "Settings",
      "PlaidSettings": { "Environment": "Sandbox", "ClientId": "your-client-id", "SecretName": "Plaid.Secret" }
    }
  ]
}
```

The secret's value comes from configuration (`OrchardCore:Secrets:Plaid.Secret`), never from the recipe. Changing **Environment** needs the secret of that environment. `ManagePlaidSettings` (security critical, Administrators by default) guards the settings page.

If the site sends a Content Security Policy, allow `https://cdn.plaid.com` for scripts and `https://*.plaid.com` for frames: Plaid requires its Link script from its own CDN, so the module declares it as a CDN-only resource (`plaid-link`) with no local copy.

## The flow

1. **Link token.** `POST /link/token/create` with `products: ["auth"]`, `country_codes: ["US"]`, depository checking and savings only, the site's name, the request's language when Plaid Link speaks it, this tenant's webhook URL, and `user.client_user_id` = SHA-256 of the tenant name and the user id. Plaid never sees a user id, name or e-mail. The token is never stored.
2. **Plaid Link** runs in the `BankAccountLink_Plaid` shape. Its `onSuccess` posts `{ publicToken, accountId, institutionName }` as Banking's `clientResult`.
3. **Exchange.** `POST /item/public_token/exchange` returns the access token and the item id. It is never retried: a public token works once.
4. **Numbers.** `POST /auth/get` for the chosen account gives `numbers.ach` (`routing`, `account`, `is_tokenized_account_number`) and the account's `subtype` and `mask`.
5. **Save.** Banking's `ApplyLink` writes the numbers through `ApplyNumbers` and sets the account to `Verified` with `VerificationMethod` = `Plaid`.
6. **Clean up.** `POST /item/remove` at once, so no access token stays on the site, unless the account number is tokenized (below).

`PRODUCT_NOT_READY` (the bank has not confirmed the account), a missing number or any Plaid error refuses the link with Plaid's `display_message` when it gives one, and removes the item. Plaid's micro-deposit flows are opt-in at Plaid and are not turned on, so a link either gives the numbers at once or fails; the person can then type the numbers or use a prenote.

### Tokenized account numbers and kept items

Some banks (Chase, PNC, US Bank) give Plaid an account number made for it, which stops working when the person takes back Plaid's access. For those the item is kept as a `PlaidItem` in the `Plaid` collection (`PlaidItemIndex`: `ItemId`, `BankAccountId`), one per bank account, so an item whose several accounts are linked to several bank accounts is kept once for each (`IPlaidItemStore.ListByItemIdAsync` lists them), with its access token protected by `ISensitiveValueProtector` under the purpose `CrestApps.OrchardCore.Plaid.AccessToken`. Banking's record gets nothing vendor-specific.

`PlaidBankAccountObserver` removes the kept item (at Plaid and on the site) when the account is deleted or closed, or when someone types other numbers over the linked ones. An item is removed at Plaid only when no other bank account is still linked to one of its accounts; until then only the bank account's own `PlaidItem` is dropped. A new link replaces what an earlier one kept. Kept items are tenant-bound and never in a recipe or deployment.

## Webhooks

`POST /plaid/webhook` (route name `CrestApps.Plaid.Webhook`, anonymous, no anti-forgery) is sent to Plaid with each link token, so it is not entered in the dashboard. Plaid must reach it from the internet; the instant link itself needs no webhook.

1. The `Plaid-Verification` header is checked first (`PlaidWebhookVerifier`): a JWT with `alg` `ES256`, signed by the key `/webhook_verification_key/get` returns for its `kid` (cached for a day, an expired key refused), `iat` no more than five minutes old, and `request_body_sha256` equal to the SHA-256 of the exact body (constant-time). Anything else is answered 400 and nothing is stored. Bodies over 64 KB are refused.
2. The webhook is stored in the provider webhook inbox (`IProviderWebhookInbox`, provider `Plaid`, handler `plaid-webhook`). Plaid sends no event id, so the delivery id is the body's SHA-256: a resent webhook is stored once.
3. After the answer, `PlaidWebhookInboxHandler` hands the webhook, whatever its type and code, to every registered `IPlaidWebhookHandler`. The signature was checked once, in step 1; nothing checks it again. Every handler runs even when another throws; a failure is then thrown so the inbox dispatches the webhook again, so handlers must be idempotent.
4. The module's own handler, `PlaidBankAccountWebhookHandler`, acts on `ITEM: USER_PERMISSION_REVOKED`, and `ITEM: USER_ACCOUNT_REVOKED` for a linked account: each kept item's bank account goes from `Verified` back to `Unverified` with a reason (only when Plaid verified it) and its item is dropped. It ignores every other webhook. Replaying does nothing more: the items are gone after the first.

## HTTP and resilience

Every call is a `POST` with the client id and secret in Plaid's `PLAID-CLIENT-ID` and `PLAID-SECRET` headers, never in a body or a log, and `Plaid-Version: 2020-09-14`. The named client `Plaid` uses the standard resilience handler, with retries only for calls marked safe to repeat (`/link/token/create`, `/auth/get`, `/accounts/get`, `/transactions/sync`, `/institutions/get_by_id`, `/item/remove`, `/webhook_verification_key/get`) through `PlaidClient.RetryableKey`. Errors become `PlaidApiException` with Plaid's `error_type`, `error_code` and `request_id`; tokens, numbers and bodies are never logged. `PlaidCredentials.ToString()` leaves out the secret.

## Sharing Plaid with other modules

A tenant has one Plaid setup: one client id and secret, and one webhook endpoint. Another module that needs Plaid, such as an accounting app's bank transaction feed, uses it through `CrestApps.OrchardCore.Plaid.Abstractions` and depends on the `CrestApps.OrchardCore.Plaid` feature (`PlaidConstants.Features.Plaid`); it keeps its own records (connections, transactions, rules).

| Seam | What it is for |
| --- | --- |
| `IPlaidClient` | Plaid calls with the tenant's credentials: `CreateLinkTokenAsync`, `ExchangePublicTokenAsync`, `GetAccountsAsync` (every account of an item, with balances), `SyncTransactionsAsync` (`/transactions/sync`, a page at a time), `GetInstitutionAsync`, `UpdateItemWebhookAsync` (`/item/webhook/update`: moves an item's webhooks to another URL, such as this tenant's endpoint after an app drops its own), `RemoveItemAsync`, and `IsConfiguredAsync`. A call throws `InvalidOperationException` when Plaid is not set up and `PlaidApiException` when Plaid answers with an error. `PlaidClient` (in `.Core`) makes the same calls with explicit credentials. |
| `IPlaidLinkSite` | Builds a `PlaidLinkTokenRequest` with an opaque `client_user_id`, the site's name and language, and this tenant's webhook URL. Set `Products` (such as `["transactions"]`), `AccountFilters` (empty for every account), `CountryCodes`, `TransactionsDaysRequested`, or `AccessToken` for update mode, then call `IPlaidClient.CreateLinkTokenAsync`. Items made this way send their webhooks to `POST /plaid/webhook`. `GetWebhookUrlAsync()` returns that URL, from the request or, outside one (a migration, a background task), from the site's **Base URL**. |
| `IPlaidWebhookHandler` | Register with `services.AddPlaidWebhookHandler<T>()`. Gets a `PlaidWebhookContext` (the `PlaidWebhook`, with `WebhookType`, `WebhookCode`, `ItemId`, `AccountId`, `Error`, `NewTransactions`, `InitialUpdateComplete`, `HistoricalUpdateComplete`, and the exact `Payload`) for every verified webhook, such as `TRANSACTIONS: SYNC_UPDATES_AVAILABLE`, `ITEM: ERROR` or `ITEM: PENDING_EXPIRATION`. Return at once for webhooks about items you don't own, and be idempotent. |
| `IPlaidCredentialsProvider` | Where the credentials come from. The default reads `PlaidSettings` and the named Orchard secret. A host that holds the keys itself, such as one set of platform keys for every tenant, registers its own with `services.AddPlaidCredentialsProvider<T>()`, which replaces the default whether it is registered before or after the Plaid feature. Returning `null` turns Plaid off for the tenant. |

```csharp
public sealed class BankFeedWebhookHandler : IPlaidWebhookHandler
{
    public async Task HandleAsync(PlaidWebhookContext context, CancellationToken cancellationToken = default)
    {
        if (!context.Is("TRANSACTIONS", "SYNC_UPDATES_AVAILABLE"))
        {
            return;
        }

        // Look up your connection by context.Webhook.ItemId and queue a sync with IPlaidClient.SyncTransactionsAsync.
    }
}

// In the module's startup:
services.AddPlaidWebhookHandler<BankFeedWebhookHandler>();
```

## Extending

`AddPlaidCore()` registers everything but two host services, which the module provides: `IPlaidCredentialsProvider` (reads `PlaidSettings` and the named Orchard secret, registered only when no other provider is) and `IPlaidLinkSite` (builds the link token request from the current request). Another host can register its own.
