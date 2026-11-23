---
sidebar_label: Taxation
sidebar_position: 15
title: Taxation Framework
description: A provider-agnostic, extensible taxation framework for Orchard Core. Sales Tax models tax as a determination rather than a stored rate, with order-wide thresholds for import duty, the EU VAT standard rates recipe, the TaxJar connector and the VAT one-stop-shop report. Income Tax works out taxes on income for any country, with the United States and Canada packs.
user_manual:
  - user-manual/commerce/taxes
  - user-manual/finance/tax-returns
---

| | |
| --- | --- |
| **Feature Name** | Sales Tax |
| **Feature ID** | `CrestApps.OrchardCore.Taxation` |
| **Category** | Commerce |
| **Other features** | Tax Tables, Income Tax, United States Income Tax, Canada Income Tax (see [Income tax](#income-tax)), TaxJar (`CrestApps.OrchardCore.Taxation.TaxJar`, see [TaxJar](#taxjar)), Tax Returns (`CrestApps.OrchardCore.Taxation.Returns`, see [Tax returns](#tax-returns)), Tax E-Filing (`CrestApps.OrchardCore.Taxation.EFiling`, see [E-filing](#e-filing)) |

Provides a provider-agnostic, extensible, international taxation framework that any Orchard Core content type or CrestApps module (Products, Subscriptions, Payments, Commerce, Bookings, Services, and custom content) can plug into.

## Overview

This video shows the taxation screens in the admin:

<YouTube video="taxation" title="Video overview of taxation" />

The framework is built on a single principle:

> **Tax is a determination, not a property.**

Instead of storing a `TaxRate` on a product, the engine determines the applicable taxes from a combination of *what* is sold, *who* buys it, *where* it is sourced, *when* the transaction happens, and *how* the tax is calculated. The result is a deterministic, auditable, line-by-line breakdown that can be captured as an immutable snapshot on the transaction.

The framework is split into three assemblies:

| Assembly | Purpose |
|----------|---------|
| `CrestApps.OrchardCore.Taxation.Abstractions` | Interfaces, models, DTOs, enums, and constants, including the `TaxationPart` and its `TaxationPartSettings`, so a module such as Products reads an item's classification as the typed part without depending on the Taxation module. Lightweight, no infrastructure dependencies. |
| `CrestApps.OrchardCore.Taxation.Core` | The determination engine, default calculation methods, sourcing strategies, resolvers, catalog stores, and dependency-injection wiring. |
| `CrestApps.OrchardCore.Taxation` | The Orchard Core module: the `TaxationPart` editors, its migration, and the content-item taxable-item provider, which prices an item from its `ProductPart.Price`. |

### Features

| Feature | Contents |
| --- | --- |
| **Tax Tables** (`CrestApps.OrchardCore.Taxation.Tables`) | Tax jurisdictions, tax tables and the calculation methods, under **Commerce > Taxation > Jurisdictions** and **Commerce > Taxation > Tables** (a site with Payroll and no store gets the same Commerce item with only Taxation in it), with their recipe and deployment steps. No Commerce dependency: Sales Tax and Income Tax share it (`AddTaxTablesCore()` registers the services). |
| **Sales Tax** (`CrestApps.OrchardCore.Taxation`) | Depends on Tax Tables only: categories, types, rules, the business's tax registrations (**Commerce > Taxation > Tax registrations**), the `TaxationPart`, the sales tax engine and tax origin settings. Commerce (orders, checkout, point of sale) uses it, and a site that keeps its own books uses it without a store: it no longer depends on Commerce Foundation, and its admin items build the **Commerce** node themselves. The id is the module's, kept from when the feature was named Taxation. |
| **Tax Returns** (`CrestApps.OrchardCore.Taxation.Returns`) | Depends on Sales Tax: a filing schedule on each registration, the periods it files for, and a return per period from the figures modules give. See [Tax returns](#tax-returns). |
| **Tax E-Filing** (`CrestApps.OrchardCore.Taxation.EFiling`) | Depends on Tax Returns: filing channels and file formats, a manual channel that hands over a file, and the task that asks channels for the agency's answers. See [E-filing](#e-filing). |
| **Income Tax** (`CrestApps.OrchardCore.Taxation.IncomeTax`) | Depends on Tax Tables only: income tax rules, the built-in income tax provider and its seam, tax categories, election forms and withholding methods. Payroll uses it; any module that pays income can. See [Income tax](#income-tax). |
| **United States Income Tax** (`CrestApps.OrchardCore.Taxation.IncomeTax.UnitedStates`) | Form W-4 and its withholding method, backup withholding, and the federal rules as dated recipes. |
| **Canada Income Tax** (`CrestApps.OrchardCore.Taxation.IncomeTax.Canada`) | The TD1 and TP-1015.3 claims, the CRA's and Revenu Québec's formulas, CPP, QPP, EI and QPIP, and the yearly figures as dated recipes. |
| **TaxJar** | See [TaxJar](#taxjar). |

A module whose records point at tax tables registers an `ITaxTableUsage`, so a table in use can't be deleted.

## Getting started

Enable the **Taxation** feature under **Tools → Features**, then:

1. Create your tax **categories**, **types**, **jurisdictions**, optional **tables**, and **rules** from the admin UI (see [Managing taxation from the admin UI](#managing-taxation-from-the-admin-ui)).
2. Attach the **Taxation** part to a content type and classify it (see [The TaxationPart](#the-taxationpart)).
3. At checkout, convert your objects into taxable items and call `ITaxService.CalculateAsync` (see [Calculating tax](#calculating-tax)).

## Managing taxation from the admin UI

Once the feature is enabled, an admin with the **Manage taxation** permission gets a **Commerce → Taxation** menu with five screens: **Categories**, **Types**, **Jurisdictions**, **Tables**, and **Sales tax rules** (and **Income tax rules** with Income Tax). Each screen is a searchable list with **Add**, **Edit**, and **Delete** actions, and is rendered through Orchard Core's display management so the list items and editors can be extended or overridden by other modules.

Setting up taxes end to end is a workflow of a few ordered steps. The order matters, because rules reference jurisdictions, types, and categories, and content classification reuses the categories you create.

### 1. Define what you sell — Categories

Go to **Commerce → Taxation → Categories** and add a category for each kind of thing you tax (for example `Electronics`, or a finer `Television`). A category has:

- **Name** — a human-readable label (the unique key, fixed after creation).
- **Code** — the value matched by tax rules and stored on taxable items (for example `Electronics`).
- **Parent code** — an optional parent category code, forming a hierarchy. A rule for a category also applies to every category under it: a rule for `Electronics` taxes an item classified as `Television` when `Television`'s parent is `Electronics`. A rule for `Television` never taxes an item classified only as `Electronics`.
- **Description** — optional notes.

Create the broad categories you match rules against, plus any finer classifications you want to assign to individual items.

### 2. Name your kinds of tax — Types

Go to **Commerce → Taxation → Types** and manage the list of **tax types** — the labels that group and report the kind of tax a rule produces (for example `SalesTax`, `VAT`, or `GST`). A tax type has:

- **Name** — the value stored on the resulting tax lines and offered in the rule editor (the unique key, fixed after creation).
- **Description** — optional notes.

The catalog is seeded with a set of well-known types on first run so existing behavior is preserved, but the list is fully editable: add the types your business uses, rename or remove the ones you do not. A tax type is a reporting and grouping label only — it never changes how an amount is calculated.

### 3. Define where you tax — Jurisdictions

Go to **Taxes → Jurisdictions** and add a taxing authority for each place you collect tax. Jurisdictions are hierarchical (country → region → county → city → special district) and are matched to an address by their non-empty components. Key fields include the **Level**, an optional **Parent jurisdiction**, the geographic components (**Country**, **Region**, **County**, **City**, **Postal code**), and optional **Effective from/to** dates. **Country** is chosen from a dropdown of ISO 3166-1 countries (the stored value is the ISO country code), and **Effective from/to** are date-only pickers.

:::tip
When the [Addresses](./addresses.md) feature is enabled, the **Country** dropdown is populated from your managed `Country` content items instead of the built-in list, so you can curate the countries you operate in. The stored value is still the ISO country code, so switching the Addresses feature on or off never orphans existing jurisdictions or rules.
:::

### 4. Provide lookup tables — Tables (optional)

Go to **Taxes → Tables** when you need a rule whose amount depends on where the taxable base falls in a set of brackets rather than a single rate. A tax table is a named, versioned list of rows, and it is the data source for the **Tax table lookup**, **Progressive**, and **Threshold** calculation methods. Skip this step if you only use flat rates or fixed amounts. A tax table has:

- **Name** — a human-readable label (the unique key, fixed after creation).
- **Effective from/to** — optional date-only window during which the table is valid. A table is only used for a transaction whose date falls inside this window; a rule that requires a table but has no table in effect on the transaction date is **skipped** (it never silently taxes at zero).
- **Rows** — one or more brackets, each with a **Minimum** (inclusive lower bound), an optional **Maximum** (exclusive upper bound; leave empty for the top bracket), a **Rate**, and a **Fixed amount**. Use **Add row** / **Remove** to manage the list. A row also has a `BaseAmount`, which the built-in calculation methods don't read; the editor doesn't show it, but keeps a value a recipe set for a custom method.

Rows are validated when you save. Minimums cannot be negative, at most one open-ended row (no maximum) is allowed, bounded ranges must be ordered and non-overlapping, and an open-ended row must start at or above the highest bounded maximum. This prevents overlapping brackets from double-counting in progressive calculations. A table that is still referenced by a rule cannot be deleted; remove or repoint the rules first.

How a rule reads the rows depends on the method it uses:

- **Tax table lookup** finds the single row whose `Minimum ≤ base < Maximum` and charges `base × Rate + Fixed amount`.
- **Progressive** taxes each bracket only on the portion of the base that falls inside it, then sums the brackets (classic tiered/marginal calculation).
- **Threshold** taxes only the amount **above** the matching row's minimum.

Each time you save a table its **Version** is incremented, and that version is captured on every tax line and [snapshot](#snapshots-immutability-and-refunds) that used it, so historical calculations stay reproducible even after you edit the table.

### 5. Define how tax applies — Rules

Go to **Commerce → Taxation → Sales tax rules** and click **Add Tax Rule**. A dialog lists the available **calculation methods** — Percentage, Fixed amount, Per unit, Per weight, Per volume, Progressive, Threshold, and Tax table lookup. The method you pick becomes the rule's **source** and is fixed for the life of the rule, exactly like the source-aware editors used elsewhere in the platform (for example AI data sources and deployments). The editor then shows the fields shared by every rule — the **Name** (the rule's unique identifier, fixed after creation), the **Jurisdiction**, the **Category** it applies to (or *Any category*), the **Tax type**, an optional **Display name**, the **Customer type** (or *Any customer*), a **Priority**, **Effective from/to** dates, minimum/maximum thresholds, and flags such as **Enabled**, **Included in price**, **Compound**, **Reverse charge**, and **Applies to shipping** — plus only the calculation fields the selected method needs: a **Rate** for the percentage method, a **Fixed amount** for the fixed/per-unit/per-weight/per-volume methods, or a **Tax table** selector for the table-driven methods (`TaxTable`, `Progressive`, `Threshold`), populated from the tables you created in step 4. A disabled rule is never applied, and rules outside their effective window are ignored.

The **Reverse charge** flag marks a rule whose liability shifts to a registered business buyer (for example the EU B2B reverse charge). When it is set and the customer is a **B2B** customer, the engine does not charge the tax; instead it emits a zero-amount line marked *Reverse charge* so the obligation is still visible on the document. For any other customer the rule behaves normally.

The **Display name** (`TaxName`) is optional: it is the label shown to customers on invoices and receipts, and when it is left empty the tax line falls back to the rule **Name**. The **Tax type** labels and groups the kind of tax the rule produces (for example `SalesTax`, `VAT`, or `GST`); the dropdown is populated from the **Tax types** you manage under **Commerce → Taxation → Types**, and a rule that already stores a value not present in the catalog keeps it. The tax type never changes how the amount is calculated.

### 6. Classify your content

Attach the **Taxation** part to your content types (see [The TaxationPart](#the-taxationpart)) and, on each item, pick its **Tax category** and optional **Tax classification** from the dropdowns. Those dropdowns are populated from the categories you created in step 1, so there are no free-text codes to keep in sync.

With categories, jurisdictions, and rules in place and your content classified, the engine determines and applies the correct tax automatically at checkout — no per-type tax code is required.

:::tip
Create at least one **Category** before configuring a content type. The **Tax category** and **Tax classification** dropdowns in the TaxationPart settings and item editors are sourced from the categories catalog, so an empty catalog leaves only the *None* option.
:::

## Core concepts

The engine works with a **taxable item** (`ITaxableItem`) — a classification-carrying abstraction that is *not* coupled to Product, Subscription, or content items. Anything can be a taxable item: a product, a variant, a subscription plan, a service, a booking, an event, digital content, shipping, or another charge.

```text
Content / Product / Subscription / Custom object
                    │
                    ▼
             ITaxableItem
                    │
                    ▼
          Tax determination  (jurisdictions + rules + customer + nexus + exemptions)
                    │
                    ▼
          Tax calculation  (calculation methods + tax tables + rounding)
                    │
                    ▼
          TaxCalculationResult  (auditable tax lines)
                    │
                    ▼
          TaxSnapshot  (immutable transaction record)
```

## The TaxationPart

The `TaxationPart` lets **any content type participate in taxation without modifying the Taxation module**. The part stores *classification and tax identity only* — never a final tax rate.

| Property | Description |
|----------|-------------|
| `Taxable` | Whether the content item is taxable. |
| `TaxCategoryCode` | The tax category (for example `Electronics`). |
| `TaxClassificationCode` | The finer classification (for example `Television`). |
| `ExternalTaxCode` | An optional external/provider tax code for mapping to third-party systems. |

Attach the part through the content-type editor, or with a migration:

```csharp
internal sealed class TelevisionMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public TelevisionMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("Television", type => type
            .WithPart("TaxationPart", part => part
                .WithDisplayName("Taxation")
                .WithSettings(new TaxationPartSettings
                {
                    DefaultTaxCategoryCode = "Electronics",
                    DefaultTaxClassificationCode = "Television",
                    AllowClassificationOverride = true,
                })
            )
        );

        return 1;
    }
}
```

A content editor can then leave the defaults or override the classification per item. During checkout the content item is discovered as a taxable item automatically — **no custom tax logic is required for the `Television` type**.

### TaxationPart settings

| Setting | Description |
|---------|-------------|
| `DefaultTaxCategoryCode` | The category applied when the item does not specify one. Selected from the categories catalog. |
| `DefaultTaxClassificationCode` | The classification applied when the item does not specify one. Selected from the categories catalog. |
| `AllowClassificationOverride` | Whether editors may override the classification per content item. |

In the content-type editor, **Default tax category** and **Default tax classification** are dropdowns populated from the tax categories you created under **Commerce → Taxation → Categories**, so create your categories first. When `AllowClassificationOverride` is disabled, editors do not see the category/classification fields and the defaults configured here are always used.

## Domain model

All taxation data is stored through the CrestApps catalog abstraction, so each entity is versioned, named, and clonable.

| Entity | Store | Purpose |
|--------|-------|---------|
| `TaxJurisdiction` | `ITaxJurisdictionStore` | A hierarchical taxing authority (country → state/province → county → city → special district), matched to an address by its non-empty components. |
| `TaxCategory` | `ITaxCategoryStore` | A classification such as `Electronics/Television` or `Alcohol/Wine`, with optional external codes. |
| `TaxType` | `INamedCatalog<TaxType>` | A user-managed label that groups and reports the kind of tax a rule produces (for example `SalesTax`, `VAT`, `GST`). Seeded with well-known values and never affects calculation. |
| `TaxRule` | `ITaxRuleStore` | Determines whether and how a tax applies. Versioned, prioritized, with effective dates. |
| `TaxTable` | `ITaxTableStore` | Brackets, ranges, and schedules for progressive, threshold, and lookup taxes. |
| `ExemptionCertificate` | `IExemptionCertificateStore` | A customer exemption scoped by tax type, jurisdiction, and classification. |
| `MerchantTaxRegistration` | `IMerchantTaxRegistrationStore` | The merchant's obligation (nexus) to collect a tax in a jurisdiction. |

### Tax rules

A `TaxRule` binds a jurisdiction, tax type, classification, and customer criteria to a calculation method. The calculation method is stored in the rule's `Source` property (inherited from `SourceCatalogEntry`), which is set when the rule is created and fixed thereafter:

```csharp
await ruleStore.CreateAsync(new TaxRule
{
    Name = "California sales tax",
    TaxType = TaxTypeNames.SalesTax,
    TaxName = "CA Sales Tax",
    TaxCode = "US-CA-SALES",
    JurisdictionId = californiaJurisdictionId,
    CategoryCode = "Electronics",
    Source = TaxCalculationMethodNames.Percentage,
    Rate = 0.075m,
});
```

The `TaxName` is optional; when it is left empty the tax line falls back to the rule `Name`. Rules carry `Version`, `Priority`, `EffectiveFromUtc`, `EffectiveToUtc`, `MinimumAmount`, `MaximumAmount`, `ThresholdBasis`, `CustomerType`, `IsCompound`, `IncludedInPrice`, `ReverseCharge`, and `AppliesToShipping`. Historical rules must remain immutable once used by a transaction; publish a new version rather than mutating a rule that has already been applied.

#### Thresholds: each item or the whole order

`MinimumAmount` and `MaximumAmount` limit a rule to amounts at least the minimum and below the maximum. `TaxRule.ThresholdBasis` (`TaxThresholdBasis`) says which amount:

| `ThresholdBasis` | Compared with |
| --- | --- |
| `Item` (the default) | The item's taxable amount (`TaxRuleQuery.TaxableAmount`). |
| `Order` | What the order's items come to, before tax, shipping left out (`TaxRuleQuery.OrderAmount`: the sum of `ITaxableBaseCalculator.GetTaxableBase` over the context's items that aren't `TaxableItemKind.Shipping`). `TaxService` sets it on every query; a query without it falls back to the item's amount. |

`CatalogTaxRuleProvider` applies it. The admin calls it **Amounts compared with** (**Each item** or **The whole order**); the `TaxRule` recipe step takes `"ThresholdBasis": "Item"` or `"Order"`.

#### Import duty

`TaxTypeNames.ImportDuty` (`ImportDuty`) is a well-known tax type: a duty on goods brought into a country, charged at checkout. New sites are seeded with it. A duty is an ordinary rule:

```json
{
  "name": "TaxRule",
  "TaxRules": [
    {
      "ItemId": "ca-import-duty",
      "Name": "Canada import duty",
      "Source": "Percentage",
      "TaxType": "ImportDuty",
      "TaxName": "Import duty",
      "JurisdictionId": "ca",
      "Rate": 0.05,
      "MinimumAmount": 150,
      "ThresholdBasis": "Order",
      "Enabled": true
    }
  ]
}
```

Every item of an order to the jurisdiction whose items come to 150 or more is charged the duty. The checkout summary groups the invoice's tax lines by display name, rate and `IncludedInPrice` (`CheckoutInvoiceSummary.cshtml`), so the duty shows once, as its own line, such as *Import duty 5%*, and an included tax as *VAT 19% (included)*.

### Nexus vs. a jurisdiction having a tax

The engine distinguishes **a jurisdiction has a tax** from **the merchant is obligated to collect it**. When no `MerchantTaxRegistration` records exist, the engine operates in permissive manual-rule mode and collects the tax. As soon as registrations exist, a rule's tax is only collected when an active registration covers its jurisdiction and tax type.

A registration also supports **economic nexus** thresholds. Set `MerchantTaxRegistration.ThresholdAmount` to the sales volume that must be reached before the obligation begins (leave it `null` to be obligated as soon as the registration is active), and keep `MerchantTaxRegistration.ThresholdAccumulatedAmount` updated with the running sales into that jurisdiction as orders are recorded. The registration only establishes nexus once the accumulated amount reaches the threshold. The framework does not own a sales ledger, so the host is responsible for maintaining the accumulated total — this keeps the engine deterministic while still letting you model destination-based economic-nexus rules such as the US *Wayfair* thresholds.

## Calculating tax

The primary API is `ITaxService`:

```csharp
public interface ITaxService
{
    Task<TaxCalculationResult> CalculateAsync(
        TaxCalculationContext context,
        CancellationToken cancellationToken = default);
}
```

Build a `TaxCalculationContext` with everything needed for a deterministic determination:

```csharp
var context = new TaxCalculationContext
{
    Currency = "USD",
    TransactionDateUtc = clock.UtcNow,
    Destination = new TaxAddress { Country = "US", Region = "CA", City = "Los Angeles", PostalCode = "90001" },
    Customer = new CustomerTaxProfile { CustomerType = CustomerTaxType.B2C },
    Items =
    [
        new TaxableItem
        {
            Id = "sku-123",
            Kind = TaxableItemKind.Physical,
            Quantity = 1m,
            UnitPrice = 500m,
            Currency = "USD",
            TaxCategoryCode = "Electronics",
            TaxClassificationCode = "Television",
        },
    ],
};

var result = await taxService.CalculateAsync(context);
```

To convert existing objects (content items, products, subscriptions) into taxable items, use `ITaxableItemResolver`, which delegates to the registered `ITaxableItemProvider` implementations:

```csharp
var taxableItem = await taxableItemResolver.ResolveAsync(contentItem);
```

### The result

`TaxCalculationResult` returns the taxable amount, total tax, grand total, and a `TaxLine` for every applied tax. Each line explains *how* the tax was determined:

| Field | Description |
|-------|-------------|
| `TaxCode`, `TaxName`, `TaxType` | Identity of the tax. |
| `JurisdictionId`, `JurisdictionName` | The taxing authority. |
| `Rate`, `TaxableAmount`, `TaxAmount` | The computed values. |
| `CalculationMethod` | The method used. |
| `IncludedInPrice`, `IsCompound` | Pricing and compounding flags. |
| `Treatment`, `TreatmentReason` | How the supply was treated (`Taxable`, `Exempt`, `ZeroRated`, `ReverseCharge`, or `OutOfScope`) and a human-readable explanation. |
| `RuleId`, `RuleVersion`, `TableId`, `TableVersion` | Audit references to the exact rule and table versions used. |

Exempt customers, valid exemption certificates, and reverse-charge rules do **not** silently drop a tax. The engine still emits a `TaxLine` with `TaxAmount = 0` and the appropriate `Treatment`, so invoices and receipts can show a compliant *Exempt* or *Reverse charge* line instead of omitting the tax entirely. Only a rule that the merchant has no nexus for produces no line at all.

## Calculation methods

The engine ships with the following calculation methods, all registered by name and resolved through `ITaxCalculationMethodProvider`:

| Name (`TaxCalculationMethodNames`) | Behavior |
|------|----------|
| `Percentage` | Percentage of the taxable base. Supports tax-inclusive extraction. |
| `FixedAmount` | A flat amount per line. |
| `PerUnit` | Amount multiplied by quantity. |
| `PerWeight` | Amount multiplied by weight. |
| `PerVolume` | Amount multiplied by volume. |
| `TaxTable` | A single matching bracket from a tax table. |
| `Progressive` | Taxes each bracket portion (tiered). |
| `Threshold` | Taxes only the amount above a threshold. |

### Tax-inclusive vs. tax-exclusive pricing

Set `TaxableItem.PriceIncludesTax` (or `TaxCalculationContext.DefaultPriceType`) to control whether the price already contains tax. For inclusive taxes the engine nets the base out of the gross price before applying each rule, so the total stays equal to the displayed price. Both inclusive **percentage** taxes and inclusive **fixed-amount** taxes (`FixedAmount`, `PerUnit`, `PerWeight`, `PerVolume`) are un-grossed: the engine subtracts the fixed portions and divides out the combined percentage rate, clamping the net base at zero. Table-driven inclusive taxes (`TaxTable`, `Progressive`, `Threshold`) cannot be un-grossed because their amount depends on the base itself; keep those rules tax-exclusive.

## Tax sourcing

A rule with a `JurisdictionId` applies only when that jurisdiction is among the ones resolved for the item's address (`ITaxJurisdictionResolver`). With no address, or an address outside it, the rule does not apply; rules without a jurisdiction apply everywhere. So the address the checkout supplies decides which jurisdiction rules can apply.

The built-in `DefaultCheckoutTaxProfileProvider` uses the address the order ships to, else the billing address, as the destination, and the store's address under **Settings > Commerce > Tax origin** (`TaxOriginSettings`) as the origin. Other modules add to the profile through `ICheckoutTaxProfileHandler`, such as the customer's exemption from [Customers](customers); see [Taxation at checkout](checkout#taxation).

Do not assume the shipping address is always the tax location. Sourcing is pluggable through `ITaxSourcingStrategy` (resolved by `ITaxSourcingStrategyProvider`), with these built-in strategies: `Origin`, `Destination`, `CustomerResidence`, `CustomerBusiness`, `ServiceLocation`, and `EventLocation`. The engine picks a strategy from the taxable item's `Kind` (for example digital goods use the destination, services and bookings use the service location, and events use the event location) and falls back through destination, item origin, context origin, and customer residence.

## Snapshots, immutability, and refunds

When taxation becomes part of a transaction, capture it with `ITaxSnapshotFactory`:

```csharp
var snapshot = snapshotFactory.Create(context, result);
```

A `TaxSnapshot` is an immutable deep copy of the determination, including every tax line with its rule and table versions. Changing a rate, rule, or tax table tomorrow never changes yesterday's snapshot. Refunds and adjustments reverse the original snapshot rather than recalculating with today's rules.

### Refunding tax from a snapshot

`ITaxRefundCalculator` derives the tax portion of a refund from the original transaction's snapshot, never from current rules, so a rate change after the sale can never alter a refund:

```csharp
// Full refund: reproduces the snapshot's tax exactly.
var full = refundCalculator.CalculateFullRefund(snapshot);

// Partial refund: proportional to the amount being refunded, allocated across the
// original tax lines so each jurisdiction is refunded per the original determination.
var partial = refundCalculator.CalculateProportionalRefund(snapshot, refundTotalAmount: 54m);
```

A refund at or above the snapshot total returns the full refund; a non-positive amount returns nothing. Refunds never introduce a second tax calculation — the historical snapshot is the single source of truth.

## Rounding

Rounding is one site setting, **Settings > Commerce > Tax calculation** (`TaxCalculationSettings.RoundingLevel`, group `tax-calculation`, permission **Manage taxation**), that the checkout and [invoices](invoicing#tax) both read through `ITaxRoundingLevelProvider`, so a cart and an invoice with the same lines come to the same tax. A calculation can still name its own level (`TaxCalculationContext.RoundingLevel`); a host without site settings falls back to `TaxationOptions.RoundingLevel` (`Line`).

| Level (`TaxRoundingLevel`) | Behavior |
|-------|----------|
| `Line` | Each tax line is rounded independently. |
| `Tax` | Amounts are aggregated per tax type, then rounded. |
| `Jurisdiction` | Amounts are aggregated per jurisdiction, then rounded. |
| `Transaction` | The whole transaction is rounded once. |
| `Rate` (a new site's default) | Amounts are aggregated per tax and rate (its code, name, rate and compound flag), rounded once, and spread back over the lines largest remainder first: the rule EN 16931 asks invoices to follow. |

Two lines of 10.05 at 7.5% come to 1.51 per tax and rate (on the summed 20.10) and to 1.50 line by line (0.75 + 0.75). A new site rounds per tax and rate. A site that already had tax rules when the setting arrived keeps rounding each line (`TaxCalculationSettingsMigrations` stores `Line`), so no checkout total changes on upgrade; staff can switch on the settings page, whose hints show the difference. Changing it changes what the checkout charges from then on; orders and issued invoices keep their amounts. With CrestApps Recipes, `TaxCalculationSettingsSchema` describes it for the Settings step.

## Document tax breakdown

`ITaxBreakdownCalculator` (Taxation.Abstractions, `DefaultTaxBreakdownCalculator` in Taxation.Core) works out a document's tax from the taxes the rules found for its items, for Invoicing's invoices, estimates, credit notes and recurring plans. Its `TaxBreakdownRequest` holds the items (lines and charges, `TaxBreakdownItem`: the amount after the document's discounts and before them, the item's own treatment and reason, its `TaxBreakdownRate`s in percent, and whether it is a charge), the document's treatment and reason (the bill-to's), whether prices include tax, the cash rounding step, and the rounding level (the site's). The `TaxBreakdownResult` has:

- one row per (treatment, code, name, rate, compound): tax worked out on the summed net, a compound tax on the net plus the other taxes, and a row with no tax, with its reason, for each treatment of the items that are not taxed (zero rated, exempt, reverse charged, out of scope, or taxable with no tax), so every item has a category as EN 16931 asks;
- each item's net before and after the document's discounts and its share of the tax, spread back largest remainder first (`MoneyAllocation`, next to `CurrencyScale`);
- with prices that include tax, each group of items that share their taxes split once into net and tax (an item with no tax to take out keeps its whole price as net);
- the tax total, the cash rounding as an explicit amount, and the total.

That is rounding per tax and rate. At any other level, each item's taxes are worked out and rounded the way the checkout's tax service does it (`TaxLineRounding`, which both use), so a cart and an invoice with the same lines come to the same tax at every level. Both round with the site's `ITaxRoundingStrategy`.

`TaxationOptions` also configures `DecimalPlaces`, `MidpointRounding`, and per-currency decimal overrides. By default `DecimalPlaces` is empty and tax is rounded to each currency's precision from `CurrencyScale` (ISO 4217): no decimals for JPY, three for KWD, two for USD, the same precision the payment uses. Set `DecimalPlaces` only to force one precision for every currency; a per-currency override still wins.

## Extending the framework

External modules integrate **without modifying the Taxation source**. Register your implementations in a `Startup` class:

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Convert your own objects into taxable items.
        services.AddTaxableItemProvider<SubscriptionTaxableItemProvider>();

        // Add a custom calculation strategy.
        services.AddTaxCalculationMethod<MyCustomTaxCalculationMethod>();

        // Add a custom sourcing strategy.
        services.AddTaxSourcingStrategy<MyCustomSourcingStrategy>();

        // Delegate determination to an external provider (Avalara, Stripe Tax, etc.).
        services.AddTaxDeterminationProvider<MyExternalTaxProvider>();
    }
}
```

The main extension points are:

| Interface | Responsibility |
|-----------|----------------|
| `ITaxableItem` / `ITaxableItemProvider` | Represent and produce taxable items from arbitrary objects. |
| `ITaxService` | The determination engine entry point. |
| `ITaxCalculationMethod` | A calculation strategy resolvable by name. It also declares, through its `Inputs` (`TaxCalculationMethodInputs`), which rule configuration fields (rate, fixed amount, or tax table) the Rules editor should show when the method is selected. |
| `ITaxSourcingStrategy` | Where the tax is sourced from. |
| `ITaxRuleProvider` | Supplies applicable rules for a query. |
| `ITaxClassificationProvider` | Supplies an *inherited* classification (category / classification / external code) for a content item that does not carry its own — for example from a taxonomy term. |
| `ITaxJurisdictionResolver` | Maps an address to jurisdictions. |
| `ITaxExemptionResolver` | Determines customer exemptions. |
| `IMerchantTaxRegistrationProvider` | Determines merchant nexus. |
| `ITaxableBaseCalculator` | Computes the taxable base for an item. |
| `ITaxRoundingStrategy` | Applies the rounding policy. |
| `ITaxSnapshotFactory` | Captures immutable transaction snapshots. |
| `ITaxRefundCalculator` | Derives full and proportional refund tax from a snapshot. |
| `ITaxDeterminationProvider` | Short-circuits the engine with an external determination. |

An `ITaxDeterminationProvider` whose `CanHandle` returns `true` takes over the entire calculation, which is how third-party tax services can be layered on top of the same abstractions; [TaxJar](#taxjar) is one. Providers are asked by their `Order`, lowest first, and the first that can handle the context determines it. The engine checks that every item is in the context's currency before asking any provider.

### Tax profile of a party

A document billed to a [party](parties.md) (an account, a customer, a user or a CRM contact), such as an invoice, has no checkout to read the buyer from. `IPartyTaxProfileService.GetAsync(party)` (Taxation.Abstractions) builds its `CustomerTaxProfile` instead: it starts from a person with no tax number and no exemption, and every `IPartyTaxProfileProvider.BuildAsync(profile, party)` adds what it knows. Each provider fills what is missing and keeps what another set, so their order doesn't change the result.

- The default provider, `BillingProfilePartyTaxProfileProvider` (Taxation.Core), reads the party's billing profile: an `Account` is a business (`B2B`) taxed at its billing address as its `BusinessAddress`; another party's billing address is its `ResidenceAddress`. The billing profile's tax id fills `TaxRegistrationNumber` and `VatNumber` (`CustomerTaxProfile.SetTaxNumber`), and an exempt party is exempt. A `Customer` party's id is the profile's `CustomerId`.
- Customers adds `CustomerPartyTaxProfileProvider`: a business customer is a business, and the customer's exemption certificates are listed.

`services.AddTaxationCore()` registers the service and the default provider; a feature that taxes documents without the Sales Tax feature calls `services.AddPartyTaxProfiles()` (Taxation.Core), and a module adds a provider with `services.AddPartyTaxProfileProvider<T>()`.

## Income tax

Income Tax works out taxes on income: income tax withholding at every level (federal, state or province, local) and the statutory contributions on earnings, such as US Social Security, Medicare and unemployment, or the Canada Pension Plan, Employment Insurance and their Quebec counterparts. It never references Commerce or Payroll. Payroll's taxes step maps a pay statement to the request and the result back to tax lines, and Payroll keeps what is built from pay records: accumulators, liabilities and deposits, the employer's returns and slips, and year end.

### The provider seam

`IIncomeTaxProvider.CalculateAsync(IncomeTaxRequest)` returns an `IncomeTaxResult` of `IncomeTaxLine`s and warnings. The request names no module's records:

| Member | What it carries |
| --- | --- |
| `PayerId`, `EarnerId` | Ids for a provider that keeps its own records. |
| `RegisteredJurisdictions`, `Registrations` | The jurisdictions the payer is registered in; its `IncomeTaxRegistration`s, each with the account id, the rate the agency assigned it on the pay date and the tax `Categories` it covers when the payer has several accounts in one jurisdiction (a registration's jurisdiction counts as registered too). |
| `IncomeKind`, `ContractorWithholding` | Employment, contractor, pension or other income; whether a contractor's payments are withheld on. |
| `HomeJurisdictions`, `WorkJurisdictions` | Where the earner lives and works, such as `CA`, `CA-ON`, or `US`, `US-MI`, `US-MI-DETROIT`. |
| `Elections` | `IncomeTaxElection`s: the values of each jurisdiction's withholding form in effect. |
| `PayDate`, `PeriodsPerYear`, `Currency` | When, how often, in what. |
| `Income`, `SupplementalIncome`, `SupplementalMethod` | The period's regular and supplemental income of each tax category, and how supplemental income is withheld on. |
| `RegularIncomePerPeriod` | For a payment with no regular income of its own (a bonus paid alone), a period of the earner's regular income: methods that add supplemental income to regular pay add it to this. |
| `YearIncome`, `YearSupplementalIncome` | The year's income of each category before this payment. |
| `TaxesToDate` | `IncomeTaxToDate` by tax code: the year's taxable income and tax, and the period's, for wage bases, maximums and a second payment in a period. |

Each line has the tax code, name, jurisdiction, the jurisdiction that collects it (`CollectedBy`), the category, the payer (`IncomeTaxPayer`), the taxable income, the part withheld at a flat rate, and the amount. Register a provider with `services.TryAddEnumerable(ServiceDescriptor.Scoped<IIncomeTaxProvider, MyProvider>())`; Payroll's settings choose one when there are several, and with none chosen Payroll uses the only one besides the built-in rules. The built-in provider's stored name is `TaxTables`, kept from before the move so saved settings still name it.

`IIncomeTaxProvider.GetElectionJurisdictionsAsync(date)` says which jurisdictions' elections the provider reads on a day, so Payroll asks people only for those. The default returns `null` (the provider can't tell, and Payroll asks for an election in every jurisdiction a person is registered or works in). The built-in provider returns the jurisdictions of its rules turned on and in effect that use elections or have an exemption allowance, reading the jurisdiction in `ElectionsFrom` when a rule names one (a city tax that takes the state certificate's exemptions asks for the state's election only).

### Income tax rules

The built-in provider (`IncomeTaxRulesProvider`) works out the `IncomeTaxRule`s in effect on the pay date, managed under **Commerce > Taxation > Income tax rules** with the **Manage income tax rules** permission. The permission is security critical (the rules decide every payer's withholding), so only the Administrator role has it by default; give it to another role on purpose. A rule applies when the payer is registered in the jurisdiction that collects it, it is for the kind of income, the person is in its jurisdiction (for one below a country), and the person is in none of its excepted jurisdictions. Its fields express each country's taxes without code:

| Field | Use |
| --- | --- |
| `Jurisdiction`, `CollectedBy` | Any level; another agency that collects it (a province's tax the CRA collects is `CA-ON` collected by `CA`). |
| `ByPlaceOfWork` | Match the jurisdiction by where the person works (Canada's province of employment), never where they live: with no place of work in the country known, a rule below the country applies nowhere and the result warns. |
| `ExceptJurisdictions` | People it doesn't apply to (`CA-QC` for the Canada Pension Plan). |
| `Category`, `IncomeKinds`, `Payer` | The income taxed, the kinds of income, who pays. |
| `Source` | The kind of rule, chosen in **Add income tax rule** and fixed afterwards; it decides which fields the editor shows (see below). |
| `Method` with `Rate`, `FixedAmount`, `TaxTableId` | A Taxation calculation method, or a country's withholding method with its `Tables` and `Parameters`. |
| `RateFromRegistration` | The payer's own rate replaces `Rate`: its registration's rate on the pay date with the jurisdiction that collects the tax (an unemployment insurance rate, Quebec's health services fund rate). With several registrations in that jurisdiction, the one whose `Categories` include the rule's category is read, else one that names no category, else the first (`IncomeTaxRulesProvider.RegistrationFor`). `Rate` applies when there is none; with neither, the rule isn't worked out and the result warns. |
| `Residency` | `Any` (default): anyone in the jurisdiction, by home or work. `Resident`: only when the jurisdiction is among the person's home jurisdictions. `Nonresident`: only when it is among their work jurisdictions and not their home ones (`IncomeTaxRulesProvider.AppliesToResidency`). |
| `ExclusiveGroup`, `GroupPriority` | Of the applicable rules that share a group, only the one with the lowest priority is worked out (`IncomeTaxRulesProvider.Exclusive`), such as Michigan's city taxes: the home city's resident rate (priority 0) before the work city's nonresident rate (priority 1). |
| `ElectionsFrom` | The jurisdiction whose election the rule reads instead of its own, such as `US-MI` for `US-MI-DETROIT`. |
| `ExemptionAllowance` | For a flat rate rule: each `Exemptions` claimed on the election (of `ElectionsFrom`, or its own jurisdiction; none when that election is exempt) lowers a year's income by this amount, so the tax is the rate on the period's income less a period's share of the exemptions, never below zero. It doesn't take the election's exemption or extra withholding. |
| `DeductedTaxCodes` | For a withholding method that deducts the employee's other taxes of the same pay, such as the state certificate method's Social Security and Medicare. |
| `WageBase`, `WageThreshold` | The most income taxed in a year; only the year's income above an amount (a second tier). |
| `YearlyExemption` | An amount never taxed, taken off each pay in proportion to the periods of a year, cut to cents. |
| `YearlyMaximum` | The most tax in a year. |
| `ShareOfTaxCode`, `ShareMultiplier` | A share of another tax of the same pay, such as an employer's 1.4 times the EI premium. |
| `Annualize`, `UsesElections`, `AllowanceAmount` | Tables written for a year; the person's election (exempt, allowances, extra withholding). |
| `SupplementalRate`, `SupplementalExcessRate`, `SupplementalExcessThreshold` | Flat rates on supplemental income. |
| `EffectiveFrom`, `EffectiveTo`, `Enabled` | Dated versions; two versions of one tax code can't overlap. |
| `VerifiedBy`, `VerifiedUtc`, `VerificationSource` | Who checked the rule against the agency's publications, when, and against what (`IsVerified`). Informational only: an unchecked rule is still worked out. |

Contributions and flat taxes are worked out first, then withholding methods that give credits for them (`UsesOtherTaxes`), then shares; lines come back in jurisdiction and tax code order. Rules are imported and exported with the `IncomeTaxRules` recipe and deployment step, and kept in the `IncomeTax` YesSql collection.

### Verifying rules

The **Income tax rules** list shows a **Verified** or **Not verified** badge on each rule, and **Verify** in a row's actions marks it checked by the signed-in user now (`IncomeTaxRulesController.Verify`, with an optional `source`). Verifying needs **Verify income tax rules** (`TaxationPermissions.VerifyIncomeTaxRules`, security critical, Administrator by default). Any change to a rule clears the mark: `IncomeTaxRuleHandler` keeps `VerifiedBy`, `VerifiedUtc` and `VerificationSource` only when the values handed to the manager carry `VerifiedUtc` (the Verify action, or a recipe or deployment plan of a rule verified where it was exported); an edit on the page or an import without it clears them. The mark is informational only and never blocks a calculation.

### Rule sources

**Add income tax rule** opens a dialog of the kinds of rule (`IncomeTaxRuleSourceOptions`, each card the `IncomeTaxRuleSource_Link` shape with a `IncomeTaxRuleSource_Link__{Name}` alternate). The editor then shows the fields every rule has (`IncomeTaxRuleDisplayDriver`: the tax, where it applies, who pays, when) and the card of the rule's source, from a display driver whose `CanHandleModel` accepts only that source:

| Source | Method | Fields | Driver |
| --- | --- | --- | --- |
| `FlatRate` | `Percentage` | `Rate`, `RateFromRegistration`, `WageBase`, `WageThreshold`, `YearlyExemption`, `YearlyMaximum`, `ExemptionAllowance` | `IncomeTaxFlatRateDisplayDriver` |
| `ShareOfTax` | `Percentage` | `ShareOfTaxCode`, `ShareMultiplier`, `YearlyMaximum` | `IncomeTaxShareOfTaxDisplayDriver` |
| `TaxTable` | `TaxTable`, `Progressive` or `Threshold` | `TaxTableId`, `WageBase`, `Annualize`, `UsesElections`, `AllowanceAmount`, supplemental rates | `IncomeTaxTableDisplayDriver` |
| `FixedAmount` | `FixedAmount` | `FixedAmount`, `YearlyMaximum` | `IncomeTaxFixedAmountDisplayDriver` |
| A pack's withholding source | the source's method | the method's `Tables` and `Parameters`, `UsesElections`, `AllowanceAmount`, supplemental rates unless the method `HandlesSupplementalIncome`, and `DeductedTaxCodes` when the source's fields list it | `IncomeTaxWithholdingDisplayDriver` |

`Residency`, `ExclusiveGroup`, `GroupPriority` and `ElectionsFrom` are fields every rule has, on the shared card.

Each source declares the amount fields it owns (`IncomeTaxRuleSource.Fields`; `IncomeTaxRuleSources.FlatRateFields` and the others, and `WithholdingFields` for a pack's withholding source). The calculation reads every field whatever the source, so the handler clears the fields a rule's source doesn't own when it is saved or imported, refuses a `ShareOfTaxCode` outside a share of another tax, and validates what the source needs; a source driver clears every amount before setting its own. Editing a rule of an older layout warns which values saving will drop. A source registered with a method sets it (the handler sets it for a recipe too, so a recipe may name the source alone). A rule without a source gets one when it is loaded (`IncomeTaxRuleSources.Infer`: a share, the rate or amount methods, Taxation's table methods, and otherwise the method's own name, so a rule of a pack whose feature is off keeps its kind and isn't edited as another). A source whose feature is off isn't offered, and its rules show only the shared fields.

### Country packs

A country pack registers what its formulas need, and the kinds of rule it adds:

```csharp
services.AddIncomeTaxCategory("InsurableEarnings", LocalizationSource.Create("Insurable earnings (EI, QPIP)", typeof(Startup)));
services.AddElectionFieldSet(MyElectionFields.Create());     // the fields of a withholding form, for a jurisdiction or a prefix such as CA-*
services.AddIncomeTaxWithholdingMethod<MyWithholdingMethod>();
services.AddIncomeTaxRuleSource(
    "MyWithholding",
    LocalizationSource.Create("My country's income tax", typeof(Startup)),
    LocalizationSource.Create("The tax agency's formulas, with each person's withholding form.", typeof(Startup)),
    method: "MyWithholding");                                // its rules get Taxation's withholding editor
```

A source with a withholding method needs no driver: `IncomeTaxWithholdingDisplayDriver` shows the method's tables and amounts. A pack that needs other fields derives its driver from `IncomeTaxRuleSourceDisplayDriver` (in `CrestApps.OrchardCore.Taxation.Core.Drivers`: `CanHandleModel` on its source, `Number`, `Text`, `ClearAmounts`), registers its source with `usesWithholdingEditor: false` so the generic card doesn't render beside its own, and lists the fields its rules own with `fields:`. The United States pack adds `UnitedStatesFormW4`; the Canada pack adds `CanadaFederal`, `CanadaProvincial` and `QuebecTP1015`.

An `IIncomeTaxWithholdingMethod` declares its tables (optional ones with `Required: false`) and parameters, and gets an `IncomeTaxWithholdingContext`: the rule, the request, the election, the period's income, the supplemental income when it `HandlesSupplementalIncome`, the lines worked out before it, and functions that compute a table's brackets, its lowest rate and its rate at an amount.

| Pack | Election forms | Methods | Recipe |
| --- | --- | --- | --- |
| United States Income Tax | Form W-4 (`US`), state withholding certificate (`US-*`) | `UnitedStatesFormW4` (Publication 15-T percentage method), `UnitedStatesStateCertificate` (a state's annualized method) | `IncomeTaxUnitedStatesFederal2026` |
| Canada Income Tax | TD1 (`CA`), provincial TD1 (`CA-*`), TP-1015.3 (`CA-QC`) | `CanadaFederal` and `CanadaProvincial` (CRA T4127, Option 1, with the bonus method), `QuebecTP1015` (Revenu Québec TP-1015.F) | `IncomeTaxCanada2026` |

The Canada recipe ships every province and territory, the July 1, 2026 changes for British Columbia, Newfoundland and Labrador and Prince Edward Island, CPP and CPP2 with the basic exemption, EI with the employer's 1.4 times, and in Quebec QPP, QPP2, QPIP and the reduced EI rate. A province's own items are parameters of its rule: a basic amount that falls with net income (Manitoba, Yukon), Alberta's supplemental credit, Yukon's employment credit, Ontario's surtax, health premium table and tax reduction, and British Columbia's tax reduction. The bracket tables carry small fixed amounts so the progressive method gives exactly R × A − K with the CRA's rounded constants.

### United States: state and local taxes

United States Income Tax adds a kind of rule for a state that withholds by the annualized method of its withholding guide (Michigan, Massachusetts, Virginia and others): **State income tax (withholding certificate)**, source and method `UnitedStatesStateCertificate` (`UnitedStatesIncomeTax.StateCertificateMethod`, `UnitedStatesStateCertificateWithholdingMethod`). A state is data: a rule per state and year with its amounts and brackets, never code. The method takes the period's wages times the periods of a year; withholds nothing when that is at or under the no-tax threshold; takes off the standard deduction, the head of household deduction, the exemptions (the first, then each further one) and each exemption for age or blindness, and the employee's taxes named by `DeductedTaxCodes` of the same pay (a year of them, up to the limit); works out the `Brackets` table on what is left; and divides by the periods. It runs after the contributions (`UsesOtherTaxes`). Exemption and extra withholding are the provider's, as for any rule with `UsesElections` on, so a state rule has `UsesElections` on.

| Key (`UnitedStatesStateCertificateParameters`) | Kind | Use |
| --- | --- | --- |
| `Brackets` | table (required) | The brackets of a year's taxable wages, worked out as progressive brackets (rates in the rows are fractions). |
| `NoTaxThreshold` | parameter | The year's wages at or under which nothing is withheld (Massachusetts' 8,000). |
| `StandardDeduction` | parameter | Everyone's deduction (Virginia's 8,750). |
| `HeadOfHouseholdDeduction` | parameter | Added for a head of household (Massachusetts' 2,400). |
| `FirstExemption`, `AdditionalExemption` | parameters | The first exemption claimed and each further one (Massachusetts' 4,400 and 1,000; Michigan's 5,800 each). |
| `SpecialExemption` | parameter | Each exemption for age or blindness. |
| `DeductedTaxesLimit` | parameter | The most of the `DeductedTaxCodes` taxes taken off a year (Massachusetts' 2,000 of `US-SS-EE`, `US-MED-EE` and `US-MED-ADD`); with none given, all of them. |

Every parameter is optional. The person's state certificate is the election field set of every US jurisdiction below the country (`US-*`, `UnitedStatesStateCertificateFields`), so the pay profile editor offers it for any state or city election:

| Field | Kind | Meaning |
| --- | --- | --- |
| `Exemptions` | whole number | Personal and dependent exemptions. |
| `SpecialExemptions` | whole number | Exemptions for age or blindness. |
| `HeadOfHousehold` | yes or no | Files as head of household. |
| `Allowances` | whole number | Allowances, for a state's tax table rule that counts allowances (`AllowanceAmount`). |
| `ExtraWithholding` | amount | Extra withholding each pay period. |
| `Exempt` | yes or no | Exempt from withholding. |

A city or other local tax is a flat rate rule of its own jurisdiction. Michigan's city taxes, for example, are two rules per city in one exclusive group, each reading the state certificate's exemptions:

```json
{ "TaxCode": "US-MI-DETROIT-R", "Name": "Detroit income tax", "Jurisdiction": "US-MI-DETROIT", "Source": "FlatRate", "Rate": 2.4,
  "Residency": "Resident", "ExclusiveGroup": "US-MI-CITY", "GroupPriority": 0, "ElectionsFrom": "US-MI", "ExemptionAllowance": 600,
  "Category": "IncomeTax", "Payer": "Employee", "EffectiveFrom": "2026-01-01" },
{ "TaxCode": "US-MI-DETROIT-NR", "Name": "Detroit income tax (nonresident)", "Jurisdiction": "US-MI-DETROIT", "Source": "FlatRate", "Rate": 1.2,
  "Residency": "Nonresident", "ExclusiveGroup": "US-MI-CITY", "GroupPriority": 1, "ElectionsFrom": "US-MI", "ExemptionAllowance": 600,
  "Category": "IncomeTax", "Payer": "Employee", "EffectiveFrom": "2026-01-01" }
```

A resident of a taxing city pays only its resident rate; anyone else who works in a taxing city pays its nonresident rate; with neither, no city tax. The payer must be registered in the city's jurisdiction (or the rule names `CollectedBy`). A contribution such as Massachusetts' paid family and medical leave is a flat rate rule of category `SocialInsurance` with its wage base. Payroll resolves the home jurisdictions from the home address (`US-MI-DETROIT` for a Detroit address) and the work jurisdictions from the work site team or the employer.

Form W-4's election adds **Nonresident alien** (`UnitedStatesIncomeTax.Fields.NonresidentAlien`): for a form from 2020 or later, the rule's `NonresidentAlienAddition` parameter (`UnitedStatesIncomeTax.NonresidentAlienAddition`, 16,100 in the 2026 recipe, from Publication 15-T) is added to the year's wages before withholding.

## Tax returns

The **Tax Returns** feature (`CrestApps.OrchardCore.Taxation.Returns`) files sales and use tax returns, and any tax a jurisdiction's return combines with them (such as a state's withholding). It never reads sales itself: the figures come from the modules that keep them, so a site with its own ledger files from its books. The screens are described in the [User Manual](../user-manual/finance/tax-returns.md).

### Filing schedules

A `MerchantTaxRegistration` has a `Filing` schedule (`TaxFilingSchedule`), or none when it files no return here:

| Property | Meaning |
| --- | --- |
| `Frequency` | `Monthly`, `Quarterly` or `Annual` (`TaxFilingFrequencies`) |
| `DueMonthsAfterPeriod`, `DueDay` | The return is due on `DueDay` of the month `DueMonthsAfterPeriod` after the period's last month (default the 20th of the month after); a day past the month's end is its last day |
| `MoveToNextBusinessDay` | A due date on a Saturday or a Sunday moves to the Monday after (default on) |
| `Form` | The `ITaxReturnForm` of the periodic returns; empty for the built-in `Generic` form |
| `AnnualForm`, `AnnualDueMonthsAfterYear`, `AnnualDueDay` | A yearly reconciliation return filed besides the monthly or quarterly ones, such as a state's annual return |

`TaxReturnPeriod` is a period (`Frequency`, `Start`, `End`) with its key (`2026-10`, `2026-Q3`, `2026`); `TaxReturnPeriod.Parse`, `For` and `Between` work periods out, and `TaxFilingSchedule.DueDateOf(period, annualReconciliation)` the due date. `ITaxReturnService.ListPeriodsAsync(from, to)` lists, for every active registration with a schedule, the periods that end in the span and that the registration is effective in, each with its due date and current return (`TaxReturnPeriodSummary`).

### Returns

A `TaxReturn` (YesSql collection `TaxReturns`) has its registration, jurisdiction, form, period and due date, the `Lines` the providers gave, the form's `Boxes`, the `AmountDue`, its `Payments`, `Notes`, and its status: `Draft`, `Ready`, `Filed` or `Amended`. One return per registration, form and period is current; an amendment is a new return with `AmendsId` and `Amendment` (1, 2...), and counts what was paid with the earlier one (`PaidWithEarlier`). `AmountPaid` and `Balance` are worked out.

`ITaxReturnService`:

| Method | Does |
| --- | --- |
| `PrepareAsync(registrationId, periodKey, annualReconciliation, user)` | A new draft, or the draft prepared again; refuses a ready or filed return |
| `PrepareAgainAsync`, `MarkReadyAsync`, `ReopenAsync` | Draft only; draft to ready; ready back to draft |
| `MarkFiledAsync(taxReturn, filedOn, confirmationNumber, channel, user)` | Refuses a day before the period ends (a return covers its whole period), works the boxes out again as filed that day, from the same lines, then marks it filed; the return it corrects becomes `Amended` |
| `AmendAsync` | A new draft correcting a filed return |
| `RecordPaymentAsync`, `RemovePaymentAsync` | Records or removes a payment (an id from the id generator); no money moves |

A draft past its due date is worked out as filed today (in the site's time zone), so a late return shows its penalty.

### Extension points

| Interface | Registration | Responsibility |
| --- | --- | --- |
| `ITaxReturnDataProvider` (`Name`, `ContributeAsync(TaxReturnDataContext)`) | `services.AddTaxReturnDataProvider<T>()` | Adds a period's figures to `context.Lines` (and notes to `context.Notes`); every provider runs for every return. A line has a `Kind` (`TaxReturnLineKinds`: `GrossSales`, `ExemptSales`, `TaxableSales`, `UseTaxPurchases`, `Withholding`, `Credit`, `ExcessCollected`, or a form's own), `JurisdictionId`, `TaxType` (such as `UseTax`), `FormLine`, `Category`, `Amount` and `TaxAmount` |
| `ITaxReturnForm` (`Name`, `DisplayName`, `ComputeAsync(TaxReturnFormContext)`) | `services.AddTaxReturnForm<T>()` | Works out the return's `Boxes` and `AmountDue` from its lines; `context.FiledOn` is the day it counts as filed (for discounts, penalties and interest). The built-in `Generic` form sums the lines by kind |
| `ITaxReturnObserver` (`ChangedAsync(TaxReturnChange)`) | `services.AddTaxReturnObserver<T>()` | Hears `Prepared`, `Ready`, `Reopened`, `Filed`, `Amended`, `PaymentRecorded` and `PaymentRemoved`, after the change is saved in the session and before it is committed: post a filed return or a payment to the books, or record a payroll tax deposit for the withholding paid with it |

**The payment form.** The **Payments** card's form is built through `IDisplayManager<TaxReturnPaymentEntry>`, from the return being paid (`Return`) and the `TaxReturnPayment` being recorded (`Payment`). Tax Returns' driver edits the amount, the day, the reference and the method; a pack adds a display driver that edits what its observer needs into `Payment.Properties`, such as the account the payment came from, and checks `entry.Return` (its jurisdiction or form) so its fields show only on its returns. A driver that adds a model error stops the payment.

```csharp
internal sealed class PaidFromDisplayDriver : DisplayDriver<TaxReturnPaymentEntry>
{
    public override IDisplayResult Edit(TaxReturnPaymentEntry entry, BuildEditorContext context)
        => entry.Return?.JurisdictionId == "us-mi"
            ? Initialize<PaidFromViewModel>("TaxReturnPaymentEntryPaidFrom_Edit", model => model.AccountId = entry.Payment.Properties.GetValueOrDefault("PaidFrom") as string).Location("Content:5")
            : null;

    public override async Task<IDisplayResult> UpdateAsync(TaxReturnPaymentEntry entry, UpdateEditorContext context)
    {
        if (entry.Return?.JurisdictionId != "us-mi")
        {
            return null;
        }

        var model = new PaidFromViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        entry.Payment.Properties["PaidFrom"] = model.AccountId;

        return Edit(entry, context);
    }
}

// services.AddDisplayDriver<TaxReturnPaymentEntry, PaidFromDisplayDriver>();
```

**When an observer fails.** An observer runs before the change is committed. When one throws on a return's page (recording or removing a payment, marking filed, amending, recording an e-file answer), the session is cancelled, so nothing of the change is saved, the error is logged, and the person sees *The change was not saved* instead of an error page.

A site that keeps its own ledger implements a data provider that reads its books for the registration's jurisdiction and period, and a form for each jurisdiction's return:

```csharp
public sealed class LedgerTaxReturnDataProvider : ITaxReturnDataProvider
{
    private readonly ILedgerTaxQueries _ledger;

    public LedgerTaxReturnDataProvider(ILedgerTaxQueries ledger)
    {
        _ledger = ledger;
    }

    public string Name => "Ledger";

    public async Task ContributeAsync(TaxReturnDataContext context, CancellationToken cancellationToken = default)
    {
        var sales = await _ledger.SalesAsync(context.Registration.JurisdictionId, context.Period.Start, context.Period.End, cancellationToken);

        context.Return.Currency = "USD";
        context.Lines.Add(new TaxReturnLine { Kind = TaxReturnLineKinds.GrossSales, Amount = sales.Gross });
        context.Lines.Add(new TaxReturnLine { Kind = TaxReturnLineKinds.TaxableSales, TaxType = TaxTypeNames.SalesTax, Amount = sales.Taxable, TaxAmount = sales.TaxCollected });
        context.Lines.Add(new TaxReturnLine { Kind = TaxReturnLineKinds.UseTaxPurchases, TaxType = TaxTypeNames.UseTax, Amount = sales.UseTaxPurchases, TaxAmount = sales.UseTax });
    }
}
```

A registration with returns can't be deleted from **Tax registrations**.

**Use tax.** `TaxTypeNames.UseTax` (`UseTax`) is a well-known tax type, seeded on new sites and added to existing ones by a migration, for the tax a business owes on what it bought or took from stock without paying sales tax.

### Permissions

| Permission | Grants |
| --- | --- |
| `ManageTaxReturns` | **Commerce > Taxation > Tax returns**: see, prepare, prepare again, mark ready, reopen, delete a draft with no payment |
| `FileTaxReturns` | Mark filed, amend, record and remove payments, file electronically; it grants `ManageTaxReturns` |

Both are given to Administrator by default.

### Recipes and deployment

The `TaxReturns` step (entries `TaxReturns`) and the **Tax returns** deployment step import and export returns, matched by `ItemId`. The registrations' `Filing` schedule travels with the `MerchantTaxRegistration` step:

```json
{
  "steps": [
    {
      "name": "MerchantTaxRegistration",
      "MerchantTaxRegistrations": [
        {
          "Name": "Michigan sales, use and withholding",
          "JurisdictionId": "us-mi",
          "RegistrationNumber": "12-3456789",
          "IsActive": true,
          "Filing": { "Frequency": "Monthly", "DueMonthsAfterPeriod": 1, "DueDay": 20, "MoveToNextBusinessDay": true, "AnnualForm": "US-MI-5081", "AnnualDueMonthsAfterYear": 2, "AnnualDueDay": 28 }
        }
      ]
    }
  ]
}
```

## E-filing

The **Tax E-Filing** feature (`CrestApps.OrchardCore.Taxation.EFiling`) files documents electronically. An `EFileSubmission` (YesSql collection `TaxReturns`) is one attempt: the target (`TargetType`, `TargetId`), the format, the channel, the file built (base64 `Content`), its status (`Created`, `Submitted`, `Accepted`, `Rejected`, `Failed`), whether it is a test (`IsTest`), the choices made when it was submitted (`Options`), the agency's `ExternalId`, `ConfirmationNumber` and `Messages`.

| Interface | Registration | Responsibility |
| --- | --- | --- |
| `IEFileTargetProvider` (`TargetType`, `FindAsync`, `CanFileAsync`, `AcceptedAsync`) | `services.AddEFileTargetProvider<T>()` | Describes a kind of document as an `EFileTarget` (with `NotFileableReason` when it can't be filed now), checks who may file it, and marks it filed once accepted. Tax returns (`TaxReturn`) are built in: a return must be `Ready` and its period over (in the site's time zone), and needs `FileTaxReturns` |
| `IEFileFormat` (`Name`, `DisplayName`, `CanWrite`, `WriteAsync(target, submission)`) | `services.AddEFileFormat<T>()` | Writes a target into an `EFilePayload`; its `Problems` stop the submission. The submission, not saved yet, carries the `Options` chosen and `IsTest`. `TaxReturnJson` writes any return as JSON and needs the registration number |
| `IEFileTransport` (`Name`, `DisplayName`, `IsManual`, `SupportsTestSubmissions`, `CanSend`, `SendAsync`, `CheckAsync`) | `services.AddEFileTransport<T>()` | Sends the file and asks for the agency's answer. A channel with `SupportsTestSubmissions` sends a submission marked `IsTest` to the agency's test system. `Manual` hands over the file to download; the person records the answer |

`IEFilingService.SubmitAsync(request, user)` takes an `EFileSubmissionRequest`: the `Target`, the `Format` and `Transport` names, `IsTest`, and the `Options` for the format and the channel, kept on the submission. The `SubmitAsync(target, format, transport, user)` extension sends no options and no test. It refuses a test through a channel that takes none, a target already accepted (a test's acceptance does not count) or with a submission waiting, builds the file, sends it (a channel that throws leaves the submission `Failed`), and records the answer; `CheckAsync` asks a channel that is not manual; `RecordAnswerAsync(submission, accepted, confirmationNumber, filedOn, message, user)` records the answer to a manual submission. An accepted submission marks the document filed through its target provider (for a return, `MarkFiledAsync` with the confirmation number and the channel's name); when that fails, the submission stays `Submitted`. The **Tax e-filing acknowledgements** background task asks the channels for the answers to up to 50 submissions every 15 minutes.

**Test submissions.** An agency's acceptance testing (a state's ATS, for example) answers *accepted* without filing anything. A submission marked `IsTest`, asked for on the submit form or set by a channel whose `EFileTransportResult.IsTest` says it sent the file to a test system, is recorded with the agency's answer and confirmation number, but its acceptance never calls the target provider: the return stays ready to file, and a test the agency accepted does not stop the real submission.

**The submit form.** The **Electronic filing** card's form is built through `IDisplayManager<EFileSubmissionRequest>`. E-Filing's driver edits the format and channel and, when a channel offered takes tests, the **Test submission** box; a pack adds a display driver that edits its own choices into `request.Options` (such as whether the agency debits the payment and on which day), checking `request.Target` so they show only for its documents. The format reads them in `WriteAsync` and the channel in `SendAsync`, from `submission.Options`.

A state's own XML schema and gateway are a format and a transport in a pack; the `EFileSubmissions` step (entries `Submissions`) and the **E-file submissions** deployment step move submissions between sites.

## TaxJar

The **TaxJar** feature (`CrestApps.OrchardCore.Taxation.TaxJar`, it needs Taxation) works out tax with TaxJar for orders to the countries it's set up for, in place of the store's rules. The settings screen is described in the [User Manual](../user-manual/commerce/taxes.md#taxjar).

| | |
| --- | --- |
| **Settings** | **Settings > Commerce > TaxJar** (settings group `taxjar`, permission `ManageTaxation`), stored as `TaxJarSettings` |
| **Provider** | `TaxJarTaxDeterminationProvider` (`ITaxDeterminationProvider`, `Order` 100), with the named `HttpClient` `TaxJar` (10-second timeout) |
| **Options** | `TaxJarOptions`: `ApiKey` (unprotected), `UseSandbox`, `Countries`, and `BaseUrl` (`https://api.taxjar.com/v2/`, or `https://api.sandbox.taxjar.com/v2/` with the sandbox) |

`TaxJarSettings.ApiKey` is protected with data protection (purpose `CrestApps.OrchardCore.Taxation.TaxJar.ApiKey`). The settings page never shows it again, keeps it when the field is left blank, and removes it with **Remove the token**. `Countries` defaults to `US`. `TaxJarOptionsConfiguration` reads the settings into `TaxJarOptions`, which the provider reads through `IOptionsMonitor<TaxJarOptions>.CurrentValue`. Saving the settings calls `IOptionsUpdateNotifier.RequestUpdate<TaxJarOptions>()` (the options are registered with `AddSignalOptionsChangeTokenSource`), so the new settings are used once they are saved, on every node when a distributed signal is enabled, without restarting the tenant.

`CanHandle` is `true` when there's a token, `TaxCalculationContext.DefaultPriceType` is `Exclusive`, and the destination's country is in `Countries`. A context whose prices include tax, such as a checkout in a [market](markets.md#tax) whose prices include tax, stays with the store's rules, because TaxJar adds tax to a price.

`DetermineAsync` posts the order to `taxes` with the token as a bearer token:

| TaxJar field | From |
| --- | --- |
| `from_country`, `from_zip`, `from_state`, `from_city`, `from_street` | `TaxCalculationContext.Origin` (**Settings > Commerce > Tax origin**) |
| `to_country`, `to_zip`, `to_state`, `to_city`, `to_street` | `TaxCalculationContext.Destination` |
| `amount`, `shipping` | The non-shipping and the shipping items' unit price times quantity, less their discounts |
| `line_items` | Each non-shipping item: `id`, `quantity`, `unit_price`, `discount`, and `product_tax_code` from `TaxableItem.TaxClassificationCode` |
| `exemption_type` | `other` for a customer whose `CustomerTaxProfile.IsTaxExempt` is `true` |

The answer becomes one `TaxLine` for each line item, and one for shipping, that owes tax: `Rate` is TaxJar's `combined_tax_rate`, `TaxableAmount` and `TaxAmount` its `taxable_amount` and `tax_collectable`, `JurisdictionName` the jurisdictions joined as *city, county, state, country*, and `CalculationMethod` `TaxJar`. The tax type and name are `SalesTax` and *Sales tax* for the United States, and `VAT` and *VAT* elsewhere.

When TaxJar answers with an error, or doesn't answer, the provider logs it and throws an `InvalidOperationException`. `TaxService` catches a determination provider's failure (anything but cancellation), logs a warning, and works the tax out with the built-in engine, so a connector that's down doesn't stop sales.

## Per-category taxation with taxonomies

A common question is *"how do I apply a different tax code to a whole category of products — for example an excise tax on all Tobacco items?"*

You do **not** put multiple codes on each item. An item carries a single **tax category code** (plus an optional classification code); the *multiplicity* of applied taxes comes from multiple **rules** matching that one category in a jurisdiction. A rule with no `CategoryCode` matches every category (a general sales tax), and it naturally **stacks** with a `CategoryCode = "Tobacco"` excise rule so a pack of cigarettes is taxed by both.

To manage the category assignment per group of products rather than item-by-item, enable the **`OrchardCore.Taxonomies`** feature and let items **inherit** their classification from the taxonomy term they belong to:

1. Create a taxonomy (for example *Product Categories*) with terms such as *Electronics*, *Tobacco*, *Alcohol*.
2. Attach the **Taxation** part to the **term** content type, and set the **Tax category** (and optional classification) on each term — e.g. the *Tobacco* term gets category `Tobacco`.
3. Attach a **Taxonomy** field to your product type and tag each product with its term(s).

Now any product tagged *Tobacco* is taxed as `Tobacco` without setting a code on the product itself. The resolution precedence is:

1. The item's **own** `TaxationPart` category (an explicit code on the item always wins).
2. The classification supplied by the registered `ITaxClassificationProvider`s, consulted in ascending `Order`; the first provider that returns a non-empty category wins (the built-in provider reads the tagged taxonomy term's `TaxationPart`).
3. The type default configured in the TaxationPart settings.

Only the category is inherited when the item omits it; a classification code set explicitly on the item is preserved. Write your own `ITaxClassificationProvider` to source inherited codes from any other place (an ERP, a parent content item, etc.).

### Worked example — an excise tax on Tobacco

Suppose a US store sells electronics and tobacco. California charges 7.5% sales tax on everything **and** an extra 30% excise tax on tobacco.

1. **Categories** — create `ELEC` (Electronics) and `TOBACCO` (Tobacco).
2. **Taxonomy** — create a *Product Categories* taxonomy whose **term** content type has the **Taxation** part attached. On the *Tobacco* term set **Tax category = Tobacco**; on the *Electronics* term set **Tax category = Electronics**.
3. **Products** — add a **Taxonomy** field to the *Product* type and tag each product with its category term. You do **not** set a tax code on the product itself.
4. **Jurisdiction** — create *California* (`US` / `CA`, level *State*).
5. **Rules** in California:
   - a general rule with **no category** at 7.5% (applies to every product), and
   - a tobacco rule with **category `TOBACCO`** at 30%.

A television tagged *Electronics* inherits category `ELEC`, matches only the general rule → 7.5%. A pack of cigarettes tagged *Tobacco* inherits category `TOBACCO`, matches **both** rules → 7.5% + 30% stacked. No per-item tax codes were entered.

The whole setup is reproducible as a recipe — the taxonomy term simply carries a `TaxationPart` value:

```json
{
  "steps": [
    {
      "name": "content",
      "data": [
        {
          "ContentType": "ProductCategory",
          "DisplayText": "Tobacco",
          "TaxationPart": { "Taxable": true, "TaxCategoryCode": "TOBACCO" }
        }
      ]
    }
  ]
}
```

## Common scenarios

| You want to… | Do this |
|---|---|
| Apply one tax to everything | Create a jurisdiction and a single rule with **no category code**. Every taxable item matches it. |
| Tax a single product differently | Set the **Tax category** directly on that item's **Taxation** part. An explicit item code always overrides inheritance. |
| Give every item of a type a default code | Set the **default tax category** in the content type's **TaxationPart settings**. It applies when the item and its taxonomy term leave the code empty. |
| Tax a whole category/group differently | Enable **`OrchardCore.Taxonomies`**, attach the **Taxation** part to the term type, set the code on the term, and tag products with the term. See [Per-category taxation with taxonomies](#per-category-taxation-with-taxonomies). |
| Stack an extra tax (excise, environmental fee) on some items | Keep the general rule (no category) and add a second rule scoped to that category. Both match and stack. |
| Exempt a customer or region | Implement an `ITaxExemptionResolver`; see [Extending the framework](#extending-the-framework). |
| Delegate to Avalara / Stripe Tax | Register an `ITaxDeterminationProvider` that short-circuits the engine. |
| Move a catalog between environments | Export the categories, types, jurisdictions, tables, and rules as a [recipe or deployment plan](#recipes-and-deployment). |

## Recipes and deployment

The five catalog entities — **Tax categories**, **Tax types**, **Tax jurisdictions**, **Tax tables**, and **Tax rules** — can be imported and exported as code.

**Recipe steps** (names `TaxCategory`, `TaxType`, `TaxJurisdiction`, `TaxTable`, `TaxRule`) each take a plural array payload. Environment-owned fields (`CreatedUtc`, `ModifiedUtc`, `Author`, `OwnerId`, and `Version`) are never imported; `Version` is stamped by the environment and incremented on each save, so it stays an authoritative audit identity and a recipe can never regress or reuse it. An entry is matched by its `ItemId` and updated in place, or created when new. Names are unique within each entity: an entry whose name another entry already uses is reported as a recipe error and not saved, the same as in the admin:

```json
{
  "steps": [
    {
      "name": "TaxCategory",
      "TaxCategories": [
        { "ItemId": "electronics", "Name": "Electronics", "Code": "ELEC" },
        { "ItemId": "tobacco", "Name": "Tobacco", "Code": "TOBACCO" }
      ]
    },
    {
      "name": "TaxType",
      "TaxTypes": [
        { "ItemId": "sales-tax", "Name": "SalesTax" },
        { "ItemId": "excise-tax", "Name": "ExciseTax", "Description": "Excise duty on regulated goods." }
      ]
    },
    {
      "name": "TaxJurisdiction",
      "TaxJurisdictions": [
        { "ItemId": "us-ca", "Name": "California", "CountryCode": "US", "RegionCode": "CA", "Level": "State" }
      ]
    },
    {
      "name": "TaxTable",
      "TaxTables": [
        {
          "ItemId": "luxury-brackets",
          "Name": "Luxury brackets",
          "Rows": [
            { "Minimum": 0, "Maximum": 1000, "Rate": 0.05 },
            { "Minimum": 1000, "Rate": 0.10 }
          ]
        }
      ]
    },
    {
      "name": "TaxRule",
      "TaxRules": [
        { "ItemId": "ca-sales", "Name": "CA sales tax", "JurisdictionId": "us-ca", "Source": "Percentage", "Rate": 0.075 },
        { "ItemId": "ca-tobacco", "Name": "CA tobacco excise", "JurisdictionId": "us-ca", "CategoryCode": "TOBACCO", "Source": "Percentage", "Rate": 0.30 },
        { "ItemId": "ca-luxury", "Name": "CA luxury tax", "JurisdictionId": "us-ca", "Source": "Progressive", "TaxTableId": "luxury-brackets" }
      ]
    }
  ]
}
```

When the **`CrestApps.OrchardCore.Recipes`** feature is enabled, JSON Schema is contributed for each of these steps (and for the `TaxationPart` and its settings), giving editor validation and IntelliSense while authoring recipes.

### EU VAT standard rates

The Taxation module ships the **EU VAT standard rates** recipe (`EuVatStandardRates`, `Recipes/eu-vat.recipe.json`, not a setup recipe), run from **Configuration > Recipes**. It enables Taxation and adds the 27 EU member states as `TaxJurisdiction`s (level `Country`, with the ISO code as code and country, Greece as `GR`), and a `Percentage` `TaxRule` for each (`TaxType` `VAT`, `TaxName` *VAT*, the country's standard rate, `AppliesToShipping`, priority 0). Its entries have fixed `ItemId`s (`4evatj…` for jurisdictions, `4evatr…` for rules), so running it again updates the same entries. Rates change: check them before relying on the recipe, and add reduced-rate rules by category.

**Deployment steps** with the same five names are available under **Configuration → Deployment**, so an existing tenant's taxation catalog can be exported into a deployment plan and re-imported elsewhere. The exported JSON is identical in shape to the recipe payloads above.

### Tax registrations and exemption certificates

The `MerchantTaxRegistration` step (`MerchantTaxRegistrations` array) imports the store's own registrations, matched by `ItemId`, then by name; a registration needs a name and a `JurisdictionId`. The `ExemptionCertificate` step (`ExemptionCertificates` array) imports customers' certificates. A certificate keeps its `ItemId`, which the customer's `ExemptionCertificateIds` list; one without an id is matched by its `CustomerId` and `CertificateNumber`. A certificate needs its customer and a name or a number, and must expire after it takes effect. Import the customers and jurisdictions first. Both records are carried with their dates; only the store's document id is left out. The **Tax registrations** (`MerchantTaxRegistrationDeploymentStep`) and **Exemption certificates** (`ExemptionCertificateDeploymentStep`) deployment steps, under *Taxation*, write them in the same shape.

```json
{
  "steps": [
    {
      "name": "MerchantTaxRegistration",
      "MerchantTaxRegistrations": [
        { "ItemId": "4tr00000000000000000000001", "Name": "California sales tax", "JurisdictionId": "us-ca", "TaxType": "SalesTax", "RegistrationNumber": "123-456", "IsActive": true, "ThresholdAmount": 100000 }
      ]
    },
    {
      "name": "ExemptionCertificate",
      "ExemptionCertificates": [
        { "ItemId": "4ec00000000000000000000001", "Name": "Resale", "CertificateNumber": "R-1001", "CustomerId": "4cs00000000000000000000001", "ExemptionType": "Resale", "Status": "Active", "TaxTypes": [ "SalesTax" ], "JurisdictionIds": [ "us-ca" ] }
      ]
    }
  ]
}
```

## Taxes on orders and the one-stop shop

When a cart's checkout completes, `OrderCheckoutHandler` copies the invoice's tax lines to `Order.TaxLines` (`List<TaxLine>`), so an order keeps every tax it was charged: its name, type, jurisdiction, rate, taxable amount, amount and whether it was included in the price. The Orders API shows them as the order's `taxes`; see [Carts and Orders](carts-and-orders.md#taxes-and-culture-on-the-order).

The **VAT one-stop shop (OSS)** report (`OrderVatOssReport`, name `order-vat-oss`, registered by the Orders module when Reports is on, permission `ManageOrders`) adds them up for the EU one-stop-shop return. `OrderVatOss.Summarize(orders, originCountry)`:

- reads the orders paid in the period;
- takes each order's country from its shipping address, else its billing address, and keeps only EU member states (`OrderVatOss.EuCountries`, Greece as `GR`) other than the origin country (`TaxOriginSettings.Origin.Country`);
- adds up the tax lines whose `TaxType` is `VAT` and whose amount isn't zero, by country, rate and currency;
- takes refunds off in proportion: each line counts `1 - RefundedAmount / Total` of its taxable amount and tax.

The report shows, per currency, the VAT due and the number of countries, and a table of country, rate, orders, taxable amount and VAT, with a total. Orders paid before `Order.TaxLines` existed have no tax lines, so they aren't counted.

## Determinism

Given the same `TaxCalculationContext` plus the same tax rule and tax table versions, the result is always the same. The engine avoids hidden dependencies on mutable global state and uses the injected `IClock` for all time-based logic.
