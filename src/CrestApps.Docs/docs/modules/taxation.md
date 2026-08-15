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

1. Attach the **Taxation** part to a content type and classify it (see [The TaxationPart](#the-taxationpart)).
2. Seed jurisdictions, categories, and rules (see [Domain model](#domain-model)).
3. At checkout, convert your objects into taxable items and call `ITaxService.CalculateAsync` (see [Calculating tax](#calculating-tax)).

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
| `DefaultTaxCategoryCode` | The category applied when the item does not specify one. |
| `DefaultTaxClassificationCode` | The classification applied when the item does not specify one. |
| `AllowClassificationOverride` | Whether editors may override the classification per content item. |

## Domain model

All taxation data is stored through the CrestApps catalog abstraction, so each entity is versioned, named, and clonable.

| Entity | Store | Purpose |
|--------|-------|---------|
| `TaxJurisdiction` | `ITaxJurisdictionStore` | A hierarchical taxing authority (country → state/province → county → city → special district), matched to an address by its non-empty components. |
| `TaxCategory` | `ITaxCategoryStore` | A classification such as `Electronics/Television` or `Alcohol/Wine`, with optional external codes. |
| `TaxRule` | `ITaxRuleStore` | Determines whether and how a tax applies. Versioned, prioritized, with effective dates. |
| `TaxTable` | `ITaxTableStore` | Brackets, ranges, and schedules for progressive, threshold, and lookup taxes. |
| `ExemptionCertificate` | `IExemptionCertificateStore` | A customer exemption scoped by tax type, jurisdiction, and classification. |
| `MerchantTaxRegistration` | `IMerchantTaxRegistrationStore` | The merchant's obligation (nexus) to collect a tax in a jurisdiction. |

### Tax rules

A `TaxRule` binds a jurisdiction, tax type, classification, and customer criteria to a calculation method:

```csharp
await ruleStore.CreateAsync(new TaxRule
{
    Name = "California sales tax",
    TaxType = TaxTypeNames.SalesTax,
    TaxName = "CA Sales Tax",
    TaxCode = "US-CA-SALES",
    JurisdictionId = californiaJurisdictionId,
    CategoryCode = "Electronics",
    CalculationMethod = TaxCalculationMethodNames.Percentage,
    Rate = 0.075m,
});
```

Rules carry `Version`, `Priority`, `EffectiveFromUtc`, `EffectiveToUtc`, `MinimumAmount`, `MaximumAmount`, `CustomerType`, `IsCompound`, `IncludedInPrice`, and `AppliesToShipping`. Historical rules must remain immutable once used by a transaction; publish a new version rather than mutating a rule that has already been applied.

### Nexus vs. a jurisdiction having a tax

The engine distinguishes **a jurisdiction has a tax** from **the merchant is obligated to collect it**. When no `MerchantTaxRegistration` records exist, the engine operates in permissive manual-rule mode and collects the tax. As soon as registrations exist, a rule's tax is only collected when an active registration covers its jurisdiction and tax type.

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
| `RuleId`, `RuleVersion`, `TableId`, `TableVersion` | Audit references to the exact rule and table versions used. |

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

Set `TaxableItem.PriceIncludesTax` (or `TaxCalculationContext.DefaultPriceType`) to control whether the price already contains tax. For inclusive percentage taxes the engine nets the base out of the gross price before applying each rule, so the total stays equal to the displayed price. Inclusive extraction is supported for percentage taxes.

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
| `ITaxCalculationMethod` | A calculation strategy resolvable by name. |
| `ITaxSourcingStrategy` | Where the tax is sourced from. |
| `ITaxRuleProvider` | Supplies applicable rules for a query. |
| `ITaxJurisdictionResolver` | Maps an address to jurisdictions. |
| `ITaxExemptionResolver` | Determines customer exemptions. |
| `IMerchantTaxRegistrationProvider` | Determines merchant nexus. |
| `ITaxableBaseCalculator` | Computes the taxable base for an item. |
| `ITaxRoundingStrategy` | Applies the rounding policy. |
| `ITaxSnapshotFactory` | Captures immutable transaction snapshots. |
| `ITaxRefundCalculator` | Derives full and proportional refund tax from a snapshot. |
| `ITaxDeterminationProvider` | Short-circuits the engine with an external determination. |

An `ITaxDeterminationProvider` whose `CanHandle` returns `true` takes over the entire calculation, which is how third-party tax services (for example `CrestApps.OrchardCore.Taxation.Avalara` or `CrestApps.OrchardCore.Taxation.Stripe`) can be layered on top of the same abstractions.

## Determinism

Given the same `TaxCalculationContext` plus the same tax rule and tax table versions, the result is always the same. The engine avoids hidden dependencies on mutable global state and uses the injected `IClock` for all time-based logic.
