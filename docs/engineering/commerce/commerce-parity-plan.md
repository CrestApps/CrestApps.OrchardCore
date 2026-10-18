# Commerce parity plan: WooCommerce and Shopify

Status: phase 1 built, 2026-10-18 (see [Phase 1 as built](#phase-1-as-built)); phases 2 to 12 to do. Follows the [commerce completion plan](commerce-completion-plan.md), which finished the
[e-commerce plan](../../ecommerce-plan.md) through phase 12. The goal here is a suite at least as complete as
WooCommerce with its common extensions and Shopify with its common apps, while keeping what we already do better.

The comparison is from what both products offer in their core and through widely used extensions and apps. Check each
row against their current documentation before its slice starts: both change often, and some features move between
paid add-ons and the core.

Legend: **Core** is built in. **Ext** is a WooCommerce extension (often paid). **App** is a Shopify app, or a feature on
higher plans only. **Partial** means we have part of it; the notes say which.

## Where we already lead

These stay as they are and are the reason to choose us; new work must not weaken them.

| Capability | Us | WooCommerce | Shopify |
| --- | --- | --- | --- |
| Many stores on one installation, isolated per tenant, and selling whole sites with automatic provisioning | Core | Multisite only | One store per plan |
| A transactions ledger: balances owed, reminders, pay links, numbered invoices, Pay Later settlement | Core | Ext, partial | App, partial |
| Installment plans: a down payment, then scheduled charges to a saved card | Core | Ext (Deposits) | App |
| A tax engine with jurisdictions, sourcing rules, exemption certificates, business tax ids and the store's origin | Core | Ext (Avalara, TaxJar) | Core in some countries (Shopify Tax) |
| A coupon use held while the buyer pays, so the last use can't be taken twice | Core | No | No |
| A stock ledger: received, sold, returned, counted, with holds during checkout and CSV count import | Core | Quantity only | Core, partial |
| Several listing pages per site, each with its own filters and sorts, over pluggable search (database, Lucene, Elasticsearch, Azure AI Search), and products that are listed but not sold (price on request, inquiry) | Core | Ext | Core filters, one catalog |
| Commerce on the same platform as a contact center, CRM, omnichannel messaging and AI agents | Core | No | No |
| Workflow events on orders, refunds and shipments | Core | Ext (AutomateWoo) | Core (Flow) |
| Setup as code: recipes and deployment plans copy a store's configuration between sites | Core | No | No |
| Distributed locks and tenant isolation for web farms | Core | No | Hosted |

## Gaps

Every feature WooCommerce or Shopify has that we don't, grouped into the slices that would close them. The slice
column refers to the delivery plan below.

### Customer accounts and APIs

| Feature | WooCommerce | Shopify | Us | Slice |
| --- | --- | --- | --- | --- |
| Front-end account pages: orders, downloads, addresses, payment methods, subscriptions, account details | Core | Core | No: customers use admin pages (CC-D5) | PA1 |
| Passwordless sign-in by email code | Ext | Core | No | PA1 |
| Saved cards for one-time purchases | Core (gateway) | Core | Partial: installments and subscriptions only | PB1 |
| Storefront and admin JSON APIs | Core | Core | No (CC-D5) | PA2 |
| Outgoing webhooks for orders, customers, products | Core | Core | No: workflow events only | PA2 |
| Headless storefront starter | Ext | Core (Hydrogen) | No | PA2 |

### Payments and risk

| Feature | WooCommerce | Shopify | Us | Slice |
| --- | --- | --- | --- | --- |
| PayPal (checkout, Pay Later, Venmo) | Core | Core | No | PB1 |
| Other card gateways (Square, Adyen, Authorize.net, Braintree) | Ext | Core | No: Stripe only, behind a provider seam | PB1 |
| Manual payment methods: bank transfer, cheque, cash on delivery | Core | Core | Partial: Pay Later | PB1 |
| Express pay buttons on product and cart pages | Ext | Core | Partial: checkout only | PB2 |
| Buy now, pay later providers (Klarna, Afterpay, Affirm) | Ext | Core | Partial: through Stripe's methods, untested | PB2 |
| Fraud scoring and manual review of risky orders | Ext | Core | No | PB3 |
| Disputes and chargebacks recorded on the order, with evidence and reports | Ext | Core | No: a workflow event only | PB3 |

### Catalog and merchandising

| Feature | WooCommerce | Shopify | Us | Slice |
| --- | --- | --- | --- | --- |
| Scheduled sale prices | Core | App | Partial: a compare-at price, not scheduled | PC1 |
| Upsells, cross-sells and related products | Core | Core (recommendations) | No | PC1 |
| Recently viewed products | Ext | App | No | PC1 |
| Grouped products | Core | App | No | PC2 |
| External and affiliate products | Core | App | No | PC2 |
| Bundles and kits, with stock taken from each part | Ext | Core (Bundles) | No | PC2 |
| Product options and personalization (text, file upload, add-on prices) | Ext | App | No | PC2 |
| Minimum, maximum and step quantities; sold individually | Ext | Core (B2B) | Partial: a maximum per line | PC3 |
| Quantity and tier pricing | Ext | Core (B2B) | No | PC3 |
| Customer-group and role pricing, price lists | Ext | Core (B2B, Markets) | No | PC3 |
| Product comparison | Ext | App | No | PC3 |
| Product feeds for Google Merchant Center and Meta | Ext | Core (channels) | No | PI1 |
| Product and order CSV import and export | Core | Core | Partial: content transfer and stock counts; no order export | PC4 |
| Migration from WooCommerce and Shopify exports | n/a | Core (Store Importer) | No | PC4 |

### Promotions, gift cards and loyalty

| Feature | WooCommerce | Shopify | Us | Slice |
| --- | --- | --- | --- | --- |
| Automatic discounts without a code | Ext | Core | No | PD1 |
| Buy X get Y, bulk and volume discounts | Ext | Core | No | PD1 |
| Rules for combining discounts | Ext | Core | No: one coupon per checkout | PD1 |
| Codes applied from a link | Ext | Core | No | PD1 |
| Generated single-use codes in bulk | Ext | Core | No | PD1 |
| Gift cards, bought and redeemed, with balances | Ext | Core | No | PD2 |
| Store credit | Ext | Core | No | PD2 |
| Loyalty points and rewards | Ext | App | No | PD3 |
| Referral and affiliate programs | Ext | App | No | PD3 |

### Checkout and conversion

| Feature | WooCommerce | Shopify | Us | Slice |
| --- | --- | --- | --- | --- |
| Abandoned checkout recovery emails | Ext | Core | No | PE1 |
| Order bumps and post-purchase offers | Ext | App | No | PC1 |
| One-page checkout option | Core (blocks) | Core | No: a step per page | PE2 |
| Address autocomplete | Ext | Core | Partial: browser autofill only | PG1 |
| Custom checkout fields, such as a gift message or delivery date | Ext | Core (extensibility) | No: steps come from code | PE2 |
| Creating an account after a guest checkout | Core | Core | Partial: guest orders join an account later | PE2 |
| Mini cart and slide-out cart | Core (blocks) | Theme | No: a cart page and count | PE2 |

### Orders and fulfillment

| Feature | WooCommerce | Shopify | Us | Slice |
| --- | --- | --- | --- | --- |
| Manual and draft orders created by staff, with an invoice to pay | Core | Core | Partial: installment plans only | PF1 |
| Editing an order after it's placed (add, remove, change items) | Ext | Core | No | PF1 |
| Packing slips and printable invoices | Ext | Core | Partial: invoices for transactions | PF1 |
| Self-service returns and exchanges, with return reasons and restocking | Ext | Core | No: refund and restock from the order | PF2 |
| Local pickup | Core | Core | No | PF3 |
| Local delivery by area, with delivery fees | Ext | Core | No | PF3 |
| Live carrier rates (UPS, FedEx, USPS, DHL, or an aggregator) | Ext | Core | No: flat, table and free rates | PF4 |
| Buying and printing shipping labels | Ext | Core | No | PF4 |
| A customer order tracking page | Ext | Core | Partial: tracking numbers and links on the order | PF4 |
| Stock in several locations, with transfers between them | Ext | Core | No: one location in the model | PF5 |
| Suppliers and purchase orders | Ext | App | No | PF5 |
| Back-in-stock alerts | Ext | App | No | PF6 |
| Pre-orders and backorders with a release date | Ext | App | Partial: allow-backorder per item | PF6 |

### Markets, languages and tax

| Feature | WooCommerce | Shopify | Us | Slice |
| --- | --- | --- | --- | --- |
| Showing prices in the shopper's currency, with conversion and rounding rules | Ext | Core (Markets) | Partial: a price per managed currency, no conversion | PG1 |
| Prices and catalogs per country or market | Ext | Core (Markets) | No | PG1 |
| Translated products, emails and checkout | Ext (WPML) | Core | Partial: Orchard Core localization; not wired through commerce | PG2 |
| Duties and import taxes at checkout | Ext | Core | No | PG3 |
| Tax-inclusive prices and EU VAT one-stop-shop reporting | Core, Ext | Core | Partial: the tax engine supports inclusive prices; no OSS report | PG3 |
| Tax automation connectors (Avalara, TaxJar) | Ext | App | No: a provider seam exists | PG3 |

### B2B

| Feature | WooCommerce | Shopify | Us | Slice |
| --- | --- | --- | --- | --- |
| Company accounts with several buyers and locations | Ext | Core (B2B) | Partial: business customers, one account each | PH1 |
| Payment terms (net 30) and deposits | Ext | Core (B2B) | Partial: Pay Later and installments | PH1 |
| Request a quote | Ext | App | No | PH2 |
| Purchase order numbers on orders | Ext | Core (B2B) | No | PH2 |
| Price lists and catalogs per company | Ext | Core (B2B) | No (see PC3) | PH2 |

### Marketing, engagement and analytics

| Feature | WooCommerce | Shopify | Us | Slice |
| --- | --- | --- | --- | --- |
| Editable transactional email templates | Core | Core | Partial: fixed templates | PJ1 |
| Email campaigns and newsletters | Ext (MailPoet) | Core (Shopify Email) | No: the omnichannel email channel is for conversations | PJ2 |
| Customer segments | Ext | Core | No | PJ2 |
| Review request emails after delivery, reviews with photos | Ext | App | No: text reviews only | PJ1 |
| Wishlists and saved for later | Ext | App | No | PJ3 |
| Ready-made automations (win-back, thank you, low stock) | Ext (AutomateWoo) | Core (Flow templates) | Partial: workflow events, no templates | PJ3 |
| Live view, cohort and retention reports | Core (Analytics), Ext | Core | Partial: customers report; no cohorts | PK1 |
| Custom reports and dashboards | Ext | Core | Partial: Reports designer, no commerce dashboard widgets | PK1 |
| Scheduled reports by email and operational alerts | Ext | App | No | PK1 |

### Channels, point of sale and other business models

| Feature | WooCommerce | Shopify | Us | Slice |
| --- | --- | --- | --- | --- |
| Point of sale (in-person sales sharing stock and customers) | Ext | Core | No | PI2 |
| Selling on Amazon, eBay, TikTok and social shops | Ext | Core (channels) | No | PI1 |
| Bookings and appointments | Ext | App | No | PL1 |
| Service fulfillment with its own status (CC-D5) | Ext | App | No | PL1 |
| Memberships and gated content | Ext | App | Partial: Content Access Control, not tied to purchases | PL2 |
| Rentals | Ext | App | No | PL1 |
| Multi-vendor marketplace: vendors, commissions, payouts | Ext (Dokan and others) | App | No (out of the first scope) | PM1 |

### Store setup and ecosystem

| Feature | WooCommerce | Shopify | Us | Slice |
| --- | --- | --- | --- | --- |
| A guided setup for a new store: what you sell, where, how you're paid | Core | Core | Partial: sample recipes | PN1 |
| A storefront theme made for commerce | Core (Storefront theme) | Core | Partial: listings render in Bootstrap themes | PN1 |
| Staff permission presets (warehouse, support, marketing) | Ext | Core | Partial: permissions, no presets | PN1 |
| A documented way for third parties to extend checkout and the storefront | Core | Core (extensibility) | Partial: drivers and seams, not documented as an extension guide | PN2 |

## Delivery plan

Ordered by how much a store owner notices the gap. Each slice is a commit on one branch per phase and one PR per
phase, verified on a test site in the browser at every Bootstrap breakpoint before it is opened.

| Phase | Slices | Exit gate |
| --- | --- | --- |
| **1. Accounts and conversion** (built) | PA1 account pages, passwordless sign-in; PE1 abandoned checkout emails; PE2 one-page option, custom fields, account after checkout, mini cart | A guest who leaves at payment gets a recovery email, comes back, pays on one page, creates an account, and finds the order and downloads on their account page on a phone. |
| **2. Payments** | PB1 PayPal, a second card gateway, bank transfer and cash on delivery, saved cards for one-time purchases; PB2 express buttons on product and cart pages, buy now pay later; PB3 fraud scoring, disputes on orders | One store takes PayPal, Stripe and bank transfer; a disputed Stripe payment shows on its order and in a report. |
| **3. Merchandising** | PC1 scheduled sales, upsells, cross-sells, related, recently viewed, order bumps and post-purchase offers; PC2 grouped, external, bundles, product options; PC3 quantity rules, tier and group pricing, comparison; PC4 CSV import and export, migration importers | A bundle with a personalized part sells at a tier price to a wholesale group, and a WooCommerce product export imports with its variants. |
| **4. Fulfillment** | PF1 manual and draft orders, order editing, packing slips; PF2 self-service returns and exchanges; PF3 local pickup and delivery; PF4 carrier rates, labels, tracking page; PF5 locations, transfers, purchase orders; PF6 back-in-stock, pre-orders | A buyer returns one item from their account, the store approves it, prints a return label, and the refund and restock land at the right location. |
| **5. Promotions and loyalty** | PD1 automatic and buy-X-get-Y discounts, combining rules, link codes, bulk codes; PD2 gift cards and store credit; PD3 loyalty points, referrals | A gift card pays part of an order that also gets an automatic discount and a code, under the store's combining rules. |
| **6. Markets** | PG1 shopper currency and market prices, address autocomplete; PG2 translated catalog, emails and checkout; PG3 duties, OSS, tax connectors | A shopper in Germany sees euros and German, pays VAT included, and the order shows in the OSS report. |
| **7. B2B** | PH1 companies, buyers, payment terms; PH2 quotes, PO numbers, company price lists | A company buyer requests a quote, the store converts it, and the company pays on net-30 terms with its PO number on the invoice. |
| **8. Marketing and analytics** | PJ1 editable emails, review requests with photos; PJ2 campaigns, segments; PJ3 wishlists, automation templates; PK1 cohorts, dashboards, scheduled reports, alerts | A segment of lapsed buyers gets a win-back campaign, and the owner gets a weekly sales report by email. |
| **9. Channels and POS** | PI1 Google and Meta feeds, marketplace channels; PI2 point of sale | A product sold in person updates stock online, and the product feed passes Google Merchant Center validation. |
| **10. Other models** | PL1 bookings, services, rentals; PL2 memberships | A booked appointment is paid, confirmed and completed; a membership purchase unlocks gated content. |
| **11. Marketplace** | PM1 vendors, commissions, payouts | A vendor's product sells; the store keeps its commission and the vendor is paid out through Stripe Connect. |
| **12. Setup and ecosystem** | PN1 setup guide, commerce theme, staff presets; PN2 extension guide | A new owner goes from a blank site to a first test sale through the guide without reading the docs. |

## Phase 1 as built

One branch, a commit per slice:

- **PA1 account pages.** The Commerce feature serves `/account` and adds **My account** to the user menu. Modules add pages through `INavigationProvider` for the `commerce-account` menu: Orders (list, order page, Pay now), Digital Downloads, Subscriptions (cancel at period end), Transactions (balances and invoices), Customers (addresses, account details). Payment methods wait for saved cards (phase 2).
- **PA1 passwordless sign-in.** A **Sign in with an email code** feature in the Users module: six-digit codes, hashed in the distributed cache per tenant, ten minutes and five tries, send limits per email and address.
- **PE1 abandoned checkout emails.** A background task every 15 minutes emails a buyer once when they left an unpaid checkout longer ago than the store's delay, with a signed link that resumes the checkout or rebuilds the cart. Off by default; needs the site's base URL.
- **PE2 account after checkout.** A guest makes an account from the confirmation page with just a password; the order joins it.
- **PE2 mini cart.** The cart link opens a slide-out cart, and Add to cart adds in place.
- **PE2 custom fields.** A **Checkout details** feature: the store adds fields to a `CheckoutDetails` content type with the content type editor; the Contact step renders their editors, and the values are kept on the order and shown on its pages.
- **PE2 one-page option.** **Settings > Commerce > Checkout** lays the checkout out on one page; payment joins the page once the details are complete, and a later change is saved before paying.

Moved out of phase 1, as the gap table now says:

- **Saved cards for one-time purchases** to phase 2 (PB1): they need the gateway work there, and the account's payment methods page comes with them.
- **Order bumps and post-purchase offers** to phase 3 (PC1), with upsells and cross-sells, which they share their product picking with.
- **Address autocomplete** to phase 6 (PG1): it needs a third-party places provider, chosen with the market work. Address forms already carry the browser autofill attributes.

## Decisions needed

Until they are answered, the work goes ahead on the default in the last column.

| # | Question | Options | Default taken |
| --- | --- | --- | --- |
| P-D1 | CC-D5 put account pages, JSON APIs and service fulfillment out of scope. Are they in scope now? | In (phases 1, 10 and PA2) / keep deferred | In |
| P-D2 | The original plan left marketplaces out. Is phase 11 in scope? | In / out / later | Later: phase 11 last |
| P-D3 | Carrier rates and labels: one aggregator (EasyPost or Shippo) or carriers one by one? | Aggregator first (fewer contracts, faster) / per carrier | Aggregator first |
| P-D4 | Email campaigns: build them, or integrate with Mailchimp or Klaviyo? | Integrate first / build on the omnichannel email channel | Integrate first |
| P-D5 | Point of sale: a web POS page, or a native app? | Web POS first / native | Web POS first |
| P-D6 | Second card gateway after PayPal | Square / Adyen / Authorize.net | Square |
| P-D7 | Shopper currency conversion: live exchange rates from a provider, or rates the store sets? | Store-set rates first / provider | Store-set rates first |
