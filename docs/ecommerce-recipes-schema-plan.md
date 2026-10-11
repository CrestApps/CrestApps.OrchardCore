# Commerce Recipes, Deployment & Part-Schema Coverage Plan

> **Historical implementation record.** The current ecommerce roadmap is
> [`docs/ecommerce-plan.md`](./ecommerce-plan.md). Use that document for the verified current
> baseline and remaining recipe, deployment, and schema work. The findings below describe the
> earlier recipe/schema effort and may contain superseded “missing” or “excluded” statements.

This plan closes the **import/export and recipe-schema gaps** across the commerce modules added on
the `ma/subscribtions` branch, so the modules are consistent with the solution-wide Orchard Core
recipe/deployment conventions and are a clean foundation for the future e-commerce modules.

It is scoped strictly to *foundation consistency*. It does **not** implement any greenfield e-commerce
functionality.

## 1. Goal

For every **user-addable entity** added in this branch, provide an Orchard Core **Deployment step**
and a **Recipe step** (import/export). For every **custom content part** added in this branch,
provide a **content part schema definition** (`IContentSchemaDefinition` via `PartSchemaDefinitionBase`)
registered when the `CrestApps.OrchardCore.Recipes` feature is enabled — exactly as the Taxation
module already does.

## 2. Current-state findings (verified against the code)

### 2.1 User-addable entities — already complete

The only user-addable catalog entities in this branch live in **Taxation**, and all four already have
a **complete** recipe + deployment + recipe-schema implementation:

| Entity | Recipe step handler | Deployment source/step/driver | Recipe schema (`IRecipeStep`) |
| --- | --- | --- | --- |
| `TaxCategory` | `Recipes/TaxCategoryStep.cs` | `Deployments/**/TaxCategory*` | `Schemas/TaxCategoryRecipeStep.cs` |
| `TaxType` | `Recipes/TaxTypeStep.cs` | `Deployments/**/TaxType*` | `Schemas/TaxTypeRecipeStep.cs` |
| `TaxJurisdiction` | `Recipes/TaxJurisdictionStep.cs` | `Deployments/**/TaxJurisdiction*` | `Schemas/TaxJurisdictionRecipeStep.cs` |
| `TaxRule` | `Recipes/TaxRuleStep.cs` | `Deployments/**/TaxRule*` | `Schemas/TaxRuleRecipeStep.cs` |

All four are registered in `ConfigurationDeploymentStartup` (`[RequireFeatures("OrchardCore.Deployment")]`)
and `RecipesSchemaStartup` (`[RequireFeatures("CrestApps.OrchardCore.Recipes")]`).

**Deliberately excluded (YAGNI):** `ExemptionCertificate`, `MerchantTaxRegistration`, and `TaxTable`
are catalog-backed types but have **no admin management UI** (no controller, not in the admin menu),
so they are not entities "the user can add" today. Adding import/export steps for them now would be
speculative. When a management surface is added for any of them, its deployment/recipe steps should
be added at that time following the Taxation pattern.

**Conclusion:** no new entity recipe/deployment steps are required. This is a verification result, not
an omission.

### 2.2 Custom content parts — schema definitions missing

The branch adds five custom content parts. Only `TaxationPart` has a schema definition today:

| Part | Module | Attachable? | Data fields | Settings | Schema today |
| --- | --- | --- | --- | --- | --- |
| `TaxationPart` | Taxation | yes | Taxable, TaxCategoryCode, TaxClassificationCode, ExternalTaxCode | `TaxationPartSettings` | ✅ present |
| `ProductPart` | Products | yes | Price (decimal), Sku (string) | `ProductPartSettings { Type }` | ❌ missing |
| `SubscriptionPart` | Subscriptions | system-defined | InitialAmountDescription, InitialAmount?, BillingDuration, DurationType, BillingCycleLimit?, SubscriptionDayDelay?, Sort? | none | ❌ missing |
| `TenantOnboardingPart` | Subscriptions (TenantOnboarding feature) | yes | RecipeName, FeatureProfile | none | ❌ missing |
| `SubscriptionSummaryPart` | Subscriptions | yes (dashboard widget) | none (marker) | none | ❌ missing |

This is the **real gap** and the focus of the implementation.

## 3. Proposed changes

Follow the established convention exactly (`RolePickerPartSchemaDefinition`,
`TaxationPartSchemaDefinition`, etc.): a sealed `PartSchemaDefinitionBase` subclass per part, an
`IContentSchemaDefinition` registration inside a `[RequireFeatures("CrestApps.OrchardCore.Recipes")]`
startup, and a `Recipes.Core` project reference on the module.

### Item A — Products: `ProductPartSchemaDefinition`

- **New file** `src/Modules/CrestApps.OrchardCore.Products/Schemas/ProductPartSchemaDefinition.cs`.
  - `Name => nameof(ProductPart)`.
  - `BuildSettingsCore`: `ProductPartSettings.Type` as a string enum (`Undefined`, `Good`, `Service`,
    `Digital`).
  - `BuildPartSchemaCore`: `Price` (number), `Sku` (string).
- **New file** `src/Modules/CrestApps.OrchardCore.Products/RecipesSchemaStartup.cs`
  (`[RequireFeatures("CrestApps.OrchardCore.Recipes")]`) registering the definition.
- **csproj**: add `ProjectReference` to `CrestApps.OrchardCore.Recipes.Core`.

### Item B — Subscriptions: `SubscriptionPartSchemaDefinition`, `TenantOnboardingPartSchemaDefinition`, `SubscriptionSummaryPartSchemaDefinition`

- **New files** under `src/Modules/CrestApps.OrchardCore.Subscriptions/Schemas/`:
  - `SubscriptionPartSchemaDefinition.cs` — payload = the seven `SubscriptionPart` fields with correct
    JSON types (`integer`, nullable `integer`, `number` for the nullable `InitialAmount` decimal,
    `DurationType` as a string enum: `Year`, `Month`, `Week`, `Day`); empty settings envelope.
  - `TenantOnboardingPartSchemaDefinition.cs` — payload = `RecipeName`, `FeatureProfile`.
  - `SubscriptionSummaryPartSchemaDefinition.cs` — marker part; payload is an open object
    (`additionalProperties: true`) with a description noting it carries no persisted data. Included for
    convention completeness and to document the part in the content-definition schema.
- **New file(s)** registering the definitions when Recipes is enabled:
  - `RecipesSchemaStartup.cs` (`[RequireFeatures("CrestApps.OrchardCore.Recipes")]`) registers
    `SubscriptionPartSchemaDefinition` and `SubscriptionSummaryPartSchemaDefinition`.
  - `TenantOnboardingRecipesSchemaStartup.cs`
    (`[Feature(TenantOnboarding)]` + `[RequireFeatures("CrestApps.OrchardCore.Recipes")]`) registers
    `TenantOnboardingPartSchemaDefinition`, matching the feature that owns the part.
- **csproj**: add `ProjectReference` to `CrestApps.OrchardCore.Recipes.Core`.

### Item C — Tests

Extend `tests/CrestApps.OrchardCore.Tests/Core/Schemas/PartSchemaDefinitionTests.cs`:
- Add the four new definitions to the `Name`, `Type`, serializable-schema, and caching theories.
- Add focused `[Fact]`s asserting the payload schema contains the expected field names and that the
  enum values (`ProductType`, `DurationType`) are present.

### Item D — Documentation

- `src/CrestApps.Docs/docs/modules/products.md` — note the ProductPart recipe schema.
- `src/CrestApps.Docs/docs/modules/subscriptions.md` — note the subscription part schemas.
- `src/CrestApps.Docs/docs/modules/taxation.md` (if present) — confirm the existing recipe/deployment
  steps are documented (they should already be).
- `src/CrestApps.Docs/docs/changelog/3.0.0.md` — one bullet: commerce parts now contribute recipe
  content-definition schemas.

## 4. Why this improves the architecture / supports future e-commerce

- **Consistency:** every custom part now participates in the same recipe-schema tooling as the rest of
  the solution; authors get validation/intellisense for commerce content definitions.
- **Tooling, not transport:** the runtime import/export of part *definitions* is already provided by
  Orchard's built-in `ContentDefinition` step, and content *items* (products, plans) by the built-in
  `Content` step. These schema classes only *describe and validate* those payloads — they add authoring
  safety and documentation, so a future e-commerce module can ship seed/demo recipes for products and
  plans with confidence. They do not themselves move data.
- **No new abstractions:** reuses `PartSchemaDefinitionBase` / `IContentSchemaDefinition` — nothing new
  is invented.

## 5. Risks & mitigations

- **Wrong JSON type for money/nullable fields.** Mitigated by unit tests that serialize each schema and
  assert field presence; `InitialAmount` and the nullable ints are modeled as nullable.
- **Feature gating.** `TenantOnboardingPart` schema is gated on its owning feature so it is not
  advertised when that feature is off. Other parts belong to the always-on Subscriptions feature.
- **Project-reference creep.** Adding `Recipes.Core` to two modules mirrors Taxation/Roles/Users/AI.Chat;
  the schema startups are `[RequireFeatures("CrestApps.OrchardCore.Recipes")]`, so nothing runs unless
  the Recipes feature is enabled.

## 6. Explicitly NOT doing (avoid over-engineering)

- No import/export steps for `ExemptionCertificate` / `MerchantTaxRegistration` / `TaxTable` (no
  management UI yet).
- No new recipe steps for content items (subscription plans/products are content items already exported
  by the built-in `Content` step).
- No changes to the already-complete Taxation recipe/deployment/schema code beyond documentation.
- No new base classes, no generalized "schema registry", no provider abstractions.

## 7. Sequence

A → build/test → B → build/test → C → build/test → D → docs build → full solution build + full test
suite → browser tests (Pay Later, customer management, admin management) → final review.

Each implementation item is independently challenged with GPT-5.6 before moving on.

## 8. Independent review outcome (GPT-5.6 challenge)

The plan was independently challenged with GPT-5.6: **APPROVE-WITH-CHANGES** (no blocking issue).
Adjustments incorporated:

- Model money/decimal fields as JSON `number`, nullable fields as `type: [T, "null"]` (e.g.
  `InitialAmount` → `number | null`, nullable ints → `integer | null`). No `multipleOf`, `minimum`,
  or currency constraints — these schemas validate shape, not business rules. Fields stay non-required.
- Include `SubscriptionSummaryPart` minimally (open-object payload; no invented properties).
- Include `SubscriptionPart` even though it is system-defined — it still appears in `ContentDefinition`
  exports.
- Corrected the wording above: schemas provide **validation/tooling**, not the import/export transport
  (built-in steps already do that).
- Tests assert schema *semantics* (nullable accepts null, decimals are `number`, enum values present,
  marker schema serializable) rather than only string presence.
- The reviewer suggested the browser-test phase is optional for *this* schema effort; it is retained
  because the **user explicitly requires** browser testing of the Stripe and Pay Later subscription
  flows and customer/admin subscription management. That phase is tracked separately from this schema
  work.
