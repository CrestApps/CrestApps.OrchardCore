---
sidebar_label: Rule Condition Schemas
sidebar_position: 6.6
title: Rule Condition Schemas
description: Describe every rule condition and operator so the Layers recipe step produces a complete, self-documenting JSON schema.
---

| | |
| --- | --- |
| **Feature Name** | CrestApps Recipes |
| **Feature ID** | `CrestApps.OrchardCore.Recipes` |

The `Layers` recipe step imports display layers. Each layer carries a `LayerRule`, a structured rule made of a `Conditions` array. A condition can be a leaf, such as a `UrlCondition` that compares the request URL against a value, or a group, such as an `AnyConditionGroup` that nests more conditions. Every condition stores a polymorphic `$type` discriminator, and value based conditions embed an `Operation` operator that itself carries a `$type`. Without extra metadata, a JSON schema can only say "conditions are objects" — it cannot tell you that a `UrlCondition` needs a `Value` and an `Operation`, that a `StringStartsWithOperator` accepts a `CaseSensitive` flag, or that an `AnyConditionGroup` nests its own `Conditions`.

**Rule condition schema definitions** close that gap. They follow the same extensibility model already used by `IWorkflowActivitySchemaDefinition`: each condition and each operator contributes its own schema fragment, and the `Layers` recipe step composes all of them into a single, fully described schema.

## Feature gating

The rule schema services are registered by the Recipes module only when the `OrchardCore.Layers` feature is enabled, because `OrchardCore.Layers` depends on `OrchardCore.Rules`. When Layers is enabled, every built-in condition and operator from `OrchardCore.Rules` is described, and the `Layers` step embeds the composed `LayerRule` schema.

## What the schema describes

For every registered condition the composed schema declares:

- **`$type`** — the polymorphic type discriminator serialized by Orchard Core, for example `OrchardCore.Rules.Models.UrlCondition, OrchardCore.Rules`. Required, because nested conditions inside a group deserialize as a polymorphic `List<Condition>`.
- **`Name`** — the condition type name, for example `UrlCondition`. Required, because the `Layers` step resolves the top-level conditions through the condition factory by name.
- **`ConditionId`** — a stable identifier. Generated when the recipe runs if omitted.
- **Per-condition members** — every member the condition persists, each emitted as a real JSON Schema member with its type and a description derived from the condition's editor view. Value based conditions add a `Value` and an `Operation`; condition groups add a `DisplayText` and a recursive `Conditions` array.

For every registered operator the schema declares its `$type` and its members, for example the `CaseSensitive` flag shared by every string operator.

Display text is not emitted into the JSON Schema. It is carried on the `RuleConditionDescriptor` and `RuleConditionOperatorDescriptor` returned by `IRuleSchemaService`, so other features can build their own listings.

### The condition stays open

Every condition object allows additional properties, so a recipe exported from a tenant with extra modules enabled still validates. Unknown or custom conditions are accepted as long as they supply a `$type` and a `Name`, which is what makes the schema extensible.

## Built-in conditions and operators

When `OrchardCore.Layers` is enabled the following are described out of the box.

| Condition | Purpose |
| --- | --- |
| `UrlCondition` | Compares the request URL against a value using an operator. |
| `ContentTypeCondition` | Compares the displayed content type against a value using an operator. |
| `RoleCondition` | Compares the current user's roles against a value using an operator. |
| `CultureCondition` | Compares the current UI culture against a value using an operator. |
| `BooleanCondition` | A fixed true or false value. |
| `HomepageCondition` | Matches the site homepage. |
| `JavascriptCondition` | A JavaScript script that returns true or false. |
| `IsAnonymousCondition` | Matches an anonymous (unauthenticated) user. |
| `IsAuthenticatedCondition` | Matches an authenticated user. |
| `AllConditionGroup` | A group that requires every nested condition to be true. |
| `AnyConditionGroup` | A group that requires at least one nested condition to be true. |

| Operator | Display text |
| --- | --- |
| `StringEqualsOperator` | Equals |
| `StringNotEqualsOperator` | Does not equal |
| `StringStartsWithOperator` | Starts with |
| `StringNotStartsWithOperator` | Does not start with |
| `StringEndsWithOperator` | Ends with |
| `StringNotEndsWithOperator` | Does not end with |
| `StringContainsOperator` | Contains |
| `StringNotContainsOperator` | Does not contain |

## How it is composed

A condition entry in the schema is an object with a shared shape (`$type`, `Name`, `ConditionId`) plus a list of conditional branches, one per condition, selected by the `Name` const. Operators use the same pattern, selected by the `$type` const:

```jsonc
{
  "allOf": [
    // One branch per condition, selecting the matching members.
    { "if": { "properties": { "Name": { "const": "UrlCondition" } } },
      "then": { "properties": { "$type": {}, "Value": {}, "Operation": { /* operator union */ } } } },

    // Groups add a recursive Conditions array.
    { "if": { "properties": { "Name": { "const": "AnyConditionGroup" } } },
      "then": { "properties": { "Conditions": { "items": { /* condition union */ } } } } }
  ]
}
```

Editors and AI agents that understand JSON Schema will therefore offer the correct member list and descriptions as soon as the condition name is typed.

Because JSON has no references, the recursion of a condition group is inlined to a bounded depth. Groups nested within that depth are fully described; conditions nested deeper still validate, but only against the shared `$type`, `Name` and open member shape.

A complete `LayerRule` looks like this:

```json
{
  "name": "Layers",
  "Layers": [
    {
      "Name": "Knowledge Base",
      "LayerRule": {
        "Conditions": [
          {
            "$type": "OrchardCore.Rules.Models.AnyConditionGroup, OrchardCore.Rules",
            "Name": "AnyConditionGroup",
            "Conditions": [
              {
                "$type": "OrchardCore.Rules.Models.UrlCondition, OrchardCore.Rules",
                "Name": "UrlCondition",
                "Value": "/knowledge-base/category/",
                "Operation": {
                  "$type": "OrchardCore.Rules.Models.StringStartsWithOperator, OrchardCore.Rules",
                  "CaseSensitive": false
                }
              }
            ]
          }
        ]
      }
    }
  ]
}
```

## Describing a custom condition

Create a class deriving from `RuleConditionSchemaDefinitionBase` (or `OperandConditionSchemaDefinitionBase` for a value plus operator condition, or `ConditionGroupSchemaDefinitionBase` for a group). The class name conventionally matches the condition name with a `Schema` suffix.

```csharp
using CrestApps.OrchardCore.Recipes.Core.Schemas.Rules;
using Json.Schema;

namespace MyCompany.Rules.Schemas;

public sealed class WeatherConditionSchema : RuleConditionSchemaDefinitionBase
{
    public override string Name { get; } = "WeatherCondition";

    public override string TypeDiscriminator { get; } = "MyCompany.Rules.WeatherCondition, MyCompany.Rules";

    protected override string DisplayText => "Weather";

    protected override string Description => "Matches when the current temperature crosses a threshold.";

    protected override IEnumerable<string> RequiredProperties => ["Temperature"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(RuleConditionSchemaContext context)
    {
        yield return ("Temperature", RuleConditionSchemaBuilders.Boolean("The threshold in Celsius the current temperature is compared against."));
    }
}
```

A value plus operator condition reuses the shared operator schema through the context:

```csharp
public sealed class RefererConditionSchema : OperandConditionSchemaDefinitionBase
{
    public override string Name { get; } = "RefererCondition";

    public override string TypeDiscriminator { get; } = "MyCompany.Rules.RefererCondition, MyCompany.Rules";

    protected override string DisplayText => "Referer";

    protected override string ValueDescription => "The referer URL the operator compares the request referer against.";
}
```

Register the definitions from your module's `Startup` class, ideally behind `[RequireFeatures("OrchardCore.Rules")]`:

```csharp
services
    .AddRuleConditionSchema<WeatherConditionSchema>()
    .AddRuleConditionSchema<RefererConditionSchema>();
```

## Describing a custom operator

Create a class deriving from `RuleConditionOperatorSchemaDefinitionBase` (or `StringOperatorSchemaDefinitionBase` when the operator is a string comparison that carries a `CaseSensitive` flag).

```csharp
using CrestApps.OrchardCore.Recipes.Core.Schemas.Rules;
using Json.Schema;

namespace MyCompany.Rules.Schemas;

public sealed class RegexMatchOperatorSchema : RuleConditionOperatorSchemaDefinitionBase
{
    public override string Name { get; } = "RegexMatchOperator";

    public override string TypeDiscriminator { get; } = "MyCompany.Rules.RegexMatchOperator, MyCompany.Rules";

    protected override string DisplayText => "Matches pattern";

    protected override string Description => "Matches when the value matches the regular expression.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("IgnoreCase", RuleConditionSchemaBuilders.Boolean("Whether the pattern is matched case insensitively."));
    }
}
```

Register it from your module's `Startup` class:

```csharp
services.AddRuleConditionOperatorSchema<RegexMatchOperatorSchema>();
```

Both registration extensions live in `Microsoft.Extensions.DependencyInjection`. Definitions are resolved once per name; if two definitions declare the same `Name`, the last one registered wins, which lets you override a built-in description.

### Available property builders

`RuleConditionSchemaBuilders` provides the fragments used by every built-in definition:

| Builder | Use for |
| --- | --- |
| `String(description)` | A plain string member. |
| `NullableString(description)` | A string member that also accepts `null`. |
| `Boolean(description)` | A boolean member. |

### Rules to follow

- **Match the type discriminator exactly.** `TypeDiscriminator` must equal the value Orchard Core serializes, in the form `Namespace.TypeName, AssemblyName`. Nested conditions and operators fail to deserialize otherwise.
- **Match `Name` to the condition factory.** For the built-in conditions the `Name` equals the type name, for example `UrlCondition`.
- **Describe every supported member.** Because every condition object stays open, an omitted member will not fail validation — it will silently lose its documentation and type.
- **Use the display text from the editor.** Operator display texts come from the invariant key inside `S["..."]`, for example `S["Does not equal"]`.
- **Reuse the operator schema through the context.** Value based conditions must expose their `Operation` through `context.OperatorSchema` so every operator stays available and future operators are picked up automatically.

## Auditing coverage

The unit test suite validates the composed schema against complete `LayerRule` payloads, including nested groups, confirms that custom or unknown conditions are accepted, and asserts that a condition missing its `$type` discriminator is rejected. When you add a new condition or operator to a CrestApps module, add its schema definition and its registration in the same change so the `Layers` step keeps describing every supported rule.
