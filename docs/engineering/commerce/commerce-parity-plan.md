# Commerce parity plan: WooCommerce and Shopify

Status: phases 1 to 6 built, 2026-10-20 (see [Phase 1 as built](#phase-1-as-built), [Phase 2 as built](#phase-2-as-built), [Phase 3 as built](#phase-3-as-built), merged as #1004, [Phase 4 as built](#phase-4-as-built), [Phase 5 as built](#phase-5-as-built) and [Phase 6 as built](#phase-6-as-built)); phases 7 to 13 to do. Follows the [commerce completion plan](commerce-completion-plan.md), which finished the
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
| Automatic discounts without a code | Ext | Core | Yes (phase 5) | PD1 |
| Buy X get Y, bulk and volume discounts | Ext | Core | Yes (phase 5): buy X get Y, minimum quantities; tier prices from phase 3 | PD1 |
| Rules for combining discounts | Ext | Core | Yes (phase 5): up to five codes, product, order and shipping classes | PD1 |
| Codes applied from a link | Ext | Core | Yes (phase 5) | PD1 |
| Generated single-use codes in bulk | Ext | Core | Yes (phase 5) | PD1 |
| Gift cards, bought and redeemed, with balances | Ext | Core | Yes (phase 5) | PD2 |
| Store credit | Ext | Core | Yes (phase 5), including refunds as store credit | PD2 |
| Loyalty points and rewards | Ext | App | Yes (phase 5): points earned and spent; no tiers or perks | PD3 |
| Referral and affiliate programs | Ext | App | Partial (phase 5): customer referrals; no affiliate commissions | PD3 |

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
| Sales by location and channel: every order records where and how it was sold, and reports split by them | Ext | Core | No: only a pickup order records its shop | PI2 |
| Registers, shifts and cash counts per location, staff limited to their locations | Ext | Core | No | PI2 |
| Selling on Amazon, eBay, TikTok and social shops | Ext | Core (channels) | No | PI1 |
| Bookings and appointments: time slots with staff or resources | Ext | App | No | PL1 |
| Travel packages and tours: fixed departures with seats per departure, a base package with package options | Ext | App | Partial: a "Departure" variant option with stock per variant | PL1 |
| Occupancy pricing: per person, a double room counting two seats, single supplements, child prices | Ext | App | No | PL1 |
| Nightly stays: check-in and check-out ranges, rooms per night, seasonal and weekend rates, minimum stays | Ext | App | No | PL1 |
| Search by date: products with availability for the dates a shopper picks | Ext | App | No | PL1 |
| Details per traveler or attendee (names, birth dates, passports), asked once per unit bought | Ext | App | Partial: checkout details once per order | PL1 |
| Deposits and payment schedules on orders: pay a deposit or in full, the rest due on dates counted from the event, reminders, saved-card charges, late fees, cancel when overdue | Ext (Deposits) | App | Partial: installment plans settle ledger balances, not cart orders | PH1, PL1 |
| Cancellation policies: tiered fees by how long before the event, feeding refunds | Ext | App | No | PL1 |
| Add-on services priced per traveler, per booking or as a percent of the booking (insurance, transfers, a car) | Ext | App | Partial: add-ons priced per unit | PL1 |
| Request to book: a booking the store confirms before the buyer pays | Ext | App | Partial: inquiry sales mode, Pay Later | PL1 |
| A fee for a payment method (a card surcharge), where the law allows it | Ext | No | No | PL1 |
| Flights and hotel rooms sold from a supplier's inventory (GDS or travel APIs) | Ext | App | No | PL3 |
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

## Bookings and travel: what it takes

A travel package page, such as a tour with fixed departures, shows what the catalog can't do yet: the buyer picks a
departure and a room, pays per person with a single supplement, adds insurance, and pays a deposit now and the rest
on dates counted back from the departure, under a cancellation policy that grows with the date. Bolting each of
these onto products would leave hotels, appointments and rentals to start again. Three foundations make them one
model, and the Bookings module sits on them.

**F1. Line details and line handlers.** A cart line carries a typed bag of details beside the product: the dates,
the travelers and their occupancy, the add-ons chosen. Modules register handlers that check the details when the
line is added, price them through the price resolver chain (the snapshot context gets the details), describe them on
the cart, order, invoice and emails, and copy them onto the order line. Two lines of the same product with other
details stay apart. Product add-ons (phase 3) move onto this as its first handler, so the path is proved before
bookings use it.

**F2. Dated capacity with holds.** Availability keyed by product and *slot*, where a slot is a departure, a night or
a time: seats per departure, rooms per night, a staff member's hour. It uses the inventory's hold, sell, release and
restock pattern, so an unpaid checkout holds the seats and an expired one gives them back, and the stock questions
the cart, product pages and listings already ask get slot answers. Search by date asks it for the dates a shopper
picks.

**F3. Order payment schedules.** A product's payment terms (a deposit as a percent or an amount; the rest due on
dates counted from a date on the line, such as 150 and 45 days before departure; a late fee; cancel when this
overdue) become a schedule on the order when it's placed. Checkout offers Pay in full or Pay a deposit and charges
only what's due now. The rest is an outstanding balance on the transactions ledger with pay links, reminders and,
with the buyer's consent, charges to their saved card, through the installment engine generalized from ledger
balances to orders. The order reads Payment due until it's paid. The same schedules are B2B's deposits and net
terms, so F3 is built in phase 8 (PH1) and reused here.

**The Bookings module (PL1)** then adds:

- Booking products of three kinds: fixed departures (tours, events, classes), nightly stays (rooms, rentals) and
  time slots (appointments), each with its capacity in F2 slots.
- Occupancy pricing: per person, units that take several seats, supplements and child prices, as price resolvers.
- A date picker on the product page and a date search on listings.
- Per-traveler forms, repeated for each unit bought, from a content type the store edits like checkout details.
- Add-ons priced per traveler, per booking or as a percent of the booking.
- Cancellation policies whose tiers suggest the refund when a booking is canceled.
- Request to book: the store confirms, then the buyer gets a pay link.
- A payment-method fee, where the law allows it, as a line the invoice adds for that method.
- Booking emails and a calendar view of what's booked.

**Supplier inventory (PL3).** Flights and most hotel rooms aren't the store's stock; they're sold from a supplier's
(Amadeus, Duffel, hotel channel managers). That's an integration that searches, holds and tickets through the
supplier's API, built on F1 and F3 once a supplier is chosen (P-D8).

## Verticals: the foundations they share

Stores ask for more than retail: rentals, events, directories, marketplaces, restaurants, courses, real estate,
memberships, gig work, automotive, wellness, software, donations, procurement, venues, creative work and
communities. Orchard Core's content model already shapes any catalog, and the checkout engine already sells more than
cart orders (ledger balances, subscriptions, whole sites). What each vertical lacks comes down to a few foundations,
each reused many times, so they're built once rather than per vertical.

| Foundation | What it is | Verticals it serves | Where |
| --- | --- | --- | --- |
| F1 Line details | Typed details on a cart line, with handlers that check, price, describe and copy them | Every configured purchase: personalization, bookings, tickets, food orders, quotes | PL0 |
| F2 Dated capacity | Capacity per product and slot (a departure, a night, a time) with holds | Rentals and leasing, events, venues and facilities, classes, appointments, restaurant reservations and order-ahead slots, test drives | PL0 |
| F3 Order payment schedules | A deposit now, the rest on dates, on the ledger, with reminders and saved-card charges | Travel, rentals and leases, tuition plans, real estate deposits, B2B net terms, big-ticket items | PH1 |
| F4 Multi-party money | Vendors, commissions, payouts (Stripe Connect), escrow released on milestones or delivery, disputes between parties | Marketplaces, freelance and gig platforms, creative and specialty marketplaces, venue hosts | PM1 |
| F5 Listings people post | Front-end submission of content, moderation, expiry and renewal, paid upgrades (featured, bumped), map and radius search | Directories, classifieds, real estate, automotive, job boards, community listings | PO1 |
| F6 Tickets and attendees | One ticket per unit with its attendee, a QR code, check-in, transfer and resale rules | Events and experiences, classes and courses, conferences | PL1 |
| F7 Fulfillment per product type | Fulfillment flows beyond shipping: services with their own status (CC-D5), kitchen and pickup status, rentals out and returned with damage charges | Services, restaurants and food, rentals, repairs, healthcare packages | PL1 |

How each vertical lands on them, and what stays an integration:

| Vertical | Today | Builds on | Integration, not built |
| --- | --- | --- | --- |
| Software and digital services | Subscriptions, whole sites sold and provisioned per tenant, downloads | Usage-based billing on the AI usage metering | |
| Membership and subscriptions | Recurring plans, installments, Content Access Control | PL2 purchase-gated access | |
| Donations and fundraising | Buyer-chosen amounts, recurring giving through subscriptions | PO2 campaigns with goals and progress, tax receipts, tributes, Gift Aid | |
| Directories, classifieds, real estate | Faceted listings, listed-only products, inquiry mode, CRM leads and opportunities | F5, map search | Property feeds (MLS/IDX) |
| Automotive and transportation | Listings, inquiry, CRM, contact center and dialer | F5, F2 test drives, F3 deposits | Vehicle data (VIN decoding) |
| Rentals, leasing, venues and facilities | Variants and stock as a stand-in | F2, F3, F7 (deposits held and returned, damage charges) | |
| Events, tickets and experiences | Variants per date with stock as seats | F2, F6 | |
| Education and training | Subscriptions, installments, downloads | F2 classes, F6 enrollment, F3 tuition plans | Learning platforms for courses and progress |
| Healthcare and wellness | Contact center; appointments with F2 | F2, F3 packages, intake forms (Orchard Forms) | Clinical records. Commerce records keep no medical data, and no compliance (such as HIPAA) is claimed |
| Restaurants and food | Products, add-ons, local pickup (PF3) | F1, F2 order-ahead slots, F7 kitchen status, delivery zones, tips | Delivery networks, kitchen printers |
| Marketplaces, freelance, gig, creative | None multi-party | F4, F5 vendor storefronts, F7 service delivery | |
| B2B procurement, supplier catalogs | Business customers, exemption certificates, Pay Later | Phase 8, approval workflows and budgets (PH3) | Punchout (cXML, OCI) to buyers' procurement systems (PH3) |
| Travel and hospitality | Departures as variants | F1, F2, F3, PL1 | Flights and supplier hotels (PL3, P-D8) |
| Community and activity platforms | Users, content, notifications, omnichannel messaging | Commerce joins through memberships, events and payouts | Groups, feeds and member messaging aren't commerce |

## Delivery plan

Ordered by how much a store owner notices the gap. Each slice is a commit on one branch per phase and one PR per
phase, verified on a test site in the browser at every Bootstrap breakpoint before it is opened.

| Phase | Slices | Exit gate |
| --- | --- | --- |
| **1. Accounts and conversion** (built) | PA1 account pages, passwordless sign-in; PE1 abandoned checkout emails; PE2 one-page option, custom fields, account after checkout, mini cart | A guest who leaves at payment gets a recovery email, comes back, pays on one page, creates an account, and finds the order and downloads on their account page on a phone. |
| **2. Payments** (built, but PayPal, Square, PB2) | PB1 PayPal, a second card gateway, bank transfer and cash on delivery, saved cards for one-time purchases; PB2 express buttons on product and cart pages, buy now pay later; PB3 fraud scoring, disputes on orders | One store takes PayPal, Stripe and bank transfer; a disputed Stripe payment shows on its order and in a report. |
| **3. Merchandising** (built) | PC1 scheduled sales, upsells, cross-sells, related, recently viewed, order bumps and post-purchase offers; PC2 grouped, external, bundles, product options; PC3 quantity rules, tier and group pricing, comparison; PC4 CSV import and export, migration importers | A bundle with a personalized part sells at a tier price to a wholesale group, and a WooCommerce product export imports with its variants. |
| **4. Fulfillment** (built, but return labels) | PF1 manual and draft orders, order editing, packing slips; PF2 self-service returns and exchanges; PF3 local pickup and delivery; PF4 carrier rates, labels, tracking page; PF5 locations, transfers, purchase orders; PF6 back-in-stock, pre-orders | A buyer returns one item from their account, the store approves it, prints a return label, and the refund and restock land at the right location. |
| **5. Promotions and loyalty** (built) | PD1 automatic and buy-X-get-Y discounts, combining rules, link codes, bulk codes; PD2 gift cards and store credit; PD3 loyalty points, referrals | A gift card pays part of an order that also gets an automatic discount and a code, under the store's combining rules. |
| **6. Headless API and point of sale** (pulled forward) | PA2 storefront and admin JSON APIs on Orchard Core's OpenAPI module, outgoing webhooks; PI2 sales channel and location on orders, reports by location, registers and shifts, split and cash tender, card readers, receipts | A phone app signed in as a cashier at one of several shops sells an item in person, taking part in cash and the rest on a card reader; the stock comes off that shop, the order shows the shop, register and cashier, and the day's sales report splits by shop. |
| **7. Markets** | PG1 shopper currency and market prices, address autocomplete; PG2 translated catalog, emails and checkout; PG3 duties, OSS, tax connectors | A shopper in Germany sees euros and German, pays VAT included, and the order shows in the OSS report. |
| **8. B2B** | PH1 companies, buyers, payment terms on F3 order payment schedules; PH2 quotes, PO numbers, company price lists; PH3 approval workflows, budgets, punchout | A company buyer requests a quote, the store converts it, and the company pays a deposit, then the rest on net-30 terms, with its PO number on the invoice. |
| **9. Marketing and analytics** | PJ1 editable emails, review requests with photos; PJ2 campaigns, segments; PJ3 wishlists, automation templates; PK1 cohorts, dashboards, scheduled reports, alerts | A segment of lapsed buyers gets a win-back campaign, and the owner gets a weekly sales report by email. |
| **10. Channels and POS** | PI1 Google and Meta feeds, marketplace channels; PI2 point of sale | A product sold in person updates stock online, and the product feed passes Google Merchant Center validation. |
| **11. Other models** | PL0 F1 line details and handlers, F2 dated capacity (add-ons move onto F1); PL1 bookings: departures, nightly stays, time slots, occupancy pricing, date search, traveler forms, cancellation policies, request to book, payment-method fees, services, rentals; PL2 memberships; PL3 supplier inventory (flights, hotel rooms) | A tour departure sells its last two seats as a double room with insurance on a 15% deposit, the balance is charged to the saved card 45 days before departure, and a cancellation refunds by the policy; a booked appointment is paid, confirmed and completed; a membership purchase unlocks gated content. |
| **12. Marketplace and listings** | PM1 F4 vendors, commissions, payouts, escrow and milestones; PO1 F5 listings people post, moderation, expiry, paid upgrades, map search; PO2 fundraising campaigns, tax receipts | A vendor's product sells; the store keeps its commission and the vendor is paid out through Stripe Connect; a visitor posts a listing, pays to feature it, and it expires after its term. |
| **13. Setup and ecosystem** | PN1 setup guide, commerce theme, staff presets; PN2 extension guide | A new owner goes from a blank site to a first test sale through the guide without reading the docs. |

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
- **Address autocomplete** to phase 7 (PG1): it needs a third-party places provider, chosen with the market work. Address forms already carry the browser autofill attributes.

## Phase 2 as built

One branch, a commit per slice:

- **PB1 bank transfer and cash on delivery.** Offline methods in the Pay Later feature (now **Settings > Commerce > Offline payments**), each recording an outstanding balance the store settles with **Record payment** from the order's page. Cash on delivery only for orders that are delivered. Providers can rule themselves out of a checkout and describe themselves; the confirmation says an order paid offline is placed, not paid.
- **PB1 saved cards for one-time purchases.** Stripe keeps one customer per user; buyers keep a card at checkout, pay with it next time, and remove it under **Payment methods** in their account.
- **PB3 disputes on orders.** Stripe's dispute webhooks are recorded on the order, with a badge in the list, a box on the page, notes, and a **Disputes** report.
- **PB3 fraud scoring.** Stripe Radar's rating is kept on the order, with a warning and a **Risky** badge for elevated and highest risk.

Not built yet, and why:

- **PayPal and Square** (PB1, P-D6). They need sandbox accounts to build against and verify; ready to start once the store has them.
- **Express buttons on product and cart pages, and buy now pay later** (PB2). Apple Pay and Google Pay need a verified domain and a real device to test, and buy now pay later moves Stripe to its Payment Element with redirects. The checkout already offers Apple Pay and Google Pay on its payment step.

## Phase 3 as built

One branch, a commit per slice:

- **PC1 scheduled sales.** A sale price per product and variant with optional start and end, in the site's time zone; listings switch within a minute.
- **PC1 upsells, cross-sells, related, recently viewed.** A **Product Recommendations** feature: pickers on the product editor, strips on the product page, *Frequently bought together* in the cart.
- **PC1 order bumps and post-purchase offers.** An order bump on the payment step, set under **Settings > Commerce > Orders**, added to the order as a line; the confirmation offers the cross-sells of what was bought, added to a new cart.
- **PC2 external products.** A link to where the product is sold in place of *Add to cart*.
- **PC2 product options.** Add-ons a buyer types or ticks, each at an extra price per unit, through the cart, order, emails, packing and recovery.
- **PC2 grouped products.** A **Grouped Products** feature: one page, a quantity per product, one *Add to cart*.
- **PC2 bundles.** A **Product Bundles** feature: kits at their own price; stock from the parts, held, sold and restocked per part.
- **PC3 quantity rules, tier and group pricing.** Minimum, maximum and pack sizes; volume discounts; role discounts.
- **PC3 comparison.** A **Product Comparison** feature: up to four products side by side.
- **PC4 CSV import and export, migration importers.** Products import and export with Content Transfer (product, variant and stock columns; taxonomy terms by name), and Shopify's and WooCommerce's product exports are read as those columns; an orders CSV export from the Orders list. (A separate Product Import and Export page was built first, then folded into Content Transfer in phase 4.)
- **Storefront theming.** The account pages, guest order and pay pages, the mini cart, the comparison page and the confirmation's thanks were plain views; each is now a named shape a theme overrides in Razor or Liquid, and a test keeps new pages that way.
- **Found in testing.** Orders paid offline read *Paid*; they now read *Payment due* until paid, and the owed note no longer names Pay Later for every method.

Not built yet, and why:

- **File uploads as add-ons** (PC2). An uploaded file needs storage outside the media library, virus scanning and a retention rule before buyers can send one; it fits the F1 line details foundation (PL0).
- **Image import** (PC4). Downloading images from the addresses in an export means fetching from servers a file names, which needs an allow-list and size limits to be safe; product photos are added in the media library.

## Phase 4 as built

One branch, a commit per slice:

- **PF1 printable invoices and packing slips.** An order's **Invoice** and **Packing slip** pages print or save as PDF, with the store's header from the Receipts settings; each shipment prints its own slip with its lines and number. Buyers print their invoice from their account and from a guest's order link.
- **PF1 manual and draft orders.** **Create order** on the Orders list puts an order together for a buyer at their own prices (`DraftOrderService`), kept as a draft with an `Order.Invoice`; **Send invoice** emails it with a signed pay link, and shipping and tax are added at checkout. A sent draft shows on the buyer's order page and account until paid, isn't swept as abandoned, and goes back to a draft when its checkout fails, so the link pays it again.
- **PF1 pay link.** The order's admin page shows a draft's **Pay link**, which pays it whether or not the invoice was emailed, for a store that sends it by hand or has no email set up.
- **PF1 order editing, as built.** Drafts are edited before they're paid (`IOrderService.ReplaceLinesAsync`, with `IOrderEventHandler.LinesChangedAsync`, where Inventory holds the new lines or refuses the change). A paid order isn't edited: **Add items** makes a second order for the same buyer to pay, linked to the first, and removing items is done through Returns or a refund.
- **PF2 self-service returns and exchanges.** A **Returns** feature: buyers ask from their account or a guest link, within a window that runs from payment, with a reason per item and a refund or an exchange; the store approves or declines, receives with what's restocked and at which location (`IOrderEventHandler.ReturnedAsync`, `IInventoryService.RestockAsync(lines)`), then refunds through the order's refund or closes the exchange. Each step is emailed and noted on the order. Exchanges are closed by staff after they send the replacement: no replacement order is made. Staff-started returns weren't added; refunds and restocking a whole order stay on the order's page.
- **PF3 local pickup and delivery.** Pickup locations in the Shipping settings, offered on the Shipping step instead of an address, taxed at the location, with pickup wording through fulfillment and a *ready for pickup* email. A **Local delivery** method by postal code (prefix wildcards), with a fee, free-from subtotal and minimum order.
- **PF4 carrier rates, labels, tracking page.** Aggregator first, as P-D3's default: `ICarrierProvider` in Shipping, a **Carrier rates** method (carriers, cheapest services, markup, fallback amount, ten-minute quote cache) and an **EasyPost** feature that quotes, buys a shipment's label (**Buy label**, which ships it and emails the tracking) and applies its signed tracking webhook, delivering the shipment when the parcel arrives. Labels and checkout quotes use the store's ship-from address and default box from the Shipping settings. A **Track your order** page finds a paid order by number and email. Carrier providers follow Orchard Core's SMS and email providers: a **Providers** tab on **Settings > Commerce > Shipping** chooses the default one among the enabled providers (`ICarrierProviderResolver`), each provider feature adds its own tab with an enable switch, and EasyPost registers its tracking webhook itself, with a secret it makes, once the site has a public base URL. Verified against an EasyPost test account: USPS and FedEx rates at checkout with a markup, and a USPS label bought from a shipment.
- **PF5 locations and transfers.** Inventory locations in its settings; a stock item keeps units per location (`StockItem.Levels`, totals unchanged, older items at the default location), with receive, adjust and count per location and transfers between them. Orders take an item from one location that has it all, in the listed order; a pickup order prefers its shop's stock location (`OrderShipping.LocationId`, through `IFulfillmentLocationProvider`). Availability adds every location together.
- **PF5 purchase orders.** A **Purchase Orders** feature: draft, ordered, partly received, received or canceled, optionally started from a location's low-stock items, received at its location as `Received` movements with the order's number. The supplier isn't emailed.
- **PF6 back-in-stock.** A **Back-in-Stock Alerts** feature: an email form on a sold-out product's page (per variant), one email per request when the item comes back, limits per email and per visitor, and the requests in the personal data export and erasure.
- **PF6 pre-orders.** A **Ships from** date on the Inventory part makes a backorder a pre-order until then (`ProductAvailabilityStatus.Preorder`): **Pre-order** on the button, and the date on the cart, mini cart and order lines.
- **Moved in from phase 3.** The Product Import and Export page was folded into Content Transfer: product, variant and stock columns, Shopify and WooCommerce exports read as those columns, and duplicate matching by SKU (`ProductImportRowFilter` over a new `ProductSkuIndex`).

The exit gate holds except for the return label: the buyer returns an item from their account, the store approves it and the buyer is sent the return instructions, and the refund and restock land at the chosen location.

Not built yet, and why:

- **Return labels** (PF2, PF4). A label for the buyer to send items back needs the return's parcel and the buyer's address as the sender; it fits on `ICarrierProvider`, now that a test account has verified outbound labels.
- **Several parcels per shipment, and package presets.** A label is one parcel in the default box with the weight staff enter; carts are quoted the same way.

## Phase 5 as built

One branch, a commit per slice:

- **PD1 several codes and combining rules.** A checkout carries up to five codes. Every discount is a product, order or shipping discount and says which kinds it combines with (`DiscountLine.Class`, `CombinesWith`); when they can't all be used, `DefaultCheckoutDiscountService.Choose` keeps the compatible set that takes off the most, and the others show what they gave way to. A coupon's editor has **Combines with**.
- **PD1 automatic discounts, buy X get Y and minimum quantities.** A coupon can apply by itself (`CouponMethod.Automatic`, found through `CouponIndex.IsAutomatic`), with its uses held and limited like a code's. A buy X get Y offer discounts the cheapest qualifying items for every so many bought, and its discount lands on those lines (`DiscountLine.LineShares`), so tax follows. Any coupon can require a minimum quantity.
- **PD1 discount links and generated codes.** `discount/{code}` keeps a code in a cookie for the next checkout. Unique codes are generated for a coupon as `CouponCode` documents with their own uses, behind `ICouponStore`, and downloaded as CSV; the coupon's own code can be turned off.
- **PD2 the balance seam.** `ICheckoutTenderProvider` lets balances pay after tax toward the one-time amount, with the payment provider charging the rest (or nothing: an order a balance covers completes without one, and its button reads **Place order**). `CheckoutTenderLedger` holds each balance under its lock while the buyer pays, takes it while the checkout completes, lets it go when it doesn't, and puts it back on a refund after the card. A payment begun for an older total is set aside once the provider confirms nothing was paid.
- **PD2 gift cards.** A Gift Cards feature in Orders: a Gift card product type that issues and emails a card per unit bought (off while a bank transfer is owed, turned off with a canceled or fully refunded order), cards issued by staff, the gift card list, each card's balance changes and activity, and the cards on orders, invoices and emails.
- **PD2 store credit.** A Store Credit feature in Customers: credit per customer and currency, given by staff or as an order's refund (**Give it as store credit**), spent by a signed-in buyer at checkout, and shown on their account. The customer's admin page gained a display hook (`DetailAdmin`) other features add to.
- **PD3 loyalty points.** A Loyalty Points feature: points on goods after discounts (not shipping, tax, or what points paid), taken back with cancels and refunds, spent at checkout at what a point is worth, under **Settings > Commerce > Rewards**, with a Rewards account page.
- **PD3 referrals.** A Referrals feature: a link per customer, a single-use code of the store's chosen coupon for the friend, and store credit or points for the customer on the friend's first order, taken back if it's canceled.

The exit gate holds: a gift card pays part of an order that also has an automatic buy X get Y discount and a free-shipping code under the combining rules, with Stripe charging the rest, verified on the test site with a test card, and refunded to the card first, then the gift card.

Not built yet, and why:

- **Loyalty tiers, perks and points expiry.** The program earns and spends; tiers and expiry wait for a store that asks.
- **Affiliate commissions.** Referrals reward customers with credit or points; paying affiliates money belongs with the marketplace payouts in phase 12.
- **Rewards for orders paid later.** Points and referral rewards are given when an order is placed, including one paid by bank transfer, and taken back if it's canceled; waiting for the money needs an event when an offline payment arrives.

## Phase 6 as built

One branch, a commit per slice:

- **PA2 the API's foundation.** The commerce JSON API is minimal API endpoints under `api/commerce/v1`, which Orchard Core's OpenAPI module documents (it reads minimal APIs through `Microsoft.AspNetCore.OpenApi`; MVC controllers aren't needed). `CommerceApi.MapCommerceApi` gives each module's group the `Api` bearer scheme, no antiforgery and a tag; guests reach the endpoints marked `AllowGuests`, their cart and checkout carried in `X-Cart-Token` and `X-Checkout-Token` headers by `GuestSessionTokenManager` instead of cookies. Money, paging and RFC 9457 problems (with a `code` on conflicts and camelCase field keys from model state) are shared, and `IdempotencyEndpointFilter` replays a final answer, its guest-token headers and `Location` included, for 24 hours per caller, route and key.
- **PA2 the step services.** What each checkout step's driver did is in a service the driver and the API both call: `CartCheckoutService`, `CustomerContactStepService`, `ShippingCheckoutStepService`, `CheckoutCouponService`, `GiftCardCheckoutService`, `CheckoutTenderChoices`.
- **PA2a storefront API.** One API feature per module: the catalog priced for the caller with variants, add-ons, specifications and stock; per-location availability; the cart; the checkout (start from the cart, contact, pickup locations, shipping rates and the step, codes, gift cards, store credit, points, payment methods with what a client SDK needs through `ICheckoutPaymentClientConfigurationProvider`, payment, completion with the order number); the signed-in buyer's account, addresses, orders, returns and shipments. `CheckoutApi.Read` gives every module one checkout shape.
- **PA2b admin API.** Orders (list with filters, by number, refund, cancel), fulfillment (what's left to ship, ship, deliver), customers, stock (receive, adjust, count, transfer, unknown locations refused) and coupons (held to the coupon admin's rules through `CouponEditor`).
- **PA2c outgoing webhooks.** A Commerce Webhooks module: subscriptions to order, shipment, customer and stock events that modules publish through `ICommerceWebhookPublisher`, each payload in the API's shapes, signed with HMAC-SHA256 over the timestamp and body, sent after the request is saved and retried with backoff for about two days, with a deliveries log, a test event and resend. Redirects aren't followed and internal hosts are refused.
- **PI2a sales channel and location.** Every order has a channel (online, point of sale, draft; orders from before channels count as online), and register sales also the store's location, the register, shift and staff (`Order.Channel`, `LocationId`, `RegisterId`, `ShiftId`, `StaffUserId`, indexed). Stock is held and sold at the order's location. The orders list, its export and the admin API filter by channel; the admin API also by location.
- **PI2b registers and shifts.** A Point of Sale module: registers at each store with its address and the roles that may sell there, shifts opened with a float, cash put in and taken out, and closed with a count against what the drawer should hold, summed from the shift's orders rather than kept on the shift. Admin lists of registers and shifts.
- **PI2c tender at the till.** A register sale is an order whose checkout skips contact and shipping and is taxed by the store's address. Cash is a tender whose reference is what was handed over, so the change falls out; the rest is paid on the store's own terminal (recorded) or on a Stripe Terminal reader the server tells to collect (server-driven, tested with a simulated reader); gift cards and codes go on the sale's checkout. The staff member owns the checkout, so their own store credit and points aren't spent on it (`CheckoutCounterSale`).
- **PI2d receipts and returns.** Receipts as data for the till app, as HTML from the Receipts settings to print, and by email; returns at the till refund through the order's operations, put stock back at the store and take cash refunds out of the drawer.
- **Tests.** A commerce integration suite starts a real host with the store sample and drives the API over HTTP: catalog, guest carts and checkouts kept apart by their tokens, a checkout paid with the test provider, idempotent retries, a signed webhook delivered to a local listener, and staff endpoints refusing guests. It runs in CI beside the tenant hierarchy suite.

The exit gate holds: a headless client (a script over the API) buys as a guest with Stripe test mode and with bank transfer, a buyer reads their orders with a bearer token, staff manage orders, stock and coupons with a client-credentials token, a webhook receiver verifies every signature, and a till opens a shift, sells for cash with change, by split cash and card and on a simulated Stripe reader, returns a sale with cash back, and closes the shift with its count.

Not built yet, and why:

- **The store's own Checkout details fields over the API.** The Contact step's endpoint takes the contact details only; collecting a store's custom content fields needs the editor pipeline driven from JSON.
- **Store credit and points at a register.** The register's checkout belongs to the staff member; spending a customer's balances there needs the customer's consent at the till.
- **Offline selling (P-D10).** The API takes orders as they happen; accepting ones made earlier needs stock reconciled when they arrive.
- **A headless storefront starter.** The API and its OpenAPI document are what a starter builds on; a sample app is its own repository.

## Headless API and point of sale (phase 6, planned)

A store may run the site headless, with its own storefront or a phone app as a point of sale at several shops. Phase 6 builds what that needs, after phase 5 adds the split payments, gift cards and store credit a till needs.

**The API is built on Orchard Core's OpenAPI module** (`OrchardCore.OpenApi`, in the app targets we use), not beside it. The module documents the site's endpoints in Swagger, ReDoc and Scalar from `Microsoft.AspNetCore.OpenApi`, which reads minimal APIs, and clients are generated from its document with NSwag. Calls authenticate with the `Api` scheme: bearer tokens from the tenant's OpenID server (authorization code with PKCE for apps and the docs explorers, client credentials for servers). So each commerce module maps minimal API endpoints:

- A route group under `api/commerce/v1` with the `Api` scheme, typed request and response records, `WithName`, `WithSummary`, `ProducesProblem`, and `ProblemDetails` for errors, so the explorers and the generated clients are complete.
- Permission checks are the same as the admin and storefront pages (`IAuthorizationService` with the existing permissions), plus new ones only where a till needs them (for example **Sell at a register**).
- The endpoints call the same services as the pages (`ICartService`, `ICheckoutEngine`, `ICustomerService`, `IOrderStore`, `IInventoryService`), so rules such as stock holds, coupon holds and tax stay in one place.

The slices:

- **PA2a storefront API.** Catalog (products, variants, prices for a customer, availability per location), carts, discounts and codes, checkout (contact, shipping rates, payment with a client-side token, or a pay link), customers and their addresses, orders and returns for the signed-in buyer.
- **PA2b admin API.** Orders (list, detail, fulfill, refund, cancel), customers, products and stock (receive, adjust, transfer), coupons, with paging and filters.
- **PA2c outgoing webhooks.** Subscriptions to order, customer, product and stock events, signed with a per-subscription secret, retried with backoff, and a delivery log, reusing the workflow events that exist.
- **PI2a sales channel and location.** `Order.Channel` (online, point of sale, draft, marketplace later), `Order.LocationId`, `RegisterId` and `StaffUserId`, indexed; the stock comes off the order's location; reports gain location and channel filters and groupings.
- **PI2b registers and shifts.** Registers per location; opening and closing a shift with its float and counted cash; cash in and out; staff limited to their locations by role.
- **PI2c tender at the till.** Several payments on one order (cash with change given, card, gift card, store credit) on the phase 5 tender seam; Stripe Terminal readers for in-person cards; tax from the shop's address.
- **PI2d receipts.** Printed and emailed receipts from the Receipts settings, and returns at the till against the original order.

Offline selling (P-D10) comes after: the API would accept orders made earlier with idempotency keys, and stock would reconcile when they arrive.

## Decisions needed

Until they are answered, the work goes ahead on the default in the last column.

| # | Question | Options | Default taken |
| --- | --- | --- | --- |
| P-D1 | CC-D5 put account pages, JSON APIs and service fulfillment out of scope. Are they in scope now? | In (phases 1, 6 and 11) / keep deferred | In |
| P-D2 | The original plan left marketplaces out. Is phase 12 in scope? | In / out / later | Later: phase 12 last |
| P-D3 | Carrier rates and labels: one aggregator (EasyPost or Shippo) or carriers one by one? | Aggregator first (fewer contracts, faster) / per carrier | Aggregator first |
| P-D4 | Email campaigns: build them, or integrate with Mailchimp or Klaviyo? | Integrate first / build on the omnichannel email channel | Integrate first |
| P-D5 | Point of sale: a web POS page, or a native app? | Web POS first / native | Answered: a native or mobile app over the API first; a web POS, if built, is one more client of the same API |
| P-D10 | Can the POS sell while offline? | Online only first / offline with sync | Answered: online only first; offline sales, which need the API to accept orders made earlier without overselling, come after |
| P-D6 | Second card gateway after PayPal | Square / Adyen / Authorize.net | Square |
| P-D7 | Shopper currency conversion: live exchange rates from a provider, or rates the store sets? | Store-set rates first / provider | Store-set rates first |
| P-D8 | Flights and supplier hotel rooms (PL3): which supplier API, or out of scope? | Duffel (flights) / Amadeus (flights and hotels) / out of scope | Later: after PL1, supplier chosen then |
| P-D9 | When should the bookings foundations (F1, F2) be built: in phase 11, or pulled forward because a store needs travel packages sooner? | Phase 11 / right after phase 3 | Phase 11, with F3 in phase 8 |
