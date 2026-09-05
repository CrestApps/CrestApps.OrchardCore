---
sidebar_label: Model Capabilities
sidebar_position: 18
title: AI Model Capabilities and Parameters
description: Declare what each AI deployment's model supports and expose configurable model parameters to AI profiles, profile templates, and chat interactions.
---

# AI Model Capabilities and Parameters

Instead of hardcoding provider- or model-specific options, the AI suite drives editors, validation, and the outgoing request from an extensible registry of **model features** and **model parameters**. Each **AI deployment** declares which registered definitions its underlying model exposes; AI profiles, profile templates, and chat interactions then only render and send the parameters that deployment actually supports.

- A **feature** is a binary capability the model either has or does not have — for example tool calling, structured outputs, reasoning, audio input, or realtime (speech-to-speech).
- A **parameter** is a configurable option carrying a kind (choice, number, integer, boolean, text), allowed values, an optional numeric range, and a default — for example reasoning effort.

Anything a deployment does not declare is never rendered in the consuming editors and is never sent to the provider.

## Declaring capabilities on a deployment

Open **AI → Deployments**, create or edit a deployment, and use the **Model capabilities** card:

- **Trained features** — tick the capabilities the underlying model was trained with. New deployments start from the features each provider registers as enabled by default, so existing chat deployments keep working without changes.
- **Model parameters** — enable each parameter the model exposes. For a *choice* parameter you can narrow the supported values and pick a default; for a *number*/*integer* parameter you can set the minimum, maximum, and step. A parameter that depends on a feature (its **required feature**) is only shown while that feature is enabled.

The declared metadata is stored on the deployment. It is the single source of truth for the editors and for request generation.

## Consuming parameters on profiles, templates, and interactions

The **AI profile**, **profile template**, and **chat interaction** editors each render a metadata-driven **Model parameters** section (on the *Parameters* tab). As the selected chat deployment changes, the section updates automatically:

- Only the parameters the selected deployment exposes are shown; the rest are hidden.
- Choice parameters are limited to the deployment's supported values, and the placeholder reflects the deployment default.
- Numeric parameters pick up the deployment's minimum, maximum, and step.
- A read-only badge row shows the trained features the selected deployment declares.

Leaving a parameter on **Use deployment default** sends nothing for it — the deployment's declared default applies. The selected values are stored per entity and bound into the outgoing request at runtime; a value for a parameter the deployment does not expose is never sent.

## Registering features and parameters (for module authors)

Modules contribute new features and parameters through the capability options, so providers can add capabilities without changing the core framework:

```csharp
services.AddAIDeploymentFeature(
    "myFeature",
    new LocalizedString("myFeature", "My feature"),
    feature =>
    {
        feature.Category = "Trained Features";
        feature.EnabledByDefault = false;
    });

services.AddAIDeploymentParameter(
    "myParameter",
    new LocalizedString("myParameter", "My parameter"),
    parameter =>
    {
        parameter.Kind = AIDeploymentParameterKind.Choice;
        parameter.AllowedValues =
        [
            new AIDeploymentParameterOption { Value = "low", DisplayName = new LocalizedString("low", "Low") },
            new AIDeploymentParameterOption { Value = "high", DisplayName = new LocalizedString("high", "High") },
        ];
        parameter.RequiredFeature = "myFeature"; // Optional: only meaningful when the feature is enabled.
    });
```

Resolve the effective capabilities of a deployment through `IAIDeploymentCapabilityService`, which merges the registered definitions with the metadata declared on the deployment.

## Related

- [Realtime Voice](realtime-voice.md) — the `realtime` feature and the speech-to-speech chat experience.
