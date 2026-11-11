# Workstream SEC: security findings

Part of the [November 2026 review plan](README.md). Paths are relative to `src/`. `M/X` is `Modules/CrestApps.OrchardCore.X`, `C/X` is `Core/CrestApps.OrchardCore.X`, and `A/X` is `Abstractions/CrestApps.OrchardCore.X`.

> **Handling.** This file describes vulnerabilities that are not fixed yet. It was published with the rest of the plan on 2026-11-11, by the contributor's decision, so that the fixes can be planned and reviewed in the open. Treat the Critical and High items as urgent: phase 1a in [`README.md`](README.md) lists the small fixes that close the worst gaps, and each can ship alone. Operators of multi-tenant or tenant-selling sites should read SEC-C2, SEC-C18, SEC-A1 and SEC-A2 first.

## How to read this file

- Reviewed on `main` at `a12102c7a`, read-only: nothing was built, run or exploited. Each finding cites the code that was read. **Confidence** says how sure the review is that the flaw is real and reachable; where it depends on Orchard Core or provider behavior that was not run, the finding says so.
- **Severity** is the impact on a site that has the feature enabled, rated Critical, High, Medium or Low. A feature that is off by default is noted, not downgraded.
- Every fix starts with the failing test listed under **Tests** (see *Test-Driven Development* in `AGENTS.md`). If the test cannot be made to fail, the finding is wrong: record that in the pull request and close the item.
- One finding is one commit. Unit tests go in `tests/CrestApps.OrchardCore.Tests`; commerce end-to-end tests in `tests/CrestApps.OrchardCore.Commerce.IntegrationTests`; Contact Center multi-node tests in `tests/CrestApps.OrchardCore.ContactCenter.DistributedTests`.
- Ids: `SEC-C` commerce, `SEC-P` platform, communication and workforce, `SEC-A` AI and data, `SEC-FE` views and scripts. Payment rate limiting has its own workstream, `02-payment-rate-limits.md`.
- Some fixes belong in the CrestApps.Core repository (SEC-FE4f, SEC-A16, SEC-A19 and parts of SEC-A5 and SEC-A20). Those items say what to do here in the meantime and what to raise upstream.
- Types named in a fix that do not exist yet are new, and the item says where they go. Existing types the fixes build on: `ICheckoutPaymentGate`, `IPaymentIssueRecorder`, `SignedTokenService`, `EgressNetwork` (`AddEgressGuardedHttpClient`, `IsAllowedUrl`), `IRoleGrantService`, `ProtectedSettingExtensions`, `CsvWriter`, `IAIToolAccessEvaluator`, `IStoreSetupStep`, `SubscriptionOnboardingSettings`.

## Findings by severity

| Severity | Id | Finding | Size |
| --- | --- | --- | --- |
| Critical | SEC-C1 | Checkout completes on a payment begun for a smaller total | M |
| Critical | SEC-C2 | A tenant purchase lets the buyer choose any URL prefix, host name and setup recipe | M |
| Critical | SEC-A1 | The MCP local (stdio) transport lets a tenant run any process on the host (High on a single-tenant site) | S, then M |
| Critical | SEC-A2 | The MCP "File" resource reads the whole application content root (High on a single-tenant site) | M |
| High | SEC-C3 | Registering with someone's e-mail address takes over their guest orders | M |
| High | SEC-C4 | Any shopper removes reverse-charge tax by calling themselves a business | M |
| High | SEC-C5 | Pay Later and offline methods hand over goods to anyone; unpaid subscriptions never lapse (off by default) | M |
| High | SEC-C7 | Card testing: the payment throttle resets with every new checkout (workstream RL) | S hotfix, then M |
| High | SEC-C18 | A sold tenant has no feature profile, so its buyer can enable host-level features | S |
| High | SEC-P1 | E-mail code sign-in can be brute-forced | M |
| High | SEC-P2 | Secure keypad digits can reach the model and the transcript on a multi-node site (CQ-1) | M |
| High | SEC-FE2 | `escapeHtml` in the soft phone does not escape quotes | S |
| High | SEC-A3 | AI tools run with system authority; per-tool permission applies only on render | S, then L |
| High | SEC-A6 | No egress guard on MCP HTTP, A2A, web crawler and provider endpoints (Medium on a single-tenant site) | M |
| High | SEC-A7 | A content import overwrites and takes ownership of any content item | S to M |
| High | SEC-A8 | The SQL Server connector lets a tenant sign in as the host's identity | S |
| Medium | SEC-C6, SEC-C8 to SEC-C16 | UCP profile fetch before authentication; AP2 mandate binding; agent signature replay; membership roles; marketplace payouts; parallel charges; punchout sign-in; stock holds; cashier refunds; tenant entitlement | S to M |
| Medium | SEC-P3 to SEC-P9 | Paid address lookups; web-push request forgery; calendar subscription guard; self-approval; supervisor AI-call scope; Content Access Control coverage; public booking page | S to L |
| Medium | SEC-FE1, SEC-FE3, SEC-FE4 a to f | Map labels rendered as HTML; Secure Capture and card data (owner decision); inline confirm injection; prompt-template list; Classifieds map; secrets in text fields; service-worker cache; missing antiforgery on document posts | S to L |
| Medium | SEC-A4, SEC-A5, SEC-A9 to SEC-A13, SEC-A15 to SEC-A19 | Template-prompt profiles in the chat hub; AI cost limits; API key roles; Copilot OAuth state; SELECT validator; report views and subscriptions; AI memory; Markdown images; ContentTransfer; wizard sessions; remote fetch limits | S to M |
| Low | SEC-A14, SEC-C17, SEC-P10, SEC-FE4 g to h, SEC-A20 | MCP content resources, and 45 smaller items in four tables | S each |

Severities were adjusted after an independent challenge of this plan; [`06-review-log.md`](06-review-log.md) records each change and why.

Fix order: the small first fixes for the Critical items and for SEC-C7 and SEC-P1 ship first, each alone (phase 1a in [`README.md`](README.md)); then the rest of Critical and High; then Medium; then Low.

## Commerce

### SEC-C1. Checkout completes on a payment that was begun for a smaller total (Critical, M)

- **Where:** `C/Checkout.Core/Services/DefaultCheckoutEngine.cs:435-513` (completion), `:796-802` and `:972-977` (`MatchesInvoice` runs only when a payment begins); `C/Checkout.Core/Services/CheckoutReconciliationService.cs:102-108,191-229`; `C/Checkout.Core/Api/CheckoutApi.cs:27-32`; `M/Checkout/Controllers/CheckoutController.cs:127-180,388-398`; `M/Orders/Drivers/OrderBumpCheckoutFlowDisplayDriver.cs:71-102`; `M/Shipping/Api/ShippingCheckoutApiEndpoints.cs:93-128`; `M/Customers/Api/CustomerCheckoutApiEndpoints.cs:72-103`; `C/Orders.Core/Handlers/OrderCheckoutHandler.cs:142-171`; `M/Stripe/Services/StripeCheckoutPaymentProvider.cs:359-380`.
- **What:** Completion settles an obligation by its id when its attempt succeeded. The amount the provider charged is compared with `attempt.ExpectedAmount` (`TryValidateSettlement`), never with the invoice as it is now. Sessions in `AwaitingProvider` and `PaymentPending` are still served for editing by the checkout page (`GetLiveSessionAsync`) and by every step, code and tender endpoint in the API (`CheckoutApi._open`). `OrderCheckoutHandler` then writes `order.Total = invoice.GrandTotal` and marks the order paid. Only `AgentCheckoutService.cs:534,856` and `PosSaleService.cs:205` guard against this.
- **Exploit:** A guest chooses free pickup and calls `checkout/{id}/payment/begin`, which creates a payment for X. Without beginning again, they switch to express shipping, tick the order bump, or change the address to a taxed region. They confirm the old client secret and call `payment/status`. The order is paid at X and recorded, and fulfilled, at X plus the difference.
- **Fix, in two commits. The first closes the money gap alone and ships first.**
  1. **Check the amount at completion.** In `TryCompleteCoreAsync`, before completing, compare every settled attempt with the invoice as it is now: the attempt's confirmed amount (or its `ExpectedAmount` when the provider reports no authoritative amount, since `TryValidateSettlement` skips the amount check when `!ReportsAuthoritativeAmount`, `CheckoutReconciliationService.cs:200-207`) against `GetObligationAmounts(invoice, attempt.ObligationId)` (`DefaultCheckoutEngine.cs:1142`, the computation `MatchesInvoice` uses), in minor units (`Money.ToMinorUnits`), for each obligation including recurring groups (`:960`). If any is short, do not complete: record a `PaymentIssue` and refund through `CompensateAsync`.
  2. **An edit supersedes the payment.** Under the session lock, any editing endpoint (`CheckoutController.DisplayPost` and `OnePagePostAsync` before `UpdateEditorAsync`; the API's step, code and tender endpoints) that finds a live attempt (`Created` or `Pending`) cancels it at the provider and supersedes it (`TrySupersedeAsync`, which begin already uses on a mismatch at `:797-798`). The stale client secret is then dead, and the buyer pays the new total on the next begin. Answer 409 "payment in progress" only when the provider refuses to cancel (processing, captured or succeeded). A buyer who returns from a wallet, a redirect or 3-D Secure to a cancelled attempt simply pays again. No new engine API and no new screen.
- **Tests:** Integration: begin a payment, change shipping (then the bump, then the address) and confirm the old amount; the order is not paid, and after commit 2 the old client secret is cancelled at the provider. Unit: `TryCompleteCoreAsync` with a settled attempt lower than the invoice refuses and records an issue, for a provider with and without an authoritative amount; an edit with a live attempt supersedes it; an edit while the provider refuses to cancel answers 409.
- **Confidence:** High (code path read end to end; not run).

### SEC-C2. A tenant purchase lets the buyer choose any URL prefix, host name and setup recipe (Critical where tenant plans are sold, M)

- **Where:** `M/Subscriptions/Drivers/TenantProvisioningStepDisplayDriver.cs:101-179`; `C/Subscriptions.Core/Services/TenantProvisioningService.cs:237,259,281-290`.
- **What:** `Prefix`, `Domains` and `RecipeName` are bound from the checkout form. The only checks are that no other tenant already has that prefix (`HasUrlPrefix`) or host (`HasUrlHost`). There are no reserved prefixes, no proof the buyer controls the host name, and no use of Orchard's `ITenantValidator`. `RecipeName` replaces the plan's recipe with any setup recipe on the host.
- **Exploit:** The host-name case needs no assumption about matching order: `HasUrlHost` is checked only against other tenants (`:126`), and the Default tenant normally has no host configured, so a buyer who enters the operator's own host name gets a tenant that answers every request to that host. They are its administrator: the operator's site, admin and webhooks are replaced, and the buyer can serve a sign-in page on the real address. The prefix case (`admin`, `Login`, `stripe`) and the wildcard case (`*.operator.com`) follow from Orchard matching host and prefix, then host, then prefix, before the Default tenant; confirm those two with one test tenant.
- **Fix:** Take the recipe from the plan only (drop `RecipeName` from the step model). Apply the rules Orchard's own tenant validation applies (name, prefix and host format, uniqueness). Orchard's `ITenantValidator` is a type of the `OrchardCore.Tenants` module and cannot be referenced from a `.Core` library: expose the check through TenantHierarchy (`ITenantRequestValidator` in `A/TenantHierarchy.Abstractions`, implemented over Orchard's validator in the TenantHierarchy module) and call that. Extend the existing `SubscriptionOnboardingSettings` (`C/Subscriptions.Core/Models/SubscriptionOnboardingSettings.cs`) with `AllowCustomDomains`, `ReservedPrefixes` (seeded with the admin prefix from `AdminOptions`, `api`, `login`, `connect`, `stripe`, `media`, `checkout`, `cart`, `orders`, and editable by the operator), `ReservedHosts` (the host's own names, always refused, wildcards refused) and `BaseDomain`; derive the prefix from the tenant name unless the operator allows a choice. `AllowCustomDomains` defaults to off for new sites; a data migration sets it on where completed provisioning jobs already carry domains, so operators who sell custom domains keep working. Run the same validation again in an `ICheckoutPaymentGate` and in `TenantProvisioningService.SetupAsync`, so a saved step cannot bypass it. Moving provisioning onto `ITenantHierarchyBroker` is a separate item (DRY section 1), not part of this fix.
- **Tests:** The step refuses a reserved or empty prefix, the host's own domain, a wildcard host and a recipe the plan does not name; the gate refuses a step that was saved before the rule existed; the migration keeps custom domains on for a site that has them.
- **Confidence:** High on the code and on the host-name case; Medium on the prefix and wildcard matching order, which was not run.
- **See also:** SEC-C18. A sold tenant with no feature profile can enable host-level features, which turns this purchase into code execution on the host through SEC-A1.

### SEC-C3. Registering with someone's e-mail address takes over their guest orders (High, M)

- **Where:** `C/Customers.Core/Services/DefaultCustomerMergeService.cs:48-57`; `M/Customers/Handlers/CustomerMergeUserEvents.cs:21-27,60-73`; `M/Subscriptions/Drivers/UserRegistrationCheckoutFlowDisplayDriver.cs:124-130`; `M/Orders/Customers/OrderCustomerMergeHandler.cs:28-76`.
- **What:** The merge treats `EmailConfirmed` as proof that the user controls the mailbox and runs on `RegisteredAsync`. A site that does not require e-mail validation marks the address confirmed at registration (the handler's own comment says so), and the checkout account step sets `EmailConfirmed = !UsersMustValidateEmail`.
- **Exploit:** On a tenant without mandatory e-mail validation, register with a guest buyer's address. Their guest customer records, orders and transactions move to the attacker's account: addresses, phone numbers and invoices are exposed, and the attacker can request returns and open pay links. Because the merge also runs on every `LoggedInAsync`, the attacker keeps absorbing the victim's later guest orders. Only guest records are merged (`CustomerStore.cs:95-98`); an existing account's records are not at risk.
- **Fix:** Merge only after a token round trip. Set a `GuestClaimVerifiedUtc` marker in the user's `Properties` in `ConfirmedAsync` (already overridden at `CustomerMergeUserEvents.cs:86`; the user followed the confirmation link) or from a claim link mailed through `ICommerceEmailSender` and signed with `SignedTokenService` (purpose `Customers.GuestClaim`). Remove the `RegisteredAsync` trigger; `LoggedInAsync` merges only when the marker is set. Fix SEC-C17h (an account created from an order with an unverified e-mail) in the same change: it is the same assumption.
- **Tests:** A user with `EmailConfirmed = true` and no marker merges nothing; after the claim token the guest records merge.
- **Confidence:** High on the code; Medium on Orchard's registration default.

### SEC-C4. Any shopper removes reverse-charge tax by calling themselves a business (High, M)

- **Where:** `C/Taxation.Core/Services/TaxService.cs:368-373`; `M/Customers/Handlers/CustomerCheckoutTaxProfileHandler.cs:58-65`; `M/Customers/Api/CustomerAccountApiEndpoints.cs:74-84`.
- **What:** Reverse charge applies when `CustomerType == B2B`. That value comes from `customer.Kind`, which the shopper sets through `PUT me` or the profile page. No registration number is read or validated.
- **Exploit:** Set the kind to business, then check out: every `ReverseCharge` rule charges nothing, even with an empty `TaxId`. No reverse-charge rule is seeded, so this applies once a store configures one.
- **Fix:** Reverse charge requires a non-empty registration number and an administrator-set `TaxIdVerified` flag on the customer (or on the B2B account, whose tax id already feeds the tax profile, audit defect 3). The shopper cannot set the flag. A validator seam for automatic checks (`ITaxRegistrationValidator`, for VIES and similar) is a later addition, not part of this fix. This also moves toward the audit's goal of dropping business fields from `Customer`.
- **Tests:** A business customer without a verified number is taxed; with one, reverse charge applies; the shopper cannot set the verified flag through the API.
- **Confidence:** High.

### SEC-C5. Pay Later and offline methods hand over goods to anyone, and unpaid subscriptions never lapse (High when the feature is on, M)

- **Where:** `M/PayLater/Services/PayLaterCheckoutPaymentProvider.cs:73-74`; `M/PayLater/Models/PayLaterSettings.cs`; `M/PayLater/Services/OfflineCheckoutPaymentProvider.cs:79-107`; `C/Subscriptions.Core/Handlers/SubscriptionActivationCheckoutHandler.cs:83-119`.
- **What:** The method is available whenever `settings.Enabled` is true. `VerifyAsync` answers succeeded, so the order becomes paid and grants run at once. Nothing implements `ITransactionEventHandler.BecameOverdueAsync`. Pay Later is off by default.
- **Exploit:** With the feature on, a guest buys a download or a tenant plan by Pay Later or bank transfer, receives it immediately and never pays. Memberships already refuse guests (`MembershipHandlers.cs:46-47`).
- **Fix, most important first:**
  1. When the provider `DefersPayment`, hold grants that deliver instantly (downloads, memberships, tenant provisioning) until the transaction is paid, behind a per-product "deliver before payment" option that defaults to off.
  2. A Subscriptions handler for `BecameOverdueAsync` that calls `MarkPastDueAsync`.
  3. `PayLaterSettings.RequireSignedIn` (default on) and `MaxOrderAmount`, enforced in `IsAvailableAsync` and in an `ICheckoutPaymentGate`. Role lists and credit limits are a later refinement (`DeferredBalanceTenderProviderBase` already carries limits for terms accounts).
- **Tests:** A deferred order grants nothing until paid; an overdue transaction moves the subscription to past due; a guest is refused; an order over the limit is refused.
- **Confidence:** High.

### SEC-C6. UCP fetches the caller's profile URL before it authenticates the caller (Medium, M)

- **Where:** `M/AgenticCommerce/Ucp/UcpRequestFilter.cs:34-75`; `M/AgenticCommerce/Ucp/UcpProfileClient.cs:58-94`.
- **What:** The `UCP-Agent` profile URL is fetched before credentials are checked. The 256 KB guard applies only when `Content-Length` is present; `ReadAsByteArrayAsync` buffers a chunked body without a limit (a 10-second timeout bounds the read). Each URL is cached with no size cap, and there is no rate limit. The client is egress-guarded (`UcpStartup.cs:56`), so internal hosts cannot be reached this way.
- **Exploit:** Anonymous POSTs that name a fast host streaming a large body use memory on every request, and each distinct URL adds a cache entry. The same call makes the store issue GET requests to third parties.
- **Fix, in this order:** read through a bounded stream; authenticate first, and fetch only a registered platform's `ProfileUrl` or, when unregistered platforms are allowed, only when `Signature-Input` is present; cap the cache (entries and bytes); add the `agent-checkout` rate-limit group (RL-3).
- **Tests:** An oversized chunked profile is refused after a bounded read; an unauthenticated unknown profile is never fetched.
- **Confidence:** High.

### SEC-C7. Card testing: the payment throttle resets with every new checkout (High, M)

Described and fixed in `02-payment-rate-limits.md` (RL-F1 to RL-F3). RL-0 is a two-line hotfix that ships first and alone; RL-2 and RL-4 complete the fix. Listed here so the severity table is complete.

### SEC-C8. The AP2 mandate is not bound to what is charged or shipped (Medium, M)

- **Where:** `M/AgenticCommerce/Ucp/UcpOperations.cs:147-180`; `C/AgenticCommerce.Core/Services/AgentCheckoutService.cs:262-283`; `M/AgenticCommerce/Ucp/UcpMcpEndpoints.cs:110-119`; `M/AgenticCommerce/Ap2/Ap2CheckoutExtension.cs:102-178`.
- **What:** The mandate is verified before the complete request's own buyer, fulfillment and discounts are applied. The match ignores the address, the shipping option and the currency. MCP `complete_checkout` skips `CompletingAsync` altogether.
- **Exploit:** Complete with a mandate for 100 plus a new address or express shipping, or complete through `/ucp/mcp` with no mandate. AP2 is negotiated per request (`AgentRequest.cs:71-72`), so a REST caller can also leave the mandate out.
- **Fix:** Store `Ap2Required` on the checkout when it is created, so the requirement no longer depends on what each request negotiates. Run the mandate check inside `AgentCheckoutService.CompleteAsync` after the final sync, through a new `IAgentCheckoutCompletionGate`, so every transport passes it. Refuse complete-time updates when AP2 applies.
- **Tests:** A mandate with changed fulfillment is refused; MCP complete without a mandate is refused.
- **Confidence:** High.

### SEC-C9. Agent request signatures can be replayed and are not bound to their target (Medium, M)

- **Where:** `C/AgenticCommerce.Core/Security/AcpRequestSignature.cs:196-234`; `C/AgenticCommerce.Core/Security/HttpMessageSignatures.cs:112-242`; `M/AgenticCommerce/Ucp/UcpRequestFilter.cs:184-212`.
- **What:** ACP signs only the body: the timestamp, method and path are not covered. UCP ignores `created`, `expires` and `nonce`, and does not require `@authority` or `content-digest`.
- **Exploit:** A captured request replays indefinitely, and against another path or another merchant.
- **Fix:** ACP signs `timestamp.method.path.JCS(body)`, versioned on `AgentPlatform` so existing platforms keep working until they move. UCP requires `@method`, `@authority`, `@path`, `content-digest` and `created` within five minutes either way. Keep a seen-signature cache for the window. Check the protocol references in `docs/engineering/commerce/agentic-commerce-protocols-reference.md` before changing what is signed.
- **Tests:** Verifier tests for a stale, a missing-component, a swapped-path and a replayed request.
- **Confidence:** High on the code (one reader).

### SEC-C10. A membership product can grant any role (Medium, S)

- **Where:** `M/Memberships/Drivers/MembershipDrivers.cs:41-61`; `M/Memberships/Services/MembershipService.cs:108-126`.
- **What:** The driver checks no permission, so anyone who can edit the product chooses the roles. The driver does drop excluded roles (Administrator) on the server (`MembershipDrivers.cs:47-49,60-61`), but a recipe or an API write bypasses the driver, and the grant applies the stored `RoleNames` unfiltered.
- **Exploit:** A product editor creates a cheap membership that grants a staff role and buys it. A recipe import can store Administrator.
- **Fix:** Require `ManageMemberships` and Orchard's role-assignment permission in the driver's update. Filter in `GrantLineAsync`, where every path ends: excluded roles are never granted, and `MembershipSettings.GrantableRoles` (an allowlist) applies when set. `IRoleGrantService` is the right home for an "is this role grantable by a purchase" rule shared with Subscriptions.
- **Tests:** The driver keeps the existing roles for a user without the permission; a stored Administrator role, or one outside the allowlist, is not granted.
- **Confidence:** High.

### SEC-C11. Marketplace payouts: a refund after payout is never recovered, and payouts are recorded after the transfer (Medium, M)

- **Where:** `M/Marketplace/Services/MarketplaceService.cs:141-192,247-351`; `M/Marketplace/Services/StripeConnectPayouts.cs:174`.
- **What:** A negative earning from a refund after payout sits alone in its `(OrderId, Currency)` group, which is skipped when it is zero or less. There is no per-vendor lock. Payout rows are saved at the end of the scope, and the idempotency key changes when the earnings change. This is what remains of audit defect 1.
- **Exploit:** A vendor and a colluding buyer refund after the payout and keep both. Separately, a lost save followed by a new refund entry can transfer twice.
- **Fix:** Carry a negative balance per vendor into the next payout, or reverse the transfer. Take `MARKETPLACE_PAYOUT_{vendorId}` through `DistributedLockExtensions`. Commit a `Pending` `VendorPayout` with its idempotency key before `PayAsync` and mark it paid after.
- **Tests:** The next payout is reduced after a refund; a retry reuses the stored key; two concurrent runs pay once.
- **Confidence:** High on recovery, Medium on the double payment.

### SEC-C12. Money collected twice by parallel charges (Medium, S each)

- **Where:** `M/Bookings/Services/BookingBalanceCharger.cs:63-144`; `M/Transactions/Services/TransactionSettlementCheckoutHandler.cs:147-162,191-196,396-401`.
- **What:** `ChargeAsync` takes no lock and starts a new session on each call. Transaction settlement fixes the billed amount when the checkout starts and has no payment gate, so a second checkout is captured too and the surplus is only logged.
- **Exploit:** Two concurrent calls (a double click, or a click while the background task runs) charge the saved card twice; a repeat after the first finished is already handled (`BookingBalanceCharger.cs:86-101`). Two tabs of a pay link both capture.
- **Fix:** Lock `BOOKING_BALANCE_{id}`, read the balance again inside the lock, and derive the provider idempotency key from the balance id and attempt number. Add a Transactions `ICheckoutPaymentGate` that refuses when the outstanding amount no longer equals the billed amount. Record a `PaymentIssue` for money that could not be applied. The UI side is UX-1.
- **Tests:** Two parallel calls begin one payment; a second session for a settled transaction is refused.
- **Confidence:** Medium.

### SEC-C13. Punchout signs a user in without checking the account and takes the key in the URL (Medium, S)

- **Where:** `M/Accounts.B2B/Controllers/PunchoutController.cs:48-77`.
- **What:** The action accepts GET with `?key=`, carries `[IgnoreAntiforgeryToken]`, and calls `SignInManager.SignInAsync` directly. The account is checked for being enabled (`:59`), but the punchout user is checked only for the literal role name `Administrator` (`:63`): not for being enabled, locked out or allowed to sign in. The key itself is sound: 256 bits, stored hashed, compared in fixed time, so guessing it is not the risk.
- **Exploit:** A disabled punchout user, or one later given another powerful role, still gets a session. With GET, the key is written to access logs and referrers. A page can sign a visitor in as the punchout buyer (login CSRF).
- **Fix:** Require `SignInManager.CanSignInAsync`, an enabled user, and no `AccessAdminPanel` permission. Add a `punchout` claim so the session can be recognized and limited. Accept POST by default; OCI "link" mode needs GET, so make GET an opt-in per account (`Punchout.AllowLinkSignIn`) with a hint that the key will appear in logs.
- **Tests:** A disabled user gets 401; an admin-capable user is refused; GET gets 405 unless the account opted in.
- **Confidence:** High.

### SEC-C14. Stock holds: an expired hold is sold anyway, and hold quotas can be bypassed (Medium, M)

- **Where:** `C/Inventory.Core/Services/DefaultInventoryService.cs:322-344`; `M/Inventory/Handlers/InventoryOrderEventHandler.cs:35-53`; `M/AgenticCommerce/Ucp/UcpCatalogEndpoints.cs:123-150`; `C/AgenticCommerce.Core/Services/AgentCheckoutService.cs:438-483`.
- **What:** `CommitAsync` sells when no hold is left. Agent carts have no cap, and a cart id is accepted where a checkout id is expected, outside `MaxOpenCheckoutsPerPlatform`.
- **Exploit:** Stock is oversold after a hold expires; an agent hoards all stock in carts.
- **Fix:** An Inventory `ICheckoutPaymentGate` that reserves again or refuses before payment begins. Cap carts per platform and reject cart-kind records in `WithCheckoutAsync`.
- **Tests:** An expired hold whose stock was sold elsewhere is refused at payment; the cart cap holds.
- **Confidence:** High on the code (one reader).

### SEC-C15. Any cashier can refund any counter sale and pay cash out of the drawer (Medium, S)

- **Where:** `M/PointOfSale/Api/PosApiEndpoints.cs:343-395`.
- **What:** `pos/returns` needs only `MaySellAtAsync` at some register. The order may belong to another register or location, and there is no separate permission. The refund is capped by the order and by the cash the sale took (`:377,387-391`) and is recorded against the cashier, as an ordinary drawer movement.
- **Exploit:** A cashier refunds another till's cash sale and keeps the cash; the drawer still reconciles.
- **Fix:** A `RefundAtRegister` permission. Require the same location or `ManageOrders`.
- **Tests:** A seller without the permission gets 403; a return across locations is refused.
- **Confidence:** High.

### SEC-C16. A tenant entitlement names the tenant the buyer typed, not the one that was provisioned (Medium, S)

- **Where:** `C/Subscriptions.Core/Handlers/TenantProvisioningCheckoutHandler.cs:193-204`; `M/Subscriptions/Services/TenantSubscriptionEntitlementApplier.cs:55-96`.
- **What and exploit:** The entitlement is added even when provisioning fails with "already in use". Revoking it then disables whichever tenant has that name, which can be another customer's.
- **Fix:** Add the entitlement only when the provisioning job succeeded, and have the applier require that job's id.
- **Tests:** A failed job leaves no entitlement; revoking never touches a tenant the job did not create.
- **Confidence:** Medium.

### SEC-C17. Smaller commerce items (Low, S each)

One unit test of the refused case for each.

| Id | Where | What | Fix |
| --- | --- | --- | --- |
| a | `M/Checkout/Controllers/DiscountLinkController.cs:42-66` | Anonymous `GET discount/{code}` says whether a code can be redeemed, with no throttle, and bypasses the `coupon-code` limiter | The `coupon-code` attempt policy and the `checkout-code` group; the same answer for a wrong and an unusable code |
| b | `M/Stripe/Services/StripeCheckoutCustomerResolver.cs:56-58`; `StripeRecurringPaymentProvider.cs:34,88-92` | `providerData.customerId` from the browser is used as the Stripe customer (needs a leaked `cus_` or `pm_` id) | Add it to `CheckoutPaymentDataKeys.IsServerOnly`; resolve on the server |
| c | `M/Stripe/Endpoints/CreateWebhookEndpoint.cs:83-84` | The body is read before the signature header is checked, bounded only by the server's default request limit (about 30 MB) | Check that the signature header is present first; then a 512 KB `RequestSizeLimit`, as the EasyPost endpoint has |
| d | `M/Shipping/Endpoints/EasyPostWebhookEndpoint.cs:35-82` | No replay or ordering guard; not on `IWebhookSignatureValidator` or `IProviderWebhookInbox` | Move to the shared webhook blocks |
| e | `M/Orders/Services/OrderOperationsService.cs:143-208`; `M/Transactions/Controllers/RefundsAdminController.cs:186-221` | No lock at the order or refund level: a double submit can credit store credit, or settle a manual refund, twice | Lock `ORDER_REFUND_{id}`; `checkConcurrency` on the save |
| f | `M/Orders/Controllers/OrderViewController.cs:116-137`; `OrderLinkService.cs:13` | An order number and an e-mail address open the full order; links last 60 days and cannot be revoked | Mail the link instead of redirecting; a per-e-mail attempt policy (RL-5) |
| g | `M/Orders/Controllers/OrderPayController.cs:42-88`; `M/Subscriptions/Controllers/SubscriptionsController.cs:65` | A GET starts a checkout, so link scanners create sessions | POST, or reuse the open session |
| h | `M/Orders/Controllers/OrderAccountController.cs:63-144` | An account is created with the order's unverified e-mail and signed in, so someone else's address can be claimed | Always require confirmation on this path |
| i | `M/AgenticCommerce/Acp/AcpRequestFilter.cs:137` | A linked buyer is bound without a scope check | Require the checkout scope, as UCP does |
| j | `M/AgenticCommerce/Controllers/AgentHandoffController.cs:74-82` | The hand-off link issues the guest token again on every GET for two days | Claim once |
| k | `M/AgenticCommerce/Feeds/AgentFeedSftpPusher.cs:78-84` | Any host key is trusted when no fingerprint is set | Require the fingerprint |
| l | `C/Commerce.Api.Core/IdempotencyEndpointFilter.cs:53-139` | No default body fingerprint; stored answers (client secrets, tokens) in clear; a two-minute lock; anonymous callers share one empty scope (`:131-136`), so one guest's key can replay another's answer | SHA-256 body fingerprint; protect stored answers with data protection; renew the lock; scope anonymous keys by the checkout or guest token, with a test |
| m | `M/Inventory/Api/InventoryApiEndpoints.cs:65-82` | Every stock location, vans included, is returned to guests | Public and pickup locations only |
| n | `M/Downloads/Services/DownloadService.cs:205-234` | Parallel requests overrun the download limit | Lock before resolving |
| o | `M/Classifieds/Drivers/ClassifiedDrivers.cs:84-111`; `M/Marketplace/Drivers/MarketplaceDrivers.cs:40-49` | Status and featured fields, and `VendorId`, are guarded by "is this an admin route" (`ClassifiedDrivers.cs:110-111`), not by `ModerateClassifieds` or `ManageMarketplace` | Check the permission in the drivers |
| p | `M/Markets/Services/ShopperMarketService.cs:77-84` | The market the shopper chose is never reconciled with the ship-to country | Resolve at checkout |
| q | `M/Shipping/Controllers/ShipmentLabelsAdminController.cs:131-154` | Buying a label takes no lock, and the quote is not bound to the shipment | Lock and bind |
| r | `M/Subscriptions/Drivers/SubscriptionRegisterUserFormDisplayDriver.cs:75-128` | The password policy is enforced only after capture; errors reveal existing users | Validate at the step with a uniform message |
| s | `M/Accounts.B2B/Services/B2BApprovalService.cs:102-122`; `M/Products/Services/ProductListingService.cs:1085` | The budget counts only paid orders, with no lock; product cards skip `ViewContent` | Lock; check the permission |

### SEC-C18. A sold tenant is provisioned with no feature profile, so its buyer can enable host-level features (High, S)

Added by the independent review.

- **Where:** `C/Subscriptions.Core/Services/TenantProvisioningService.cs:292-295` (`FeatureProfile` is applied only when set); `C/Subscriptions.Core/Handlers/TenantProvisioningCheckoutHandler.cs:101-102` (taken from optional onboarding settings); `M/Subscriptions/Drivers/TenantProvisioningStepDisplayDriver.cs:89-96,171` (the buyer picks any setup recipe).
- **What:** The buyer becomes the new tenant's Administrator. With no feature profile they can enable any feature the host has installed, including MCP Local Client (SEC-A1), MCP file resources (SEC-A2) and SQL Server connections that use the host's identity (SEC-A8).
- **Exploit:** Buy the cheapest plan, enable MCP Local Client, add a connection whose command is a shell: code execution on the host, and every tenant's data.
- **Fix:** Refuse to sell a tenant plan whose onboarding names no feature profile (validation on `TenantOnboardingPart`), and have `SetupAsync` refuse an empty profile. Restrict the recipe to the plan's (SEC-C2). Document a recommended "sold tenant" profile that excludes MCP Local Client, MCP file resources, data connectors with host identity, and Tenants.
- **Tests:** Provisioning without a profile fails; a sold tenant cannot enable a feature outside its profile.
- **Confidence:** High on the code. The impact is highest while SEC-A1 is open; the fix stays worthwhile as defense in depth.

### Commerce endpoints that move money or validate a secret

Used by RL-3 to tag endpoints. "Throttle" is what exists today.

| Route | File:line | Authentication | Antiforgery | Throttle today |
| --- | --- | --- | --- | --- |
| POST `checkout/{sessionId}/payment/begin`, `/status` | `M/Checkout/Endpoints/PaymentEndpoints.cs:32,38` | Anonymous; session owner or guest cookie | Disabled; same-origin check | `checkout-payment` group and limiter (address + session) |
| POST `Checkout/{sessionId}/{step?}` | `M/Checkout/Controllers/CheckoutController.cs:123` | Owner or guest cookie | Yes | `checkout` group; code limiters |
| POST `api/commerce/v1/checkout/{id}/payment`, `/complete` | `M/Checkout/Api/CheckoutApiEndpoints.cs:54,64` | Bearer or `X-Checkout-Token` | Disabled (no cookies) | `checkout-payment` group and limiter; idempotency on payment |
| POST `.../checkout/{id}/codes` | `CheckoutApiEndpoints.cs:34` | Same | Disabled | `checkout-payment` group and `coupon-code` (address) |
| DELETE `.../checkout/{id}/codes/{code}`, DELETE `.../checkout/{id}` | `CheckoutApiEndpoints.cs:42,72` | Same | Disabled | None |
| POST `.../checkout/{id}/gift-cards` | `M/Orders/Api/OrderCheckoutApiEndpoints.cs:38` | Same | Disabled | `checkout-payment` group and `gift-card-code` (address) |
| POST, DELETE `.../checkout/{id}/store-credit`, `/points` | `M/Customers/Api/CustomerCheckoutApiEndpoints.cs:40-64` | Bearer, owner | Disabled | None |
| PUT `.../checkout/{id}/shipping`, POST `/shipping-rates` | `M/Shipping/Api/ShippingCheckoutApiEndpoints.cs:31,37` | Bearer or guest token | Disabled | None |
| POST `.../checkout`; POST `cart/checkout` | `OrderCheckoutApiEndpoints.cs:23`; `M/Orders/Controllers/CartCheckoutController.cs:31` | Guests allowed | Disabled; yes | `Cart` group; `checkout` group |
| GET `discount/{code}` | `M/Checkout/Controllers/DiscountLinkController.cs:42` | Anonymous | Not applicable | None |
| POST `orders/track`, `orders/find` | `M/Orders/Controllers/OrderViewController.cs:116,151` | Anonymous | Yes | `find-order` group and limiter (address; e-mail on find) |
| GET `orders/view/{token}` and its invoice, pay and return pages | `OrderViewController.cs:60,96`; `OrderPayController.cs:42`; `OrderReturnsController.cs:73-114` | Signed token, 60 days | Yes on POST | None (the token cannot be forged) |
| POST `orders/create-account` | `M/Orders/Controllers/OrderAccountController.cs:63` | Signed claim, 2 hours | Yes | Limiter (address) |
| POST admin `orders/{id}/refund`, `/cancel`; API `orders/{id}/refunds`, `/cancel` | `M/Orders/Controllers/OrdersAdminController.cs`; `M/Orders/Api/OrderAdminApiEndpoints.cs:42,50` | `ManageOrders` | Global; disabled (bearer) | API idempotency; no order lock |
| POST `pos/sales/{id}/pay`, `pos/returns`, `pos/shifts/{id}/cash` | `M/PointOfSale/Api/PosApiEndpoints.cs:52,72,90` | Bearer and sell-at-register | Disabled | None seen |
| POST `stripe/webhook` | `M/Stripe/Endpoints/CreateWebhookEndpoint.cs:65` | Stripe signature | Disabled | None; no body cap |
| POST EasyPost webhook | `M/Shipping/Endpoints/EasyPostWebhookEndpoint.cs:26` | HMAC | Disabled | 512 KB cap |
| GET, POST `transactions/pay/{token}` | `M/Transactions/Controllers/TransactionPayController.cs:50,79` | Signed token, 60 days | Yes | None |
| GET, POST `downloads/{token}` | `M/Downloads/Controllers/DownloadsController.cs:33,41` | Protected token | Global | None |
| GET, POST `punchout/oci/{accountId}` | `M/Accounts.B2B/Controllers/PunchoutController.cs:48` | 256-bit key | Ignored | None |
| POST `marketing/unsubscribe` | `M/Marketing/Controllers/UnsubscribeController.cs:48` | Signed token | Ignored | None |
| UCP and ACP checkout, cart, MCP and A2A routes | `M/AgenticCommerce/Ucp/UcpEndpoints.cs:61-66`; `Acp/AcpEndpoints.cs:55-59` | Platform key, bearer or signature | Disabled | None; idempotency |
| POST admin installment `BeginPayment`, `PaymentStatus` | `M/Subscriptions/Endpoints/InstallmentPlanPaymentEndpoints.cs:29,33` | Cookie and `ManageInstallmentPlans` | Validated by hand | None |
| POST admin vendor `PayOut`, booking `ChargeBalance` | `M/Marketplace/Controllers/VendorsAdminController.cs:219`; `M/Bookings/Controllers/BookingsAdminController.cs:247` | Module permission | Global | None; no lock |

### Commerce: checked and sound

Do not re-audit these.

- **Checkout ownership:** the store filters by owner id or a 256-bit guest token (SHA-256 stored, fixed-time compare, HttpOnly and Lax cookie, header transport for the API).
- **Commerce API:** bearer `Api` scheme only, so no cookie CSRF.
- **Prices:** cart, order, POS and agent prices are resolved on the server from published products; quantities are clamped; return URLs go through `CheckoutReturnInfo.IsLocalUrl`.
- **Engine:** a lock per session, attempts saved before the provider call, stable idempotency keys, underpayment and currency checked against the attempt, superseding on a new begin.
- **Tenders and coupons:** gift card, store credit and loyalty use a lock and holds per balance; coupon holds are locked per code; gift card codes are 80 bits from a cryptographic generator.
- **Refunds:** a lock per transaction, capped at confirmed minus in flight, recorded before the provider call, with an idempotency key.
- **Stripe:** `ConstructEvent` signature check with the default tolerance, event-id de-duplication, secrets protected and handled in deployment steps, saved cards bound to their owner, server-only provider-data keys stripped.
- **Links:** order, recovery, account-claim and pay links use `SignedTokenService` with distinct purposes and expiries.
- **Authorization:** `account/*` and `me/*` orders, returns, addresses and payment methods compare the user id; sampled admin actions in Orders, Coupons, Gift cards, Customers, Registers, Stripe Connect and Store setup check their permission.
- **Point of sale and test providers:** cash and counter-card providers require a server-set `CheckoutCounterSale`; the test provider does nothing outside Development unless configured.

## Platform, communication and workforce

### SEC-P1. E-mail code sign-in can be brute-forced (High, M)

- **Where:** `M/Users/Services/EmailSignInCodes.cs:16-21,60-86,88-113`; `M/Users/Controllers/EmailSignInController.cs:19-20,88-115`; `M/Users/Startup.cs:204-211`.
- **What:** `VerifyAsync` reads the cache entry, compares, then writes `Attempts + 1`, with no lock, so concurrent requests all see fewer than five attempts. `CountAsync` (the send limiter) has the same read-then-write race. A wrong code never calls `UserManager.AccessFailedAsync`, so three sends of five tries each repeat every 15 minutes for ever against a six-digit code. The controller calls `SignInManager.SignInAsync` directly and never raises `ILoginFormEvent`, so reCAPTCHA, audit and lockout hooks do not see the sign-in. Any account without two-factor sign-in qualifies, Administrator included. The controller has no `[Feature(UsersConstants.Feature.EmailCodeSignIn)]`.
- **Exploit:** An anonymous attacker posts a known administrator's address to `/sign-in/email`, then sends a few hundred parallel guesses to `/sign-in/email/code` for each issued code, and repeats every window.
- **Fix, now and in place (do not wait for RL-1):** Take an `IDistributedLock` per tenant and e-mail around the read, count and write in `VerifyAsync` and `CountAsync`, and store the incremented attempt *before* comparing. Call `AccessFailedAsync` on a miss and `ResetAccessFailedCountAsync` on success. Raise the `ILoginFormEvent` methods (`LoggingInAsync`, `ValidatingLoginAsync`, `LoggingInFailedAsync`, `LoggedInAsync`). Add `EmailSignInOptions.ExcludedRoles` (default: Administrator) and the missing `[Feature]` attribute.
- **Afterwards (RL-8):** replace the two private counters with `IAttemptLimiter` policies (`email-signin-send-email`, `email-signin-send-network`, `email-signin-verify`) and tag both POST routes with a `sign-in-code` rate-limit group.
- **Tests:** One hundred parallel wrong `VerifyAsync` calls evaluate at most `MaxAttempts` and the right code is then refused; lockout after the configured failures; the login events are raised; the route is 404 when the feature is off; an Administrator is refused.
- **Confidence:** High on the design; Medium on how wide the race is in practice.

### SEC-P2. Secure keypad digits can reach the model and the transcript on a multi-node site (High, M)

Described under CQ-1 in `04-code-quality.md`, which carries the fix and tests. `C/Omnichannel.Voice.Core/Services/SecureKeypadCaptures.cs:23` holds card, bank and national-id digit captures in one node's memory; a DTMF webhook that lands on another node is treated as ordinary input, so the digits go to the AI model and into the call transcript. Fix the fail-closed part first.

### SEC-P3. Anonymous address-suggestion endpoints call a paid provider with no throttle unless Rate Limits happens to be on (Medium, S to M)

- **Where:** `M/Addresses/Startup.cs:36-45,53-62`; `M/Addresses/Endpoints/AddressAutocompleteEndpoints.cs:26-74`; `M/Addresses/Manifest.cs:20-27`.
- **What:** `/addresses/suggest` and `/addresses/resolve` require no authorization. The 60-a-minute limit is registered only under `[RequireFeatures(OrchardCoreFeatureIds.RateLimits)]`, and the Google Places feature does not depend on that feature. The `session` value is supplied by the caller and not verified.
- **Exploit:** Script either endpoint from a few addresses: each request is a billed Places call until the provider quota runs out, which also stops address entry at checkout. Rated Medium because the loss is bounded by the provider quota and no data is exposed.
- **Fix:** Require a short-lived token from `SignedTokenService`, rendered by the address editor, on both endpoints, so only a page that shows the editor can call them. Add a `DailyRequestLimit` setting that fails closed. Keep the existing `[RequireFeatures]` group limit; when the attempt limiter exists (RL-1), add an attempt policy per network so the endpoints are throttled even with Rate Limits off.
- **Tests:** The 61st request in a minute gets 429; a missing or expired editor token gets 404; the provider is not called after the daily cap.
- **Confidence:** High.

### SEC-P4. The web-push subscription endpoint gives workers blind request forgery and unbounded fan-out (Medium, S)

- **Where:** `M/Workforce/Controllers/MyWorkController.cs:728-747`; `C/Workforce.Core/WorkforcePortalServiceCollectionExtensions.cs:31`; `C/Workforce.Core/Services/WebPushNotificationChannel.cs:62-96`; `C/Workforce.Core/Services/DefaultWebPushService.cs:63-95`.
- **What:** `Subscribe` accepts any absolute `https://` endpoint. Notifications are posted to it through a plain `AddHttpClient` client: no `EgressNetwork`, and redirects are followed. A worker can register any number of subscriptions. A 404 or 410 deletes the row and a success stamps `LastSentUtc`, and both are visible on the settings page.
- **Exploit:** A user with `UseWorkforcePortal` registers internal HTTPS URLs, triggers a notification, and reads the device list to learn which hosts answered. Thousands of endpoints turn each notification into an outbound flood.
- **Fix:** `services.AddEgressGuardedHttpClient(WorkforceConstants.WebPushHttpClient)` (`C/Core/Http/EgressNetwork.cs:128`); refuse at subscribe with `EgressNetwork.IsAllowedUrl`; cap subscriptions per worker at ten and evict the oldest; an optional `AllowedHosts` list of known push services.
- **Tests:** Subscribing to a loopback, private or link-local address is refused; a send to a host that resolves to an internal address is refused at connect; the eleventh subscription evicts the oldest.
- **Confidence:** High.

### SEC-P5. The external calendar subscription guard can be bypassed, and the body is read without a limit (Medium, S)

- **Where:** `M/Calendars/External/ICalendarUrlProvider.cs:41-99,101-126`; `M/Calendars/ExternalStartup.cs:23`.
- **What:** `EnsurePublicAsync` resolves DNS once and checks a private copy of the internal-range logic; `client.GetAsync` then resolves again and follows redirects on a default client. The size check trusts `Content-Length`, and `ReadAsStringAsync` buffers the whole body.
- **Exploit:** A calendar manager subscribes to a URL that redirects to `http://169.254.169.254/...` or an internal service, or uses DNS rebinding; the sync task issues the request, and success against the "not an iCalendar document" error tells them what answered. A chunked multi-gigabyte response exhausts memory.
- **Fix:** `AddEgressGuardedHttpClient`; delete `EnsurePublicAsync` and `IsPrivate` (another private-address classifier, which the audit's defect 17 was meant to end); validate with `EgressNetwork.IsAllowedUrl` in `CalendarSubscriptionDisplayDriver`; read through a bounded stream like `MessagingInboundMediaIngestor.ReadBoundedAsync`, which moves to `C/Core/Http` as a shared helper.
- **Tests:** A redirect to loopback is refused; a rebinding host is refused at connect; a chunked body over 10 MB is aborted.
- **Confidence:** High.

### SEC-P6. Team managers can approve their own timesheets, punches and time off (Medium, S to M)

- **Where:** `C/Workforce.Core/Services/DefaultWorkforceScopeService.cs:34-83,86-100`; `M/Workforce/Controllers/TimesheetsController.cs:181-200,341-358,645-674`; `M/Workforce/Api/WorkforceApiEndpoints.cs:449-459,471-481`.
- **What:** The scope is "teams where I am a manager or scheduler, and their subtree"; nothing excludes the caller's own worker record. `CanManageAsync`, `ForPunchesAsync`, `ApproveTimesheetAsync` and `DecideTimeOffAsync` check only the permission and `scope.Covers(TeamId)`.
- **Exploit:** A manager with `ApproveTimesheets`, `EditPunches` or `ApproveTimeOff` adds or corrects their own punches, approves their own timesheet (which then goes to the payroll export) and approves their own leave.
- **Fix:** Add `IWorkforceScopeService.CanDecideForWorkerAsync(user, workerId)`, which refuses the caller's own record unless they hold `ManageAllWorkforce` or a new `ApproveOwnRecords` permission. Use it in the timesheet, punch, time-off, shift-trade and open-shift decisions and in the matching API endpoints.
- **Tests:** A manager approving their own timesheet gets 403; a peer manager succeeds; `ManageAllWorkforce` succeeds; a bulk approval skips the caller's own row.
- **Confidence:** High.

### SEC-P7. Supervisor AI-call endpoints skip queue and campaign scoping (Medium, M)

- **Where:** `M/ContactCenter/Endpoints/SupervisorAICallsEndpoints.cs:54-65,115-149,186-254,256-285`; compare `C/ContactCenter.Core/Services/SupervisorQueueAuthorizationService.cs:28-58`.
- **What:** Supervision of human calls is limited to the supervisor's `AllowedQueueIds` and `AllowedCampaignIds`. The AI-call list, transcript, note, take-over, end and listen handlers check only the tenant-wide `MonitorContactCenter` and `InterveneInCalls`. `HandleTranscriptAsync` returns the turns of any activity with an `AISessionId`, live or past, on any channel.
- **Exploit:** A supervisor scoped to one queue reads live and past AI transcripts of other business units, listens in, and ends or takes over their calls.
- **Fix:** Add an activity-level check to `ISupervisorQueueAuthorizationService` (the campaign through `activity.CampaignId`, or the entry point's queue), or a new `MonitorAllAICalls` permission. Filter the list; require a live AI call for transcripts and a recordings-level permission for history.
- **Tests:** A scoped supervisor gets 403 or an empty list for out-of-scope AI activities on all six routes; the transcript of an ended activity is refused.
- **Confidence:** Medium (it depends on scoping being an intended boundary for AI calls; confirm with the owner).

### SEC-P8. Content Access Control restricts only the item-level view check (Medium, L)

- **Where:** `M/ContentAccessControl/Handlers/RoleBasedContentItemAuthorizationHandler.cs:40-50,106-119`.
- **What:** The handler acts only when the permission is `CommonPermissions.ViewContent` and the resource is a `ContentItem`. The module has no index, query filter, search or GraphQL filter, and no feed or sitemap exclusion. `PreviewContent`, `EditContent` and `CloneContent` are untouched.
- **Exploit:** Titles, summaries and fields of restricted items appear in list rendering, search, GraphQL content queries, RSS and sitemaps, and to editors through preview and clone.
- **Fix:** A `RolePickerPartIndex`; a GraphQL filter for content items; a search index handler that excludes restricted items or tags them with their roles; sitemap and feed exclusion; handle `PreviewContent` and `CloneContent`. Until then, state the limit at the top of the module's documentation and README.
- **Tests:** A restricted item is absent from GraphQL, search, the feed and the sitemap for a user outside the roles; preview is denied.
- **Confidence:** Medium (the leak surfaces are Orchard Core behavior that was not run).

### SEC-P9. The public booking page has no throttle and no proof of contact (Medium, M)

- **Where:** `M/Scheduling/Controllers/BookingPagesController.cs:78-126`; `C/Scheduling.Core/Services/DefaultAppointmentService.cs:122-150`.
- **What:** `POST book/{slug}` is anonymous with only an antiforgery token. The per-customer cap is keyed on the unverified e-mail address; there is no rate limit, no CAPTCHA and no confirmation step.
- **Exploit:** A script books every open slot with throwaway addresses, taking distributed locks and starting booking workflows on each request.
- **Fix:** A `scheduling-booking` rate-limit group with a built-in limit and an attempt policy per network (RL building block); an optional `AppointmentType.RequireEmailConfirmation` (a pending hold released after a few minutes); render the reCAPTCHA shape when that feature is on.
- **Tests:** One booking too many from one network in the window gets 429; an unconfirmed hold expires and frees the slot.
- **Confidence:** High.

### SEC-P10. Smaller platform items (Low, S each)

| Id | Where | What | Fix | Test |
| --- | --- | --- | --- | --- |
| a | `M/Omnichannel.EventGrid/Endpoints/AzureEventGridEndpoint.cs:101-120,486-512` | A new `ConfigurationManager` per request: any anonymous request with a bearer header causes outbound metadata fetches | Cache one manager per metadata address; require HTTPS; reject a malformed token before fetching | Two requests cause one fetch |
| b | `M/Geo.Osrm/OsrmProvider.cs:117-141`; `OsrmSettingsDisplayDriver.cs:63`; `Startup.cs:21` | Any URL is accepted as the OSRM base, and the error path returns the remote message | The egress-guarded client with a host-level `Geo:Osrm:AllowPrivateNetwork` opt-in; stop returning remote messages | A private address is refused without the opt-in |
| c | `M/Calendars/Invitations/CalendarResponseEndpoint.cs:20-46` | An RSVP changes state on GET, so mail scanners accept or decline for the attendee | GET shows a confirmation form; POST records the answer | GET leaves the response unchanged |
| d | `Startup/CrestApps.OrchardCore.Asterisk.Web/Program.cs:9,29-84` | The sample host exposes call control with no authentication; the only guard is "Development only" | Bind to loopback, filter hosts, require a shared-secret header | A request with a foreign host header is refused |
| e | `M/ContactCenter/Endpoints/VoiceIngressEndpoint.cs:17-18,31` | Cookie authentication with antiforgery disabled | Require the `Api` authentication scheme, as the other `api/*` groups do | A cookie-only request gets 401 |
| f | `M/Omnichannel.Messaging/Startup.cs:184`; `C/Omnichannel.Messaging.Core/Attachments/MessagingInboundMediaIngestor.cs:134-161` | The inbound media downloader follows redirects anywhere | `AddEgressGuardedHttpClient` | A redirect to an internal address is refused |
| g | `M/DncRegistry/Controllers/LocalDncRegistryAdminController.cs:177-200`; `M/DncRegistry/Services/DefaultLocalDncListManager.cs:128-133` | `ProcessNow` has no `[HttpPost]`; the list name and file name are logged raw | `[HttpPost]`; `SanitizeLogValue()` | GET gets 405 |
| h | `C/ContactCenter.Core/Services/SecureCaptureAccessToken.cs:20` | The token that opens the card-capture page is built from two `IdGenerator` ids, not from `RandomNumberGenerator` | `SecretTokens.Create()` (DRY-2) | The token is 43 base64url characters |
| i | `C/Workforce.Core/Services/DefaultWorkerFeedService.cs:68`; `CalendarStore.cs:48` | Feed tokens are stored and compared in plain text | Store a hash; fixed-time compare (DRY-2 step 3) | A database row does not contain the token |

### Anonymous endpoints in this slice

| Route | File:line | How the caller is authenticated | Throttle |
| --- | --- | --- | --- |
| POST `api/twilio/webhook/sms` | `C/Omnichannel.Messaging.Sms.Core/Twilio/TwilioWebhookEndpoint.cs:42` | Twilio signature, constant time; 400 without a token | No (inbox de-duplication) |
| POST `api/telnyx/webhook/call` | `M/Telnyx/Endpoints/TelnyxWebhookEndpoint.cs:32` | Ed25519 over the raw body and a timestamp window; 401 without a key | Yes (ingress limiter, 1 MB cap) |
| POST `api/telnyx/webhook/sms` | `M/Telnyx/Endpoints/TelnyxSmsWebhookEndpoint.cs:40` | Ed25519 and freshness; fails closed | No (size cap) |
| GET (WebSocket) `api/telnyx/media/stream?t=` | `M/Telnyx/Endpoints/TelnyxMediaStreamEndpoint.cs:22` | One-time 256-bit token | No |
| POST `api/azure/webhook/eventgrid` | `M/Omnichannel.EventGrid/Endpoints/AzureEventGridEndpoint.cs:51` | Shared key (constant time) or Azure AD token; handshake only after authentication | No (size cap) |
| POST `api/omnichannel/subject-disposition-actions` | `M/Omnichannel.Managements/Endpoints/SubjectActionEndpoints.cs:23` | `AllowAnonymous`, but the `CompleteActivity` permission is enforced | No |
| GET `messaging/attachments/{token}/{fileName?}` | `M/Omnichannel.Messaging/Endpoints/MessagingAttachmentEndpoint.cs:19` | Time-limited protected token | No |
| GET `call-recordings/shared/{token}` | `M/ContactCenter/Endpoints/CallRecordingLinkEndpoints.cs:31` | Protected token with expiry (7 days at most) | No |
| GET, POST `contact-center/secure-capture/{token}` | `M/ContactCenter/Controllers/SecureCaptureController.cs:42,68` | Random one-time token, stored hashed | No |
| GET `api/contact-center/health/ready` | `M/ContactCenter/Endpoints/ContactCenterHealthEndpoints.cs:54-58` | None (node status only) | No |
| GET `track/{token}` | `M/Dispatch/Controllers/TrackingController.cs:45` | Time-limited protected token, open visits only | No |
| GET `delegated-access/enter`, `/callback` | `M/TenantHierarchy/Controllers/DelegatedEntryController.cs:72,136` | Encrypted state cookie, PKCE, single-use hashed code | No |
| GET, POST `sign-in/email`, `sign-in/email/code` | `M/Users/Controllers/EmailSignInController.cs:51-90` | None (it is the sign-in) | Partial and racy (SEC-P1) |
| GET `my-work/feed/{token}.ics` | `M/Workforce/Controllers/MyWorkController.cs:777` | 256-bit token | No |
| GET, POST `workforce/kiosk/{token}`, `.../punch` | `M/Workforce/Controllers/TimeClockKioskController.cs:61,81,131` | 256-bit kiosk token, badge and PIN, 2-minute ticket | PIN lockout (5 tries in 15 minutes per worker) |
| GET, POST `book/{slug}` | `M/Scheduling/Controllers/BookingPagesController.cs:55,78` | None | No (SEC-P9) |
| GET, POST `book/manage/{id}/{token}` | `M/Scheduling/Controllers/BookingPagesController.cs:135,162` | 128-bit token, constant time | No |
| GET `calendars/feed/{token}.ics` | `M/Calendars/Endpoints/CalendarFeedEndpoint.cs:22` | 192-bit token | No |
| GET `calendars/respond/{eventId}/{token}/{response}` | `M/Calendars/Invitations/CalendarResponseEndpoint.cs:20` | Per-attendee token, constant time | No |
| GET `/addresses/formats/{countryCode}`, `/addresses/suggest`, `/addresses/resolve` | `M/Addresses/Startup.cs:36,40,43` | None | Only with Rate Limits on (SEC-P3) |

### Platform: checked and sound

- **Provider webhooks:** signatures over the raw body, constant-time comparison, closed when not configured, a replay window on Telnyx voice and SMS, durable inbox de-duplication; the Event Grid handshake is reachable only after authentication.
- **`TelephonyHub`:** `[Authorize]` and `UseSoftPhone` on every method; every action on a call verifies the caller owns it; caller id is resolved on the server; transfers go through `ITransferTargetPolicy`.
- **`ContactCenterHub`, `MessagingHub`, `DispatchHub`:** group joins are permission-gated; supervision of human calls is queue-scoped; Contact Center REST endpoints validate antiforgery.
- **Recordings, attachments and secure capture links:** share links need `ListenToAllCallRecordings`; view tokens are bound to the conversation and authorized again; capture tokens are stored hashed. Encryption at rest and transcript redaction were not traced end to end.
- **Soft-phone credentials:** short-lived per-user credentials for Telnyx and Asterisk; the ARI key stays out of logs.
- **TenantHierarchy:** PKCE with a state cookie, 256-bit codes stored hashed and used once, safe-identifier checks on provisioning SQL, an egress guard for child tenants.
- **APIs:** the Workforce and field-service APIs (Api scheme, permission, managed-team scope), mobile sync, Calendars access levels, and the user, account, asset and customer search endpoints (audit defect 4 confirmed fixed).
- **Other:** kiosk PIN hashing and lockout, feed and manage tokens, the `CsvWriter` formula guard, `returnUrl` handling.

## Views and scripts

### SEC-FE1. The shared map helper renders marker and shape labels as HTML (Medium, S)

- **Where:** `M/Resources/Assets/js/crestapps-map.js:71` (`bindTooltip(shape.label)`), `:92` (`bindPopup(marker.label)`). Callers with outside data: `M/Dispatch/Views/Visits/Details.cshtml:303` (`label: @Json.Serialize(visit.Summary ?? visit.Number)`), `M/Dispatch/Assets/js/dispatch-board.js:573` (technician name), `M/Geo/Views/Territories/Index.cshtml:25,28`, `Edit.cshtml:102` (territory names).
- **What:** Leaflet sets a string popup or tooltip with `innerHTML`. `visit.Summary` is copied from the work order (`C/Dispatch.Core/Services/DefaultDispatchService.cs:517`). The work-order AI tool and the field-service API need a signed-in user with `ManageWorkOrders` (`WorkforceAIToolBase.cs:34-38,60`), so they are not an outside path. The outside path is `M/WorkOrders/Integrations/OmnichannelWorkOrders.cs:99-101`, which builds the summary from a contact's name and AI-written notes. `dispatch-board.js:557` strips `<>&` by hand and then adds a deliberate `<a>`; the details view does not escape.
- **Exploit:** A contact whose name carries markup (from a web form, an import or an inbound conversation), or a caller who gets the AI to write markup into its notes, ends up in a work-order summary. The script runs in the dispatcher's admin session when the marker is clicked. Rated Medium because it needs such a path to exist; fix it early because the fix is small.
- **Fix, locally and now:** In `crestapps-map.js`, treat a string `label` as text (build the element with `textContent`) and also accept a DOM node, so the dispatch board passes the link it builds as an element. When the shared browser helpers exist (UX-8), use their `escape`.
- **Tests:** Vitest for `addMarkers` and `showShapes` with a markup label; Playwright on the visit details page.
- **Confidence:** High (the sink and the data flow were read; Leaflet's behavior is from its documentation, not run).

### SEC-FE2. `escapeHtml` in the soft phone does not escape quotes and is used inside attributes with the caller's number (High, S)

- **Where:** `M/Telephony/Assets/js/telephony-client.js:20-25` (`textContent` then `innerHTML`); the same fallback in `M/ContactCenter/Assets/js/contact-center-agent-bar.js:21-25` and `M/Omnichannel.Managements/Assets/js/activity-query-builder.js:291-296`. Attribute sinks: `M/Telephony/Assets/js/soft-phone.js:10847` (`data-telephony-history-number`, from `interaction.from`), `:11057`, `soft-phone/conference.js:155-162`, `soft-phone/transfer-panel.js:443-452`, `soft-phone/keypad-search.js:77`.
- **What:** A `"` in the value closes the attribute, so `x" onmouseover="...` adds an event handler. The Asterisk caller number is stored without validation (`M/Asterisk/Services/AsteriskRealtimeVoiceEventMapper.cs:47`; `M/Telephony/Services/TelephonyCallHistoryVoiceEventHandler.cs:154` rejects only SIP URIs).
- **Exploit:** A caller sends a crafted caller id; the entry stays in the agent's recent calls and voicemail list and fires when the agent hovers or clicks the call-back button.
- **Fix, locally and now:** Make `escapeHtml` in `telephony-client.js` and its two fallbacks replace `"` and `'` as well (the five-character form `supervisor-actions.js:40-47` uses). Validate the caller number on the server where voice events are mapped (`AsteriskRealtimeVoiceEventMapper.cs:47`, `TelnyxWebhookService.cs:171`): keep digits, `+`, `*`, `#` and a known anonymous marker, and drop anything else. UX-8 later replaces the copies with one shared helper.
- **Tests:** Vitest for the helper and for the call list with a quote in the number; a unit test for the caller-number validation.
- **Confidence:** Medium (the injection is certain; whether a carrier passes a quote through was not verified). High where SIP ingress is exposed to untrusted peers.

### SEC-FE3. Secure Capture collects the card number, expiry and security code in plain inputs posted to this application (Medium, a compliance decision for the owner, L)

- **Where:** `M/ContactCenter/Views/SecureCapture/Index.cshtml:51-62`; `A/ContactCenter.Abstractions/Models/SecureCaptureField.cs:13-21`; `M/ContactCenter/Controllers/SecureCaptureController.cs:16-18,112-117`; `M/ContactCenter/SecureCaptureStartup.cs:52-59`.
- **What:** The page is anonymous. When a site configures a capture sink, the raw card number and security code reach the application, which puts that deployment in card-data scope, unlike checkout, which uses the provider's hosted fields. Outside Development the default sink refuses every value, so nothing is captured until an integrator supplies a sink, and the security code is never kept. The page sets no Content Security Policy, and the inputs use `autocomplete="off"` instead of the card tokens.
- **Fix:** A product decision for the owner: collect the card in provider-hosted fields (Stripe Elements with a SetupIntent or PaymentIntent created for the capture) so the application never sees it, or accept the scope and document it for integrators who add a sink. In either case add a strict Content Security Policy for the page and the right `autocomplete` tokens.
- **Confidence:** High on the facts; the severity is a policy call.

### SEC-FE4. Smaller view and script items (Medium and Low, S each unless noted)

| Id | Severity | Where | What | Fix |
| --- | --- | --- | --- | --- |
| a | Medium | `M/Subscriptions/Views/AccountSubscriptions.cshtml:65` and the views in UX-1a | Model or localized text inside an inline `confirm('...')`: a subscription title with an apostrophe injects script; the others lose their confirmation | UX-1 |
| b | Medium | `M/AI.Prompting/Views/AIProfilePromptSelection.Edit.cshtml:70`, `ChatInteractionPromptSelection.Edit.cshtml:70` | `li.innerHTML = '<strong>' + p.Name + ... p.Description` from prompt templates (authored by an administrator or imported by a recipe) | `textContent` |
| c | Medium | `M/Classifieds/Views/ClassifiedsSearch.cshtml:60-61,75` | An anonymous page loads Leaflet from a CDN without an integrity hash; the popup escapes only `<`, and `href="${point.url}"` is raw | Use the `leaflet` and `crestapps-map` resources (which carry integrity hashes) and the fixed helper from SEC-FE1 |
| d | Medium | `M/Stripe/Views/StripeSettings.Edit.cshtml:92,139,179,226`; likely also the connection and deployment editors in AI.Mcp, OpenAI, OpenAI.Azure, AzureAIInference, Cartesia, ElevenLabs; private keys in textareas in AI.FileSources.Sftp, AI.Mcp.Resources.Sftp, AgenticCommerce feed settings | Secrets typed into `type="text"` fields: visible while typed and kept in browser form history. Values are not rendered back (good) | One secret-input tag helper in `C/Core` (`type="password"`, `autocomplete="new-password"`, blank with a "saved" placeholder); extend `tests/js/lint/secret-fields-not-autofilled.test.js` to property names that end in `Key`, `Secret`, `Token` or `Password` |
| e | Medium, M | `M/Workforce/Controllers/MyWorkController.cs:833-895`; `M/Workforce/Views/Shared/_MyWorkAssets.cshtml:11`; `M/Dispatch/MobileStartup.cs:42` | The My Work service worker caches pages in one cache for every user and never clears it: on a shared tablet that is offline, the next user sees the previous worker's page | Key the cache by user id, or delete the caches on sign-out and when the user changes |
| f | Medium | `M/AI.Documents/Views/ChatInteractionDocuments.Edit.cshtml:304-307,477-483`; endpoints mapped in `M/AI.Documents/Startup.cs:109-110`, implemented in CrestApps.Core | Document upload and remove POSTs send no antiforgery token | Send `RequestVerificationToken` (through `crestapps.http.request`, UX-8); ask CrestApps.Core to validate it. Confidence Low to Medium: the endpoint code is not in this repository |
| g | Low | `M/ContentTransfer/Views/Admin/Import.cshtml:152`; `M/Products/Views/ProductFacet.cshtml:12,50`; `M/ContentTransfer/Views/ImportContentFile.Edit.cshtml:31`; `M/AI.Documents/Views/ChatInteractionDocuments.Edit.cshtml:455` | Administrator or own values in raw sinks (`Html.Raw("<code>" + value)`, a raw `id`, `innerHTML = file.name`, `innerHTML` of an error body) | Encode, or `textContent` |
| h | Low | `M/Transactions/Controllers/AdminController.cs:436`; `M/Commerce/Controllers/StoreSetupController.cs:110,116` | A `LocalizedHtmlString` built with the message as the format string: a brace in a provider error throws when the notice renders | The form in `M/Carts/Controllers/CartController.cs:162` (`"{0}"` and an argument) |

## AI and data

"(Core package)" marks code that ships in the CrestApps.Core NuGet packages (pinned at `2.0.0-preview.367`) and was read from a checkout of that repository, which may be slightly newer. The fix is given for this repository; where the right fix is upstream, the item says so.

### SEC-A1. The MCP local (stdio) transport lets a tenant run any process on the host (Critical on a multi-tenant host, M)

- **Where:** `M/AI.Mcp/Manifest.cs:26-35`; `M/AI.Mcp/Startup.cs:74-93`; `M/AI.Mcp/Drivers/StdioMcpConnectionDisplayDriver.cs:125-131`; `M/AI.Mcp/Recipes/McpConnectionStep.cs:47-66`; `C/AI.Core/McpPermissions.cs:11`; (Core package) `CrestApps.Core.AI.Mcp/Services/StdioClientTransportProvider.cs:31`.
- **What:** "MCP Local Client" is an ordinary tenant feature: no `DefaultTenantOnly`, no host-level switch and no allowlist of commands. The driver stores `Command`, `Arguments`, `WorkingDirectory` and `EnvironmentVariables` as posted, and the package passes them to `StdioClientTransport`. The only gate is `ManageMcpConnections`, which is not marked security-critical. The recipe step imports stdio connections too.
- **Exploit:** A tenant administrator enables the feature, adds a connection whose command is `cmd.exe` or `/bin/sh`, attaches it to a profile and sends one message. That is code execution as the application's identity, across every tenant. Anyone who can run a recipe (including through SEC-A3) reaches the same point. On a single-tenant site this is a role with `ManageMcpConnections` gaining the server.
- **Severity:** Critical wherever a tenant's administrator is not the host's operator: self-service or sold tenants (SEC-C18), or any multi-tenant host. On a single-tenant site it is High: the permission is granted to Administrator only by default (`M/AI.Mcp/Services/McpPermissionsProvider.cs:25-32`), but it turns an application role into operating-system process execution, which an Orchard administrator cannot otherwise do, and it is reachable through a recipe.
- **Fix, in two steps. Step 1 is small and ships first.**
  1. Set `DefaultTenantOnly = true` on the feature, and add a run-time check in the stdio startup and transport provider that refuses to start a process on a non-default tenant, for child tenants that already have the feature enabled. Add a security-critical permission `ManageMcpStdioConnections`, required by the controller when `Source == StdIo` and by `McpConnectionStep` (which today accepts any registered transport, `:58`).
  2. Optional hardening: a host-only option `CrestApps:AI:Mcp:Stdio:AllowedCommands`, read from host configuration (not tenant settings) and enforced in an Orchard transport provider that wraps the package's. Document Feature Profiles as the operator's control over which tenants may have the feature.
- **Tests:** The stdio transport is absent and refuses on a non-default tenant; the recipe step rejects a stdio source without the permission; 403 without the new permission; with an allowlist set, a command outside it is refused.
- **Confidence:** High.

### SEC-A2. The MCP "File" resource reads the whole application content root (Critical on a multi-tenant host, M)

- **Where:** `M/AI.Mcp/Services/DefaultMcpFileProviderResolver.cs:28-31`; `M/AI.Mcp/Handlers/FileResourceTypeHandler.cs:40-55,88-123`; `M/AI.Mcp/Startup.cs:180-192`.
- **What:** `Resolve` ignores `providerName` and returns `IWebHostEnvironment.ContentRootFileProvider`; the `{fileName}` value goes straight to `GetFileInfo`. There is no tenant folder and no size cap (`ReadToEndAsync`, an unbounded `MemoryStream`). Only the physical file provider's own `..` and dot-file rules apply.
- **Exploit:** A tenant administrator with `ManageMcpResources` publishes a File resource with a `{fileName}` template and reads `App_Data/tenants.json`, `appsettings.json`, another tenant's database file or the data-protection key ring. Once published, every MCP caller can do the same.
- **Fix:** Return a `PhysicalFileProvider` rooted at a folder per tenant: move `TenantFileSourceRoot` (`M/AI.FileSources/Services`) to a `.Core` library and reuse it (it already solves this for file sources). Reject rooted paths; cap the bytes read.
- **Tests:** `App_Data/tenants.json`, `appsettings.json`, an absolute path and a symbolic link out of the root all answer "not found"; an oversized file is refused.
- **Confidence:** High.

### SEC-A3. AI tools run with system authority, and the per-tool permission is applied only when the editor is drawn (High, L)

- **Where:** `M/AI/Tools/Drivers/AIProfileToolsDisplayDriver.cs:57-63` (filter on render) against `:108-110` (save); `AIProfileTemplateToolsDisplayDriver.cs:105-107`; `M/AI.Chat/Drivers/AIProfilePostSessionDisplayDriver.cs:173-175`; `M/AI/Endpoints/Api/AIProfilesApiEndpoints.cs:137,189`; `C/AI.Core/ImportRecipeBaseTool.cs:52-73`; `M/AI.Agent/Users/GetUserInfoTool.cs:50-91`; `M/AI.Agent/OrchardCorePermissions.cs` (referenced nowhere); (Core package) `FunctionInvocationAICompletionServiceHandler` (`EnforceListableAccess` only for chat interactions).
- **What:** On save, the posted tool keys are intersected with every selectable tool, not with the tools the editor may use; the profiles API accepts raw JSON. At run time a profile session applies no per-user tool check, and no tool in `AI.Agent` checks a permission (recipe import, features, content, users, e-mail, SMS, workflows). There is no approval step. Tenant tools do check for the default shell.
- **Exploit:** (a) A user with `ManageAIProfiles` but without `AccessAITool_importOrchardCoreRecipe` posts that key, or PUTs the profile through the API, chats, and imports a recipe that creates an administrator or enables stdio (SEC-A1). (b) A visitor of a public profile that has `getUserInfo` or `sendEmail` attached drives them by prompt. (c) A poisoned crawled page or document does the same in an administrator's chat.
- **Also (added by the independent review):** the tenant tools on the Default shell check only `IsDefaultShell()`: `M/AI.Agent/Tenants/CreateTenantTool.cs:97`, `RemoveTenantTool.cs:65`, `SetupTenantTool.cs:127`, `DisableTenantTool.cs:62`. `OrchardCorePermissions.ManageTenants` is defined (`OrchardCorePermissions.cs:21`) and unused. A profile on the host tenant that carries these tools lets its users, or a prompt injection, create, set up, disable or remove tenants.
- **Fix, staged:**
  1. (S, first) In the three drivers and in `AIProfilesApiEndpoints`, keep only the tools for which `IAIToolAccessEvaluator.IsAuthorizedAsync` passes, and preserve tools already on the profile that the editor cannot see. Each tenant tool checks `ManageTenants` against the current user. Refuse to save a profile that anonymous visitors can use when it carries a tenant tool, the recipe-import tool or any tool that later gains a required permission.
  2. (L, separate item) Add a `RequiredPermission` to tool registrations (use the unused `OrchardCorePermissions`) and check it inside each tool against the current user, with an explicit per-profile "run with the profile's authority" option that requires `AccessAnyAITool` to set.
- **Tests:** A posted unauthorized tool key is dropped; creating a profile with it through the API is a validation error; each tenant tool refuses a user without `ManageTenants`; `ImportOrchardTool` refuses a caller without `ManageRecipes`; an anonymous-accessible profile with a guarded tool does not save.
- **Confidence:** High on the drivers and tools; Medium on the run-time rule, which is in the package.

### SEC-A4. The chat hub runs any template-prompt profile without authorizing it (Medium, S)

- **Where:** `C/AI.Chat.Core/Hubs/AIChatHub.cs:508-577` (only the parent profile is authorized, `:535`); `M/AI.Chat/Views/Widget-AIChat.cshtml:47,231-246`; `M/AI.Chat/Views/AIChatSessionChat.cshtml:43`.
- **What:** The override returns before the base hub's `AuthorizeProfileAsync(profile)`. The session-start limiter does run on this path (`:606-615`), and the visitor's own prompt is not used (`:623`), but there is no limit per message. The widget renders every template-prompt profile's id and name to any visitor, without `CanAccessProfileAsync`.
- **Exploit:** An anonymous visitor of one public profile calls `SendMessage(templateId, null, ownSessionId, publicProfileId)` in a loop. Every internal template profile runs with its own system prompt, tools and data sources.
- **Fix:** Authorize `profile` as well as `parentProfile`; add a per-message limit on this path; filter the template list in both views.
- **Tests:** The hub answers "not authorized" for a template the caller lacks; the caller is throttled after the message limit; the view lists only permitted templates.
- **Confidence:** High.

### SEC-A5. AI cost limits are missing on the completion API and on document upload (Medium, M)

- **Where:** (a) `M/AI/Endpoints/Api/ApiAICompletionEndpoint.cs:41-47,104-108`, `ApiAIUtilityCompletionEndpoint.cs:29-31`, `C/AI.Core/ApiKeys/AIApiKeys.cs:123-136`, `M/AI/Controllers/AIApiKeysController.cs:188`; (b) (Core package) `UploadChatSessionDocument.cs:126-129,143`, mapped at `M/AI.Documents/Startup.cs:146-148`; (c) `M/AI.Chat/Startup.cs:111`, which registers (Core package) `DefaultAIVisitorIdentityResolver.ResolveRemoteAddress`.
- **What:** (a) The completion endpoints call the orchestrator with no limiter and no quota per key. The routes do require the `Api` or `AIApiKey` scheme, but a browser key is public by design and its origin check trusts the `Origin` header, which a script can set. (b) Upload by profile id creates and saves a session before it authorizes, with no session-start limit, 100 MB per file and no cap on the number of files (one reading). (c) **Disputed:** the first audit read the visitor resolver as taking the first `X-Forwarded-For` value from the raw request; the independent review read the pinned package as using the connection's remote address. Read the package source before acting on (c). Do not replace it with `C/AI.Core/ClientIpAddressAccessorExtensions.cs`, which hashes the client-chosen `User-Agent` into the key (`:29`).
- **Exploit:** A script replays a page's browser key with a forged `Origin`, or posts uploads in a loop: model, embedding and storage cost without a ceiling.
- **Fix:** A quota per API key as an attempt policy (RL-1), and a rate-limit group on the completion routes. Wrap the upload route: authorize first, apply the session-start limiter, add `MaxFilesPerSession`, and tag it with a group. For (c), if the resolver does read the raw header, replace it with one that uses the connection address through `ClientNetworkKey`.
- **Tests:** The call after the key's quota gets 429; an upload without access creates no session; the file cap is enforced.
- **Confidence:** High on (a); Medium on (b); (c) unconfirmed.

### SEC-A6. No egress guard on MCP HTTP, A2A, web crawler and provider endpoints (High, M)

- **Where:** `M/AI.Mcp/Drivers/SseMcpConnectionDisplayDriver.cs:56-67`; `M/AI.A2A/Drivers/A2AConnectionDisplayDriver.cs:78-86`; `M/AI.WebCrawlers/Drivers/SitemapWebCrawlerDisplayDriver.cs:70,75`; `M/OpenAI/Drivers/OpenAIProviderConnectionDisplayDriver.cs:64-74`; the ElevenLabs and Cartesia deployment drivers (`:61-71`); (Core package) `CrestApps.Core.AI.WebCrawlers/ServiceCollectionExtensions.cs:50-55` (`AllowAutoRedirect = true`).
- **What:** `EgressNetwork` is used only by Webhooks, DataConnectors and AgenticCommerce. These URLs are checked only for being absolute; a crawler's page and child-sitemap URLs come from the remote site.
- **Exploit:** A tenant administrator points a connection or a crawler at `http://169.254.169.254/` or an internal service and reads the answer through tool results or the knowledge base. The owner of any crawled site can list internal URLs or redirect to them.
- **Severity:** High on a multi-tenant host; Medium on a single-tenant site, where the people who can set these URLs are the site's administrators.
- **Fix, now, with what exists (do not wait for DRY-7):** Register the MCP, A2A and crawler named clients with `AddEgressGuardedHttpClient` (`C/Core/Http/EgressNetwork.cs:128`); replace the package's SSE transport provider with an Orchard one that passes a guarded `HttpClient`; validate with `EgressNetwork.IsAllowedUrl` in the drivers, the recipe steps and `AISourceCatalogApi`. A host-level option that allows loopback and private addresses for local providers such as Ollama is required, or local model hosts stop working. DRY-7 later merges the other classifiers into `EgressNetwork` and adds resilience overloads.
- **Tests:** The drivers and recipes reject `http://127.0.0.1`, `http://10.0.0.5` and `https://[::1]`; a redirect to loopback is refused.
- **Confidence:** High.

### SEC-A7. A content import overwrites and takes ownership of any content item (High, M)

- **Where:** `M/ContentTransfer/BackgroundTasks/ImportFilesBackgroundTask.cs:1042-1057,1297-1320`; `C/ContentTransfer.Core/Handlers/CommonContentImportHandler.cs:66-81`.
- **What:** Ids in the file's `ContentItemId` column are loaded with no check of the content type, and the job never calls `IAuthorizationService`. It then sets `Owner` and `Author` to the uploader and can publish.
- **Exploit:** A role that holds only `ImportContentFromFile_Article` uploads a row with another item's id (a page, an order) and ticks Publish.
- **Fix:** The essential part is small: in `ImportBatchAsync`, reject a row whose loaded item has another `ContentType`, and keep `Owner` and `Author` on an update. A per-item `EditContent` and `PublishContent` check against the uploader's rebuilt principal is the stricter option; put it behind `ContentImportOptions.RequireEditPermissionPerItem` (default off), because it can refuse imports that sites rely on today (an import role that updates items it does not own).
- **Tests:** A row with a foreign type's id produces a row error and leaves the item unchanged; an update keeps the owner; with the option on, an uploader without `EditContent` on the item is refused.
- **Confidence:** High.

### SEC-A8. The SQL Server connector lets a tenant sign in as the host's identity (High where the host identity has database rights, S)

- **Where:** `M/DataConnectors.SqlServer/SqlServerDataConnector.cs:160-190`.
- **What:** The `Integrated`, `EntraManagedIdentity` and `EntraDefault` authentication modes are available to any tenant with no host option; the network guard does not cover a public database host.
- **Exploit:** A tenant administrator picks managed identity, names the host's own database server and selects other tenants' `Document` or `UserIndex` tables.
- **Fix:** Allow the host-identity modes only on the Default tenant, or where `DataConnectorsSqlServerOptions.AllowHostIdentity` is set in host configuration (default false); enforce in `BuildConnectionString` and hide the modes in the driver otherwise. The tests at `tests/CrestApps.OrchardCore.Tests/.../SqlServerDataConnectorTests.cs:42-56` change with it.
- **Tests:** Each host-identity mode throws on a non-default tenant unless the option is set.
- **Confidence:** High on the code; the impact depends on the deployment.

### SEC-A9. An AI API key can be given any role (Medium, S)

- **Where:** `M/AI/Drivers/AIApiKeyDisplayDriver.cs:91-95,158-166`; `C/AI.Core/ApiKeys/AIApiKeys.cs:160-161`; `C/AI.Core/AIPermissions.cs:44`.
- **What:** The editor offers every role except Anonymous and stores the posted names unchecked; the key's principal carries them. `ManageAIApiKeys` is not security-critical.
- **Exploit:** A role given "Manage AI API keys" creates a key with Administrator and calls every `api/ai/v1/*` route with full rights.
- **Fix:** Mark the permission security-critical (one line; do it first). Then allow only the roles the editor holds unless they have `ManageRoles`, and validate names against `IRoleService`.
- **Tests:** Posting a role the editor lacks is a model error.
- **Confidence:** High.

### SEC-A10. The Copilot OAuth callback never validates `state` (Medium, S)

- **Where:** `M/AI.Chat.Copilot/Controllers/CopilotAuthController.cs:91-134` (`:98,106,125` use `state` as the return URL).
- **What:** The package issues a nonce and a protected state cookie and ships `GitHubOAuthService.TryValidateCallbackState`, but nothing in this repository calls it.
- **Exploit:** An attacker gets a GitHub `code` for their own account and has a signed-in user open `/copilot/OAuthCallback?code=...`. The victim's account is linked to the attacker's GitHub, so the victim's Copilot chats run under it.
- **Fix:** Call `TryValidateCallbackState` first, answer 400 on failure, and use the validated return URL.
- **Tests:** A callback without the cookie or with a wrong nonce stores no credential.
- **Confidence:** High.

### SEC-A11. The "single SELECT" validator is bypassed with nested comments (Medium, S)

- **Where:** `C/DataConnectors.Core/Relational/RelationalDataConnector.cs:393-403` (wrapped at `:324`, run at `:238-241`).
- **What:** The validator ends a comment at the first `*/`. T-SQL and PostgreSQL nest block comments and need no `;` between statements.
- **Exploit:** A user with `ManageConnectedDataSets` saves a query whose nested comment hides a closing parenthesis and further statements from the validator. Traced by hand, not run; what it can write is limited by the connection's login.
- **Fix:** Reject comments in data-set queries, or track nesting per dialect; return a generic database error. Recommend a read-only login in the connector's documentation and editor hint.
- **Tests:** `ValidateQuery` refuses a nested-comment payload for both dialects.
- **Confidence:** High on the validator; Medium on execution.

### SEC-A12. Scheduled report views serve the owner's rows to every reader (Medium, M)

- **Where:** `M/Reports/Designer/Services/ReportViewsDataSource.cs:114-128`; `ReportViewSnapshotRefresher.cs:297-308`.
- **What:** A scheduled view is refreshed as its owner, and the snapshot is returned to any reader, so a data set's row restrictions are not applied again.
- **Exploit:** A designer with a narrow row scope reads all rows of an administrator's scheduled view.
- **Fix:** Use the snapshot only for the owner, or when no data set in the plan restricts rows; otherwise run live.
- **Tests:** A restricted reader gets only their rows.
- **Confidence:** Medium.

### SEC-A13. Report subscriptions keep sending after the author loses access (Medium, S)

- **Where:** `M/Reports/Subscriptions/Services/ReportSubscriptionService.cs:192-203`.
- **What:** At send time only the export permission is checked again.
- **Fix:** In `TrySendAsync`, resolve the author and require the report's permission and `ManageReportSubscriptions`; otherwise disable the subscription and record why.
- **Tests:** A send is refused for a disabled author and for one who lost the permission.
- **Confidence:** High.

### SEC-A14. MCP content resources skip content permissions (Low by default, S)

- **Where:** `M/AI.Mcp/Handlers/ContentByIdResourceTypeHandler.cs:60-67`; `ContentByTypeResourceTypeHandler.cs:57-60`.
- **What:** No `ViewContent` check; `GetVersionAsync` returns drafts; the by-type list is unbounded. Reaching the MCP server needs `AccessMcpServer`, which is security-critical and granted to administrators only by default (`McpServerSettings.cs:15,27`), so this matters once an operator gives MCP access to a narrower role or an API key.
- **Fix:** Authorize `ViewContent` per item against the caller; refuse unpublished versions; page and cap the list.
- **Tests:** A caller without `ViewContent` gets nothing; a draft version is denied.
- **Confidence:** High.

### SEC-A15. "Clear AI memory" can clear any user's memory (Medium, S)

- **Where:** `M/AI.Memory/Controllers/UserMemoryController.cs:66`; `M/AI.Memory/Handlers/AIMemoryAuthorizationHandler.cs:26-30`.
- **What:** The controller asks for `ClearAIMemory` with the target user as the resource. Orchard's role handler grants a permission without looking at the resource, so `ClearAIMemoryForOthers` is never required.
- **Fix:** Choose the permission by whether the target is the current user, and authorize without a resource, in the controller and in `UserMemoryDisplayDriver`.
- **Tests:** A role with only `ClearAIMemory` gets 403 for another user. Use the real authorization service in the test: the existing test mocks it and cannot catch this.
- **Confidence:** High (the module's handler never calls `Fail()`, so nothing denies the other-user case).

### SEC-A16. Model output can leak data through Markdown images (Medium, S to M)

- **Where:** `M/AI.Chat/wwwroot/scripts/ai-chat.js:110-115,313-323,437-443`; `M/AI.Chat.Interactions/wwwroot/scripts/chat-interaction.js:78-83,262,373-379`. Both are vendored from `@crestapps/ai-chat-ui` (see `M/AI.Chat/Assets.json`).
- **What:** Output is sanitized with DOMPurify, which stops script, but an `<img>` to any host is rendered.
- **Exploit:** A poisoned page or document tells the model to append an image whose URL carries the conversation; the browser fetches it with no click.
- **Fix:** Upstream, in CrestApps.Core: an image-host allowlist, with other images shown as links. Here, until then: a `Content-Security-Policy` `img-src` on pages that render chat.
- **Tests:** A renderer test in the package; a header test here.
- **Confidence:** High.

### SEC-A17. ContentTransfer: raw HTML on import, an unguarded CSV export, and resource exhaustion (Medium, M)

- **Where:** `C/ContentTransfer.Core/Handlers/HtmlBodyPartContentImportHandler.cs:59-62`; `M/ContentTransfer/FileFormats/CsvContentTransferFileFormatProvider.cs:339-366`; `M/ContentTransfer.OpenXml/Services/ExcelContentTransferFileFormatProvider.cs:74-79,127-147`; `C/ContentTransfer.Core/Services/ContentTransferChunkFileUploadService.cs:93,170-187`.
- **What:** Imported HTML is stored unsanitized. The module's own CSV writer does not use the guarded `CsvWriter`, so the formula-injection fix (audit defect 12) is incomplete here (also DRY-12h). XLSX columns and shared strings are uncapped, and each upload id preallocates the declared size, up to 1 GB (these two rest on one reading).
- **Fix:** Apply `IHtmlSanitizerService`; write through `CsvWriter`; add limits to `ContentImportOptions` (columns, rows, cell length, shared strings); do not preallocate.
- **Tests:** A script is stripped on import; `=1+1` is guarded on export; oversized references fail fast.
- **Confidence:** High on CSV and HTML; Medium on the limits.

### SEC-A18. An anonymous wizard start writes sessions without bound (Medium, M)

- **Where:** `M/Wizard/Controllers/WizardController.cs:98-129`.
- **What:** The action checks the `StartWizard` permission (`:98-100`), which the Anonymous role holds by default, so for a public wizard every cookieless `GET /Wizard/Start/{type}` stores a session; `definitionId` is not validated and nothing purges the sessions.
- **Fix:** 404 for an unknown definition; store the session on the first valid POST; apply the `wizard` rate-limit group to the wizard routes (the constant at `A/Wizard.Abstractions/WizardConstants.cs:78` is defined and used nowhere, RL-F4) and register a built-in limit for it; add an `IRetentionPolicy` purge.
- **Tests:** A GET alone stores nothing; expired sessions are purged.
- **Confidence:** High.

### SEC-A19. Remote file and crawl fetches lack integrity checks and size limits (Medium, M)

- **Where:** (Core package) `CrestApps.Core.AI.Sftp/SftpRemoteFileClientFactory.cs:76-80`; `CrestApps.Core.AI.Ftp/Handlers/FtpResourceTypeHandler.cs:95-98`; `CrestApps.Core.AI/Crawling/SitemapCrawler.cs:235,458`; `C/AI.FileTransfer.Core/Drivers/SftpConnectionDisplayDriverBase.cs` (no fingerprint field).
- **What:** No SFTP host-key check; an FTP "accept any certificate" switch; MCP remote reads into an unbounded `MemoryStream`; crawler bodies and gzip without a cap. One reading; the package files were not read a second time.
- **Fix:** Add `HostKeyFingerprint` and fail closed (the same fix as SEC-C17k: one SFTP connection helper for both); put "accept any certificate" behind a host option; set `MaxResponseContentBufferSize` and decompress through a bounded stream. Part of this is upstream.
- **Tests:** A fingerprint mismatch is refused; an oversized body aborts.
- **Confidence:** Medium.

### SEC-A20. Smaller AI and data items (Low, S each; not read a second time)

| Id | Where | What | Fix |
| --- | --- | --- | --- |
| a | `M/AI/Endpoints/Api/ApiAICompletionEndpoint.cs:124-131` | The API's template-prompt path does not authorize the parent profile | Authorize both |
| b | `C/AI.Core/Services/DefaultAIChatSessionManager.cs:197-211` | Anonymous history loads every anonymous session into memory | Add `ClientId` to `AIChatSessionIndex` and query by it |
| c | `M/AI.Chat/Controllers/RealtimeController.cs:30` | An anonymous voice list that also reveals deployment names | Require profile access |
| d | `C/AI.Chat.Core/Hubs/ChatHubBase.cs:50-95` | A hub base with an unchecked `HandleNotificationAction` and no subclass | Delete it |
| e | `M/AI.Mcp/Drivers/McpServerSettingsDisplayDriver.cs:190`; `C/AI.Core/Drivers/ConnectionAuthDisplayDriverBase.cs:103,213` | The MCP server API key is stored in clear in site settings; connection custom headers are stored, shown and exported in clear; stdio environment variables are exported | `ProtectedSettingExtensions`; blank on export |
| f | `M/AI.A2A/Startup.cs:99` | Every agent profile is callable through the A2A host and the agent card is anonymous | An allowlist of exposed profiles; a rate-limit group |
| g | `M/AI.DataSources.AzureAI/Services/OrchardCoreAzureAISearchDataSourceContentManager.cs:19-28` | The caller's filter is concatenated after a lexical check only | Parse and rebuild the filter, or use the package's escaped builder |
| h | `M/AI.DataSources.PostgreSQL/Services/PostgreSQLAIDataSourceSourceHandler.cs:93,188` | A PostgreSQL data source can read any table of a host-shared connection | Restrict to an allowlisted schema per connection |
| i | (Core package) `UploadChatSessionDocument.cs:37` | Document upload routes disable antiforgery under cookie authentication | Validate the token (with SEC-FE4f) |
| j | `C/Core/Http/EgressNetwork.cs:53-61,114-119` | `UseProxy` left on; NAT64, 6to4 and `168.63.129.16` ranges missing; the SQL host is resolved twice | Tighten in DRY-7 |
| k | `M/Reports/.../ReportDesignService.cs:131-137`; `ExportReportTask.cs:127-135` | An edit by a non-owner runs with the owner's access; workflow exports land in public Media; sharing with Authenticated is not gated by `ShareReportsPublicly` | Run as the editor; write exports to a tenant file store with signed links; gate the share |
| l | `M/DataConnectors/.../HttpConnectionDisplayDriver.cs:161-186`; `C/Webhooks.Core/.../WebhookSender.cs:51` | A stored secret survives a change of host; error and token bodies are read without a cap; webhooks accept `http`; the secret is shown on every edit | Clear the secret when the host changes; bound reads; HTTPS only outside Development; show the secret once |
| m | `M/ContentTransfer` admin controller | Bulk Remove needs only the list permission; `Delete`, `ResumeImport` and `PauseImport` are GETs; export has no `ViewContent` check | Permissions per action; POST; filter by `ViewContent` |
| n | `C/FileStorage.Core/TenantBlobContainerEvents.cs:110` | Tenant removal deletes blobs by a raw prefix | Require a delimiter after the tenant name |
| o | `src/Startup` sample hosts | Development credentials are committed; the MCP and A2A sample clients have no Development guard | Guard and document |

### AI and data: checked and sound

- **AI API keys:** cryptographic generator, a 24-byte secret, SHA-256 stored, fixed-time compare, prefix lookup, expiry and disable; API routes accept only the `Api` and `AIApiKey` schemes, so no cookie CSRF.
- **Chat session isolation:** `DefaultAIChatSessionManager.FindAsync` scopes by user id or visitor cookie; the hub authorizes the profile on every method except the one in SEC-A4; `ChatInteractionHub` is `[Authorize]` with own-versus-all checks.
- **Admin controllers:** every action in the AI, AI.Chat, Interactions, MCP, A2A, DataSources, WebCrawlers, FileSources, Reports and DataConnectors controllers checks a permission.
- **Provider secrets:** OpenAI, Azure, Azure AI Inference, Claude, Copilot, ElevenLabs and Cartesia keys are protected, write-only in editors and blanked on export.
- **Chat rendering:** Markdown goes through DOMPurify before it is rendered; `javascript:` links are blocked.
- **Liquid:** only administrator-authored templates are rendered; user text is never a template.
- **MCP server endpoint:** always behind `McpServerPolicy`, API key or `Api` scheme only, tools allowlisted and empty by default.
- **WebMcp and NLWeb:** antiforgery on invoke, `ViewContent` per item, published items only, size caps.
- **Vector search:** documents filter on reference id and type; memory on a user id resolved on the server.
- **Reports:** SQL building uses allowlisted, quoted identifiers and bound parameters; `ReportsHub` authorizes per report; exports go through `CsvWriter`.
- **Not audited:** PDF and OpenXml extraction limits in the Core packages, FTP library command handling, the MCP SDK's origin checks, Orchard Core internals.
