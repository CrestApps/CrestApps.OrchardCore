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
- **Merging into an account:** when a user signs in or registers with a **verified** email, their guest customers with that email merge into the user's customer. Orders, transactions, carts and subscriptions move to it, and the merge is recorded on both records. Unverified emails never merge; the user can still open each order through its link.
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

- Should a guest be offered **Create an account** on the order confirmation page (an emailed invitation link), or only through the site's normal registration? The proposal is the confirmation page, as an optional prompt.
- Should the Contact step also show for **subscription** checkouts, replacing Subscriptions' own name and email fields? The proposal is not yet (out of scope above), to keep this phase to orders.
