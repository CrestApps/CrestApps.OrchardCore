# Customers plan

Status: decisions agreed 2026-10-16; CU1 in progress on `claude/customers-plan`. Phase 1 of the [e-commerce plan](../../ecommerce-plan.md), after
the product listing (#834), carts and orders (#835), search servers and variants (#837, #848).

## Goal

Give every buyer a customer record, whether they have an account or check out as a guest:

- **Records:** customers hold their contact details and an address book.
- **Orders:** checkout asks for what an order needs.
- **Guests:** guests can find their orders again without an account, and their history joins their account when
  they sign up.
- **Privacy:** customer data follows the retention rules already agreed in the foundation decisions.

This is what shipping, guest order lookup and the customer account pages wait on.

## Where things stand

| Piece | Today |
| --- | --- |
| `Customers.Abstractions` | `CustomerOwner` (an authenticated user id, or a guest id) and `CustomerOwnerKind`. Also `ICustomerContact` and `ICustomerContactResolver` (a user resolves through `IUserService`; a guest's contact is whatever the caller kept). |
| `Customers.Core` | `DefaultCustomerContactResolver` only. There is no Customers module, no customer record and no address book. |
| Orders | `Order.UserId`, plus `CustomerName` and `CustomerEmail` copied from the signed-in user. **A guest order records no name or email**, so a guest cannot be sent a confirmation or find the order again. |
| Transactions, Pay Later | Owner is a `CustomerOwner`. A guest's name and email are copied onto the transaction (`GuestContactName`, `GuestContactEmail`). |
| Subscriptions, Installment plans | Each keeps its own copy of the buyer's name and email. |
| Carts | Owned by a user id or a hashed guest cookie (`GuestSessionTokenManager`); merged on sign-in. |
| Checkout | A pipeline of steps that handlers add. Orders adds a hidden step carrying the charges; Subscriptions adds Registration. **No step collects contact details or addresses.** |
| Addresses | The immutable `Address` value, `IAddressResolver`, and country, region and city content. Nothing outside the module resolves addresses yet. |
| Customer pages | My Orders, My Transactions and My Subscriptions are admin pages marked `[CustomerAdminPage]`. |
| Foundation decisions | F3: order addresses are immutable snapshots, editable addresses belong to Customers, and address details are visible only to their owner and to customer managers. F5: retention windows (customer data 24 months after last activity, guest orders 12 months after fulfillment, then anonymized). |

## Decisions (agreed 2026-10-16)

| # | Proposal | Why |
| --- | --- | --- |
| C-D1 | **A customer is its own record**, a YesSql document in a Customers collection, linked to a user when there is one. It is not a user setting. | Guests have no user. A business can have several contacts later. Orders, transactions and subscriptions can then point at one record whatever happens to the account. |
| C-D2 | **A guest becomes a guest customer at checkout**, identified by the email they enter. Typing an email grants no access: a guest reaches their orders only through signed links, and guest history joins an account only when that account's email is verified. | Anyone can type someone else's email. |
| C-D3 | **Guests find their orders through signed links**: one in the order confirmation, and a *Find my order* page that emails a fresh link to the order's address. Links expire (60 days, like pay links). | The page never shows whether an order or email exists, and no order number plus email pair can be guessed. |
| C-D4 | **The checkout asks for contact details and a billing address**: email and name always for guests, the billing address set by a site setting (*Off*, *Optional*, *Required*; default *Optional*). Shipping addresses come with Shipping (Phase 7). | Many stores need a billing address for tax or invoices; others don't want the friction. |
| C-D5 | **Person or business**: a business customer has a company name and a tax id. Tax exemptions stay with Taxation and come later. | Cheap now, and invoices need them. |
| C-D7 | **Accounts are Orchard Core users.** Customers builds no registration, sign-in, password reset or email confirmation: Orchard Core Users does those. A customer with an account links to its user; a guest customer has none until they register through Orchard Core's registration. | One account system for the site, with Orchard Core's email confirmation, lockout and external logins. |
| C-D6 | **Customer pages stay admin pages for now** (`[CustomerAdminPage]`), like My Orders: My Account and My Addresses. Front-end account pages come with the storefront (Phase 10). | One way to build customer pages until the storefront exists. |

## Design

### Customer record

- **`Customer`:**
  - identity: `CustomerId`, `Kind` (Person or Business), `Status` (Active, Disabled, Anonymized);
  - names and contact: `DisplayName`, `FirstName`, `LastName`, `CompanyName`, `TaxId`, `Email` (normalized for lookup), `Phone`;
  - account link: `UserId` (null for a guest);
  - preferences: `ContactPreferences` (order updates are always sent; marketing is opt-in);
  - addresses: `DefaultBillingAddressId`, `DefaultShippingAddressId`;
  - external links: `ExternalIds` (provider-neutral, such as a Stripe customer id);
  - dates: `CreatedUtc`, `LastActivityUtc`;
  - extension: an entity bag for modules.
- **`CustomerIndex`:** `CustomerId`, `UserId`, `NormalizedEmail`, `Kind`, `Status`, `LastActivityUtc`.
- **`ICustomerService`:**
  - `GetOrCreateForUserAsync(userId)`;
  - `GetOrCreateGuestAsync(email, name)` (always a new guest record per checkout unless the guest token already names one);
  - `FindAsync`, `UpdateAsync`, `MergeAsync(guestId, userId)`, `AnonymizeAsync`.
- **Contact resolution:** `ICustomerContactResolver` resolves through the customer record when it exists, so Transactions, receipts and reminders get the same contact everywhere.
- **Signed-in users:** a user gets a customer record the first time they buy, sign up for a plan, or open My Account. Nothing is created for users who never shop.

### Address book

- `CustomerAddress` (mutable, owned by the customer): label, recipient name, company, phone, and the `Address` fields resolved through `IAddressResolver` (country, region, city from the Addresses module).
- **My Addresses** for customers, and an **Addresses** tab on the admin customer page: add, edit, delete, set default billing and shipping.
- Orders never point at an address-book entry. They copy an immutable `Address` snapshot when placed (F3), so editing or deleting an address never changes an order.

### Checkout: contact and address step

- A **Contact** step for order checkouts (Orders), added by the Customers module when it is enabled:
  - **guests:** email (required), name (required), phone (optional), with an option to create an account after the order (sign-up later, not a password field in checkout);
  - **signed-in customers:** prefilled and editable for this order only;
  - **billing address:** per the site setting. Signed-in customers pick a saved address or enter one, with *Save to my addresses*.
- **When the checkout completes:**
  - the order gets `CustomerId`, `CustomerName`, `CustomerEmail` and a `BillingAddress` snapshot;
  - a Pay Later transaction gets the same contact, so guest reminders and receipts work.
- **Shape and extension:** the step is a shape with a display driver, like the other steps, so a theme can override it and another module can add fields.

### Guest access and merging

- **Order links:** a signed, expiring link names one order (data protection, like `ITransactionPayLinkService`). It opens a read-only order page with its receipt, and pay links where something is owed.
- ***Find my order*:** the page takes an email and always answers "if we have orders for that email, we sent a link". It emails one link per recent order, rate-limited.
- **Merging into an account:** when a user signs in or registers with a **verified** email (Orchard Core's email
  confirmation, `User.EmailConfirmed`), their guest customers with that email merge into the user's customer. Orders, transactions, carts and subscriptions move to it, and the merge is recorded on both records. Unverified emails never merge; the user can still open each order through its link.
- **Carts:** they keep their own guest cookie and merge on sign-in as today. A cart's owner becomes the customer when it is checked out.

### Privacy and retention

- **Background task:** applies the F5 windows from site settings:
  - a customer inactive past the window is anonymized;
  - guest order details are anonymized 12 months after fulfillment.
  - Order snapshots and payment references stay for their own legal windows.
- **Erase customer:** an admin action that anonymizes now (names, email, phone, addresses), keeps orders with anonymized contacts, and leaves an audit entry.
- **Export:** an admin action that downloads a customer's data (profile, addresses, orders, transactions) as JSON, for access requests.
- **Who can see what:** address and contact details are readable only by the owning customer and by holders of **Manage customers** (new permission); a guest only through a signed link.

### Admin

- **Commerce > Customers:** a list with search by name, email or company, filters by kind, status and *has account*, and a detail page with tabs: profile, addresses, orders, transactions, subscriptions, merges. Actions: edit, disable, erase, export.
- **Permissions:**
  - **View customers** and **Manage customers**;
  - **Manage own customer profile**, implied for authenticated users, for My Account and My Addresses.
- **Recipes and deployment:** customers are personal data, so they are not exported by default; the address-book settings are.

### Integration

- **Orders:**
  - `Order.CustomerId` and `BillingAddress`;
  - My Orders lists by customer, so orders placed as a guest appear after a merge;
  - the admin order page links the customer.
- **Transactions, Pay Later:** the owner resolves through the customer, and guest Pay Later gets a contact from the Contact step.
- **Subscriptions, Installment plans:** they keep their own fields for now and gain `CustomerId` where they have a buyer. Moving them fully onto customers is a later clean-up.
- **Taxation:** `TaxId` and the billing country are available to tax providers through the existing tax context (no new rules).

## Slices

| Slice | Scope | Exit gate |
| --- | --- | --- |
| **CU1 Customer record** | Customers module and feature, `Customer` and index, `ICustomerService`, contact resolver through customers, Commerce > Customers list and detail, permissions | An order placed by a signed-in user creates their customer record; the admin can find and edit them. |
| **CU2 Address book** | `CustomerAddress`, My Addresses, admin Addresses tab, defaults, Addresses module integration | A customer adds, edits and deletes addresses, with country and region from the Addresses module. |
| **CU3 Checkout contact step** | Contact step for orders, billing-address setting, saved-address picker, snapshots on the order, contact on Pay Later transactions | A guest order records name, email and billing address; the confirmation reaches the guest; a guest Pay Later reminder is sent. |
| **CU4 Guest access and merging** | Signed order links, read-only order page, *Find my order*, merge on verified sign-in or sign-up | A guest opens their order from the confirmation link; after signing up with that verified email, the order is in My Orders. |
| **CU5 Privacy** | Retention task and settings, Erase, Export, audit entries | An inactive guest's data is anonymized on schedule while the order stays; Export returns everything the store holds on a customer. |
| **CU6 Docs and samples** | User and technical manuals, the store sample with a customer and addresses, changelog | Docs build passes; a fresh store sample shows customers end to end. |

### CU1 as built

- `Customer` (`CatalogItem`, `Customers` collection), `CustomerIndex`, `ICustomerStore` (search by the start of name,
  company or email; concurrency-checked saves), `ICustomerService.GetOrCreateForUserAsync` under a per-user
  distributed lock, `CustomerRecordContactResolver` replacing the default contact resolver.
- Commerce > Customers: list with search and filters (Active, With an account, Guests, Businesses, Disabled, All),
  Add customer, edit (an account's email is read-only), View and Manage customers permissions.
- Orders: `Order.CustomerId` and an indexed `CustomerId` column (upgrade step uses `IndexTableIndexCreation`); a
  signed-in checkout sets it.
- Orders placed before Customers was enabled are not linked to a customer. The customer's orders view (with the
  address book or guest access slice) lists by `CustomerId` or, for a customer with an account, by `UserId`, so they
  still show.

### CU2 as built

- `CustomerAddress` entries live on the customer (`Addresses`, `DefaultBillingAddressId`, `DefaultShippingAddressId`),
  capped at 20. `ToAddress()` gives the immutable `Address` snapshot an order keeps.
- The admin edits a customer's addresses under the customer (`customers/{customerId}/addresses/...`).
- **My Account** (`my-account`, `[CustomerAdminPage]`, Manage own customer profile and addresses permission for the
  Authenticated role) edits the user's own details and addresses, making their customer record on the first visit.
- The customer's orders list moves to CU4, where guest access adds the order page.
- Amended before merging (step AD3 of the [Addresses plan](../addresses-plan.md)): a `CustomerAddress` is an id, a
  label and the shared `Address` value, edited with the Addresses module's `AddressEditor` and checked against the
  country's rules; addresses are listed through `IAddressFormatter`. Customers depends on the Addresses feature,
  which also supplies the country list, so the `DefaultCountryService` fallback is gone.

### CU3 as built

- The **Contact** step (`CustomerContactCheckoutHandler`, `CustomerContactCheckoutFlowDisplayDriver`), registered when
  Orders is enabled, asks for a name, an email and a phone, and a billing address per `CustomerCheckoutSettings`
  (Optional by default, Required, Off; Settings › Commerce › Customers). The checkout keeps the setting it started
  with. The billing address uses the shared `AddressEditor`; a signed-in customer can bill a saved address or save a
  new one.
- The step stores a `CheckoutContactInfo` (now with `Phone` and `BillingAddress`). `OrderCheckoutHandler` copies it
  onto the order when paid: the entered name, email and phone replace the account's, and `Order.BillingAddress` and
  `Order.CustomerPhone` are new. Pay Later already read the contact for guests.
- When paid, the order is linked to the signed-in buyer's customer or a new guest customer
  (`ICustomerService.CreateGuestAsync`), and a guest's billing address becomes their default billing address.
- **CU3b, order confirmations** (decided 2026-10-16: on by default): `OrderConfirmationCheckoutHandler` emails a paid
  order's confirmation to the order's email once (`Order.ConfirmationSentUtc`) after the completion is committed,
  when `OrderSettings.SendConfirmations` is on and the site has an email service. CU4 adds the signed order link to
  it.

### CU4 as built

- Orders: `IOrderLinkService` (data protection, 60-day time limit, purpose `CrestApps.OrchardCore.Orders.OrderLink`)
  and `OrderViewController`: `orders/view/{token}` (read-only order, pay link for an outstanding Pay Later transaction)
  and `orders/find` (emails links to orders from the last year, same answer either way, limited by
  `IPaymentAttemptLimiter` per IP and per email, plus the `find-order` rate-limit group). `OrderQuery` gained
  `CustomerId` and `CustomerEmail`; `OrderIndex.CustomerEmail` is now trimmed and indexed (upgrade step 3 with
  `IndexTableIndexCreation`). The confirmation page and a guest's confirmation email carry the link; the page invites
  a guest to create an account when registration is on.
- Customers: `CustomerStatus.Merged`, `MergedIntoCustomerId`, `MergedUtc`, `MergedCustomerIds`;
  `ICustomerStore.ListGuestsByEmailAsync`; `ICustomerMergeService` run from sign-in, registration and email
  confirmation; `ICustomerMergeHandler` with `OrderCustomerMergeHandler` (orders and their guest transactions). The
  customer page lists the customer's orders (by user for account holders, so orders from before CU1 show) and merges.
- Not moved: subscriptions and installment plans keep their own owner fields, as the plan says.
- Found live: injecting the merge service into the user event handlers is a circular dependency (UserStore → user
  event handlers → merge service → customer service → user service → registration events). They resolve it when they
  run.

Each slice is a PR with tests (unit, and SQLite integration for the store and merge), docs, and a live pass on the test
store.

## Out of scope

- Customer groups and group pricing (Phase 2 pricing).
- Tax exemption certificates.
- Shipping addresses at checkout (Phase 7).
- Front-end account pages (Phase 10).
- Several contacts per business.
- Marketing lists and campaigns.
- Moving Subscriptions and Installment plans fully onto customer records.

## Open questions

- **Create an account** after a guest order is a prompt on the confirmation page that opens Orchard Core's registration
  with the email filled in. Orchard Core sends its own confirmation email, and the merge follows once the email is
  confirmed. Nothing in Customers issues registration tokens.
- Should the Contact step also show for **subscription** checkouts, replacing Subscriptions' own name and email fields? The proposal is not yet (out of scope above), to keep this phase to orders.
