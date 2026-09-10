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

Capabilities are also what a deployment is *selected* by. The older **deployment purpose** is gone: a deployment
says what its model can do, and where it gets used is decided by a **slot**. See
[Capabilities and slots](#capabilities-and-slots) below.

## Declaring capabilities on a deployment

Open **AI → Deployments**, create or edit a deployment, and use the **Model capabilities** card:

- **Trained features** — tick the capabilities the underlying model was trained with. New deployments start from the features each provider registers as enabled by default, so existing chat deployments keep working without changes.
- **Model parameters** — enable each parameter the model exposes. For a *choice* parameter you can narrow the supported values and pick a default; for a *number*/*integer* parameter you can set the minimum, maximum, and step. A parameter that depends on a feature (its **required feature**) is only shown while that feature is enabled.

The declared metadata is stored on the deployment. It is the single source of truth for the editors, for
deployment selection, and for request generation. **At least one capability is required.**

## Capabilities and slots

A **capability** says what the model can do. A **slot** says what this installation uses a deployment *for*.
The framework registers these slots, and each picker lists exactly the deployments that can fill the slot
behind it:

| Slot | Required capability | Notes |
|------|--------------------|-------|
| `chat` | `textGeneration` | Excludes realtime deployments — they cannot serve a text completion |
| `utility` | `textGeneration` | Falls back to the `chat` slot; also excludes realtime |
| `embedding` | `textEmbedding` | |
| `image` | `imageOutput` | |
| `vision` | `imageInput` | |
| `speechToText` | `speechToText` | |
| `textToSpeech` | `textToSpeech` | |
| `realtime` | `realtime` | |

`textGeneration` is **opt-out**: a deployment that declares no capability metadata at all is assumed to be
text capable, so an installation that predates this card keeps working. Every other capability is
**opt-in** and has to be declared.

The chat deployment picker on an AI profile, profile template, or chat interaction is the one exception to
the one-slot rule: it lists the text-capable *and* the realtime deployments together, because it answers
"what can this converse with" rather than "what can serve a text completion". Which of the two a selection
turns out to be is then read back from the deployment's own capabilities.

### Upgrading from deployment purposes

**No action is required.** A deployment record that still carries a `Purpose`, `Capability`, or `Type`
field — in the store, in a recipe, or in `appsettings.json` — is projected onto capabilities every time it
is read:

| Legacy purpose | Capabilities |
|----------------|-----------|
| `Chat`, `Utility` | `textGeneration`, `toolCalling`, `streaming` — nothing, if the deployment declares `realtime` |
| `Embedding` | `textEmbedding` |
| `Image` | `imageOutput` |
| `Vision` | `imageInput` |
| `SpeechToText` | `speechToText` |
| `TextToSpeech` | `textToSpeech` |

The chat and utility purposes are the only two that never named a single capability, and they are credited
with three. A deployment on the legacy purpose was driven through a chat client that called tools and
streamed its response, with no switch to turn either off, so text generation on its own would understate
what the site already had. Anything a particular model cannot really do is one checkbox away in the
deployment editor.

A deployment that declares `realtime` is credited with none of the three. It is a speech-to-speech model
stored under the chat purpose: it answers a text completion with an HTTP 400, and the purpose says nothing
about it beyond that it is conversational.

The projection is additive, and it is permanent rather than a one-time migration, so a site that never
rewrites its stored JSON stays correct. Declaring capabilities directly is optional cleanup.

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
