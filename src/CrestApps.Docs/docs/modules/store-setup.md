---
sidebar_label: Store setup and theme
title: Store setup and the Storefront theme
description: "The store's setup guide and its IStoreSetupStep seam, the steps each commerce module adds, staff role presets, the Online store setup recipe, and the Storefront theme."
user_manual: user-manual/commerce/set-up-your-store
---

| | |
| --- | --- |
| **Features** | `CrestApps.OrchardCore.Commerce` (the guide), `CrestApps.OrchardCore.StorefrontTheme` (the theme) |
| **Category** | Commerce |
| **Seams it adds** | `IStoreSetupStep` |

**Commerce > Set up your store** (`commerce/setup` in the admin, permission `SetUpStore`) takes a new owner from a blank site to a first paid order. Each commerce module adds the steps about what it does, so a store sees only the steps of the features it has on. The screens are described in the [User Manual](../user-manual/commerce/set-up-your-store.md).

## Setup steps

`IStoreSetupStep` lives in `CrestApps.OrchardCore.Abstractions` (namespace `CrestApps.OrchardCore.Commerce`), so any module can add a step without depending on the Commerce module:

- `Name`: the step's unique name. `Order`: where it comes. The built-in steps are 10 apart.
- `DescribeAsync` returns a `StoreSetupStepDescription`:
  - What the step is for: `Title`, `Description`, `Status` and `IsOptional`.
  - Whether it's done: `IsDone`, which is `null` for a step that's done once its quick action has run.
  - The page where it's done in full: `Url`, an app path such as `~/Admin/shipping/zones`, and `UrlText`.
  - An optional quick action: its `Fields` (text, email, number, textarea or select) and its `ActionText`.
- `RunAsync` does the quick action with the posted field values (`StoreSetupActionContext.GetValue`) and returns a `StoreSetupActionResult`. The default implementation returns `null`, for a step without one.

The guide keeps the steps whose quick action succeeded and those the owner skipped in `StoreSetupSettings` (site settings). It opens the first step that's neither done nor skipped, the required ones first.

To add a step, register it in your module's startup:

```csharp
services.AddScoped<IStoreSetupStep, MyIntegrationSetupStep>();
```

## The built-in steps

| Order | Step | Module | Done when | Quick action |
| --- | --- | --- | --- | --- |
| 10 | `store-details` | Receipts | `ReceiptSettings` has a business name and contact email | Saves them, with the phone and address |
| 20 | `currency` | Checkout | Its quick action has run | Sets `CheckoutSettings.Currency` from the currency catalog |
| 30 | `stripe` | Stripe | `StripeOptions` has a secret and publishable key, from settings or configuration | |
| 35 | `offline-payments` (optional) | PayLater | Bank transfer or cash on delivery is on, with instructions | Turns bank transfers on with the instructions |
| 40 | `shipping` | Shipping | A zone has an enabled method | Adds an "Everywhere" zone (rest of world) with a flat rate |
| 50 | `taxes` | Taxation | A tax rule exists | |
| 60 | `products` | Products | A product type has a published item | Adds a `Product` type when there's no product type |
| 80 | `test-order` | Orders | A paid order exists | |
| 90 | `staff-roles` (optional) | Commerce, with Roles on | The four staff roles exist | Adds them |

## Staff role presets

The role names are `CommerceConstants.StaffRoles` (in `CrestApps.OrchardCore.Abstractions`): `StoreManager` ("Store manager"), `Warehouse`, `CustomerSupport` ("Customer support") and `Marketing`. `StaffRolePresets` gives each a description. The staff roles step only creates the missing roles, with `RoleManager<IRole>`. Their permissions come from the modules: each commerce module's `IPermissionProvider.GetDefaultStereotypes()` returns a `PermissionStereotype` for each staff role it grants something, and Orchard Core applies the stereotypes of every enabled feature when a role is created, and adds a feature's stereotypes to an existing role of that name when the feature is enabled for the first time. Existing roles aren't otherwise changed.

| Module | Store manager | Warehouse | Customer support | Marketing |
| --- | --- | --- | --- | --- |
| Commerce | `AccessAdminPanel`, `ListContent`, `ViewContent`, `EditContent`, `PublishContent`, `DeleteContent`, `ManageMediaContent` | `AccessAdminPanel` | `AccessAdminPanel` | `AccessAdminPanel`, `ListContent`, `ViewContent`, `EditContent`, `PublishContent`, `ManageMediaContent` |
| Orders | `ManageOrders`, `ManageGiftCards` | | `ManageOrders` | |
| Transactions | `ManageTransactions`, `ManageRefunds` | | `ManageRefunds` | |
| Inventory | `ManageInventory`, `ViewInventory` | `ManageInventory`, `ViewInventory` | `ViewInventory` | |
| Shipping | `ManageShipping`, `FulfillOrders` | `FulfillOrders` | | |
| Customers | `ManageCustomers`, `ViewCustomers` | | `ManageCustomers`, `ViewCustomers` | `ViewCustomers` |
| Checkout | `ManageCoupons` | | | `ManageCoupons` |
| Marketing | `ManageMarketing` | | | `ManageMarketing` |
| Product Reviews | `ManageProductReviews` | | | `ManageProductReviews` |
| Product feeds | `ManageProductFeeds` | | | `ManageProductFeeds` |
| Subscriptions | `ManageSubscriptions` | | `ManageSubscriptions` | |
| Bookings | `ManageBookings` | | `ManageBookings` | |
| Memberships | `ManageMemberships` | | `ManageMemberships` | |
| Digital Downloads | `ManageDigitalDownloads` | | | |
| Fundraising | `ManageFundraising` | | | |
| Classifieds | `ModerateClassifieds` | | | |
| Marketplace | `ManageMarketplace` | | | |
| Point of Sale | `ManageRegisters`, `SellAtRegister` | | | |
| Reports | `ViewAllReportDesigns` | | | `ViewAllReportDesigns` |

A module that adds a staff permission declares it in its own permission provider, against the `CommerceConstants.StaffRoles` name.

## The Online store setup recipe

`online-store.recipe.json` in the Commerce module is a setup recipe (`issetuprecipe`). It does the following:

- Enables the content features, the commerce features (products and listings, carts, checkout, orders, customers, transactions, receipts, inventory, shipping, taxation, Stripe and offline payments), TheAdmin and the Storefront theme.
- Defines a `Product` type: title, autoroute under `products/`, the Product part with its details shown on its page, an `Images` media field, a description, shipping and SEO.
- Creates a product listing at `/shop` as the home page, and a main menu.

## The Storefront theme

`CrestApps.OrchardCore.StorefrontTheme` has TheTheme as its base theme, so TheTheme's shapes and templates carry over. It overrides `Layout` with the following:

- A sticky header with the store's name or logo, a search box that sends `q` to the home page's listing, and the `Navbar` shape (the cart link, the market picker and the user menu).
- The `alias:main-menu` menu.
- The `Title` zone, where account and admin-style pages put their breadcrumb.
- The `Header`, `Messages` and `Footer` zones.
- A footer with the business name, address, email and phone from the typed `ReceiptSettings` of `CrestApps.OrchardCore.Receipts.Core`, which the theme references; the Receipts module itself doesn't have to be enabled.

`wwwroot/styles/storefront.css` styles it with Bootstrap variables, so a child theme can restyle it by overriding `--storefront-accent`, `--storefront-header-bg` and `--storefront-footer-bg`. To change a commerce page, override its shape, as described in [Theming the storefront](commerce.md#theming-the-storefront).
