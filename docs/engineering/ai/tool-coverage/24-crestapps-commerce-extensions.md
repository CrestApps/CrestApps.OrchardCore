# CrestApps: commerce extensions

Part of the [AI tool coverage plan](README.md). Nothing in this file is built yet. The design rules, the permission and approval model and the test plan that apply to every tool below are in [01-foundation.md](01-foundation.md) and [03-testing.md](03-testing.md); the final agent roster is in [02-agents.md](02-agents.md) and wins over the agent notes in this file.

Scope: `CrestApps.OrchardCore.Subscriptions`, `.Memberships`, `.Marketplace`, `.Classifieds`, `.Fundraising`, `.Downloads`, `.Reviews`, `.Marketing`, `.Bookings`, `.AgenticCommerce` under `src/Modules/`. Everything below was read from code (manifests, startups, controllers, drivers, services, recipe steps); doc pages were used only to cross-check menu paths.

How the records are stored, because it decides whether the generic content tools reach them:

| Record | Store | Reachable through content tools |
| --- | --- | --- |
| Subscription plan (a content type with stereotype `Subscription`; `SubscriptionPart` and `ProductPart` are attached automatically) | Content item | Yes |
| Subscription agreement (`Subscription`) | Own YesSql collection `Subscription` | No |
| Site provisioning job (`TenantProvisioningJob`) | Own collection `TenantProvisioning` | No |
| Installment plan (`InstallmentPlan`) | Own collection `InstallmentPlan` | No |
| Membership product (`MembershipPart` on a product) | Content item | Yes |
| Membership (`Membership`) | Own collection `Memberships` | No |
| Vendor, vendor earning, vendor payout | Own collection `Marketplace` | No |
| Product-to-vendor link (`VendorPart.VendorId` on a product) | Content item | Yes |
| Posted listing (`ClassifiedPart` on a content item), listing upgrade product (`ClassifiedUpgradePart`) | Content item | Yes for the content; no for the moderation actions |
| Fundraising campaign (`FundraisingCampaignPart` on a product) | Content item | Yes |
| Donation (`Donation`) | Own collection `Fundraising` | No |
| Digital files on a product (`DigitalFilesPart`; the bytes live under `App_Data/.../DigitalFiles`) | Content item + private file store | Part fields yes, file bytes no |
| Download entitlement (`DownloadEntitlement`) | Own collection `Downloads` | No |
| Product review (`ProductReview`) | Own collection `Reviews` | No |
| Marketing segment, campaign, customer purchases | Own collection `Marketing` | No |
| Bookable product (`BookingPart` on a product) | Content item | Yes |
| Booking, booking date/slot (`BookingSlot`), booking balance | Own collection `Bookings` | No |
| Agent platform (`AgentPlatform`), agent checkout | Own collection (`AgenticCommerceStorage.CollectionName`) | No |
| Per-product agent visibility (`ProductFeedPart.ShowToAgents`, `.AgentCheckout`, owned by the Products module) | Content item | Yes |

## Subscriptions — `CrestApps.OrchardCore.Subscriptions`

**What an admin can do in the UI:**
- Subscriptions → Agreements (`SubscriptionAgreementsController`): list every agreement with a status filter (`Active`, `Trialing`, `PastDue`, `Canceled`, `Expired`, `Paused`, `Incomplete`) and a search on the title; open one agreement (lines, entitlements, history, owner); Cancel (now or at the end of the paid period, with a reason); Pause (suspend billing, with a reason); Resume (a paused or past-due agreement back to active).
- Subscriptions → My Plans and the storefront `account/subscriptions` page: the signed-in user's own plans and their own cancel. Customer-side, not an admin action.
- Settings → Subscriptions: currency, allow guest sign-up; with Roles on, the roles a plan may grant.
- Plans themselves: content items of any type with stereotype `Subscription`. `SubscriptionPart` (initial amount, billing duration and unit, cycle limit, start delay, trial days, sort) and, with Roles on, `SubscriptionEntitlementPart.RoleNames`.
- Reports (when the Reports feature is on), by class name: `SubscriptionRevenueReport`, `SubscriptionsDashboardReport`, `ExpiringSubscriptionsReport`, `NewSubscriptionsTrendReport`, `TaxCollectedReport`, `ProductPerformanceReport`. Report data sets for Data Pipelines: subscriptions, subscription lines.
- Workflow events: subscription started, renewed, past due, canceled, expired.

**Already covered:** content tools cover subscription plans (create, edit, publish, delete a plan and its `SubscriptionPart` / `SubscriptionEntitlementPart` fields). `listWorkflowActivities` / `createOrUpdateWorkflow` cover the five workflow events. Nothing covers agreements. There is no recipe or deployment step for agreements (only settings schemas for the `Settings` step).

**Settings groups:**
- `SubscriptionSettings` / group `subscriptions` / permission `ManageSubscriptionSettings` (security critical) / secrets: none. Fields edited by the driver: `Currency`, `AllowGuestSignup` (`DunningGraceDays` exists on the class but the driver does not edit it).
- `SubscriptionRoleSettings` / group `subscriptions` / permission `ManageSubscriptionSettings` / secrets: none. Only when `OrchardCore.Roles` is on.
- `SubscriptionOnboardingSettings` / group `subscriptions` / permission `ManageSubscriptionSettings` / secrets: `ProtectedConnectionString` (write-only; the editor shows only whether one is set). Only with the Sites feature.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listSubscriptions` | Read | no | `ManageSubscriptions` | Lists subscription agreements with optional status, title search, owner and paging. Use it to find an agreement's id before reading, canceling or pausing it. | SubscriptionsAgent |
| `getSubscription` | Read | no | `ManageSubscriptions` | Returns one agreement: owner, plan, amount, billing period, next billing date, lines, entitlements and its history of events. Use it before any change so the user sees what will be affected. | SubscriptionsAgent |
| `cancelSubscription` | External | yes | `ManageSubscriptions` | Cancels an agreement now or at the end of the paid period and tells the payment provider to stop billing. Cannot be undone; a canceled agreement is not restarted, the customer must subscribe again. | SubscriptionsAgent |
| `setSubscriptionPaused` | External | yes | `ManageSubscriptions` | Pauses billing on an agreement (with a reason) or resumes a paused or past-due one. The change is sent to the payment provider, so resuming means the customer is billed again. | SubscriptionsAgent |

**Implementation notes:** `ISubscriptionManager.PageAsync(page, pageSize, SubscriptionQuery)` and `FindByIdAsync` for reads; `ISubscriptionLifecycleService.CancelAsync(id, atPeriodEnd, reason, source)`, `PauseAsync(id, reason)`, `ResumeAsync(id)` for changes. Pass the calling user's id as `source`, as the controller does. `GatewayBillingSubscriptionLifecycleHandler` calls the gateway (`CancelRecurringAsync`, `PauseRecurringAsync`); when the gateway refuses, the local record still changes and a payment issue is recorded, so the tool result must say "check the provider dashboard" in that case. `ResumeAsync` only changes a `Paused` or `PastDue` agreement and `PauseAsync` ignores a canceled, expired or already paused one; both return without an error when nothing changed, and the admin page reports success anyway, so the tool must compare the status before and after and say when nothing happened. The owner is stored as a user id: resolve the display name the way `SubscriptionAgreementsController.ResolveOwnerNameAsync` does. Search matches the title only. Show dates in the site or user time zone (`ILocalClock`), never UTC.

**Test prompts:**
- "Which subscriptions are past due right now?"
- "Show me the history of Maria Lopez's Pro plan subscription."
- "Cancel subscription 4x9… at the end of the period, reason: customer asked by phone."
- "Pause billing on the Acme agreement until they sort out their card."

## Subscriptions - Sites — `CrestApps.OrchardCore.Subscriptions.Tenants`

**What an admin can do in the UI:**
- Subscriptions → Site provisioning (`TenantProvisioningAdminController`, default tenant only): list provisioning jobs with a status filter; Retry a job that is not succeeded or running (resets the attempt counter and runs it immediately); Mark as created for an abandoned job (optionally with the site URL shown to the customer).
- Subscriptions → My Sites: the signed-in user's own sites. Customer-side.
- Settings → Subscriptions: database provider, connection string, schema for sold sites.
- On a plan: `TenantOnboardingPart` (recipe name, feature profile).
- Workflow events: subscribed tenant setup succeeded, subscribed tenant setup failed.

**Already covered:** content tools cover `TenantOnboardingPart` on a plan. The tenant tools (`listTenant`, `getTenant`, `createTenant`, `setupTenant`) cover creating the site by hand when a job can no longer be retried. Nothing covers the jobs.

**Settings groups:** `SubscriptionOnboardingSettings` (see Subscriptions above).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listSiteProvisioningJobs` | Read | no | `ManageSubscriptions` | Lists the jobs that create sold sites, with their status, attempts, last error, tenant name and site URL, optionally filtered by status. Use it to find paid customers whose site was not created (status Abandoned). | SubscriptionsAgent |
| `resolveSiteProvisioningJob` | Write | no | `ManageSubscriptions` | Retries a site provisioning job now (`action: retry`), or closes an abandoned job whose site was created by hand, with its URL (`action: markCreated`). Retry is refused once the job no longer holds the administrator password; then create the site with the tenant tools and mark the job created. | SubscriptionsAgent |

**Implementation notes:** `ITenantProvisioningJobStore` (`GetAllAsync`, `FindByIdAsync`, `UpdateAsync`), `ITenantProvisioningService.ProvisionAsync(jobId)`, and the guards `TenantProvisioningJobActions.CanRetry` / `CanMarkCreated`. The retry and mark-created logic (resetting `Status`, `AttemptCount`, `NextAttemptUtc`; setting `CompletedUtc`, `SiteUrl`) lives inside `TenantProvisioningAdminController` and must be extracted into a service first. Never return `ProtectedAdminPassword`. Retry creates and sets up a tenant, which can take a while and is tenant-wide; the feature is `DefaultTenantOnly`. `markCreated` tells the customer the site is ready, so the tool should require the URL.

**Test prompts:**
- "Are there any site provisioning jobs that failed or were abandoned?"
- "Retry the provisioning job for tenant 'bluebird'."
- "I created the 'bluebird' site by hand at https://bluebird.example.com, mark its job as created."

## Subscriptions - Installment Plans — `CrestApps.OrchardCore.Subscriptions.Installments`

**What an admin can do in the UI:**
- Subscriptions → Installment Plans (`InstallmentPlansController`): list plans with a status filter (`Draft` = waiting for down payment, `Active`, `PastDue`, `Completed`, `Canceled`) and search; Create a plan (title, existing customer or a new customer's name and email, currency, total, down payment, number of payments, frequency, first due date, collection method `AutoCharge` or `Invoice`, notes); then the Pay page takes the first payment by card through the payment provider (`BeginPayment` / `PaymentStatus` endpoints, card entered in the browser); Detail (schedule, ledger transactions, receipts); Charge one payment to the saved card now; Cancel the plan (with a reason).
- Settings → Installment Plans: retry days, default collection method, and the "pay in installments at checkout" options (sources, counts, frequency, minimum amount).

**Already covered:** nothing. No recipe or deployment step for plans.

**Settings groups:** `InstallmentPlanSettings` / group `installment-plans` / permission `ManageInstallmentPlans` (security critical) / secrets: none.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listInstallmentPlans` | Read | no | `ManageInstallmentPlans` | Lists installment plans with optional status, search and paging, showing customer, total, paid, outstanding and next payment. Use it to find past-due plans or a plan's id. | SubscriptionsAgent |
| `getInstallmentPlan` | Read | no | `ManageInstallmentPlans` | Returns one plan with its schedule (each payment's number, due date, amount, status), its history, and, while it waits for its down payment, the admin link where staff take that payment by card. | SubscriptionsAgent |
| `createInstallmentPlan` | Write | no | `ManageInstallmentPlans` | Creates a draft installment plan for an existing customer (total, down payment, number of payments, frequency, first due date, collection method) and returns the link where staff take the first payment by card. Nothing is charged by this tool; the plan stays a draft until that payment is taken. | SubscriptionsAgent |
| `chargeInstallmentPayment` | External | yes | `ManageInstallmentPlans` | Charges the customer's saved card for one scheduled payment of a plan now, by payment number. Use it for a due or failed payment; it fails when the plan has no saved card. | SubscriptionsAgent |
| `cancelInstallmentPlan` | Destructive | yes | `ManageInstallmentPlans` | Cancels a plan so payments not yet received are no longer collected. Cannot be undone; payments already received are not refunded. | SubscriptionsAgent |

**Implementation notes:** `IInstallmentPlanStore.PageAsync(page, pageSize, InstallmentPlanQuery)` / `FindByIdAsync`; `IInstallmentPlanService.CreateAsync(CreateInstallmentPlanRequest)`, `ChargeAsync(planId, paymentNumber)`, `CancelAsync(planId, reason)`. `CreateAsync` puts an outstanding transaction on the customer's ledger and starts a checkout session; it fails when no payment provider that can keep a card is enabled. The first payment cannot be taken by a tool (the card is entered in the provider's browser component), so the tool must return the `InstallmentPlansPay` admin URL. `CreateAsync` can also create a passwordless user account for a new customer (`NewCustomerName` / `NewCustomerEmail`); I recommend the tool accepts only `customerUserId` (found with `searchForUsers`) so it stays a plain write; see Open questions. `FirstDueUtc` must be entered as a local date and converted. `ChargeAsync` may return "sent, still being confirmed"; report that state rather than success.

**Test prompts:**
- "Which installment plans are past due?"
- "Set up a payment plan for user jsmith: 1,200 USD season fee, 200 down, 5 monthly payments starting next month."
- "Charge payment 3 of the Thompson plan to the saved card now."
- "Cancel the Garcia installment plan, they paid the rest in cash."

## Memberships — `CrestApps.OrchardCore.Memberships`

**What an admin can do in the UI:**
- Commerce → Memberships (`MembershipsAdminController`): list memberships with a search and a status filter (`Active`, `Expired`, `Revoked` shown as "Ended early"); End now on an active membership (takes the member out of the membership's roles, refunds nothing).
- On a product: `MembershipPart` (`RoleNames`, `DurationDays`) makes the product a membership.
- Storefront `account/memberships`: the customer's own list.
- Background task "Membership end dates" expires memberships.

**Already covered:** content tools cover the membership product (`MembershipPart`). Recipe step `Memberships` (through `importOrchardCoreRecipe`) can add or update membership records and grants their roles when the status is Active; deployment step for the same data. Refund-driven ending happens through the order (Orders group).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listMemberships` | Read | no | `ManageMemberships` | Lists memberships with optional member search, status and paging: member, product, roles, start and end dates, orders. Use it to see who is a member or to find a membership's id. | SubscriptionsAgent |
| `endMembership` | Security | yes | `ManageMemberships` | Ends an active membership now and removes the member from the roles it added. Refunds nothing and cannot be undone from the admin; the member must buy again. | SubscriptionsAgent |

**Implementation notes:** `IMembershipStore.PageAsync(search, status, skip, take)` and `FindByIdAsync`; `MembershipService.EndAsync(membership, MembershipStatus.Revoked, reason)`. The controller refuses anything that is not `Active`; keep that guard. The UI has no "grant a membership" action; the only non-purchase path is the `Memberships` recipe step, which grants roles, so `importOrchardCoreRecipe` is effectively a security tool for this step.

**Test prompts:**
- "How many active members do we have, and whose membership ends this month?"
- "End Sam Carter's membership now."

## Marketplace — `CrestApps.OrchardCore.Marketplace`, `CrestApps.OrchardCore.Marketplace.StripeConnect`

**What an admin can do in the UI:**
- Commerce → Vendors (`VendorsAdminController`): list and search vendors; Add vendor / Edit (name, email, the user names that may open the vendor page, commission percent or empty for the store's, country, status `Active` or `Suspended`); Detail (totals per currency: held, available, paid out; up to 500 earnings; up to 100 payouts); Release what a vendor earned on one order before its hold ends; Pay out everything available to the vendor. There is no delete; a vendor is suspended instead.
- On a product: `VendorPart.VendorId` assigns the product to a vendor.
- Settings → Commerce → Marketplace: store commission, hold days, release on fulfillment, automatic payouts, payout provider, default country.
- Storefront `account/vendor`: the vendor's own dashboard and Stripe onboarding (vendor-side).
- Background task "Marketplace releases and payouts" (hourly) releases due earnings and, when automatic payouts are on, pays out.
- Data sets for Data Pipelines: vendors, vendor earnings, vendor payouts.

**Already covered:** content tools cover assigning a product to a vendor (`VendorPart.VendorId`; the id comes from `listVendors`). Recipe step `Vendors` (through `importOrchardCoreRecipe`) adds or updates vendors without their users, Stripe account or payout flag; deployment step for the same.

**Settings groups:** `MarketplaceSettings` / group `marketplace` / permission `ManageMarketplace` (security critical) / secrets: none.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listVendors` | Read | no | `ManageMarketplace` | Lists marketplace vendors with optional name search and paging: status, commission, country, whether payouts are set up. Use it to find a vendor's id. | MarketplaceAgent |
| `getVendor` | Read | no | `ManageMarketplace` | Returns one vendor with its totals per currency (held, available, paid out), its recent earnings per order and its payouts with any failure message. Use it before releasing or paying out. | MarketplaceAgent |
| `saveVendor` | Security | yes | `ManageMarketplace` | Creates or updates a vendor: name, email, the user accounts that may open the vendor page and set up its payout account, commission percent, country and status (Active or Suspended). Suspending stops payouts; there is no delete. | MarketplaceAgent |
| `releaseVendorEarnings` | External | yes | `ManageMarketplace` | Releases what a vendor earned on one order from escrow before its hold ends, making it available to pay out. Cannot be re-held, and with automatic payouts on it is paid out within the hour. | MarketplaceAgent |
| `payOutVendor` | External | yes | `ManageMarketplace` | Pays out everything available to one vendor through the payout provider (one transfer per order) and returns the amount paid and any failures. Requires an enabled payout provider, an active vendor and a payout account that is set up. | MarketplaceAgent |

**Implementation notes:** `IVendorStore.PageAsync(search, skip, take)` / `FindByIdAsync` / `CreateAsync` / `UpdateAsync`; `IVendorEarningStore.ListByVendorAsync(vendorId, 500)`; `IVendorPayoutStore.ListByVendorAsync(vendorId, 100)`; `MarketplaceService.GetTotals`, `ReleaseOrderAsync(vendorId, orderId)`, `PayOutAsync(vendor)`, `CanPayOut`. The vendor validation and user-name resolution live in `VendorsAdminController.ApplyAsync` and must be extracted. With Stripe Connect on, the admin detail page calls `StripeConnectVendorService.RefreshAsync(vendor)` (a call to Stripe) before showing the vendor; `getVendor` should do the same so `PayoutsEnabled` is current. `PayOutAsync` is idempotent per vendor, order and earnings set. `saveVendor` is marked Security because the listed users control where the vendor's money goes; a commission change affects only later sales (unverified beyond `RecordSaleAsync` reading the percent at sale time).

**Test prompts:**
- "How much do we owe each vendor right now?"
- "Add a vendor called Clay & Co, 12% commission, user 'clayco' manages it."
- "Release Clay & Co's earnings on order 10482, it was delivered early."
- "Pay out Clay & Co."

## Classifieds — `CrestApps.OrchardCore.Classifieds`

**What an admin can do in the UI:**
- Content → Posted listings (`ClassifiedsAdminController`): list posted listings, by default those waiting for approval, with a status filter (`Pending`, `Approved`, `Rejected`, `Expired`, all) and search; Approve (publishes and starts the listing's term); Reject with an optional reason (unpublishes and emails the poster).
- Content type part settings for `ClassifiedPart`: days a listing runs, whether a moderator approves each listing.
- Upgrade products: `ClassifiedUpgradePart` (`Kind` Feature or Bump, `Days`).
- Storefront: post, edit, renew, buy an upgrade, search by distance (visitor-side).
- Background task "Listing terms" expires listings and emails posters.

**Already covered:** content tools cover the listing content items themselves (read, edit, delete) and the upgrade products; content definition tools cover attaching `ClassifiedPart` and its settings. Do not approve or reject by editing `ClassifiedPart.Status` through the content tools: that skips the term, the publish/unpublish and the email. `searchForContentItems` cannot filter by listing status.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listClassifieds` | Read | no | `ModerateClassifieds` | Lists posted listings by status (default: waiting for approval) with search and paging: title, type, poster, place, expiry, featured-until. Use it to see what needs moderation. | MarketplaceAgent |
| `approveClassified` | Write | no | `ModerateClassifieds` | Approves a posted listing: publishes it and starts its term from now using the content type's listing days. | MarketplaceAgent |
| `rejectClassified` | External | yes | `ModerateClassifieds` | Rejects a posted listing with an optional reason: takes it down and emails the poster the reason. Use it for listings that break the rules; the poster can edit and post again. | MarketplaceAgent |

**Implementation notes:** `ClassifiedService.PageAsync(status, search, skip, take)`, `ApproveAsync(item)`, `RejectAsync(item, reason)`; load the item with `IContentManager.GetAsync(id, VersionOptions.Latest)` and require `ClassifiedPart`, as the controller does. The owner name is resolved from the content item's `Owner` through `IUserService`. Reject is External only because it sends an email; the reason text goes to the poster verbatim, so the model must show it to the admin before approval.

**Test prompts:**
- "What listings are waiting for approval?"
- "Approve the 'Mountain bike, barely used' listing."
- "Reject the listing from user 'spam4u', reason: duplicate post."

## Fundraising — `CrestApps.OrchardCore.Fundraising`

**What an admin can do in the UI:**
- Settings → Commerce → Fundraising: organization name, tax ID, receipt statement, email receipts.
- A campaign is a product with `FundraisingCampaignPart` (goal, end date, suggested amounts, minimum amount, tax deductible); the admin edits it in the content editor and sees progress on the campaign's page.
- Donations appear as orders under Commerce → Orders; refunds go through the order. There is no admin list of donations; the donation list and printable receipt exist only on the donor's `account/donations` pages.

**Already covered:** content tools cover campaigns (`FundraisingCampaignPart`). Orders tools (Orders group) cover the donation orders and refunds. Nothing reads a campaign's raised amount or its donation records.

**Settings groups:** `FundraisingSettings` / group `fundraising` / permission `ManageFundraising` / secrets: none (`TaxId` is printed on receipts, not a secret).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `getFundraisingCampaignProgress` | Read | no | `ManageFundraising` | Returns a campaign's amount raised, goal, percent, number of donors and days left, and optionally its donations (donor, amount, receipt number, tribute, refunded). Use it to report on a campaign; the content tools return only the campaign's settings. | OrdersAgent |

**Implementation notes:** `DonationService.GetProgressAsync(campaignContentItem)` and `IDonationStore.ListByCampaignAsync(campaignId)` (no paging in the store; cap the output). This tool goes beyond what the admin UI shows (it is the one read an admin cannot do today), so it is optional for strict UI parity. Donor names and emails are personal data; return them only to a user who holds `ManageFundraising`.

**Test prompts:**
- "How much has the library campaign raised so far?"
- "List the donations to the library campaign over 500 dollars."

## Digital Downloads — `CrestApps.OrchardCore.Downloads`

**What an admin can do in the UI:**
- On a product: `DigitalFilesPart` editor uploads files (up to 1 GB each, `DigitalFilesAdminController.Upload`), downloads a stored file, and sets downloads per buyer and days the links work.
- On an order's admin page (`OrderDownloadsDisplayDriver`): each download entitlement with its count, limit, expiry and why a link stopped; Revoke (the buyer can no longer download); Reset (count back to zero, revoke cleared, a fresh expiry).
- Settings → Commerce → Downloads: default download limit and link lifetime.
- Report `DownloadsReport` (with Reports); data set download entitlements.

**Already covered:** content tools cover `DigitalFilesPart.DownloadLimit`, `.LinkLifetimeDays` and removing an entry from `.Files`. Recipe step `DigitalFiles` (through `importOrchardCoreRecipe`) attaches a file given as Base64 or text, up to 10 MB, to a product. Nothing covers entitlements.

**Settings groups:** `DownloadSettings` / group `downloads` / permission `ManageDigitalDownloads` / secrets: none.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listOrderDownloads` | Read | no | `ManageDigitalDownloads` | Lists the download entitlements of an order: file, downloads used and allowed, expiry, and whether the link is working, revoked, expired or over its limit. Use it when a buyer says a download link does not work. | OrdersAgent |
| `setOrderDownloadAccess` | Security | yes | `ManageDigitalDownloads` | Revokes one download entitlement so the buyer can no longer download the file, or resets it so the buyer can download again with the count at zero and a new expiry. Takes the entitlement id from `listOrderDownloads`. | OrdersAgent |

**Implementation notes:** `DownloadService.ListAsync(orderId)`, `FindAsync(itemId)`, `RevokeAsync(entitlement, reason)`, `ResetAsync(entitlement)`. `DownloadService.GetProblem` is `internal static`; make it public or return the problem from a service method. The permission's code name is `ManageDigitalDownloads` (C# field `DownloadsPermissions.ManageDownloads`). Revoke is reversible with reset, so the approval could be relaxed when the roster is harmonized. Large binary uploads stay in the UI; a tool cannot stream a 1 GB file.

**Test prompts:**
- "The buyer on order 10391 says the e-book link expired, can you check?"
- "Give them another 5 downloads on that file."
- "Revoke the downloads on order 10402, it was charged back."

## Product Reviews — `CrestApps.OrchardCore.Reviews`

**What an admin can do in the UI:**
- Commerce → Reviews (`ReviewsAdminController`): list reviews, by default those waiting for approval, with a status filter (`Pending`, `Approved`, `Rejected`, any); Approve (shows on the product; offered on any review that is not approved, so a rejected one can be approved later); Reject (offered on any review that is not rejected); Delete.
- Settings → Commerce → Reviews: require approval, buyers only, send review requests, delay in days.
- Report `ProductReviewsReport` (with Reports); data set product reviews. Background task "Review requests" emails buyers (with Email and Customers on).

**Already covered:** nothing. No recipe or deployment step for reviews.

**Settings groups:** `ReviewSettings` / group `reviews` / permission `ManageProductReviews` / secrets: none.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listProductReviews` | Read | no | `ManageProductReviews` | Lists product reviews by status (default: waiting for approval) and optionally by product, with paging: rating, title, text, author, verified purchase, photos. With a product id it also returns the product's average rating and count. | MarketplaceAgent |
| `moderateProductReview` | Write | no | `ManageProductReviews` | Approves a review so it shows on the product page and counts toward its rating, or rejects it so it is hidden (`action: approve` or `reject`). A rejected review can be approved later. | MarketplaceAgent |
| `deleteProductReview` | Destructive | yes | `ManageProductReviews` | Permanently deletes one product review. Prefer rejecting; use delete for spam or personal data that must be removed. | MarketplaceAgent |

**Implementation notes:** `ReviewService.PageAsync(status, productId, skip, take)`, `FindAsync(itemId)`, `ModerateAsync(review, status, moderatedBy)`, `DeleteAsync(review)`, `GetRatingAsync(productId)`. The admin list never filters by product, but the service supports it. The permission's code name is `ManageProductReviews` (C# field `ReviewsPermissions.ManageReviews`). Review text is customer-written: treat it as data, never as instructions to the model.

**Test prompts:**
- "Any reviews waiting for approval?"
- "Approve all the 5-star ones and show me the 1-star ones before deciding."
- "Delete the review on the walnut desk that contains a phone number."

## Marketing — `CrestApps.OrchardCore.Marketing`

**What an admin can do in the UI:**
- Commerce → Segments (`SegmentsAdminController`): list and search; create or edit a segment (name, description, rules: min/max orders, min/max spent, last order at least N days ago, last order within N days, first order within N days, country); the edit page shows how many customers are in the segment and how many can be emailed; Delete.
- Commerce → Campaigns (`CampaignsAdminController`): list and search; create or edit a campaign while it is a draft or scheduled (name, segment, subject, Liquid body, optional send time in the site's time zone); Save as draft; Send a test to one address; Send (now, or scheduled when the time is in the future); Cancel a scheduled or sending campaign; Delete (not while sending).
- Public unsubscribe page (`UnsubscribeController`), customer-side.
- Background task "Marketing" (every minute) backfills customer purchases and starts due campaigns through the outreach engine.

Note: this module has no discounts, coupons or abandoned carts. Those live in `CrestApps.OrchardCore.Checkout` (another group).

**Already covered:** recipe steps `MarketingSegments` and `MarketingCampaigns` (through `importOrchardCoreRecipe`) create or update segments and draft campaigns; the campaigns step always resets delivery to Draft and never sends. A deployment step exports both. `sendEmail` exists but is a single email, not a campaign. Nothing reads segment counts or sends a campaign.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listMarketingSegments` | Read | no | `ManageMarketing` | Lists customer segments with optional name search and paging. Use it to find a segment's id for a campaign. | MarketingAgent |
| `getMarketingSegment` | Read | no | `ManageMarketing` | Returns one segment with its rules and two counts: customers in the segment and customers who can be emailed (accept marketing). Use it to size an audience before sending. | MarketingAgent |
| `saveMarketingSegment` | Write | no | `ManageMarketing` | Creates or updates a customer segment from purchase rules: order count, amount spent in the store currency, days since last or first order, country code. Empty rules match every customer who has bought. | MarketingAgent |
| `deleteMarketingSegment` | Destructive | yes | `ManageMarketing` | Deletes a segment. Campaigns already sent to it are kept, but drafts that point to it can no longer be sent until another segment is chosen. | MarketingAgent |
| `listMarketingCampaigns` | Read | no | `ManageMarketing` | Lists email campaigns with optional name search and paging: status (Draft, Scheduled, Sending, Sent, Canceled), segment, send time, and sent, skipped and failed counts. | MarketingAgent |
| `getMarketingCampaign` | Read | no | `ManageMarketing` | Returns one campaign with its subject, Liquid body, segment, schedule, status and delivery counts. | MarketingAgent |
| `saveMarketingCampaign` | Write | no | `ManageMarketing` | Creates or updates a draft email campaign: name, segment, subject, Liquid body, optional send time in the site's time zone. Only a draft or scheduled campaign can be changed, and saving returns it to draft; this tool never sends. | MarketingAgent |
| `sendMarketingCampaignTest` | External | yes | `ManageMarketing` | Sends one test email of a campaign, rendered with a sample customer from its segment, to the address given. Use it to check the content before sending the campaign. | MarketingAgent |
| `sendMarketingCampaign` | External | yes | `ManageMarketing` | Starts sending a draft campaign to every reachable customer in its segment, or schedules it when its send time is in the future. Requires the site's email and base URL to be set up; emails go out in batches and cannot be recalled. | MarketingAgent |
| `cancelMarketingCampaign` | Destructive | yes | `ManageMarketing` | Cancels a scheduled or sending campaign and stops its outreach run. Emails already sent stay sent, and a canceled campaign cannot be restarted. | MarketingAgent |
| `deleteMarketingCampaign` | Destructive | yes | `ManageMarketing` | Deletes a campaign and its delivery counts. Refused while the campaign is sending; cancel it first. | MarketingAgent |

**Implementation notes:** `ISegmentStore` (`PageAsync`, `ListAllAsync`, `FindByIdAsync`, create/update/delete), `ICampaignStore` (`PageAsync`, `FindByIdAsync`, create/update/delete), `CampaignService.CountAsync(segment)`, `CanSendAsync()`, `SendTestAsync(campaign, to, unsubscribeUrl)`, `CancelAsync(campaign)`. Validation and the send/schedule decision live in the controllers (`SegmentsAdminController.Apply`, `CampaignsAdminController.ApplyAsync` and `AfterSaveAsync`) and must be extracted: "send" only sets `Status = Sending` (or `Scheduled`) and the background task then calls `CampaignService.StartSendingAsync`. The unsubscribe URL is built with `Url.RouteUrl(MarketingConstants.UnsubscribeRouteName, ...)` from the request; a tool must build it from the site base URL or `LinkGenerator`. The send time is entered in the site's time zone and converted with `ILocalClock`. The store currency for "spent" rules comes from `CheckoutSettings.Currency`. The agent's instructions should make it call `getMarketingSegment` (counts) and offer `sendMarketingCampaignTest` before `sendMarketingCampaign`. Consent is checked by the outreach engine per email, so "reachable" can shrink after the count.

**Test prompts:**
- "Create a segment of customers who haven't ordered in 90 days and have spent over 200 dollars."
- "How many people are in the Lapsed buyers segment and how many can we email?"
- "Draft a win-back campaign for lapsed buyers with a friendly subject line, then send me a test at me@example.com."
- "Send the Spring Sale campaign tomorrow at 9am."

## Bookings — `CrestApps.OrchardCore.Bookings`

**What an admin can do in the UI:**
- Commerce → Bookings (`BookingsAdminController`): list bookings with search, a status filter (`Requested`, `Confirmed`, `Completed`, `NoShow`, `Canceled`) and "from today" or "any date"; Detail (booking, travelers, the balance still owed, the cancellation quote: days before, fee percent, paid, refund); Complete or mark as a no-show (confirmed bookings only); Cancel with an optional refund amount and reason (refunds through the order, cancels the balance, releases the dates, emails the buyer); Charge balance now (charges the card the deposit was paid with; also retries a failed balance).
- Dates page per bookable product (`Dates`, reached from "Manage dates" on the product's `BookingPart` editor): add a date or time slot with capacity and an optional price; change a date's capacity (never below what is booked), price, or close it. There is no delete; a date is closed.
- Calendar (`Calendar`, month view across products): booked versus capacity per date.
- With Calendars on: "Sync" (`BookingCalendarController.Sync`) copies every booking to the code-managed Bookings calendar; the product editor gets a blocking calendar picker.
- On a product: `BookingPart` (kind Departure/Nightly/TimeSlot, capacity, price per person, occupancies, min/max nights, weekend price, opening hours, slot minutes, days ahead, deposit percent, balance due days, cancellation tiers, request to book, ask for travelers, blocking calendar).
- Workflow events: booking created, confirmed, rescheduled, canceled, completed, no-show, expired, payment received, payment failed, refunded.
- Background task "Booking holds and balances" (every 5 minutes) releases expired holds and charges due balances. Data sets: bookings, booking dates, booking balances.

**Already covered:** content tools cover the bookable product and its `BookingPart` (a content schema definition exists, so `getContentItemSchema` describes it; the nested occupancies, hours and cancellation tiers are easy to break, so always read the item first). Recipe step `BookingDates` (through `importOrchardCoreRecipe`) adds dates but skips existing ones and cannot change or close them; a deployment step exports dates. Calendar tools (CalendarAgent) read the synced Bookings calendar. A request-to-book booking is confirmed through its order's quote, which belongs to the Orders group. `listWorkflowActivities` covers the events.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listBookings` | Read | no | `ManageBookings` | Lists bookings with optional search, status, product, date range and paging; by default only bookings from today on. Returns title, date, people, customer, total, status and order number. | BookingsAgent |
| `getBooking` | Read | no | `ManageBookings` | Returns one booking with its travelers, the balance still owed and when it is due, and the cancellation quote (fee percent, amount paid, refund the policy gives). Use it before canceling or charging. | BookingsAgent |
| `completeBooking` | Write | no | `ManageBookings` | Marks a confirmed booking as completed, or as a no-show. Only a confirmed booking can be finished. | BookingsAgent |
| `cancelBooking` | External | yes | `ManageBookings` | Cancels a booking, refunds the buyer (the policy's amount unless another amount is given), cancels any balance still owed, frees the dates and emails the buyer. Cannot be undone. | BookingsAgent |
| `chargeBookingBalance` | External | yes | `ManageBookings` | Charges the balance still owed on a booking to the card its deposit was paid with, now, including after an earlier failed attempt. Fails when nothing is owed. | BookingsAgent |
| `listBookingDates` | Read | no | `ManageBookings` | Lists the dates or time slots of one bookable product, or of every product in a date range (the calendar view): capacity, held, sold, remaining, price override, closed. | BookingsAgent |
| `saveBookingDate` | Write | no | `ManageBookings` | Adds a date or time slot to a bookable product with a capacity and optional price, or changes an existing date's capacity, price or closed flag. Capacity cannot go below what is already booked; a date is closed, never deleted. | BookingsAgent |
| `syncBookingsCalendar` | Write | no | `ManageBookings` | Copies every booking to the Bookings calendar and returns how many events were added or updated. Needs the Calendars feature; use it when the calendar is missing bookings. | BookingsAgent |

**Implementation notes:** `IBookingStore.PageAsync(BookingQuery, utcNow, skip, take)` / `FindByIdAsync`; `IBookingBalanceStore.FindByOrderAsync(orderId)`; `BookingService.QuoteCancellationAsync`, `FinishAsync(booking, noShow, actorName)`, `CancelAsync(booking, refund, reason, actorName)`; `BookingBalanceCharger.ChargeAsync(balance)`; `IBookingSlotStore.ListAsync(contentItemId or null, fromUtc, toUtc)`, `FindAsync(contentItemId, slotKey)`, create/update; `BookingSchedule.GetStartUtc(key, timeZone)`; `BookingCapacityService.GetTimeZoneAsync()`. Logic that must be extracted from controllers: the add/update date rules (`DatesPost`, `UpdateDate`), resetting a failed balance before charging (`ChargeBalance`), the calendar month grouping (`Calendar`), and the sync loop (`BookingCalendarController.Sync`, which pages all bookings plus held ones, up to its `MaxPages`). `cancelBooking` moves money through the order refunder and sends email, and the refund is clamped to what was paid. Dates and slot keys are in the store's booking time zone (`yyyy-MM-dd` or `yyyy-MM-ddTHH:mm`). The customer name can be missing on a request and is filled from the order, as the detail action does. `syncBookingsCalendar` must only be registered when `CalendarsConstants.Features.Calendars` is on.

**Test prompts:**
- "What bookings do we have this weekend?"
- "Add a departure for the Coastal Hike on June 14 with 12 seats at 95 dollars."
- "Close July 4 for the kayak tour."
- "Cancel Dana Reed's booking and refund what the policy allows."

## Agentic Commerce — `CrestApps.OrchardCore.AgenticCommerce`

**What an admin can do in the UI:**
- Commerce → AI agents (`AgentPlatformsController`, route `commerce/ai-agents`): list and search agent platforms; create, edit, delete a platform (name, description, "its agents may buy" enabled flag, test mode, protocols UCP and/or ACP, OAuth client id, UCP profile URL, order updates webhook URL, allowed payment handlers, allowed IP ranges, embedded checkout origins, ACP event names, ACP signed-request requirement with algorithm, public key and clock skew); New API key (the old key stops working at once; the new key is shown once); New webhook secret (shown once).
- Settings → Commerce → Agentic commerce: checkout lifetime hours, max open checkouts per platform, allow unregistered UCP platforms, allow embedded checkout, Stripe business profile id, Google Pay (enable, merchant id, merchant name, test environment), delegated authentication (acquirer BIN, country, merchant id, merchant name, 3-D Secure requestor id).
- Background tasks: "Agent checkout expiry" (every 5 minutes), "Agentic commerce signing key rotation" (daily; no manual rotate action exists in the UI).

**Already covered:** five customer-facing AI tools, registered in `AIToolsStartup` (feature `CrestApps.OrchardCore.AgenticCommerce`, requires `CrestApps.OrchardCore.AI`, Product Listing and Product Feeds; category Commerce; selectable; all marked anonymous-safe, no approval): `searchStoreProducts`, `getStoreProduct`, `startStoreCheckout`, `addToStoreCart`, `getStoreCart`. They read the public catalog or act on the caller's own cart and checkout; none is an admin tool and none should be given to the admin agents below except for testing. Recipe step `AgentPlatforms` (through `importOrchardCoreRecipe`) creates or updates platforms but never imports API keys or webhook secrets; a deployment step exports platforms.

Missing on the admin side: reading and managing agent platforms, rotating their credentials, and (see the next section) feed status, feed push and per-product agent visibility.

**Settings groups:** `AgenticCommerceSettings` / group `agentic-commerce` / permission `ManageAgenticCommerceSettings` / secrets: none (merchant and acquirer identifiers are not secrets, but they are payment configuration).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listAgentPlatforms` | Read | no | `ManageAgentPlatforms` | Lists the AI agent platforms allowed to buy from the store, with optional name search and paging: enabled, test mode, protocols, whether an API key and a webhook secret are set. Never returns a key or secret. | AgenticCommerceAgent |
| `getAgentPlatform` | Read | no | `ManageAgentPlatforms` | Returns one agent platform with all its settings: protocols, OAuth client, profile and webhook addresses, allowed payment handlers, IP ranges and origins, and its ACP signing settings. Secrets are shown only as "set" plus the API key hint. | AgenticCommerceAgent |
| `saveAgentPlatform` | Security | yes | `ManageAgentPlatforms` | Creates or updates an AI agent platform: whether its agents may buy, test mode, protocols, addresses, allowed payment handlers, IP ranges, embedded checkout origins and ACP signing settings. A new platform cannot sign in until it gets an API key or an OAuth client. | AgenticCommerceAgent |
| `deleteAgentPlatform` | Destructive | yes | `ManageAgentPlatforms` | Removes an AI agent platform; its agents can no longer buy from the store. To stop a platform temporarily, set it disabled with `saveAgentPlatform` instead. | AgenticCommerceAgent |
| `regenerateAgentPlatformSecret` | Security | yes | `ManageAgentPlatforms` | Issues a new API key or a new webhook signing secret for a platform (`secret: apiKey` or `webhookSecret`). The old API key stops working at once and the new value is shown only once, so the admin must copy it to the platform immediately. | AgenticCommerceAgent |

**Implementation notes:** `IAgentPlatformManager` (`ICatalogManager<AgentPlatform>`) for list, find, create, update and delete, with validation in `AgentPlatformHandler` (name required, at least one protocol, https URLs, origins, signature algorithm and P-256 public key); the exact manager method names used by `CatalogControllerBase` were not read (unverified). Field mapping and list splitting live in `AgentPlatformDisplayDriver.UpdateAsync`; the key and secret generation lives in `AgentPlatformsController.ApiKey` / `WebhookSecret` (`AgentPlatformApiKeys.Create/Hash/Hint`, data protector purpose `AgenticCommerceStorage.SecretProtectionPurpose`) and must be extracted. Never return `ApiKeyHash` or `ProtectedWebhookSecret`. Risk: `regenerateAgentPlatformSecret` has to hand a live credential back; returned as a tool result it lands in the chat transcript and at the model provider. See Open questions for the alternative (rotate, then deliver the value outside the model).

**Test prompts:**
- "Which AI agent platforms can buy from our store?"
- "Add ChatGPT as an agent platform using ACP, in test mode, order updates to https://example.com/acp/webhook."
- "Turn off buying for the Gemini platform for now."
- "Rotate the API key for the ChatGPT platform."

## Agent Product Feeds — `CrestApps.OrchardCore.AgenticCommerce.Feeds`

**What an admin can do in the UI:**
- Settings → Commerce → Agent feeds (`AgentFeedSettingsDisplayDriver`): target countries; daily SFTP push (enable, host, port, user name, password or private key with passphrase, host key fingerprint, remote folder, format, currency); ACP feed API targets (name, base URL, bearer token, feed id, target country, currency, enabled); the page also shows the last SFTP push and the last push per feed API target (time, success, message).
- "Push now" button (`AgentFeedsAdminController.Push`): pushes the feed over SFTP and to every enabled feed API target with the saved settings.
- Public feed files at `feeds/openai/products.{jsonl,csv}[.gz]` (protected by the product feeds key, owned by Products).
- Per product: "show to AI agents" and "agents may check out" on `ProductFeedPart` (Products module, Product Feeds feature), with site defaults `ShowToAgentsByDefault` / `AgentCheckoutByDefault` in the `product-feeds` settings group (`ManageProductFeeds`, Products group).
- Background task "Agent product feeds push" (daily at 02:30); products are also pushed to feed APIs after publish or a stock change.

**Already covered:** content tools can set `ProductFeedPart.ShowToAgents` / `.AgentCheckout` on one product at a time. The generic settings tools will cover the `agent-feeds` and `product-feeds` groups. Nothing reads the push status or triggers a push.

**Settings groups:** `AgentFeedSettings` / group `agent-feeds` / permission `ManageAgentFeeds` / secrets: `Sftp.Password`, `Sftp.PrivateKey`, `Sftp.PrivateKeyPassphrase`, `FeedApiTargets[].BearerToken` (all data-protected; the editor shows only whether each is set and has explicit "remove" flags for the SFTP ones).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `getAgentFeedStatus` | Read | no | `ManageAgentFeeds` | Returns the state of the AI agent product feed: number of products in it, products left out and why, the last SFTP push and the last push to each feed API target with its result message. Use it to answer "are our products reaching ChatGPT?". | AgenticCommerceAgent |
| `pushAgentFeeds` | External | yes | `ManageAgentFeeds` | Pushes the product feed now, over SFTP and to every enabled ACP feed API, using the saved settings, and returns each result. Use it after fixing products or settings instead of waiting for the nightly push. | AgenticCommerceAgent |
| `setProductAgentVisibility` | Write | no | `EditContent` on each product (unverified: the part driver adds no check of its own) | Sets, for one or more products, whether AI agents may show them to buyers and whether agents may check them out, or clears the choice so the site default applies. Use it for bulk changes; for a single product the content tools work too. | AgenticCommerceAgent |

**Implementation notes:** status comes from `IDocumentManager<AgentFeedStatusDocument>` (`LastSftpPush`, `FeedApiPushes`, `FeedApiFeedIds`), the same document the settings driver and both pushers use, and from `OpenAIProductFeedService.GetAsync(currency)` (`Records`, `Problems` with `LeftOut`, `BuiltUtc`). Push uses `AgentFeedSftpPusher.PushAsync()` and `AcpFeedPusher.HasTargetsAsync()` / `PushAllAsync()`; the orchestration and the "nothing to push" case live in `AgentFeedsAdminController.Push` and should move to a service. The push connects to external servers with stored credentials and can take a while for large catalogs. `setProductAgentVisibility` edits `ProductFeedPart` (`bool?` values, `AgentCheckout` is ignored when the product is hidden from agents) and belongs in the Products module's own tool startup, because the part lives there; it is listed here because agent visibility is this group's use case. Check the content permission per item.

**Test prompts:**
- "When did we last push the product feed to OpenAI, and did it work?"
- "Which products are left out of the agent feed and why?"
- "Push the agent feed now."
- "Hide all gift cards from AI agents."

## No tools needed

| Module | Reason | Settings group (if any) |
| --- | --- | --- |
| `CrestApps.OrchardCore.Marketplace.StripeConnect` | Payout provider only. No admin page or settings of its own: vendors onboard themselves from `account/vendor`, and the admin vendor page refreshes the Stripe status. Covered by `getVendor` and `payOutVendor`. | `marketplace` (the payout provider is chosen there) |
| `CrestApps.OrchardCore.AgenticCommerce.Ucp` | Protocol endpoints (`/.well-known/ucp`, REST checkout and orders, webhooks). No admin UI; configured through agent platforms and the `agentic-commerce` settings. | `agentic-commerce` |
| `CrestApps.OrchardCore.AgenticCommerce.Ucp.IdentityLinking` | Endpoints only; the OAuth client is managed in OpenID (another group). | none |
| `CrestApps.OrchardCore.AgenticCommerce.Ucp.Catalog` | Endpoints only (catalog search and cart for UCP agents). | none |
| `CrestApps.OrchardCore.AgenticCommerce.Ucp.Mcp` | Serves UCP as MCP tools at `ucp/mcp` for outside agents. No admin UI. | none |
| `CrestApps.OrchardCore.AgenticCommerce.Ucp.A2a` | Serves UCP through an A2A agent. No admin UI. | none |
| `CrestApps.OrchardCore.AgenticCommerce.Acp` | Protocol endpoints and order webhooks. Per-platform ACP fields are on the agent platform. | `agentic-commerce` |
| `CrestApps.OrchardCore.AgenticCommerce.Acp.Mcp` | Serves ACP as MCP tools at `acp/mcp`. No admin UI. | none |
| `CrestApps.OrchardCore.AgenticCommerce.Ap2` | Preview mandates extension of UCP. No admin UI. | none |
| Customer-side pages in every module (`account/subscriptions`, `account/memberships`, `account/vendor`, `account/listings`, `account/donations`, `account/downloads`, My Plans, My Sites, storefront review form, unsubscribe page) | Acts for the signed-in customer, not for an administrator; the existing storefront tools are the customer surface. | none |
| Reports and data sets contributed by these modules (subscription, download and review reports; Data Pipelines data sets for subscriptions, installment plans, vendors, earnings, payouts, bookings, slots, balances, downloads, reviews) | They plug into the Reports and Data Pipelines modules; a generic "run report" / "query data set" tool from that group covers them. No per-module report tools. | none |
| Background tasks of these modules (Subscription Lifecycle, Installment Plans, Tenant Provisioning, Membership end dates, Marketplace releases and payouts, Listing terms, Review requests, Marketing, Booking holds and balances, Agent checkout expiry, signing key rotation, Agent product feeds push) | Run through Orchard Core's Background Tasks admin ("Run now"); a generic background-task tool from the Orchard Core group covers them. | none |

## Proposed agents for this group

| Agent | Purpose (one sentence) | Tools | Could merge with |
| --- | --- | --- | --- |
| SubscriptionsAgent | Manages recurring revenue and paid access: subscription agreements, sold-site provisioning jobs, installment plans and memberships. | `listSubscriptions`, `getSubscription`, `cancelSubscription`, `setSubscriptionPaused`, `listSiteProvisioningJobs`, `resolveSiteProvisioningJob`, `listInstallmentPlans`, `getInstallmentPlan`, `createInstallmentPlan`, `chargeInstallmentPayment`, `cancelInstallmentPlan`, `listMemberships`, `endMembership` (13) | A payments/transactions agent from the commerce core group (installment plans are ledger transactions); tenant tools for the provisioning part |
| MarketplaceAgent | Runs third-party selling and customer-generated content: vendors with their earnings and payouts, moderation of posted listings and of product reviews. | `listVendors`, `getVendor`, `saveVendor`, `releaseVendorEarnings`, `payOutVendor`, `listClassifieds`, `approveClassified`, `rejectClassified`, `listProductReviews`, `moderateProductReview`, `deleteProductReview` (11) | The products/catalog agent (reviews are about products); ContentAgent for listing moderation |
| MarketingAgent | Builds customer segments and writes, tests, sends and cancels email campaigns. | `listMarketingSegments`, `getMarketingSegment`, `saveMarketingSegment`, `deleteMarketingSegment`, `listMarketingCampaigns`, `getMarketingCampaign`, `saveMarketingCampaign`, `sendMarketingCampaignTest`, `sendMarketingCampaign`, `cancelMarketingCampaign`, `deleteMarketingCampaign` (11) | The Checkout group's discounts, coupons and abandoned-cart tools; an Outreach/Omnichannel campaigns agent; a customers agent |
| BookingsAgent | Manages bookings and the dates and capacity of bookable products, including cancellations with refunds and balance charges. | `listBookings`, `getBooking`, `completeBooking`, `cancelBooking`, `chargeBookingBalance`, `listBookingDates`, `saveBookingDate`, `syncBookingsCalendar` (8) | CalendarAgent or a Scheduling agent; an orders agent |
| AgenticCommerceAgent | Controls which AI agent platforms may buy from the store, their credentials, and how products reach them through feeds and visibility. | `listAgentPlatforms`, `getAgentPlatform`, `saveAgentPlatform`, `deleteAgentPlatform`, `regenerateAgentPlatformSecret`, `getAgentFeedStatus`, `pushAgentFeeds`, `setProductAgentVisibility` (8) | A product feeds / SEO / NLWeb "discoverability" agent; a webhooks or API-clients agent |
| OrdersAgent (owned by the commerce core group, name to be confirmed) | Receives the three order-side tools of this group. | `listOrderDownloads`, `setOrderDownloadAccess`, `getFundraisingCampaignProgress` (3) | If no orders agent exists, put the two download tools in MarketplaceAgent and keep the fundraising read direct |

Total: 54 new tools across 10 modules (12 feature sections); 5 existing customer-facing tools left unchanged.

## Open questions

- The Marketing module contains only purchase-based segments and email campaigns. Discounts, coupons and abandoned carts are implemented in `CrestApps.OrchardCore.Checkout`, so they must be inventoried by the group that owns Checkout. Should MarketingAgent own those tools anyway, since an admin thinks of them as marketing?
- `regenerateAgentPlatformSecret` returns a live credential. Should the value be returned in the tool result (it then sits in the chat history and at the model provider), or should the tool rotate the secret and deliver it outside the model (a one-time admin notification or a link to the platform's edit page)? The same question applies to any other "regenerate key" tool in the plan.
- `createInstallmentPlan`: the first payment needs a card entered in the browser, so the tool can only create the draft and return the Pay link. Is that acceptable, and should the tool be allowed to create a new customer account (the service supports it and creates a passwordless user), which would make it a Security tool?
- Approval for reversible changes: `setOrderDownloadAccess` (revoke is undone by reset) and `cancelMarketingCampaign` (stopping a mailing is the safe direction) are marked approval-required by the rules. Relax them?
- `releaseVendorEarnings` is classed External because released money cannot be re-held and is paid out automatically when automatic payouts are on. Confirm, or treat it as a plain write.
- `saveVendor` is classed Security because it assigns the user accounts that control a vendor's payout account. If that is too strict, split the user assignment into its own tool and make the rest a write.
- Fundraising has no admin list of donations (only the donor's account pages and the orders list). `getFundraisingCampaignProgress` goes beyond UI parity. Keep it, or leave donations to the orders tools and reports?
- Digital file upload: a tool cannot upload large binaries. Is the `DigitalFiles` recipe step (Base64, up to 10 MB) enough, or should a tool attach a file that is already in the media library to a product (a new server-side copy that the UI does not offer today)?
- `importOrchardCoreRecipe` can run the `Memberships` step, which grants roles, and the `Vendors` and `AgentPlatforms` steps. Does that tool's approval and permission model account for steps that change access?
- `setProductAgentVisibility` edits a part owned by the Products module. Which group registers it, and should it check `EditContent` per product or a feed permission (`ManageProductFeeds`)?
- Vendors and booking dates cannot be deleted in the UI (suspend and close instead), and memberships cannot be granted by hand. The tools mirror that. Is deletion or manual granting wanted as a new capability?
- A lot of the logic these tools need sits inside controllers and has to be extracted into services first: site provisioning retry and mark-created, vendor validation, campaign and segment validation and the send/schedule decision, booking date add and update, booking balance retry, bookings calendar sync, agent feed push orchestration, and agent platform key and secret generation. Is that refactoring in scope for the plan?
