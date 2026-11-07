# CrestApps Recipes

Enhances Orchard Core recipe functionality by enabling detailed descriptions for recipes.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| CrestApps Recipes | `CrestApps.OrchardCore.Recipes` | Enhances Orchard Core recipe functionality by enabling detailed descriptions for recipes. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- `OrchardCore.Recipes.Core`

## Extension points

- `IAdminMenuSchemaService` (CrestApps.OrchardCore.Recipes.Core): Builds JSON schemas describing the admin menu nodes available on the current tenant, used by the `AdminMenu` recipe step to describe each menu and its recursive `MenuItems` array.
- `IAdminNodeSchemaDefinition` (CrestApps.OrchardCore.Recipes.Core): Produces the JSON schema and metadata that describe a single admin menu node inside the `MenuItems` array of the `AdminMenu` recipe step.
- `IContainedContentPartSchemaDefinition` (CrestApps.OrchardCore.Recipes.Core): Provides nested content-type constraints for parts that contain child content items.
- `IContentItemSchemaService` (CrestApps.OrchardCore.Recipes.Core): Builds JSON schemas that describe Orchard Core content item payloads.
- `IContentPartSchemaDefinition` (CrestApps.OrchardCore.Recipes.Core): Produces a JSON schema fragment describing the content item payload for a specific content part.
- and 21 more contracts in the same projects.

## Documentation

See [CrestApps Recipes](../../CrestApps.Docs/docs/modules/recipes.md) in the technical manual.
