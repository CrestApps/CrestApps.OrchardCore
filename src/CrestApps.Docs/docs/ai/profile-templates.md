---
sidebar_label: Profile Templates
sidebar_position: 11
title: AI Profile Templates
description: Orchard Core guidance for reusable AI profile templates and their relationship to the shared CrestApps.Core profile model.
---

# AI Profile Templates

AI profile templates are the Orchard Core-friendly way to stamp out repeatable AI profile configurations inside the admin UI and recipes.

## Orchard-specific scope

Use profile templates when you want to:

- create reusable Orchard-managed AI profile defaults
- standardize provider, deployment, prompt-template, and capability choices
- generate multiple tenant profiles from a curated template definition

## Where to manage them

After enabling the base AI module, Orchard adds:

- **Artificial Intelligence -> Templates**

This screen is backed by the Orchard profile-template manager and related display drivers contributed by enabled AI features.

The screencast below adds a Profile template and captures reusable defaults — profile type, the `gpt-4.1-mini` deployment, and a welcome message — that new profiles can be stamped from.

<video controls preload="metadata" width="100%" aria-label="Screen cast of creating an AI profile template with reusable defaults">
  <source src="/img/docs/ai-profile-templates.mp4" type="video/mp4" />
</video>

## Creating a profile from a starting point

Profile templates are how new profiles begin. Instead of opening a single page with every setting, **Add Profile** asks what you want to build, asks only what that starting point needs, and creates the profile for you. Everything else stays optional and lives in the usual profile editor.

### Step 1: pick a starting point

**Artificial Intelligence -> Profiles -> Add Profile** opens the **New AI Profile** picker over the profiles list.

![The New AI Profile picker, with a filter box and categories on the left and a card for each starting point](/img/docs/ai-profile-new-picker.png)

- **Blank profile** is always the first card. It opens the full profile editor with nothing filled in, exactly as **Add Profile** did before. The editor's **Apply Templates** card still works as it always has.
- The featured scenarios come next, then every other starting point, sorted by category and title.
- Each card shows the title, the description, the kind of profile it creates (**Chat**, **Utility**, **Agent**) and a **Start** button.
- Type in **Filter** to narrow the cards by title, description, category or profile type, or pick a category on the left to see only that category. **All** shows everything again.
- A starting point that needs a feature that is not enabled is still listed, greyed out, with the name of the feature to enable and a disabled **Start** button.

On a narrow screen the categories sit in one row above the cards and scroll sideways. When a site has no profiles yet, the empty profiles list offers a **Choose a starting point** button that opens the same picker.

### Step 2: set it up

**Start** opens a short setup step for that starting point.

![The setup step, showing the chosen starting point above the Title, Technical name and Chat deployment fields](/img/docs/ai-profile-new-setup.png)

- The top of the page repeats what the starting point does, so you can confirm the choice.
- **Title** is prefilled with the starting point's name. **Technical name** follows the title as you type, until you type a name of your own. It must be unique and cannot be changed later.
- **Chat deployment** is optional. **Default** uses the site's default chat deployment; if the site has none, a warning says so. A deployment named by the template is preselected when it exists on this site.
- **Create profile** saves the profile and opens it in the profile editor, with a notice that everything else is optional. A missing title, a missing or duplicate technical name, or an invalid deployment shows the setup step again with the error next to the field.
- **Back**, or **Choose a starting point** in the breadcrumb, returns to the profiles list with the picker open.

Because the profile is built from the template itself rather than from the editor's form, every value the template carries reaches the profile, including values that have no field in the editor. Documents attached to the template are copied, so the new profile owns its copies and they are indexed for retrieval.

### Which templates get a card

The picker builds its cards from the profile templates themselves, so a new template shows up without any extra setup. A card appears for every template that meets all of these:

| Rule | Why |
| --- | --- |
| Its source is **Profile**. | System prompt templates fill in instructions; they do not create profiles. |
| **Listable** is checked (`IsListable: true` in a template file, which is the default). | Uncheck **Listable** on the template to keep it out of the picker and out of the **Apply Templates** list. |
| Its profile type is not **Template generated prompt**. | Such a profile runs inside an existing chat session rather than standing on its own. |

Templates come from every source the site has: the ones created under **Artificial Intelligence -> Templates**, the files shipped in modules (including the starter scenarios and the building-block agents), and files placed in `App_Data`. A template created under **Artificial Intelligence -> Templates** gets a card with its title, description and category; only template files can be featured today (see [Featuring your own template](#featuring-your-own-template)).

A template left out of the picker can still create a profile: **Artificial Intelligence -> Templates** shows a **Create profile** button on every profile template, for users who can manage AI profiles, and it opens the same setup step. The blank editor's **Apply Templates** card lists every listable template, template-generated prompts included.

`/Admin/ai/profile/new` opens the profiles list with the picker showing, so it can be linked to directly.

### Starter scenarios

The AI module ships four featured scenarios:

| Scenario | Profile type | Category | Needs |
| --- | --- | --- | --- |
| Website assistant | Chat | Talk to people | AI Chat |
| Answers from your docs | Chat | Talk to people | AI Chat, AI Profile Documents |
| Guided intake | Chat | Talk to people | AI Chat |
| Background summarizer | Utility | Do work in the background | |

After creating **Answers from your docs**, upload its documents in the editor's **Knowledge** tab. After creating **Guided intake**, choose the fields it collects in the **Data Extraction** card.

### Featuring your own template

A profile template stored as a file becomes a featured scenario through four front-matter keys. The AI module reads template files from:

- `Templates/Profiles/` inside any module
- `App_Data/AITemplates/Profiles/`, for every tenant
- `App_Data/Sites/{tenant}/AITemplates/Profiles/`, for one tenant

| Key | Type | Description |
| --- | --- | --- |
| `Featured` | `bool` | `true` lists the template among the featured scenarios, ahead of the other starting points. |
| `Icon` | `string` | Font Awesome class for the card, for example `fa-solid fa-comments`. |
| `Order` | `int` | Position within the category. Lower numbers come first, and a category's first scenario decides where the category appears. |
| `RequiresFeatures` | `string` | Comma-separated feature IDs that must be enabled before the scenario can be used. |

The card shows the template's `Title` and `Description`, and its `Category` becomes a filter in the picker. An agent's own description, which other agents read, is set with `ProfileDescription` so that it can differ from the card text.

```md
---
Title: Website assistant
Description: A friendly chat assistant for your site's visitors.
Category: Talk to people
IsListable: true
Featured: true
Icon: fa-solid fa-comments
Order: 1
RequiresFeatures: CrestApps.OrchardCore.AI.Chat
ProfileType: Chat
TitleType: Generated
WelcomeMessage: Hi there! How can I help you today?
---

You are a friendly, helpful assistant on this website.
```

A value that does not parse, such as a non-numeric `Order`, is ignored. These keys describe the template only and are never copied onto a profile created from it.

Multi-line values use a `Key: |` block. The parser trims every continuation line and drops blank lines, so keep a multi-line value such as a Liquid `PromptTemplate` short and free of meaningful indentation.

### Finishing a profile built from a template

A module can adjust a profile built from a template before it is saved by implementing `IAIProfileTemplateApplicationHandler`. Handlers run once, just before the profile is persisted. They never run while the profile editor is merely prefilled from a template, so a form that is never saved leaves nothing behind.

```csharp
internal sealed class MyTemplateApplicationHandler : IAIProfileTemplateApplicationHandler
{
    public Task AppliedAsync(AIProfileTemplateAppliedContext context)
    {
        // context.Profile already has its ItemId; context.Template is the template it came from.
        return Task.CompletedTask;
    }
}
```

Register it with `services.AddScoped<IAIProfileTemplateApplicationHandler, MyTemplateApplicationHandler>()`. The AI Profile Documents feature uses this hook to replace the template's document references with copies scoped to the new profile.

## What can be templated in Orchard

Depending on the enabled features, profile templates can carry Orchard-managed defaults for items such as:

- selected orchestrator
- provider connection and deployment choices
- prompt-template selections
- tool and agent selections
- memory-related options
- chat-specific settings exposed by enabled modules

This makes profile templates the easiest way to standardize new AI profiles for content editors and administrators.

## How they fit with recipes

The base AI module registers Orchard recipe support for AI profile templates, so templates can be created and promoted through recipes as part of tenant provisioning or deployment workflows.

Recipe imports use the `AIProfileTemplate` step. Each imported template item must supply `Name`, `DisplayText`, and `Source`. When `ItemId` is not provided, Orchard treats the `Name` + `Source` pair as the identifier used to locate an existing template before creating a new one.

## Shared framework documentation

The underlying profile model and shared profile concepts are documented in **CrestApps.Core**:

- [AI profiles](https://core.crestapps.com/docs/core/ai-profiles)
- [AI templates](https://core.crestapps.com/docs/core/ai-templates)

## Related Orchard docs

- [AI Services](overview)
- [AI Prompt Templates](prompt-templates)
- [AI Chat](chat)
- [AI Chat Interactions](chat-interactions)
