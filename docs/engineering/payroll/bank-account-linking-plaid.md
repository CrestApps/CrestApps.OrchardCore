# Instant bank account verification (Plaid) — design note

Status: **draft, waiting for Mike's agreement**. Nothing is built yet. Branch `ma/keen-yalow-7407fa`, on top of `ma/payroll-module-design-f04b4a` (Banking exists only there).

## 1. Goal

A person links their bank in a pop-up, signs in to it, picks the account, and the bank account is saved with the bank's own routing and account numbers and the status **Verified** at once. No typed numbers, no prenote wait, no administrator setting the status by hand.

Today (plan section 13, F3 and F5): an administrator sets the status, or **Check with Test entry (prenote)** sends a zero-amount ACH entry and verifies the account days later when the batch settles.

## 2. Vendor: Plaid

- Plaid **Auth** returns the ACH routing and account numbers of an account the person signed in to, through Plaid Link (a browser pop-up Plaid hosts). It covers almost every US bank and credit union. Banks it can't reach instantly fall back to Plaid's own micro-deposit or database checks, which finish later by webhook.
- Environments: **Sandbox** (`https://sandbox.plaid.com`, test banks, free) and **Production** (`https://production.plaid.com`). Plaid retired its Development environment.
- United States only in the first version (`country_codes: ["US"]`), which matches the only routing validator Banking has. Canada can follow with Banking's Canadian validator.
- Other vendors (Stripe Financial Connections, MX, Finicity) plug into the same Banking seam (section 4) as their own modules later.

### SDK decision (recorded)

Plaid publishes no official .NET library; `Going.Plaid` is a community package. **Decision: no new package.** The module calls five JSON endpoints with a named `HttpClient` from `IHttpClientFactory` and the standard resilience handler, as `Geo.GoogleMaps` does. The calls are small, and a fake `HttpMessageHandler` tests them without a network. Webhook signatures are checked with `System.Security.Cryptography` (ES256), no JWT package.

## 3. The flow

```
Browser                        Our site (tenant)                       Plaid
  | click "Link your bank" ------> POST banking/link/plaid/start
  |                                  /link/token/create  ----------------->  link_token
  | <------ link_token ------------
  | Plaid Link pop-up: sign in, pick account (Plaid's own page)
  | onSuccess(public_token, account_id) ---> POST banking/link/plaid/complete
  |                                  /item/public_token/exchange --------->  access_token, item_id
  |                                  /auth/get (account_id) -------------->  routing, account, mask, subtype
  |                                  IBankAccountService.ApplyLinkAsync: numbers + Verified
  |                                  /item/remove (when nothing waits on the item)
  | <------ redirect to the account (Verified)
```

1. **Link token.** `POST /link/token/create` with `client_id`, `secret`, `user.client_user_id` (a hash of the tenant and user id, never an e-mail), `client_name` (site name), `products: ["auth"]`, `country_codes: ["US"]`, `language` from the request culture, `account_filters` limited to checking and savings, and `webhook` (this tenant's absolute webhook URL). The token lives 4 hours and is used once; it is never stored.
2. **Plaid Link** runs in the browser from Plaid's script (`https://cdn.plaid.com/link/v2/stable/link-initialize.js`; Plaid requires it from its CDN, so it is a CDN-only resource in the module's resource manifest, not a vendored asset). `onSuccess` gives the `public_token` and the chosen account's `id`, `mask` and `subtype`, and the institution name. `onExit` with an error shows it; a plain close does nothing.
3. **Exchange.** `POST /item/public_token/exchange` returns `access_token` and `item_id`. Never retried (a public token is single-use; a retry after a lost reply fails with `INVALID_PUBLIC_TOKEN`).
4. **Numbers.** `POST /auth/get` with `options.account_ids: [account_id]`. From `numbers.ach[0]`: `routing`, `account`, `is_tokenized_account_number`. From `accounts[0]`: `subtype` (checking/savings → `BankAccountKind`), `verification_status`, `name`.
5. **Save.** Through the new `IBankAccountService.ApplyLinkAsync` (section 4): the numbers go through `ApplyNumbers` (so the US routing check, protection, `NumberChanged` and the observers all run as for typed numbers), then `TrySetStatus(Verified, verificationMethod: "Plaid")`. `BankName` is filled from the institution when empty.
6. **Clean up.** When the account is verified and its number is a real one, `POST /item/remove` at once: we keep no access token at all (section 6).

### Pending verification

When `/auth/get` has no numbers yet (`verification_status` is `pending_automatic_verification` or `pending_manual_verification`, Plaid's micro-deposit paths), the account moves to **Verification sent** with method `Plaid`, and the item is kept. The `AUTH: AUTOMATICALLY_VERIFIED` webhook calls `/auth/get` again and finishes step 5; `AUTH: VERIFICATION_EXPIRED` moves it to **Failed**. Manual micro-deposit entry (the person types two amounts in Plaid Link again, in "update mode") is **out of the first version**: such banks are offered only automatic micro-deposits, and the person can still use the prenote or type the numbers.

### Tokenized account numbers

Some banks (Chase, PNC, US Bank and others) give Plaid a tokenized account number: it works for ACH but stops working if the person revokes Plaid's access at the bank. For those the item is kept. Only revocation kills the number (an expired bank sign-in, `ITEM_LOGIN_REQUIRED`, does not), so `USER_PERMISSION_REVOKED` (and `ITEM: PENDING_DISCONNECT` past its date) moves the account to **Unverified** with a reason, so no payment goes to a dead number; the observers tell Payroll as for any status change.

## 4. The Banking seam (reuse ladder step 3: the owner gains the extension point)

`IBankAccountVerifier.StartAsync(account)` is a server call on an account that already has numbers. Linking is different: it needs a browser step and it *produces* the numbers. Squeezing it into the verifier would mean Banking pages calling Plaid-specific script. So Banking gains a second, vendor-neutral seam, and Plaid is its first implementation:

```csharp
// Banking.Abstractions
public interface IBankAccountLinkProvider
{
    string Name { get; }                       // "Plaid", stored as VerificationMethod
    LocalizedString DisplayName { get; }       // "Plaid"
    Task<bool> IsAvailableAsync(CancellationToken ct = default);   // configured for this tenant
    Task<BankAccountLinkStart> StartAsync(BankAccountLinkContext context, CancellationToken ct = default);
    Task<BankAccountLinkResult> CompleteAsync(BankAccountLinkContext context, string clientResult, CancellationToken ct = default);
}
```

- `BankAccountLinkContext`: the account (new or existing), the signed-in user id, the return URL, the culture.
- `BankAccountLinkStart`: the shape name the browser part renders (`BankAccountLink_Plaid`) and an opaque client payload (the link token). Banking renders the shape; the provider module ships the shape's view and script.
- `BankAccountLinkResult`: succeeded or refused (with a message), routing and account numbers (clear text, in memory only), kind, bank name, holder name if known, and the status (`Verified` or `VerificationSent`).
- `AddBankAccountLinkProvider<T>()` beside `AddBankAccountVerifier<T>()`; `IBankAccountService.LinkProviders` beside `Verifiers`.
- `IBankAccountService.ApplyLinkAsync(account, providerName, result)`: refuses a closed account, calls `ApplyNumbers`, then `TrySetStatus(result.Status, verificationMethod: providerName)`, and saves. The result's numbers never touch a log or the observers (they see the masked number only, as today).

Banking owns the pages and endpoints, so the vendor module never adds routes to Banking's screens:

- `POST banking/link/{provider}/start` and `POST banking/link/{provider}/complete` (anti-forgery, JSON). For a new or own account: `ManageOwnBankAccounts` plus the owner check `MyBankAccountsController` already does. For any account: `ManageBankAccounts`.
- The provider id in the route is matched against registered providers only.

## 5. Where it shows

- **My Bank Accounts > Add**: above the form, one **Link your bank with {provider}** button per available provider, with "or enter the numbers yourself" below. Linking creates the account (name defaults to "{Bank} ••••1234", owner as `NewAccountAsync` picks it today) and returns to the list with "Your bank account has been added and verified."
- **My Bank Accounts > Edit** (own account, not closed): **Link your bank instead** replaces the numbers with the bank's and verifies. A changed number fires `NumberChanged`, so Payroll's security notice still goes out (decision 3).
- **Admin bank account edit page**: in the action bar's secondary buttons, beside **Check with {verifier}**, a **Link with {provider}** button. Useful when an administrator sits with the person; the person signs in to their own bank on the administrator's screen.
- **List rows**: the existing status and method show "Verified (Plaid)"; no new column.
- **Settings**: **Configuration > Settings > Plaid** (environment, client id, secret reference, a **Test the connection** button that calls `/link/token/create`, and the webhook URL to copy). Group permission `ManagePlaidSettings`, registered with the settings group guard.
- Plaid Link needs `cdn.plaid.com` (script) and `*.plaid.com` (frame) allowed if the site sends a Content Security Policy; the docs say so.

## 6. Credentials and stored data

- **Plaid secret**: an Orchard secret only. The settings keep `SecretName` (default `Plaid.Secret`), picked with the `SelectSecret` view component, so the feature depends on `OrchardCore.Secrets`. Read with `ISecretManager.GetSecretAsync<TextSecret>`. No protected copy in our settings (the brief says never in our own protected setting).
- **Client id and environment**: Plaid's client id is an identifier shown in its dashboard, not a credential, so it and the environment are plain settings (decision 2). Separate sandbox and production secrets are two secret names, chosen with the environment.
- **Access token**: kept only while something waits on the item (pending micro-deposits, a tokenized number). Then it is stored in the module's own `PlaidItem` document (own collection and index: `ItemId`, `BankAccountId`, `Reason`, created and updated times), protected with `ISensitiveValueProtector` under the purpose `CrestApps.OrchardCore.Plaid.AccessToken`. Banking's record gets nothing vendor-specific.
- When the account is closed, deleted or its numbers are typed over: an `IBankAccountObserver` in the module calls `/item/remove` and deletes the `PlaidItem`.
- **Not exported**: `PlaidItem` documents are tenant-bound (their tokens are protected with the tenant's keys) and are never in a recipe or deployment. The Plaid settings have a recipe step and deployment step without the secret value (the secret travels with Orchard's own Secrets step).
- Personal data: the module's `IPersonalDataContributor` (as Stripe has) lists linked items for a user's data export and removes them on erasure.

## 7. Webhooks through the durable inbox

- Endpoint `POST plaid/webhook` (anonymous, no anti-forgery), per tenant URL.
- **Verify first**: header `Plaid-Verification` is a JWT (ES256). Read its `kid`, fetch the key with `POST /webhook_verification_key/get` (cached by `kid`, a key that is expired is refused), check the signature, `iat` no older than 5 minutes, and that `request_body_sha256` equals the SHA-256 of the raw body (constant-time compare). Anything else: 400, nothing stored, a warning without the body.
- Then `IProviderWebhookInbox.AcceptAsync` with `ProviderName = "Plaid"`, `HandlerName = "Plaid.Webhook"`, `DeliveryId` = the SHA-256 of the body (Plaid sends no event id, so the same retried body dedupes), and the payload. Reply 200 at once.
- `PlaidWebhookInboxHandler` (`IProviderWebhookInboxHandler`, replay-safe: every action is a status move the transition table allows or a no-op):
  - `AUTH: AUTOMATICALLY_VERIFIED` → `/auth/get`, `ApplyLinkAsync`, then `/item/remove` unless tokenized.
  - `AUTH: VERIFICATION_EXPIRED` → Failed, reason "The bank did not confirm the test deposits in time.", `/item/remove`.
  - `ITEM: USER_PERMISSION_REVOKED`, `ITEM: PENDING_DISCONNECT` → for a tokenized number, Unverified with a reason; `PlaidItem` deleted.
  - Unknown `item_id` (already removed) or other codes → logged at debug, done.
- Local sites can't receive webhooks without a tunnel; the instant path needs none.

## 8. HTTP and resilience

- Named client `Plaid`, base address from the environment, `AddStandardResilienceHandler` with retries allowed only on calls that are safe to repeat (`/link/token/create`, `/auth/get`, `/webhook_verification_key/get`, `/item/remove`), marked per request; `/item/public_token/exchange` is never retried.
- Plaid errors (`error_type`, `error_code`, `display_message`) map to a refusal the person can read (`display_message` when Plaid gives one, else a localized generic message); `request_id` is logged, never the token, the numbers or the body.
- Telemetry only on the module's own operations (memory rule: own components only).

## 9. Projects

- `src/Core/CrestApps.OrchardCore.Plaid.Core`: `PlaidClient` (the five calls, request and response records), `PlaidWebhookVerifier`, `PlaidOptions`. References `Banking.Abstractions` only.
- `src/Modules/CrestApps.OrchardCore.Plaid`: feature **Plaid bank account linking** (`CrestApps.OrchardCore.Plaid`, category Finance, depends on Banking and `OrchardCore.Secrets`): settings driver and views, the link provider, the `BankAccountLink_Plaid` shape (view + a small ES module that opens Plaid Link and posts the result), webhook endpoint, inbox handler, `PlaidItem` store, index and migration, observer, recipe and deployment steps with schemas, permissions.
- Banking changes: the seam in section 4, the link endpoints, the buttons on three pages.
- Named after the vendor like `Stripe` and `Telnyx`, so later Plaid features (balance checks before an ACH debit for Payments) live in the same module.

## 10. Tests and docs

- `PlaidClientTests` against a fake handler: request bodies (no e-mail in `client_user_id`, products, filters, webhook URL), each response, error mapping, no retry on exchange.
- `PlaidWebhookVerifierTests` with a locally generated P-256 key: good signature, wrong body hash, old `iat`, unknown `kid`, `alg` other than ES256.
- `BankAccountServiceTests`: `ApplyLinkAsync` on a new account, on an account with other numbers (`NumberChanged` + Verified), on a closed account (refused), pending → Verification sent.
- `PlaidLinkProviderTests`: instant (item removed), pending (item stored, token protected), tokenized (item kept).
- `PlaidWebhookInboxHandlerTests`: each code, replay, unknown item.
- Authorization tests for the link endpoints (own account only, other person's account 404, no permission 403). Architecture guards (recipe schema coverage, settings group permission, layering, project references).
- Docs: `modules/banking.md` (linking, the seam), a new `modules/plaid.md` (setup, sandbox, CSP, webhooks), both manuals' Banking pages, the feature reference, the changelog.

## 11. Decisions for Mike

1. **Seam**: a new `IBankAccountLinkProvider` in Banking (recommended) rather than squeezing linking into `IBankAccountVerifier`.
2. **Client id and environment as plain settings**, only the secret in Orchard Secrets (recommended). The alternative is all three as secrets, which makes switching environment awkward and hides a value that is not secret.
3. **Linking an account that already has numbers replaces them with the bank's** and fires `NumberChanged` (recommended: the bank is the source of truth, and Payroll's security notice still goes out). The alternative is to refuse when they differ and ask the person to check.
4. **Keep no access token unless needed** (pending micro-deposits, tokenized numbers), removing the item right after the numbers are read (recommended: least stored secrets, no ongoing Plaid item billing).
5. **No new NuGet package** (plain HTTP, recorded in section 2).
6. **First version is US only and skips manual micro-deposits** (the person can still type the numbers or use the prenote).

## 12. Live sandbox run

Needs Plaid sandbox keys (client id and sandbox secret) from Mike. With them: link the sandbox bank (`user_good` / `pass_good`) from My Bank Accounts and from the admin page, a pending automatic micro-deposit account (Plaid's sandbox `/sandbox/item/set_verification_status`), and a webhook through a tunnel. Without them it is listed as owed.
