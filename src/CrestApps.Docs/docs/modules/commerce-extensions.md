---
sidebar_label: Extending commerce
title: Extending commerce
description: "A guide for developers who extend the commerce modules: which seam to implement for prices, cart lines, checkout, payments, tenders, orders, shipping, tax, markets, payouts and setup, how to register it, a worked example, and the traps to avoid."
---

The commerce modules are built so another module can add to them without changing them. Each extension point is an interface (a seam) that the modules call through every registered implementation. This page says which seam fits which job. The module pages have the details.

Register an implementation in your module's `Startup`, and depend on the feature that owns the seam in your manifest, so the seam's services are there.

## Which seam to use

### Products and prices

| To | Implement | Lives in | See |
| --- | --- | --- | --- |
| Give a buyer their own price, such as a company price list | `IBuyerPriceProvider` | Products.Core | [Companies](companies.md) |
| Say a product can't be bought here, with the reason shown in place of Add to cart | `IProductSaleRestriction` | Products.Core | [Markets](markets.md) |
| Show and convert prices in the shopper's currency | `IShopperCurrency` (one per site; replace the default) | Products.Core | [Markets](markets.md) |
| Say how many are available, such as from a supplier | `IProductAvailabilityProvider` | Products.Core | [Inventory](inventory.md) |
| Resolve the price of a product for a quantity | `IPriceResolver` | Products.Core | [Products](products.md) |
| Add a button or form to a product's card and page | A display driver placing a shape in the `ProductAction` display type | | [Commerce](commerce.md#theming-the-storefront) |

### Carts and checkout

| To | Implement | Lives in | See |
| --- | --- | --- | --- |
| Keep what the buyer chose with a cart line (a date, an amount, a listing) and price, limit or refuse the line by it | `ICartLineHandler` | Carts.Core | [Bookings](bookings.md), [Fundraising](fundraising.md) |
| Add an action to the cart page, such as Request a quote | `ICartActionProvider` | Carts.Core | [Companies](companies.md) |
| Follow a checkout through its life: started, loaded, completing, completed, failed, canceled or expired | `ICheckoutHandler` | Checkout.Abstractions | [Checkout](checkout.md) |
| Hold back the payment step until something is done, such as an approval | `ICheckoutPaymentGate` | Checkout.Abstractions | [Companies](companies.md) |
| Add a step or a panel to the checkout flow | A display driver for `CheckoutFlow` | Checkout | [Bookings](bookings.md) |
| Pay part of an order from a balance (a gift card, credit, terms, a deposit) | `ICheckoutTenderProvider` | Checkout.Abstractions | [Checkout](checkout.md) |
| Add a payment method | `ICheckoutPaymentProvider`, with `ICheckoutPaymentRefundProvider` to refund it | Checkout.Abstractions | [Payments](payments.md) |
| Offer a payment method only sometimes | `ICheckoutPaymentProviderAvailability` | Checkout.Abstractions | [Checkout](checkout.md) |
| Work out tax at checkout | `ICheckoutTaxService` | Checkout.Abstractions | [Taxation](taxation.md) |

### Orders, shipping and tax

| To | Implement | Lives in | See |
| --- | --- | --- | --- |
| React when an order is created, paid, canceled, refunded, returned or its lines change | `IOrderEventHandler` (derive from `OrderEventHandlerBase`) | Orders.Core | [Carts and orders](carts-and-orders.md) |
| Email a customer through the store's email templates and layout, in the language they shopped in | `ICommerceEmailSender`, with `AddCommerceEmailSender()`; register the email's kind with `AddCommerceEmailKind` so the store can write its template | Orders.Core | [Carts and orders](carts-and-orders.md#commerce-emails-and-their-templates) |
| Give the buyer what a paid order bought (files, a membership, a gift card) and take it back when the order is canceled or refunded in full | `OrderGrantHandlerBase`: implement `GrantAsync` and `RevokeAsync`, and register it as an `IOrderEventHandler`. After a partial refund the buyer keeps what they bought. | Orders.Core | [Downloads](downloads.md), [Memberships](memberships.md) |
| Add a shipping rate, such as a carrier's | `IShippingRateProvider` | Shipping.Core | [Shipping](shipping.md) |
| Buy labels and track parcels with a carrier | `ICarrierProvider` | Shipping.Core | [Shipping](shipping.md) |
| React when a shipment ships or is delivered | `IShipmentEventHandler` | Shipping.Core | [Shipping](shipping.md) |
| Add tax rules from another source | `ITaxRuleProvider`, and the other taxation seams | Taxation.Abstractions | [Taxation](taxation.md) |

### The rest of the store

| To | Implement | Lives in | See |
| --- | --- | --- | --- |
| Pay marketplace vendors another way | `IVendorPayoutProvider` | Marketplace | [Marketplace](marketplace.md) |
| Add a step to the store's setup guide | `IStoreSetupStep` | Abstractions | [Store setup](store-setup.md) |
| Send a signed, time-limited link, such as one that opens an order | `SignedTokenService` to sign and read the token for a purpose, and `SiteUrlGenerator` for the route's absolute URL from the site's base URL; register both with `services.AddSignedLinks()` | CrestApps.OrchardCore.Core | [Carts and orders](carts-and-orders.md) |
| Keep store configuration records (such as zones or tax rules) in a named catalog that recipes and deployment plans carry | `IAuditedModel` on the record; `AuditedCatalogEntryHandlerBase<T>` for its handler, which binds the recipe JSON and stamps `CreatedUtc`, `ModifiedUtc`, `OwnerId` and `Author`, so the handler only validates (check a unique name with `INamedCatalog<T>.IsNameUsedByAnotherEntryAsync`); `NamedCatalogRecipeStep<T>` for the recipe step and `NamedCatalogDeploymentSource<TStep, T>` for the deployment step, which read and write the same `{ "name": "...", "<Entries>": [ ... ] }` shape | CrestApps.OrchardCore.Core | [Taxation](taxation.md#recipes-and-deployment), [Shipping](shipping.md) |
| Store a secret in your settings, such as a provider's API key | `ProtectSetting(purpose, value)` on `IDataProtectionProvider` to save it, and `UnprotectSettingOrDefault(purpose, value, logger, name)` to read it, which logs a warning and treats a value the site's keys can't read as not entered (`UnprotectSetting` throws instead) | CrestApps.OrchardCore.Core | [Shipping](shipping.md), [Taxation](taxation.md) |
| Export and erase your module's records of a person | `ICommercePersonalDataContributor` | Customers.Abstractions | [Customers](customers.md) |
| Add a page to the storefront account | A `NamedNavigationProvider` created for the `CommerceConstants.Account.MenuName` menu, and a view rendering its content in the `CommerceAccountPage` shape | Abstractions | [Commerce](commerce.md) |
| Add a screen under Commerce | An `AdminNavigationProvider` adding under the Commerce node | | [Commerce](commerce.md) |
| Change how a storefront page looks | Override its shape in your theme | | [Commerce](commerce.md#theming-the-storefront) |

## A worked example: gift wrapping

A module that offers gift wrapping on any product needs to do three things:

- Keep the buyer's choice with the cart line.
- Charge for it.
- Tell the warehouse when the order is paid.

The cart line handler reads what the buyer posted with Add to cart, keeps it under a key named after the module, and prices it:

```csharp
internal sealed class GiftWrapLineHandler : ICartLineHandler
{
    private const string Detail = "giftwrap.message";

    public int Order => 200;

    public Task PrepareAsync(CartLinePrepareContext context, CancellationToken cancellationToken = default)
    {
        if (context.PostedDetails.TryGetValue(Detail, out var message) && !string.IsNullOrWhiteSpace(message))
        {
            context.Details[Detail] = message.Trim();
        }

        return Task.CompletedTask;
    }

    public async Task EvaluateAsync(CartLineEvaluateContext context, CancellationToken cancellationToken = default)
    {
        if (context.GetDetail(Detail) is not { } message)
        {
            return;
        }

        // 3.00 in the product's currency, converted into the line's.
        context.UnitPrice += await context.ConvertAsync(3m, cancellationToken);
        context.Details.Add(new CartLineDetail(Detail, "Gift wrapped", message));
    }
}
```

The details go onto the order line, where the order event handler finds them:

```csharp
internal sealed class GiftWrapOrderHandler : OrderEventHandlerBase
{
    private readonly IServiceProvider _serviceProvider;

    public GiftWrapOrderHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override async Task PaidAsync(OrderPaidContext context, CancellationToken cancellationToken = default)
    {
        // Resolved here, not in the constructor: see the traps below.
        var notes = _serviceProvider.GetRequiredService<IGiftWrapNotes>();

        foreach (var line in context.Order.Lines.Where(line => line.Details?.ContainsKey("giftwrap.message") == true))
        {
            await notes.AddAsync(context.Order, line, cancellationToken);
        }
    }
}
```

Register both, and add the message field to the product page with a driver for the `ProductAction` display type. The field must post as `details[giftwrap.message]` with the `form` attribute of the product's Add to cart form (`add-to-cart-{contentItemId}`):

```csharp
services.AddScoped<ICartLineHandler, GiftWrapLineHandler>();
services.AddScoped<IOrderEventHandler, GiftWrapOrderHandler>();
```

## Traps

- **Order event handlers and circular dependencies.** `IOrderService` is built with every `IOrderEventHandler`. A handler that takes a service needing `IOrderService` (directly or not) in its constructor makes a cycle. Resolve such services from `IServiceProvider` when the event happens. The same goes for a tender provider that needs the checkout session store.
- **Commits during a request.** Checkout and refunds commit the session part-way through a request. A document read before then and saved after is saved twice. Read it again by id after the payment or refund.
- **Amounts typed by buyers.** Read amounts that a form posts in the invariant culture. A German admin's culture reads "40.24" as 4024.
- **Line detail keys.** Prefix your keys with your module's name, such as `giftwrap.message`. Other handlers share the same dictionary.
- **Products chosen on their page.** A product whose line needs details can't be added from a card. Add your part's name to `CartOptions.ChooseOnProductPageParts` so cards link to the page instead.
- **Discounts.** A line that no discount may touch, such as a donation, sets `ExcludeFromDiscounts` in `EvaluateAsync`.
- **Admin lists.** List records with Orchard Core's `<admin-list>` and an `AdminListContext<T>`, with a display driver for both the `SummaryAdmin` and `SummaryAdminGrid` display types, so lists follow the admin's list or grid setting. Put the list's filter selects in its toolbar with `IShapeFactory.CreateAdminListFiltersAsync(listName, formId, filters)`, which builds the shared `CommerceAdminListFilters` shape; a theme overrides one list's filters with `CommerceAdminListFilters-{ListName}.cshtml`.
