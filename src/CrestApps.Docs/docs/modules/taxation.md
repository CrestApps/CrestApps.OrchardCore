---
sidebar_label: Taxation
sidebar_position: 15
title: Taxation Framework
description: A provider-agnostic, extensible taxation framework for Orchard Core that models tax as a determination rather than a stored rate.
---

| | |
| --- | --- |
| **Feature Name** | Taxation |
| **Feature ID** | `CrestApps.OrchardCore.Taxation` |
| **Category** | Commerce |

Provides a provider-agnostic, extensible, international taxation framework that any Orchard Core content type or CrestApps module (Products, Subscriptions, Payments, Commerce, Bookings, Services, and custom content) can plug into.

## Overview

The framework is built on a single principle:

> **Tax is a determination, not a property.**

Instead of storing a `TaxRate` on a product, the engine determines the applicable taxes from a combination of *what* is sold, *who* buys it, *where* it is sourced, *when* the transaction happens, and *how* the tax is calculated. The result is a deterministic, auditable, line-by-line breakdown that can be captured as an immutable snapshot on the transaction.

The framework is split into three assemblies:

| Assembly | Purpose |
|----------|---------|
| `CrestApps.OrchardCore.Taxation.Abstractions` | Interfaces, models, DTOs, enums, and constants. Lightweight, no infrastructure dependencies. |
| `CrestApps.OrchardCore.Taxation.Core` | The determination engine, default calculation methods, sourcing strategies, resolvers, catalog stores, and dependency-injection wiring. |
| `CrestApps.OrchardCore.Taxation` | The Orchard Core module: the `TaxationPart`, its editors, migration, and the content-item taxable-item provider. |

## Getting started

Enable the **Taxation** feature under **Tools → Features**, then:

1. Create your tax **categories**, **types**, **jurisdictions**, optional **tables**, and **rules** from the admin UI (see [Managing taxation from the admin UI](#managing-taxation-from-the-admin-ui)).
2. Attach the **Taxation** part to a content type and classify it (see [The TaxationPart](#the-taxationpart)).
3. At checkout, convert your objects into taxable items and call `ITaxService.CalculateAsync` (see [Calculating tax](#calculating-tax)).

## Managing taxation from the admin UI

Once the feature is enabled, an admin with the **Manage taxation** permission gets a **Commerce → Taxation** menu with five screens: **Categories**, **Types**, **Jurisdictions**, **Tables**, and **Rules**. Each screen is a searchable list with **Add**, **Edit**, and **Delete** actions, and is rendered through Orchard Core's display management so the list items and editors can be extended or overridden by other modules.

Setting up taxes end to end is a workflow of a few ordered steps. The order matters, because rules reference jurisdictions, types, and categories, and content classification reuses the categories you create.

### 1. Define what you sell — Categories

Go to **Commerce → Taxation → Categories** and add a category for each kind of thing you tax (for example `Electronics`, or a finer `Television`). A category has:

- **Name** — a human-readable label (the unique key, fixed after creation).
- **Code** — the value matched by tax rules and stored on taxable items (for example `Electronics`).
- **Parent code** — an optional parent category code, forming a hierarchy.
- **Description** — optional notes.

Create the broad categories you match rules against, plus any finer classifications you want to assign to individual items.

### 2. Name your kinds of tax — Types

Go to **Commerce → Taxation → Types** and manage the list of **tax types** — the labels that group and report the kind of tax a rule produces (for example `SalesTax`, `VAT`, or `GST`). A tax type has:

- **Name** — the value stored on the resulting tax lines and offered in the rule editor (the unique key, fixed after creation).
- **Description** — optional notes.

The catalog is seeded with a set of well-known types on first run so existing behavior is preserved, but the list is fully editable: add the types your business uses, rename or remove the ones you do not. A tax type is a reporting and grouping label only — it never changes how an amount is calculated.

### 3. Define where you tax — Jurisdictions

Go to **Commerce → Taxation → Jurisdictions** and add a taxing authority for each place you collect tax. Jurisdictions are hierarchical (country → region → county → city → special district) and are matched to an address by their non-empty components. Key fields include the **Level**, an optional **Parent jurisdiction**, the geographic components (**Country**, **Region**, **County**, **City**, **Postal code**), and optional **Effective from/to** dates. **Country** is chosen from a dropdown of ISO 3166-1 countries (the stored value is the ISO country code), and **Effective from/to** are date-only pickers.

:::tip
When the [Addresses](./addresses.md) feature is enabled, the **Country** dropdown is populated from your managed `Country` content items instead of the built-in list, so you can curate the countries you operate in. The stored value is still the ISO country code, so switching the Addresses feature on or off never orphans existing jurisdictions or rules.
:::

### 4. Provide lookup tables — Tables (optional)

Go to **Commerce → Taxation → Tables** when you need a rule whose amount depends on where the taxable base falls in a set of brackets rather than a single rate. A tax table is a named, versioned list of rows, and it is the data source for the **Tax table lookup**, **Progressive**, and **Threshold** calculation methods. Skip this step if you only use flat rates or fixed amounts. A tax table has:

- **Name** — a human-readable label (the unique key, fixed after creation).
- **Description** — optional notes.
- **Effective from/to** — optional date-only window during which the table is valid. A table is only used for a transaction whose date falls inside this window; a rule that requires a table but has no table in effect on the transaction date is **skipped** (it never silently taxes at zero).
- **Rows** — one or more brackets, each with a **Minimum** (inclusive lower bound), an optional **Maximum** (exclusive upper bound; leave empty for the top bracket), a **Rate**, a **Fixed amount**, and an optional **Base amount**. Use **Add row** / **Remove** to manage the list.

Rows are validated when you save. Minimums cannot be negative, at most one open-ended row (no maximum) is allowed, bounded ranges must be ordered and non-overlapping, and an open-ended row must start at or above the highest bounded maximum. This prevents overlapping brackets from double-counting in progressive calculations. A table that is still referenced by a rule cannot be deleted; remove or repoint the rules first.

How a rule reads the rows depends on the method it uses:

- **Tax table lookup** finds the single row whose `Minimum ≤ base < Maximum` and charges `base × Rate + Fixed amount`.
- **Progressive** taxes each bracket only on the portion of the base that falls inside it, then sums the brackets (classic tiered/marginal calculation).
- **Threshold** taxes only the amount **above** the matching row's minimum.

Each time you save a table its **Version** is incremented, and that version is captured on every tax line and [snapshot](#snapshots-immutability-and-refunds) that used it, so historical calculations stay reproducible even after you edit the table.

### 5. Define how tax applies — Rules

Go to **Commerce → Taxation → Rules** and click **Add Tax Rule**. A dialog lists the available **calculation methods** — Percentage, Fixed amount, Per unit, Per weight, Per volume, Progressive, Threshold, and Tax table lookup. The method you pick becomes the rule's **source** and is fixed for the life of the rule, exactly like the source-aware editors used elsewhere in the platform (for example AI data sources and deployments). The editor then shows the fields shared by every rule — the **Name** (the rule's unique identifier, fixed after creation), the **Jurisdiction**, the **Category** it applies to (or *Any category*), the **Tax type**, an optional **Display name**, the **Customer type** (or *Any customer*), a **Priority**, **Effective from/to** dates, minimum/maximum thresholds, and flags such as **Enabled**, **Included in price**, **Compound**, **Reverse charge**, and **Applies to shipping** — plus only the calculation fields the selected method needs: a **Rate** for the percentage method, a **Fixed amount** for the fixed/per-unit/per-weight/per-volume methods, or a **Tax table** selector for the table-driven methods (`TaxTable`, `Progressive`, `Threshold`), populated from the tables you created in step 4. A disabled rule is never applied, and rules outside their effective window are ignored.

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

The `TaxName` is optional; when it is left empty the tax line falls back to the rule `Name`. Rules carry `Version`, `Priority`, `EffectiveFromUtc`, `EffectiveToUtc`, `MinimumAmount`, `MaximumAmount`, `CustomerType`, `IsCompound`, `IncludedInPrice`, `ReverseCharge`, and `AppliesToShipping`. Historical rules must remain immutable once used by a transaction; publish a new version rather than mutating a rule that has already been applied.

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

Rounding is explicit and controlled by `TaxationOptions` (or `TaxCalculationContext.RoundingLevel`):

| Level (`TaxRoundingLevel`) | Behavior |
|-------|----------|
| `Line` | Each tax line is rounded independently. |
| `Tax` | Amounts are aggregated per tax type, then rounded. |
| `Jurisdiction` | Amounts are aggregated per jurisdiction, then rounded. |
| `Transaction` | The whole transaction is rounded once. |

`TaxationOptions` also configures `DecimalPlaces`, `MidpointRounding`, and per-currency decimal overrides.

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

An `ITaxDeterminationProvider` whose `CanHandle` returns `true` takes over the entire calculation, which is how third-party tax services (for example `CrestApps.OrchardCore.Taxation.Avalara` or `CrestApps.OrchardCore.Taxation.Stripe`) can be layered on top of the same abstractions.

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

**Recipe steps** (names `TaxCategory`, `TaxType`, `TaxJurisdiction`, `TaxTable`, `TaxRule`) each take a plural array payload. Environment-owned fields (`CreatedUtc`, `ModifiedUtc`, `Author`, `OwnerId`, and `Version`) are never imported; `Version` is stamped by the environment and incremented on each save, so it stays an authoritative audit identity and a recipe can never regress or reuse it. An entry is matched by its `ItemId` and updated in place, or created when new:

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

**Deployment steps** with the same five names are available under **Configuration → Deployment**, so an existing tenant's taxation catalog can be exported into a deployment plan and re-imported elsewhere. The exported JSON is identical in shape to the recipe payloads above.

## Determinism

Given the same `TaxCalculationContext` plus the same tax rule and tax table versions, the result is always the same. The engine avoids hidden dependencies on mutable global state and uses the injected `IClock` for all time-based logic.
