---
sidebar_label: AI Personas
sidebar_position: 12
title: AI Personas
description: Give AI profiles, chat interactions and workflow tasks a reusable character with its own instructions, name, avatar and voice.
user_manual:
  - user-manual/ai/profiles
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

<YouTube video="ai-profiles-personas-templates" title="Video overview of AI profiles, personas and templates" />

The video shows how an administrator creates a persona in the admin and chooses it in a profile.

## Enabling personas

Enable the **AI Personas** feature (`CrestApps.OrchardCore.AI.Personas`). It adds:

- **Artificial Intelligence → Personas**, guarded by the **Manage AI personas** permission;
- the **AI persona** picker on AI profiles, Profile templates, chat interactions and the workflow task;
- the persona name and avatar in AI Chat sessions, the chat widgets and Chat Interactions;
- the `AIPersonas` recipe step and the **AI Personas** deployment step (see [Importing and exporting](#importing-and-exporting)).

A tenant starts with no personas. Create them in the admin, import them with a recipe, or register them in code (see
[Registering personas in code](#registering-personas-in-code)).

Without the feature, persona links that are already stored are kept but ignored: chats run with the profile's own
instructions, label and voice.

## Creating a persona

<YouTube video="ai-profiles-personas-templates" title="Video of creating a persona and choosing it in a profile" start={372} end={440} />

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

Personas that code provides (see [Registering personas in code](#registering-personas-in-code)) are marked **From
file** in the list. They cannot be deleted. Editing one and saving it creates a saved persona with the same technical
name, which replaces the provided one for everything that links to it.

## Using a persona

- **AI profile**: the **AI persona** field in the **Instructions** card, above the system instructions.
- **Profile template**: the same field in a profile template's **Instructions** card. Profiles created from the
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

Contact Center AI calls that run as a realtime (speech-to-speech) session use the same order.

Turn-based automated calls, where the telephony provider speaks each reply, choose their voice in this order:

1. the voice of the activity (for example, the voice of an inventory load)
2. the voice of the subject flow
3. the persona's voice, when it was chosen on a deployment of the same provider as the call's text-to-speech
   deployment (the activity's, then the subject flow's, then the site default)
4. the telephony provider's default voice

The voice goes to the telephony provider's speak command, so it must be a voice that provider accepts. When the
persona has a **Language**, these calls speak in it and listen for it (`es-MX` listens for `es`); otherwise they speak
`en-US`. If the provider refuses to speak a line with the persona's voice or language, the line is spoken again as if
there were no persona, and the log says why.

A live automated call, held as a speech-to-speech session, listens for the persona's **Language** too: the caller is
transcribed in its language part (`el-GR` listens for `el`), and the model is told to keep to that language. Without a
**Language** the model hears and answers in whatever language the caller speaks.

## Deleting a persona

Profiles that use a deleted persona keep working without one. When you delete a persona that AI profiles still
use, a warning says how many were affected. When a persona file has the same technical name, the links fall back to
the file.

## Importing and exporting

Saved personas move between tenants with the standard Orchard Core deployment and recipe pipeline. Profiles link to a
persona by its technical name, which survives the move, so a profile and its persona can be exported and imported
together in either order.

### Deployment plan

Enable **OrchardCore.Deployment** together with **AI Personas**. The **AI Personas** deployment step then becomes
available when you build a deployment plan under **Tools > Deployments > Plans**. It exports either every saved
persona (**Include all saved personas**) or only the ones you select. Each exported persona keeps its technical name,
title, description, instructions, assistant name, language, voice, avatar, owner and properties.

Only saved personas are exported. Personas that code provides ship with that code, so the target tenant gets them by
enabling the same module. Export AI profiles with the **AI Profiles** step; a profile's link travels in its
`Properties.AIPersonaMetadata`.

### Recipe step

The deployment step emits an `AIPersonas` recipe step, which is also the step you write by hand to create personas
when a tenant is set up. Each entry is matched by `ItemId` first and then by `Name`. A match is updated in place,
keeping its technical name; otherwise a new persona is created. `Name` and `Instructions` are required for a new
persona. A profile links to one with a top-level `PersonaId` in the `AIProfile` step: an empty value unlinks it, and
leaving the key out keeps the current link.

```json
{
  "steps": [
    {
      "name": "AIPersonas",
      "personas": [
        {
          "Name": "front-desk",
          "DisplayText": "Front desk",
          "Description": "Gracious and efficient.",
          "Instructions": "You are {{ assistant_name }}, a gracious, efficient front-desk assistant.",
          "AssistantName": "Ava",
          "Language": "en-US",
          "VoiceName": "alloy",
          "VoiceDeploymentName": "gpt-realtime",
          "AvatarUrl": "/media/avatars/ava.png"
        }
      ]
    },
    {
      "name": "AIProfile",
      "Profiles": [
        {
          "Name": "support",
          "DisplayText": "Support",
          "Type": "Chat",
          "PersonaId": "front-desk"
        }
      ]
    }
  ]
}
```

| Property | Required | Description |
| --- | --- | --- |
| `ItemId` | No | Matches an existing persona to update. |
| `Name` | Yes | The technical name profiles link to. It cannot change once created. |
| `DisplayText` | No | The title shown in editors. Defaults to `Name`. |
| `Description` | No | Helps editors choose the right persona. |
| `Instructions` | For a new persona | The Liquid persona prompt, placed first in the system message. |
| `AssistantName` | No | The name the assistant uses for itself. |
| `Language` | No | A BCP-47 tag such as `en-US`. |
| `VoiceName` | No | The provider-specific voice. |
| `VoiceDeploymentName` | No | The deployment the voice was chosen from. |
| `AvatarUrl` | No | An image shown next to assistant messages. |
| `CreatedUtc`, `OwnerId`, `Author` | No | Kept from an export. |
| `Properties` | No | Extra data stored with the persona. |

### Recipe schema

With the **CrestApps Recipes** feature (`CrestApps.OrchardCore.Recipes`) enabled, the tenant exposes JSON schemas for
the `AIPersonas` step and for the **AI Personas** deployment step (`AIPersonaDeploymentStep`, with `IncludeAll` and
`PersonaIds`), and the `AIProfile` step schema describes `PersonaId` and `AIPersonaMetadata`. Recipe tooling and AI
assistants use them to validate and suggest persona recipes. See [Recipes](../modules/recipes).

## Registering personas in code

There are two kinds of persona a module or a site can register.

### Saved personas: seed them once

A saved persona is an ordinary catalog record: editors can change or delete it, and it is exported with the others.
To give a tenant personas when it is set up, ship them in a recipe with the `AIPersonas` step, or create them with
`IAIPersonaManager`, for example in a data migration:

```csharp
public sealed class PersonaMigrations : DataMigration
{
    private readonly IAIPersonaManager _personaManager;

    public PersonaMigrations(IAIPersonaManager personaManager)
    {
        _personaManager = personaManager;
    }

    public async Task<int> CreateAsync()
    {
        var persona = await _personaManager.NewAsync("front-desk", new JsonObject
        {
            ["DisplayText"] = "Front desk",
            ["Instructions"] = "You are {{ assistant_name }}, a gracious, efficient front-desk assistant.",
            ["AssistantName"] = "Ava",
        });

        var result = await _personaManager.ValidateAsync(persona);

        if (result.Succeeded)
        {
            await _personaManager.CreateAsync(persona);
        }

        return 1;
    }
}
```

Register the migration in a startup that requires the feature, so it only runs where personas are enabled:

```csharp
[RequireFeatures("CrestApps.OrchardCore.AI.Personas")]
public sealed class PersonasStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<PersonaMigrations>();
    }
}
```

### Provided personas: read from files or code

A provided persona is read from code every time it is needed. Every tenant that enables the module gets it, editors
cannot delete it, and it is not exported. Saving one in the editor creates a saved persona with the same technical
name, which replaces it.

To ship persona files with a module, put Markdown files in the module's `Templates/Personas` folder. Orchard Core
embeds module files, so register the assembly from a startup that requires the feature:

```csharp
[RequireFeatures("CrestApps.OrchardCore.AI.Personas")]
public sealed class PersonasStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAIPersonasFromAssembly(typeof(PersonasStartup).Assembly);
    }
}
```

The file name, without its extension, is the technical name. The front matter holds the persona's fields and the
body holds its instructions:

```markdown
---
Title: Front desk
Description: Gracious and efficient.
AssistantName: Ava
Language: en-US
---

You are {{ assistant_name }}, a gracious, efficient front-desk assistant.
```

A site can also drop such files into the `Templates/Personas` folder of its content root, the web project's folder.
For any other source, implement `IAIPersonaProvider` and register it with
`services.AddSingleton<IAIPersonaProvider, MyPersonaProvider>()`.

## Related docs

- [AI Profile Templates](profile-templates)
- [AI Prompt Templates](prompt-templates)
- [Realtime Voice](realtime-voice)
- [AI Chat Interactions](chat-interactions)
