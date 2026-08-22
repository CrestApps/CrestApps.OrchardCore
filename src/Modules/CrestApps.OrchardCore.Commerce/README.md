# CrestApps OrchardCore Commerce

The Commerce module registers the shared **Commerce** admin menu and its icon. Commerce-related modules
(such as Transactions and Taxation) contribute their own screens under this single top-level menu instead of
each creating their own copy of it.

## Why this module exists

Several modules add entries under a top-level **Commerce** menu. When each module declared the menu on its
own, the menu icon appeared only when a specific module happened to provide it, and it disappeared when that
module was disabled. This module owns the top-level node, its identifier, and its icon, so the menu always
renders consistently whenever any commerce-related feature is enabled.

## Feature

- **Commerce** (`CrestApps.OrchardCore.Commerce`) — Registers the Commerce admin menu and icon. The feature
  is **enabled by dependency only**; it offers no standalone functionality, so it is activated automatically
  when a module that depends on it is enabled.

## Contributing to the Commerce menu

To add screens under the Commerce menu from another module:

1. Add a dependency on the `CrestApps.OrchardCore.Commerce` feature in the module manifest.
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
