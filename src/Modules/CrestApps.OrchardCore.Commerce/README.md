# CrestApps OrchardCore Commerce

The Commerce module registers the shared **Commerce** admin menu and its icon, the storefront account and the other
pieces commerce modules share, and the store's setup guide. Commerce-related modules (such as Transactions and
Taxation) contribute their own screens under this single top-level menu instead of each creating their own copy of it.

## Why this module exists

Several modules add entries under a top-level **Commerce** menu. When each module declared the menu on its
own, the menu icon appeared only when a specific module happened to provide it, and it disappeared when that
module was disabled. This module owns the top-level node, its identifier, and its icon, so the menu always
renders consistently whenever any commerce-related feature is enabled.

## Features

Both features are **enabled by dependency only**: each is activated automatically when a module that depends on it
is enabled.

- **Commerce Foundation** (`CrestApps.OrchardCore.Commerce.Foundation`) — What billing and the store share: the
  Commerce admin menu and its icon, the storefront account (Account in the user menu), the commerce admin list helpers, customers'
  access to the admin pages of their own data, and the export and anonymization of a person's commerce data.
  Transactions, Taxation, Exchange Rates and Customers depend on it, so a site with Invoicing alone does not turn on
  the store.
- **Commerce** (`CrestApps.OrchardCore.Commerce`) — The store layer, on top of Commerce Foundation: the store's setup
  guide (Commerce > Set up your store), its permission and the roles it adds for a store's staff. The store modules
  (Products, Carts, Checkout, Orders, Shipping, Inventory and the others that sell) depend on it.

## Contributing to the Commerce menu

To add screens under the Commerce menu from another module:

1. Add a dependency on the `CrestApps.OrchardCore.Commerce.Foundation` feature in the module manifest (or on
   `CrestApps.OrchardCore.Commerce` when the module is part of a store).
2. In an `AdminNavigationProvider`, add children under the existing `S["Commerce"]` node. Do not set the node
   identifier or icon again; the Commerce module owns them.

```csharp
builder
    .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
        .Add(S["My Screen"], S["My Screen"].PrefixPosition(), item => item
            .Action("Index", "Admin", "My.Module")
            .Permission(MyPermissions.ManageThings)
            .LocalNav()
        )
    );
```

## Architectural boundary — Commerce is a thin orchestrator

Commerce is a **composition and orchestration shell**, not a domain. It owns the shared admin menu and
may later host cross-domain orchestration (feature profiles, shared policies, and commands that span
several domains), but it must never own domain data:

- Commerce **must not** define persistence: no YesSql indexes, index providers, data migrations, or
  domain stores.
- Commerce **must not** own the order, cart, customer, payment, tax, receipt, or report data models.
  Those belong to their reusable domain modules (Orders, Carts, Customers, Transactions, Taxation,
  Checkout, Receipts, Reports).
- Reusable domain commands live in their owning module. Commerce only **composes** them. Storefront and
  Admin surfaces are adapters over those contracts, never a place to put domain logic.

This boundary is enforced by `CommerceModuleBoundaryTests`, which fail the build if the Commerce assembly
references a domain persistence assembly (or YesSql) or defines a migration, index, or index provider.

## License

This project is licensed under the MIT License.
