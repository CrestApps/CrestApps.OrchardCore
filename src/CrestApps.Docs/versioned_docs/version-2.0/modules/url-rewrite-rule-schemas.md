---
sidebar_label: URL Rewrite Rule Schemas
sidebar_position: 6.11
title: URL Rewrite Rule Schemas
description: Describe every rewrite rule source so the UrlRewriting recipe step produces a complete, self-documenting JSON schema.
---

| | |
| --- | --- |
| **Feature Name** | CrestApps Recipes |
| **Feature ID** | `CrestApps.OrchardCore.Recipes` |

The `UrlRewriting` recipe step creates or updates URL rewrite rules. Each entry of its `Rules` array is polymorphic: a `Source` discriminator selects the rewrite rule source, and the well known members of the rule depend on that value. A `Redirect` rule carries a `Pattern`, a `SubstitutionPattern` and a `RedirectType`, while a `Rewrite` rule carries a `Pattern`, a `SubstitutionPattern` and a `SkipFurtherRules` flag. Without extra metadata, a JSON schema can only say "a rule is an object" — it cannot tell you that a `Redirect` rule needs a `Pattern` and a `RedirectType`, or that a `Rewrite` rule can skip further rules.

**URL rewrite rule source schema definitions** close that gap. They follow the same extensibility model already used by `IWorkflowActivitySchemaDefinition`, `IRuleConditionSchemaDefinition`, `ISitemapSourceSchemaDefinition`, `IDeploymentStepSchemaDefinition`, `IAdminNodeSchemaDefinition` and `IQuerySourceSchemaDefinition`: each rewrite rule source contributes its own schema fragment, and the `UrlRewriting` recipe step composes all of them into a single, fully described schema.

## Feature gating

The rewrite rule schema service and its built-in `Redirect` and `Rewrite` sources are registered by the Recipes module only when the `OrchardCore.UrlRewriting` feature is enabled, so a source is described when the feature that owns it is enabled.

## What the schema describes

For every entry of the `Rules` array the composed schema declares the shared members every rule carries, including:

- **`Id`** — the identifier of an existing rule to update. Leave it empty to create a new rule.
- **`Name`** — the display name of the rule.
- **`Source`** — the rewrite rule source provider name, for example `Redirect` or `Rewrite`. The suggestions list every source available on the tenant. The well known members of the rule depend on this value.
- **`Order`** — the order in which the rule is evaluated relative to the other rules.

For every described source the schema narrows the members with a conditional keyed on `Source`, emitting each member the source persists as a real JSON Schema member with its type and a description derived from the source's editor view. As with the `Queries` step, the source members are flattened onto the rule object itself rather than nested under a child object, which matches how Orchard Core serializes rewrite rules.

Display text is not emitted into the JSON Schema. It is carried on the `RewriteRuleSourceDescriptor` returned by `IRewriteRuleSchemaService`, so other features can build their own listings.

### The rule stays open

Every rule allows additional properties, and the `Rules` array item accepts unknown `Source` values, so a rule exported from a tenant with extra modules enabled still validates. Unknown or custom rewrite rule sources are accepted as long as they supply a `Source`, which is what makes the schema extensible.

## Describe a custom rewrite rule source

Derive from `RewriteRuleSourceSchemaDefinitionBase`, name the source through `Name`, and return the members the source accepts beyond the shared ones:

```csharp
using CrestApps.OrchardCore.Recipes.Core.Schemas.UrlRewriting;
using Json.Schema;

namespace MyModule;

public sealed class GeoRedirectRuleSourceSchema : RewriteRuleSourceSchemaDefinitionBase
{
    public override string Name => "GeoRedirect";

    protected override string DisplayText => "Geo Redirect";

    protected override string Description => "Redirects a request based on the caller's country.";

    protected override IEnumerable<string> RequiredProperties => ["Country"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(RewriteRuleSourceSchemaContext context)
    {
        yield return ("Country", new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description("The ISO country code the rule matches."));
    }
}
```

Register the definition through the Recipes module's URL rewriting feature:

```csharp
services.AddRewriteRuleSourceSchema<GeoRedirectRuleSourceSchema>();
```

The `Name` must match the rewrite rule source name Orchard Core serializes into the `Source` member.
