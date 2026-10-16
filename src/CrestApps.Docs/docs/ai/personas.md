---
sidebar_label: AI Personas
sidebar_position: 12
title: AI Personas
description: Give AI profiles, chat interactions and workflow tasks a reusable character with its own instructions, name, avatar and voice.
---

# AI Personas

An **AI persona** is a reusable character: who the assistant is, how it talks, the name it uses, the avatar it
shows and the voice it speaks with. You create personas under **Artificial Intelligence → Personas**, then choose
one on an AI profile, a profile template, a chat interaction or the **AI completion with configuration** workflow
task.

A persona answers *who*. The profile's system instructions and prompt templates still answer *what*: the task, the
rules and the tools. Keeping the two apart lets one persona serve many profiles, and lets a profile change
character without touching its rules.

Personas have their own catalog. They are not AI templates: a template is a blueprint that is copied when it is
used, while a persona stays **linked**, so an edit applies to everything that uses it from the next message.

The framework part is documented in [CrestApps.Core AI Personas](https://core.crestapps.com/docs/core/ai-personas).

## Enabling personas

Enable the **AI Personas** feature (`CrestApps.OrchardCore.AI.Personas`). It adds:

- **Artificial Intelligence → Personas**, guarded by the **Manage AI personas** permission;
- the **AI persona** picker on AI profiles, Profile templates, chat interactions and the workflow task;
- the persona name and avatar in AI Chat sessions, the chat widgets and Chat Interactions;
- the `AIPersonas` recipe step and the **AI Personas** deployment step.

Without the feature, persona links that are already stored are kept but ignored: chats run with the profile's own
instructions, label and voice.

## Creating a persona

1. Go to **Artificial Intelligence → Personas** and choose **Add Persona**.
2. Fill in:
   - **Title** and **Technical name**. Profiles link to the technical name, so it cannot change once saved.
   - **Description** (optional): helps editors choose the right persona.
   - **Persona instructions**: the personality, tone and manners. Liquid is supported, and the variable
     `assistant_name` holds the assistant name. Leave the task and the rules to the profile.
   - **Assistant name** (optional): the name the assistant uses for itself. The model is told its name, and chat
     screens show it on the assistant's messages.
   - **Language** (optional): such as `en-US`. Spoken conversations listen in this language when nothing else sets
     one.
   - **Voice** (optional): choose a deployment, then a voice from that deployment's list.
   - **Avatar URL** (optional): an image shown next to the assistant's messages.
3. Save. The persona can now be chosen anywhere a persona picker appears.

Six ready-made personas ship with the AI module: *Friendly assistant*, *Professional concierge*, *Technical expert*,
*Patient tutor*, *Empathetic support agent* and *Upbeat sales guide*. They are marked **From file** in the list and
have no voice, because voices belong to a provider. Editing one and saving it creates a saved persona with the same
technical name, which replaces the file for everything that links to it. A persona file cannot be deleted.

## Using a persona

- **AI profile**: the **AI persona** field on the **Instructions** tab, above the system instructions.
- **Profile template**: the same field on a Profile template's **Instructions** tab. Profiles created from the
  template start linked to that persona.
- **Chat interaction**: the **AI persona** field in the **Settings** panel. It saves like the other interaction
  settings and applies from the next message.
- **Workflows**: the **AI completion with configuration** task has the same field.

You can choose one persona, and it is optional.

## What the assistant receives

On every request, the persona block is placed first in the system message:

1. "Your name is …" when the persona has an assistant name
2. the persona's instructions
3. the selected prompt templates
4. the profile's or interaction's own system instructions

Business rules come last, so they win any conflict with the persona's personality. A deleted or unknown persona is
logged and the request carries on without one.

## In the chat

AI Chat sessions, the admin and front-end chat widgets and Chat Interactions show the persona's assistant name and
avatar on assistant messages and on the typing indicator. Without a persona, or when a value is empty, the chat
keeps its usual **Assistant** label and robot icon. How a chat opens stays with the profile's welcome message.

New chat sessions record the persona their profile linked to, for reporting.

## Voice

A spoken conversation chooses its voice in this order:

1. a voice chosen for the call or session (for example, the voice of an inventory load in the Contact Center)
2. the voice chosen on the profile or interaction
3. the persona's voice
4. the deployment's default voice

On the profile editor, leaving **Voice** empty shows **Use persona voice (…)** when the selected persona has one.
Pick a voice there only to override the persona.

Voice names belong to one provider. An OpenAI realtime voice does not exist in Azure Speech, for example. The
persona remembers the deployment its voice was chosen from, and the voice is used only on deployments of the same
provider. Elsewhere the deployment's default voice is used, and the log says why.

Contact Center AI calls that run as a realtime (speech-to-speech) session use the same order. Turn-based automated
calls use the voice of their activity, subject flow or site settings.

## Deleting a persona

Profiles that use a deleted persona keep working without one. When you delete a persona that AI profiles still
use, a warning says how many were affected. When a persona file has the same technical name, the links fall back to
the file.

## Recipes

The `AIPersonas` step creates or updates personas. A persona is matched by `ItemId`, then by `Name`, so links keep
working across tenants. A profile links to a persona with a top-level `PersonaId` in the `AIProfile` step: an empty
value unlinks it, and leaving the key out keeps the current link.

```json
{
  "steps": [
    {
      "name": "AIPersonas",
      "personas": [
        {
          "Name": "front-desk",
          "DisplayText": "Front desk",
          "Instructions": "You are a gracious, efficient front-desk assistant.",
          "AssistantName": "Ava",
          "Language": "en-US"
        }
      ]
    },
    {
      "name": "AIProfile",
      "Profiles": [
        {
          "Name": "support",
          "DisplayText": "Support",
          "PersonaId": "front-desk"
        }
      ]
    }
  ]
}
```

A Profile template file can name the persona its profiles start with by adding `PersonaId: front-desk` to its front
matter.

## Persona files

Modules ship personas as Markdown files in their `Templates/Personas` folder, and the site can add its own in the
content root's `Templates/Personas` folder. The file name, without its extension, is the technical name. The front
matter holds the persona's fields and the body holds its instructions:

```markdown
---
Title: Front desk
Description: Gracious and efficient.
AssistantName: Ava
Language: en-US
---

You are a gracious, efficient front-desk assistant.
```

## Related docs

- [AI Profile Templates](profile-templates)
- [AI Prompt Templates](prompt-templates)
- [Realtime Voice](realtime-voice)
- [AI Chat Interactions](chat-interactions)
