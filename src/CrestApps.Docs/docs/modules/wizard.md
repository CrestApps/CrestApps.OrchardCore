---
sidebar_label: Wizard
sidebar_position: 2
title: Wizard (Stepper) Feature
description: Build reusable multi-step wizard experiences in Orchard Core via content items or code.
---

| | |
| --- | --- |
| **Feature Name** | Wizard |
| **Feature ID** | `CrestApps.OrchardCore.Wizard` |
| **Contents feature ID** | `CrestApps.OrchardCore.Wizard.Contents` |
| **Workflows feature ID** | `CrestApps.OrchardCore.Wizard.Workflows` |

The Wizard module provides a reusable, multi-step **wizard** (also called a *stepper*) framework. A wizard
guides a user through an ordered sequence of steps, collects data on each step, and finalizes the flow once
every step is fulfilled. The engine is completely decoupled from any one scenario, so the same infrastructure
powers a sign-up flow, an onboarding flow, a survey, a checkout, or any other guided experience.

You can build a wizard two ways:

- **Via content items** — enable **Wizard Contents** and attach the `WizardPart` to a content type. Editors
  compose the steps from contained content items, just like the `BagPart` from `OrchardCore.Flows`. No code
  required.
- **Via code** — register an `IWizardDefinition` and an `IWizardHandler` to contribute and validate steps
  programmatically, and an `IDisplayDriver<WizardFlow>` to render them.

Both models share the same engine, session store, distributed-lock completion, and workflow events.

## Features

| Feature | Description |
| --- | --- |
| **Wizard** | The core framework: the flow engine, session store, public host controller, and code extension points. |
| **Wizard Contents** | Adds the `WizardPart` so editors can build wizards from content items. Depends on `OrchardCore.Contents` and `OrchardCore.Flows`. |
| **Wizard Workflows** | Raises workflow events for the wizard lifecycle. Depends on `OrchardCore.Workflows`. |

## Concepts

| Type | Role |
| --- | --- |
| `IWizardDefinition` | Declares a wizard type, its display name, and whether it requires an authenticated user. |
| `IWizardHandler` | Contributes steps and reacts to lifecycle events (activating, loading, completing, completed, failed). |
| `WizardFlow` | Navigates the ordered steps of a session (current, first, last, next, previous). |
| `WizardSession` | The persisted state: the wizard type, definition, current step, saved step data, and owner. |
| `WizardStep` | One step: its key, title, order, and whether it must collect data. |
| `IWizardEngine` | Finalizes a wizard under a distributed lock, idempotently, after validating every data-collecting step. |
| `IWizardSessionStore` | Creates, loads, and persists wizard sessions. |

The public host exposes three routes:

| Route name | URL | Purpose |
| --- | --- | --- |
| `WizardStart` | `GET/POST Wizard/Start/{wizardType}` | Start or resume a wizard of a given type. |
| `WizardStep` | `GET Wizard/Step/{sessionId}/{step?}` | Display a specific step of a session. |
| `WizardConfirmation` | `GET Wizard/Confirmation/{sessionId}` | Show the confirmation of a completed session. |

## Using the wizard via content items

Enable the **Wizard Contents** feature. It also enables **Wizard** and `OrchardCore.Flows`.

### 1. Attach the wizard part

1. Go to **Content Definition → Content Types** and edit (or create) the content type that will host the
   wizard, for example `Registration`.
2. Add the **WizardPart** to the type.
3. Open the part settings on the type and configure the wizard:
   - **Content types** — select one or more specific step content types.
   - **Stereotype** — allow any content type that has the configured stereotype (for example, `WizardStep`).
   - **Completion policy** — what to do with the content items the visitor fills in on each step when the
     wizard completes: **None** (do not persist them), **Draft** (save them as drafts), or **Publish**
     (save and publish them). The default is **Publish**.
   - **Requires authenticated user** — when enabled, the public start route challenges anonymous visitors
     before the wizard begins. This is enforced per host content item through an `IWizardAccessPolicy`, so
     each wizard instance can require sign-in independently of the code-level definition.

Each allowed step type is an ordinary content type, so you build the step's fields with the usual content
fields and parts.

:::note
The step editor is chosen per allowed step content type through the normal content type/part settings, just
like any other content part. Content field drivers render in the default editor group, so a step always uses
the content type's standard editor. Only content types that are still in the configured allowlist (content
types or stereotype) are rendered at runtime, and — as with the Flows bag part — the site administrator is
responsible for choosing step content types that are safe to expose to the wizard's audience.
:::

### 2. Author the steps

Create a content item of the host type. The wizard part editor works exactly like the flow `BagPart`: use
**Add step** to append a contained content item for each step, fill in its fields, and drag the cards to
reorder them. The order of the cards is the order of the steps.

### 3. Run the wizard

Start the wizard by pointing the user at the start route with the `Content` wizard type and the host content
item id as the `definitionId`:

```text
/Wizard/Start/Content?definitionId={contentItemId}
```

The host renders one step at a time. On each step the user fills in a fresh content item that is seeded from
the authored step's default values; the response is saved into the session. When every data-collecting step
has data, the wizard finalizes and redirects to the confirmation route. The response content items are
created (and, per the completion policy, published) inside the completion lock, so a failure to persist marks
the wizard as failed instead of silently losing the collected data.

:::note
Content-driven wizards all share the wizard type `Content`. Individual wizards are distinguished by the
`definitionId` (the host content item id).
:::

## Using the wizard via code

Enable the **Wizard** feature. A code wizard needs three parts: a **definition**, a **handler** that
contributes and validates the steps, and a **display driver** that renders them.

### 1. Declare the wizard definition

```csharp
public sealed class OnboardingWizardDefinition : IWizardDefinition
{
    public string WizardType => "Onboarding";

    public string DisplayName => "Onboarding";

    public bool RequiresAuthenticatedUser => true;
}
```

### 2. Contribute steps with a handler

Derive from `WizardHandlerBase` and override only the lifecycle events you need. Add the steps in
`ActivatingAsync`, which runs once when a new session is created (so resuming a session does not add steps
again). Always early-return for wizard types you do not own.

```csharp
public sealed class OnboardingWizardHandler : WizardHandlerBase
{
    public override Task ActivatingAsync(WizardFlowActivatingContext context)
    {
        if (context.Flow.Session.WizardType != "Onboarding")
        {
            return Task.CompletedTask;
        }

        context.Flow.Session.Steps.Add(new WizardStep
        {
            Key = "profile",
            Title = "Your profile",
            Order = 1,
            CollectData = true,
        });

        context.Flow.Session.Steps.Add(new WizardStep
        {
            Key = "preferences",
            Title = "Preferences",
            Order = 2,
            CollectData = true,
        });

        return Task.CompletedTask;
    }

    public override Task CompletedAsync(WizardFlowCompletedContext context)
    {
        if (context.Flow.Session.WizardType != "Onboarding")
        {
            return Task.CompletedTask;
        }

        // Finalize: provision the account, send a welcome email, etc.
        return Task.CompletedTask;
    }
}
```

A step's collected data is stored in `WizardSession.SavedSteps`, keyed by the step key. The engine treats a
`CollectData` step as complete only once its key has an entry there.

### 3. Render the steps with a display driver

Implement `IDisplayDriver<WizardFlow>` to build the editor for the current step and to save the submitted
values into `Session.SavedSteps`. Inspect `flow.GetCurrentStep()` and early-return for steps you do not own.

### 4. Register everything

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IWizardDefinition, OnboardingWizardDefinition>();
        services.AddScoped<IWizardHandler, OnboardingWizardHandler>();
        services.AddScoped<IDisplayDriver<WizardFlow>, OnboardingWizardStepDisplayDriver>();
    }
}
```

Start the wizard by directing the user to:

```text
/Wizard/Start/Onboarding
```

### Finalizing under a lock

Completion runs through `IWizardEngine.CompleteAsync`. The engine acquires a distributed lock for the
session, reloads the authoritative session, verifies every data-collecting step has data, and runs the
completing and completed handlers **exactly once**, even under concurrent or repeated attempts. Use the
optional `prepareUnderLock` callback to validate and record external evidence (for example a payment
provider's return) inside the same lock before the wizard is finalized:

```csharp
var result = await _wizardEngine.CompleteAsync(
    flow,
    prepareUnderLock: async authoritativeFlow =>
    {
        // Validate an external receipt and record it on the session before finalizing.
        return await ValidateAndRecordAsync(authoritativeFlow);
    });
```

## Workflow events

Enable the **Wizard Workflows** feature to react to wizard activity with workflows. It contributes four
events under the **Wizard** category:

| Event | Raised when |
| --- | --- |
| **Wizard Started** | A new wizard session is activated. |
| **Wizard Step Displayed** | A step is displayed to the user. |
| **Wizard Completed** | A wizard session completes successfully. |
| **Wizard Failed** | A wizard session fails. |

Each event has an optional **Wizard type** filter. Leave it empty to match any wizard, or set it to a wizard
type (for example `Onboarding` or `Content`) to scope the workflow. Every event supplies the following
workflow input: `SessionId`, `WizardType`, `DefinitionId`, `CurrentStep`, `Status`, and the `Session`
object. The event is correlated by the session id, so you can resume a workflow instance for the same
session across steps.

## Controlling access

A code-level `IWizardDefinition` exposes `RequiresAuthenticatedUser`, which the public host enforces for the
whole wizard type. When a wizard type can host many independent instances (as content-driven wizards do),
register an `IWizardAccessPolicy` to decide per instance whether sign-in is required:

```csharp
public sealed class MyWizardAccessPolicy : IWizardAccessPolicy
{
    public Task<bool> RequiresAuthenticatedUserAsync(string wizardType, string definitionId)
    {
        // Return true to force the anonymous visitor to sign in before this wizard starts.
        return Task.FromResult(false);
    }
}
```

Register it with `services.AddScoped<IWizardAccessPolicy, MyWizardAccessPolicy>();`. The host challenges the
visitor when either the definition's `RequiresAuthenticatedUser` is `true` or any policy returns `true`. The
**Wizard Contents** feature ships such a policy so the per-instance **Requires authenticated user** part
setting is honored.

## Reusing the wizard from other modules

Because the engine is decoupled from any scenario, other modules can build their own guided experiences on
top of it by registering an `IWizardDefinition`, an `IWizardHandler`, and an `IDisplayDriver<WizardFlow>`,
then reusing the shared host, session store, completion locking, and workflow events. This is the same
foundation intended for flows such as subscription sign-up.
