---
sidebar_label: Customers
title: Customers
description: A customer record for everyone who buys from the store, linked to their Orchard Core user when they have an account, with store credit, loyalty points and referrals.
user_manual:
  - user-manual/commerce/customers
  - user-manual/commerce/store-credit-and-rewards
  - user-manual/commerce/headless-api
---

| | |
| --- | --- |
| **Features** | `CrestApps.OrchardCore.Customers`, `CrestApps.OrchardCore.Customers.CheckoutDetails` (Checkout details), `CrestApps.OrchardCore.Customers.StoreCredit` (Store Credit), `CrestApps.OrchardCore.Customers.Loyalty` (Loyalty Points), `CrestApps.OrchardCore.Customers.Referrals` (Referrals), `CrestApps.OrchardCore.Customers.Api` (Customers API) |
| **Core** | `CrestApps.OrchardCore.Customers.Core` |
| **Category** | Commerce |
| **Depends on** | Commerce, `OrchardCore.Users` |

The **Customers** feature keeps a record of everyone who buys from the store: a person or a business, with an account or as a guest. Orders, transactions and subscriptions can point at one customer, whatever later happens to the account.

<YouTube video="customers-companies" title="Narrated video overview of the Customers and Companies modules" />

## Customers and users

A customer is not a user. Accounts stay with Orchard Core Users, which handles registration, sign-in, password resets, email confirmation and external logins. A customer with an account is linked to its user by `Customer.UserId`. The customer record keeps only what the store needs: how to address the customer, their company and tax id, and how they want to be contacted.

A guest customer has no user. Customers builds no registration of its own; a guest who wants an account registers through Orchard Core.

## The customer record

`Customer` is a `CatalogItem` stored in its own YesSql collection (`Customers`), so it carries the usual extensible properties for other modules.

| Property | |
| --- | --- |
| `Kind` | `Person` or `Business`. |
| `Status` | `Active`, `Disabled` (the store stopped doing business with them; their history stays) or `Anonymized` (their personal data was erased). |
| `UserId` | The Orchard Core user, or `null` for a guest. |
| `DisplayName`, `FirstName`, `LastName` | How the store addresses the customer. `GetName()` returns the display name, then a business's company, then the person's name, then the email. |
| `CompanyName`, `TaxId` | For a business, such as a VAT number. |
| `IsTaxExempt`, `TaxExemptionReason` | A customer who pays no tax, such as a reseller, and why, such as a certificate number. A signed-in exempt customer pays no tax at checkout; see [Taxation at checkout](checkout#taxation). |
| `Email`, `Phone` | For a customer with an account, `Email` is the account's email when the record was made. |
| `AcceptsMarketing` | Whether the customer agreed to news and offers. Order and payment messages are always sent. |
| `ExternalIds` | Ids at outside services, such as a payment provider's customer id, by service name. |
| `CreatedUtc`, `ModifiedUtc`, `LastActivityUtc` | `LastActivityUtc` is when the customer last bought or changed something. |
| `Addresses`, `DefaultBillingAddressId`, `DefaultShippingAddressId` | The address book, below. |

`CustomerIndex` indexes the user id, the email, name and company in upper case for searching, the kind, the status, whether the customer has an account, and the dates.

## Address book

A customer keeps up to 20 `CustomerAddress` entries on the record itself. Each has an `AddressId`, a `Label` (such as *Home*) and an `Address`: the shared, immutable value from the [Addresses](addresses) module, so a customer's address is the same type an order, a tax rule or a content field holds.

- `GetDefaultBillingAddress()` and `GetDefaultShippingAddress()` return the defaults, or `null`. `RemoveAddress(addressId)` also clears a default that named the address.
- `ToAddress()` returns a copy of the `Address`. An order stores that copy, so editing or deleting an address never changes a past order.

The address pages use the Addresses module's editor (`IAddressEditorService` and the `AddressEditor` shape, posted under the `Address` prefix), so they lay the form out for the chosen country and check the address against its rules: the parts the country requires, its postal code pattern, a region from its list, and a postal code within that region. The address book also requires the recipient's name and offers a phone, and a new address starts in the country of the customer's other addresses. Addresses are listed and printed through `IAddressFormatter`, the way each country writes them. Customers depends on the Addresses feature for all of this.

## Services

- **`ICustomerStore`** finds customers by id or user id and pages them with a `CustomerQuery`. A search matches the start of the name, company or email, ignoring case. Saving checks concurrency, so two editors never overwrite each other silently.
- **`ICustomerService.GetOrCreateForUserAsync(userId)`** returns a user's customer, making it from the account the first time. A distributed lock keyed by the user makes two requests from the same user share one customer. `TouchAsync` records activity.
- **`ICustomerOwnerNotifier`** (`DefaultCustomerOwnerNotifier`, registered by `AddCustomersCore()`) sends a buyer a receipt, reminder or notice: a user through `INotificationService`, a guest by email at the contact `ICustomerContactResolver` resolves.
- **`ICustomerContactResolver`** is replaced by `CustomerRecordContactResolver`. A customer with an account is addressed by the name on their customer record and the account's email, so statements, receipts and reminders read the same everywhere. A guest is still addressed from the contact kept at purchase.

## Orders

With Customers enabled, an order placed by a signed-in user belongs to their customer: `Order.CustomerId` is set at checkout, and the customer's record is made on their first order. Orders keep their own copy of the customer's name and email, so an order reads the same after the customer changes.

## The checkout's Contact step

With Orders enabled, Customers adds a **Contact** step to every order's checkout (`CustomersConstants.ContactStepKey`, right after the hidden order step and before payment). `CustomerContactCheckoutHandler` adds it, and `CustomerContactCheckoutFlowDisplayDriver` renders it as the `CustomerContactStep_Edit` shape.

- **What it asks:** a name and an email (required), a phone, and a billing address as `CustomerCheckoutSettings.BillingAddress` says: `Optional` (the default), `Required` or `Off`, under **Settings > Commerce > Customers** with the **Manage customers** permission. The checkout keeps the setting it started with, stored on the session, so changing it never strands a checkout. A signed-in buyer never sees the step when the setting is `Off`.
- **The billing address** uses the Addresses module's `AddressEditor` under the step's prefix, without a recipient name (the buyer named above is the recipient) and with the buyer's phone. A signed-in customer can pick one of their saved addresses and ask for a new one to be saved.
- **What it keeps:** the entries as `CustomerContactStep` in the session's saved steps, and a `CheckoutContactInfo` (name, email, phone, billing address) on the session. The order copies that contact and billing address when it is paid, and Pay Later puts the guest's name and email on its transaction, so receipts and reminders reach them.
- **When the order is paid** (`CompletingAsync`, inside the completion's transaction): the order is linked to the signed-in buyer's customer, or to a new guest customer made with `ICustomerService.CreateGuestAsync`; each guest checkout makes its own, since an email typed at checkout proves nothing. A guest's billing address goes into their address book as the default billing address; a signed-in customer's new address goes in when they asked, never twice.

## Checkout details

The **Checkout details** feature (`CrestApps.OrchardCore.Customers.CheckoutDetails`, depends on Customers, Orders, Contents and Content Types) creates a `CheckoutDetails` content type with no fields. Its fields are the store's own checkout fields:

- `CheckoutDetailsCheckoutFlowDisplayDriver` renders the type's editor on the **Contact** step with the `CheckoutDetails` prefix, through `IContentItemDisplayManager`, so every field validates as it does in the admin. A valid post stores the item's parts on `Order.CheckoutDetails` (a `JsonObject`).
- `CheckoutDetailsCheckoutHandler` keeps the Contact step visible (`Conceal = false`) when the type has fields, after every handler has initialized the checkout.
- `OrderCheckoutDetailsDisplayDriver` shows the values, each with its field's name, on the order's admin and customer pages (`OrderConstants.DisplayTypes.DetailAdmin` and `Detail`).
- `Order.Anonymize` removes them.

## Storefront account pages

`account/details` (`AccountDetails`) and `account/addresses` (`AccountAddresses`, with `.../add`, `.../{addressId}` and `.../{addressId}/delete`) edit the signed-in buyer's customer record and address book on the storefront, through `CustomerAccountPages`, the same validation as **My Account**. With their features on, `account/store-credit`, `account/rewards` and `account/refer` show the buyer's [store credit](#store-credit), [points](#loyalty-points) and [referral link](#referrals); each is in the account menu with **Manage own customer profile and addresses**.

## Sections on the customer's page

The customer's admin page (`CustomersAdminController.Edit`) builds the customer's `DetailAdmin` display through `IDisplayManager<Customer>` and renders it below the orders, as `CustomerEditViewModel.Sections`. A module adds a section with a `DisplayDriver<Customer>` that answers only `context.DisplayType == "DetailAdmin"` and places its shape in the `Content` zone; the `Customer.DetailAdmin` template renders that zone. Store credit (`Content:5`) and loyalty points (`Content:6`) use it.

## Store credit

The **Store Credit** feature (`CrestApps.OrchardCore.Customers.StoreCredit`, depends on Customers and Orders) keeps money the store owes a customer, given by hand or as a refund, and lets it pay toward the signed-in customer's checkouts as a [tender](checkout#balances-that-pay-after-tax), after tax.

- **The account.** A `StoreCreditAccount` (a `CatalogItem` in the `Customers` collection, `StoreCreditAccountIndex` on customer, currency and balance, `StoreCreditMigrations`) holds one customer's `Balance` in one `Currency`, with `Activity` (`StoreCreditActivity`: `CreatedUtc`, `Amount`, `Balance` after, `Reason`, `SessionId`, `Actor`). `ChangeBalance` never goes below zero and always writes the activity. `IStoreCreditStore` (`StoreCreditStore`, with concurrency checks) adds `FindAsync(customerId, currency)` and `ListByCustomerAsync`.
- **Changing it.** `StoreCreditService.ChangeAsync(customerId, currency, amount, reason, actor, sessionId)` makes the account on a first credit, and otherwise changes it under `CheckoutTenderLocks.GetKey(StoreCreditService.TenderKey, accountId)`, the lock the checkout's ledger takes. It returns `null` when the lock is busy, or when there's nothing to take credit from.
- **Paying with it.** `StoreCreditTenderProvider` is the `ICheckoutTenderProvider` with the key `store-credit`. Store credit belongs to an account, so `FindForCheckoutAsync` resolves it only from the session's `OwnerId`, through the user's customer, in the invoice's currency, and its lines pay only from the buyer's own account, whatever the selection names. The line's description is *Store credit*. `StoreCreditCheckoutFlowDisplayDriver` renders `CheckoutStoreCredit` at `Aside:7` for a signed-in buyer with credit, on a checkout that doesn't settle an existing balance or save a payment method. Its button posts `apply-tender:store-credit` (`StoreCreditCheckoutFlowDisplayDriver.ApplyAction`), and **Remove** posts `remove-tender:store-credit:` and the account id.
- **Refunds as credit.** The provider is also the `IOrderStoreCreditRefunder` for the [Orders](carts-and-orders#managing-an-order) feature: `CanRefundAsync` is true when the order's `UserId` has a customer, and `RefundAsync` adds the amount in the order's currency with the reason *Refund of order …* and the order's checkout session id.
- **Admin.** `CustomerStoreCreditDisplayDriver` adds the `CustomerStoreCredit_DetailAdmin` section: the balances, the latest 20 changes, and for **Manage customers** a form that posts to `StoreCreditAdminController.Change` (`Admin/customers/{customerId}/store-credit`, with `amount`, `currency` and a required `reason`). A guest customer can't be given credit.
- **Storefront.** `account/store-credit` (`AccountStoreCredit`, `StoreCreditAccountController`) shows the signed-in customer's credit and the latest 50 changes, and `StoreCreditAccountMenu` adds **Store credit** to the account menu.

## Loyalty points

The **Loyalty Points** feature (`CrestApps.OrchardCore.Customers.Loyalty`, depends on Customers and Orders) gives a signed-in customer points for what they buy, and lets them spend the points toward later checkouts as a tender.

- **The account.** A `LoyaltyAccount` (in the `Customers` collection, `LoyaltyAccountIndex` on the customer, `LoyaltyMigrations`) holds whole `Points` and `Activity` (`LoyaltyActivity`: `Points` changed, `Balance`, `Kind`, `Reason`, `OrderId`, `SessionId`, `Actor`). `LoyaltyActivityKind` is `Earned`, `Reversed`, `Spent`, `Returned` or `Adjusted`. `ChangePoints` never goes below zero and records the change that actually happened. `GetEarnedFor(orderId)` is what an order earned and hasn't had taken back.
- **Settings.** `LoyaltySettings` (**Settings > Commerce > Rewards**, group `loyalty`, `CustomersConstants.LoyaltySettingsGroupId`, permission **Manage customers**): `ProgramName` (blank shows *Rewards*), `PointsPerUnit` (1, 0 or more), `PointValue` (0.01, more than zero) and `MinimumPointsToRedeem` (100, 1 or more). `LoyaltyService.GetSettingsAsync` fills in the name.
- **Earning.** `LoyaltyOrderEventHandler.PaidAsync` gives the order's signed-in buyer `LoyaltyService.GetPointsFor(order, settings)`, once per order: the floor of `PointsPerUnit` times the order's `Subtotal`, less its discounts whose `OrderDiscount.Target` isn't `DiscountTarget.Shipping`, less what points paid (`Order.Tenders` with the key `loyalty-points`). Shipping and tax earn nothing. It adds an order note.
- **Taking back.** `CanceledAsync` takes back everything the order still has earned. `RefundedAsync` takes back the share of the points the order earned that the refund is of the order's total, rounded, never more than it still has earned. Both are `Reversed` changes.
- **Spending.** `LoyaltyTenderProvider` is the `ICheckoutTenderProvider` with the key `loyalty-points`, for the session owner's customer only, described by the program's name. It offers nothing below `MinimumPointsToRedeem`. A balance is worth `LoyaltyService.GetValue(points, settings, currency)`, the points times `PointValue` to the cent below. `DebitAsync` spends `GetPointsWorth(amount)`, the points that pay the amount rounded up, never more than the customer has; `CreditAsync` gives back the points an amount is worth, as `Returned`. `LoyaltyCheckoutFlowDisplayDriver` renders `CheckoutLoyalty` at `Aside:8`, posting `apply-tender:loyalty-points` and `remove-tender:loyalty-points:` and the account id.
- **Changing points by hand.** `LoyaltyService.ChangeAsync(customerId, points, kind, reason, actor, orderId)` works like store credit, under `CheckoutTenderLocks.GetKey(LoyaltyService.TenderKey, accountId)`. `CustomerLoyaltyDisplayDriver` adds `CustomerLoyalty_DetailAdmin`, with a form that posts to `LoyaltyAdminController.Change` (`Admin/customers/{customerId}/loyalty`, `points` and a required `reason`, as `Adjusted`).
- **Storefront.** `account/rewards` (`AccountRewards`, `LoyaltyAccountController`) shows the points, their value, how they're earned and spent, and the latest 50 changes. `LoyaltyAccountMenu` adds **Rewards** to the account menu, and `LoyaltySettingsAdminMenu` the settings page.

## Referrals

The **Referrals** feature (`CrestApps.OrchardCore.Customers.Referrals`, depends on Customers and Orders) gives each signed-in customer a referral link. A friend who opens it can get a single-use discount, and the customer is rewarded with store credit or points for the friend's first order.

- **The profile.** A `ReferralProfile` (in the `Customers` collection, `ReferralProfileIndex` on customer and code, `ReferralMigrations`) holds the customer's `Code` and `Referrals`: one `ReferralRecord` per rewarded friend (`FriendEmail` in lower case, `FriendName`, the friend's first name, `OrderId`, `OrderNumber`, `Reward`, the reward's description or `null`, and `RewardKind`, `RewardAmount`, `RewardCurrency` and `RewardPoints`, what was given, which a canceled order takes back whatever the settings say by then, `CreatedUtc`). `ReferralService.GetOrCreateAsync(customerId)` makes it the first time the customer's account page is opened, with an 8-character code from the alphabet without 0, O, 1 or I.
- **Settings.** `ReferralSettings` (**Settings > Commerce > Referrals**, group `referrals`, permission **Manage customers**): `FriendCouponId` (a coupon that isn't automatic, or `null`), `RewardKind` (`None`, `StoreCredit` or `Points`), `RewardAmount` (10), `RewardPoints` (500) and `MinimumOrderAmount` (0).
- **The link.** `GET refer/{code}` (`ReferralController`) stores the code in the `orchard-referral` cookie (`ReferralService.CookieName`; HTTP-only, essential, the tenant's path, 30 days) and redirects to the home page with a message. When the store offers a coupon that is redeemable now, and this browser hasn't opened this link before, it generates one single-use code of it with the prefix `REF-` (`ICouponStore.GenerateCodesAsync(coupon, 1, "REF-", usesPerCode: 1)`) and stores it in the [discount link](checkout#discount-links-and-generated-codes) cookie (`CheckoutConstants.DiscountLinkCookieName`) for 30 days, so it is applied to the friend's checkouts. Generating codes is rate limited per address (`IPaymentAttemptLimiter`, scope `referral-link`). A code that matches no profile says the link isn't valid; a signed-in customer opening their own link is told so, and gets no code.
- **Remembering it.** `ReferralCheckoutHandler.ActivatingAsync` puts the cookie's code on each new checkout as a `CheckoutReferral`, except one that settles an existing balance.
- **The reward.** `ReferralOrderEventHandler.PaidAsync` reads the paid order's checkout session. It rewards the profile's customer once, and only when the order has an email; the friend isn't the customer (by email or user); no record of the profile names the friend's email; the friend has no other paid order (by `UserId`, or for a guest by email, through `IOrderStore.PageAsync`); and the order's `Subtotal`, less its discounts that didn't come off shipping, reaches `MinimumOrderAmount`. The reward is `RewardAmount` of store credit in the order's currency through `StoreCreditService`, or `RewardPoints` through `LoyaltyService` (as `Adjusted`), when the referrer has an account and that feature is on; the record is kept either way, and an order note names the referrer and the reward.
- **Taking it back.** `CanceledAsync` removes the friend's record, so they can be referred again, and takes back `RewardAmount` or `RewardPoints` for the current `RewardKind` when a reward was given. A refund doesn't.
- **Storefront.** `account/refer` (`AccountReferrals`, `ReferralAccountController`) shows the link (`{scheme}://{host}{pathBase}/refer/{code}`), what a friend gets (the coupon's description), what the customer gets, and the friends rewarded. `ReferralAccountMenu` adds **Refer a friend** to the account menu, and `ReferralSettingsAdminMenu` the settings page.

## Merging guests into an account

`ICustomerMergeService.MergeVerifiedGuestsAsync(userId)` joins a guest's history to their account. It runs when a user signs in (`ILoginFormEvent.LoggedInAsync`), registers (`IRegistrationFormEvents.RegisteredAsync`) or confirms their email (`IUserEventHandler.ConfirmedAsync`), and does nothing unless the user's `EmailConfirmed` is true. Under a per-user distributed lock it takes every active guest customer with the user's email (`ICustomerStore.ListGuestsByEmailAsync`) and, for each:

- calls every `ICustomerMergeHandler`. With Orders on, `OrderCustomerMergeHandler` moves the guest's orders to the account's customer (setting `UserId` too, so they appear under My Orders) and, with Transactions on, turns the guest transactions paying for them into the user's;
- moves the guest's addresses into the account's book, skipping duplicates, up to 20;
- marks the guest `Merged` with `MergedIntoCustomerId` and `MergedUtc`, and adds it to the account customer's `MergedCustomerIds`.

The event handlers resolve the merge service when they run: Orchard Core's user store and user service create them, and the merge service needs the user service, so injecting it would be a circular dependency. A failed merge is logged and tried again on the next sign-in.

## Privacy

`ICustomerPrivacyService` answers access and erasure requests and applies the retention windows.

- **`EraseAsync(customer, by)`** calls every `ICustomerPrivacyHandler.ErasingAsync`, then `Customer.Anonymize`: names, company, tax id, email, phone, addresses, outside ids and marketing consent are cleared, the account is unlinked (`UserId`), and the status becomes `Anonymized`. With Orders on, `OrderCustomerPrivacyHandler` anonymizes the customer's orders (by `CustomerId`, and by `UserId` for an account holder's older orders) through `OrderAnonymizer`: `Order.Anonymize` clears the name, email and phone and keeps only the billing address's country and region, and the guest contact of the transactions paying for the order is cleared. Every checkout session behind the order (the one it names and any earlier attempt for it) and behind those transactions is anonymized through `ICheckoutSessionAnonymizer`, which clears the owner, contact, IP address, user agent and saved steps; see [Checkout](checkout). Handlers see the account before it is unlinked (`CustomerPrivacyContext.UserId`), and run by `ICustomerPrivacyHandler.Order` (lower first, then in registration order). Erasing an anonymized customer does nothing.
- **`ExportAsync(customer, by)`** returns a `JsonObject` with the customer and their addresses; each handler's `ExportingAsync` adds its own section (`orders`, each with its `transactions`).
- **`CommercePersonalDataCustomerPrivacyHandler`** covers what the other commerce modules keep, through `ICommercePersonalDataService`: the customer is identified by their account (when they have one) and their email. Erasing anonymizes their checkout sessions, transactions, subscriptions, installment plans, coupon redemptions and Stripe customers, clearing names the same way the customer record is cleared; exporting adds a `commerce` section with those records and the payments and refunds of their checkouts. It runs before the other handlers (`Order` is `-100`), because they erase the checkout emails and owners it follows to the customer's guest transactions, subscriptions and plans. See [Personal data requests](commerce#personal-data-requests).
- **`ExpireAsync(customer)`** erases as the retention task.

Each export and erasure appends a `CustomerPrivacyEntry` (`Utc`, `Action`: `Exported`, `Erased` or `Expired`, `By`) to `Customer.PrivacyLog`.

**Retention.** `CustomerRetentionBackgroundTask` runs daily at 03:30 under the background-task lock and reads `CustomerPrivacySettings` (Settings › Commerce › Customers; zero turns a window off):

- `InactiveGuestMonths` (24): guests (`HasAccount = false`, merged ones included) whose `LastActivityUtc` is older are expired. Customers with an account are not, since the account holds the same data; erase them on request.
- `GuestOrderMonths` (12): orders placed without an account (`OrderQuery.GuestsOnly`), `Paid` or `Canceled`, created before the window, are anonymized, with the checkout sessions behind them. Orders awaiting payment keep their contact.

Each run handles up to 500 of each, and queries leave out what is already anonymized (`CustomerQuery.NotAnonymized`, `OrderQuery.NotAnonymized` on the new `OrderIndex.AnonymizedUtc`, upgrade step 4), so a backlog is worked off over a few days.

**Admin.** `POST customers/{customerId}/erase` and `GET customers/{customerId}/export` (a `customer-{id}.json` download) need **Manage customers**.

## Recipes

The `Customers` recipe step adds or updates guest and business customers, for sample stores and test sites:

```json
{
  "name": "Customers",
  "Customers": [
    {
      "Kind": "Business",
      "CompanyName": "Contoso Supplies",
      "TaxId": "123456789RT0001",
      "Email": "purchasing@contoso.example",
      "Addresses": [
        {
          "AddressId": "contoso-office",
          "Label": "Office",
          "Address": { "Company": "Contoso Supplies", "AddressLine1": "100 King Street West", "City": "Toronto", "Region": "ON", "PostalCode": "M5X 1C7", "Country": "CA" }
        }
      ],
      "DefaultBillingAddressId": "contoso-office",
      "DefaultShippingAddressId": "contoso-office"
    }
  ]
}
```

A customer is matched by `ItemId`, then by the email of a guest, so running a recipe again updates rather than duplicates. Customers with an account are made from their user, and erased or merged customers are left alone. Customers are personal data, so there is no deployment step that exports them. The **Product listing: online store sample** recipe uses this step for its two sample customers.

## API

The **Customers API** feature (`CrestApps.OrchardCore.Customers.Api`) maps into the commerce JSON API at `api/commerce/v1`:

- `me` and `me/addresses`, the signed-in buyer's account and address book, through `CustomerAccountPages` and `CustomerAddressBook` with **Manage own customer profile and addresses**, held to the same rules as **My Account**.
- `customers` and `customers/{customerId}`, read-only, with **View customers**.
- With Orders and the Checkout API on, `PUT checkout/{id}/contact`, the Contact step, through `CustomerContactStepService`.
- With Store Credit or Loyalty Points and the Checkout API on, `checkout/{id}/store-credit` and `checkout/{id}/points`, for signed-in buyers.

See [Commerce API](commerce-api.md) for the endpoints, the conventions they share, and how apps sign in, and [Headless commerce API](../user-manual/commerce/headless-api.md) in the User Manual for setting it up in the admin.

## Admin

**Commerce > Customers** lists customers, the most recently active first, with a search by name, company or email and filters for *Active*, *With an account*, *Guests*, *Businesses*, *Disabled* and *All customers*. **Add customer** creates one by hand, for example for an order taken by phone. Each customer opens an editor for their details, with their account, when they became a customer and when they were last active, and their address book (`customers/{customerId}/addresses/...`). An anonymized customer's addresses can't be edited.

**My Account** (`my-account`) is the signed-in user's own page: their customer details and address book. It is an admin-area page marked `[CustomerAdminPage]`, so a customer reaches it without **Access admin panel**. The first visit makes the user's customer record with `GetOrCreateForUserAsync`; a disabled or anonymized customer can't edit it.

## Report Builder data source

The **Customers** data source (technical name `Customers`) lets the [Report Builder](report-builder/index.md) and Orchard Core data pipelines read the store's customers, their loyalty points, their store credit and their referral links. It is registered when the Customers feature and **Data Pipelines Core** (`OrchardCore.DataPipelines.Core`) are both enabled. The Report Builder enables Data Pipelines Core. Each data set is listed only while the feature that stores its records is enabled, and only to a person whose role has its permission. A person without the permission does not see the data set, and a report that reads it gets no rows.

| Data set | Display name | Registered by | Permission |
| --- | --- | --- | --- |
| `Customers` | Customers | Customers (`CrestApps.OrchardCore.Customers`) | **View customers** |
| `LoyaltyAccounts` | Loyalty accounts | Loyalty Points (`CrestApps.OrchardCore.Customers.Loyalty`) | **View customers** |
| `LoyaltyActivity` | Loyalty activity | Loyalty Points | **View customers** |
| `StoreCreditAccounts` | Store credit accounts | Store Credit (`CrestApps.OrchardCore.Customers.StoreCredit`) | **View customers** |
| `StoreCreditActivity` | Store credit activity | Store Credit | **View customers** |
| `Referrals` | Referrals | Referrals (`CrestApps.OrchardCore.Customers.Referrals`) | **Manage customers** |
| `ReferralRewards` | Referral rewards | Referrals | **Manage customers** |

**View customers** is the permission of the customers list and of the customer's page, where the store credit and the points are shown. No admin screen lists referral links, so their data sets need **Manage customers**, the permission of the **Referrals** settings. **Manage customers** implies **View customers**.

The personal details are the ones the customer's admin page shows, and an anonymized customer has none. The tax id, the reason for a tax exemption, the ids at outside services, the street lines of addresses, the privacy log, the code in a referral link, and the name and email of a referred friend are never exposed. The customer record keeps no customer group, tags, order count or total spent: join the Orders data source on **Customer ID** to report on what customers bought.

### Customers

One row per customer, guests, disabled, merged and anonymized customers included. Customers are read newest first by **Customer since**, the data set's main date. A date range on it, a join on **Customer ID** or **User ID**, and a filter on **Status** or **Kind** are applied in the database, which also groups and counts customers by the fields its index holds. The Customers feature's migration adds the index `IDX_CustomerIndex_Report` on the creation date for these reads.

| Field | Display name | Notes |
| --- | --- | --- |
| `ItemId` | Customer ID | Identifies the customer. The `CustomerId` of the Orders data set refers to it. |
| `Name` | Name | The name the store shows: the display name, the person's or company's name, or the email. |
| `FirstName` | First name | |
| `LastName` | Last name | |
| `CompanyName` | Company | |
| `Kind` | Kind | `Person` or `Business`. |
| `Status` | Status | `Active`, `Disabled`, `Anonymized` or `Merged`. |
| `HasAccount` | Has an account | |
| `UserId` | User ID | Refers to the Users data source. Empty for a guest. |
| `IsTaxExempt` | Exempt from tax | |
| `IsAnonymized` | Anonymized | |
| `MergedIntoCustomerId` | Merged into customer ID | The customer a merged guest's history moved to. Refers to `Customers`. |
| `MergedCustomerCount` | Merged guests | |
| `Email` | Email | |
| `Phone` | Phone | |
| `AcceptsMarketing` | Accepts marketing | |
| `AddressCount` | Addresses | The addresses in the customer's address book. |
| `BillingCountry`, `BillingRegion`, `BillingCity`, `BillingPostalCode` | Billing country, Billing region, Billing city, Billing postal code | Of the default billing address. |
| `ShippingCountry`, `ShippingRegion`, `ShippingCity`, `ShippingPostalCode` | Shipping country, Shipping region, Shipping city, Shipping postal code | Of the default shipping address. |
| `CreatedUtc` | Customer since | The main date. |
| `LastActivityUtc` | Last activity | |
| `ModifiedUtc` | Last changed | |
| `MergedUtc` | Merged | |
| `AnonymizedUtc` | Anonymized on | |

### Loyalty accounts and loyalty activity

`LoyaltyAccounts` gives one row per customer's loyalty points account. `LoyaltyActivity` gives one row per change to the points, as the customer's page lists them, with each account's changes oldest first. Points are not money, so these data sets have no currency. The accounts' index holds no date, so accounts are read newest first by their index rows, and these data sets have no main date. A join on the account or on **Customer ID** is applied in the database, and the accounts can be grouped and counted there.

| Field | Display name | Data set | Notes |
| --- | --- | --- | --- |
| `ItemId` | Loyalty account ID | Loyalty accounts | Identifies the account. |
| `LoyaltyAccountId` | Loyalty account ID | Loyalty activity | Refers to `LoyaltyAccounts`. |
| `CustomerId` | Customer ID | Both | Refers to `Customers`. |
| `Points` | Points | Both | On an account, the points held now. On a change, positive when points were earned or given and negative when they were spent or taken back. |
| `EarnedPoints` | Points earned | Loyalty accounts | The points orders earned, before any were taken back. |
| `SpentPoints` | Points spent | Loyalty accounts | The points spent at checkouts, before any were given back. |
| `ActivityCount` | Changes | Loyalty accounts | |
| `CreatedUtc` | Opened | Loyalty accounts | |
| `UpdatedUtc` | Last changed | Loyalty accounts | |
| `Position` | Change number | Loyalty activity | The change's place in the account, from 1. |
| `CreatedUtc` | When | Loyalty activity | |
| `Kind` | Kind | Loyalty activity | `Earned`, `Reversed`, `Spent`, `Returned` or `Adjusted`. |
| `Balance` | Balance after | Loyalty activity | |
| `Reason` | What | Loyalty activity | The reason the customer's page shows. |
| `Actor` | Changed by | Loyalty activity | The staff member who changed the points by hand. |
| `OrderId` | Order ID | Loyalty activity | Refers to the Orders data source. |
| `CheckoutSessionId` | Checkout session ID | Loyalty activity | Refers to the checkout sessions of the Checkout data source. |

### Store credit accounts and store credit activity

`StoreCreditAccounts` gives one row per customer's store credit in one currency. `StoreCreditActivity` gives one row per change to it, as the customer's page lists them, with each account's changes oldest first. All money is in the account's own currency, which the **Currency** field next to it names. As for loyalty points, the accounts are read newest first by their index rows, and there is no main date. A join on the account or on **Customer ID**, and a filter on **Currency**, are applied in the database, and the accounts can be grouped and summed there, such as the balance by currency.

| Field | Display name | Data set | Notes |
| --- | --- | --- | --- |
| `ItemId` | Store credit account ID | Store credit accounts | Identifies the account. |
| `StoreCreditAccountId` | Store credit account ID | Store credit activity | Refers to `StoreCreditAccounts`. |
| `CustomerId` | Customer ID | Both | Refers to `Customers`. |
| `Currency` | Currency | Both | The account's currency. |
| `Balance` | Balance | Store credit accounts | In the account's currency. |
| `GivenAmount` | Given | Store credit accounts | All the credit given by staff, as refunds or as rewards, in the account's currency. |
| `SpentAmount` | Spent | Store credit accounts | All the credit spent or taken away, in the account's currency. |
| `ActivityCount` | Changes | Store credit accounts | |
| `CreatedUtc` | Opened | Store credit accounts | |
| `UpdatedUtc` | Last changed | Store credit accounts | |
| `Position` | Change number | Store credit activity | The change's place in the account, from 1. |
| `CreatedUtc` | When | Store credit activity | |
| `Amount` | Amount | Store credit activity | Positive when credit was given, negative when it was spent or taken away, in the account's currency. |
| `Balance` | Balance after | Store credit activity | In the account's currency. |
| `Reason` | What | Store credit activity | The reason the customer's page shows. |
| `Actor` | Changed by | Store credit activity | The staff member who changed the credit by hand. |
| `CheckoutSessionId` | Checkout session ID | Store credit activity | Refers to the checkout sessions of the Checkout data source. |

### Referrals and referral rewards

`Referrals` gives one row per customer's referral link. `ReferralRewards` gives one row per friend's first order the customer was rewarded for, with each link's rewards oldest first. Links are read newest first by their index rows, and there is no main date. A join on the link or on **Customer ID** is applied in the database.

| Field | Display name | Data set | Notes |
| --- | --- | --- | --- |
| `ItemId` | Referral ID | Referrals | Identifies the link. |
| `ReferralId` | Referral ID | Referral rewards | Refers to `Referrals`. |
| `CustomerId` | Customer ID | Both | The customer who shared the link. Refers to `Customers`. |
| `ReferralCount` | Friends rewarded for | Referrals | |
| `RewardPoints` | Points rewarded | Referrals | All the points the link earned the customer. |
| `LastReferralUtc` | Last friend's order | Referrals | |
| `CreatedUtc` | Created | Referrals | |
| `UpdatedUtc` | Last changed | Referrals | |
| `Position` | Reward number | Referral rewards | The reward's place in the link, from 1. |
| `CreatedUtc` | Rewarded | Referral rewards | |
| `OrderId` | Friend's order ID | Referral rewards | Refers to the Orders data source. |
| `OrderNumber` | Friend's order number | Referral rewards | |
| `RewardKind` | Reward kind | Referral rewards | `None`, `StoreCredit` or `Points`. |
| `Reward` | Reward | Referral rewards | What the customer was given, as text. |
| `Currency` | Currency | Referral rewards | The currency of the store credit given. |
| `RewardAmount` | Store credit given | Referral rewards | In the reward's currency. Zero for a reward in points. |
| `RewardPoints` | Points given | Referral rewards | |

## Permissions

| Permission | Allows |
| --- | --- |
| **Manage customers** | Adding, editing, exporting and erasing customers, and the Customers settings. Giving and taking store credit and points, and the **Rewards** and **Referrals** settings. Implies **View customers**. |
| **View customers** | Listing and opening customers, read-only. |
| **Manage own customer profile and addresses** | Using **My Account**. Granted to the Authenticated role by default. |

Administrators have the first two by default.
