# Wizard

Provides a reusable, multi-step wizard (stepper) services.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Wizard | `CrestApps.OrchardCore.Wizard` | Provides a reusable, multi-step wizard (stepper) services. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Extension points

- `IWizardAccessPolicy` (CrestApps.OrchardCore.Wizard.Abstractions): Contributes per-instance access rules for a wizard so a feature can, for example, require an authenticated visitor for a specific wizard definition that the type-level `IWizardDefinition` cannot express on its own.
- `IWizardDefinition` (CrestApps.OrchardCore.Wizard.Abstractions): Declares a wizard that can be started and navigated through the generic wizard host.
- `IWizardDefinitionProvider` (CrestApps.OrchardCore.Wizard.Abstractions): Resolves the `IWizardDefinition` instances registered by features.
- `IWizardEngine` (CrestApps.OrchardCore.Wizard.Abstractions): Orchestrates wizard completion.
- `IWizardFlowSession` (CrestApps.OrchardCore.Wizard.Abstractions): The read/write surface of a wizard session that the flow navigation and step drivers operate on.
- and 2 more contracts in the same projects.

## Sessions and limits

- Opening a wizard stores nothing: the session is created on the first valid submission of its first step.
- A wizard started with a `definitionId` that no `IWizardAccessPolicy.IsKnownDefinitionAsync` recognizes answers 404. A policy that owns instances of a wizard type (the content wizard does) must implement it.
- Pending sessions untouched for 30 days are removed by the platform retention task (`WizardSessionRetentionPolicy`).
- With Orchard Core's Rate Limits feature on, the wizard routes carry the `wizard` rate-limit group (60 requests a minute per visitor by default).

## Documentation

See [Wizard (Stepper) Feature](../../CrestApps.Docs/docs/modules/wizard.md) in the technical manual.
