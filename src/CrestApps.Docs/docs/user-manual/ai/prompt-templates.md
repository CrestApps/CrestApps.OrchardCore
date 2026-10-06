---
sidebar_label: Templates
title: Profile Templates and Prompt Templates
description: Save reusable starting points for new AI profiles, and reuse pieces of instructions across profiles and chats.
technical_manual:
  - ai/profile-templates
  - ai/prompt-templates
---

Templates save you from setting up the same thing twice. A **profile template** is a ready-made starting point for new [AI profiles](profiles.md): pick it in the **New AI Profile** picker and the new profile gets its type, model, instructions, knowledge and tools. A **system prompt template** is a reusable piece of instructions, such as "Answer in Markdown", that you add to any profile or chat interaction.

| | |
| --- | --- |
| **Menu** | Artificial Intelligence > Templates |
| **Permission** | Manage AI profile templates (Manage AI profiles to create a profile from a template) |
| **Feature** | AI Services; AI Prompt Templates to add prompt templates to profiles and chats |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of creating an AI profile template with reusable defaults">
  <source src="/img/docs/ai-profile-templates.mp4" type="video/mp4" />
</video>

## Create a template

1. Open **Artificial Intelligence > Templates** and click **Add Template**.
2. In **Available Sources**, click **Add** on the kind of template:
   - **Profile**: a starting point for new AI profiles.
   - **System Prompt**: a reusable piece of instructions.
3. Fill in the fields below and click **Save**.

Every template has these fields:

| Field | What it does |
| --- | --- |
| **Title** | The name shown in lists and in the picker. |
| **Technical name** | A unique name used behind the scenes. It cannot be changed later. |
| **Description** | What the template is for. For a profile template, it is the text on its card in the **New AI Profile** picker. |
| **Category** | A group name. Profile templates with the same category are grouped together in the picker. |
| **Listable** | For a profile template: show it in the **New AI Profile** picker. For other templates: show it in the template list where you pick templates. Untick it to keep a template out of sight without deleting it. |

### Profile template

A profile template has the same tabs as the [profile editor](profiles.md#the-profile-editor): **General**, **Deployments & Interactions**, **Knowledge**, **Instructions**, **Parameters**, **Capabilities**, **Data Processing & Metrics** and **Prompt Security**. Whatever you fill in is copied onto every profile created from the template. Leave a field empty to let each new profile use the default.

On the **General** tab, **Profile type** decides what kind of profile the template creates. Templates of type **Template generated prompt** never appear in the **New AI Profile** picker.

Documents you upload to a template are copied to each profile created from it, so each profile owns its own copy.

### System prompt template

A system prompt template has one more field, **System prompt**: the instructions to reuse, written like the **System instructions** of a profile. For example, a *Company tone* template that tells the AI to write in your brand's voice.

## Create a profile from a template

Every listable profile template appears as a card in the **New AI Profile** picker. See [Create a profile](profiles.md#create-a-profile).

You can also start from the template list: click **Create profile** on a profile template (you need **Manage AI profiles**). This works for templates that are not listable, too. The same short setup step opens: check the **Title**, **Technical name** and **Chat deployment**, then click **Create profile**.

A profile keeps no link to its template. Changing the template later does not change profiles already created from it.

:::tip[Standardize your assistants]
Create one profile template per kind of assistant your teams need, for example *Internal helpdesk* and *Website assistant*, with your approved model, tone and safety settings. Teams then start from the template and only add their own knowledge.
:::

## Reuse pieces of instructions

With the **AI Prompt Templates** feature on, profiles, profile templates, chat interactions and AI workflow tasks have a **Prompt templates** field next to their instructions. The selected templates are added, in order, before your own instructions.

<video controls preload="metadata" width="100%" aria-label="Screencast of attaching reusable prompt templates to an AI profile">
  <source src="/img/docs/ai-prompt-templates.mp4" type="video/mp4" />
</video>

1. In the profile editor, find **Prompt templates** in the **Instructions** card (in a chat interaction, on the **Settings** tab).
2. Click **Add prompt template**, type in **Search prompt templates...**, and pick a template. The list is grouped by category.
3. Repeat to add more. Click a template's card to see its details, and **Remove** to take it off.
4. Save the profile. A chat interaction saves by itself.

The list holds your **System Prompt** templates plus ready-made templates that come with the site's features, such as **Use Markdown Syntax**.

Some templates have settings of their own. Their card lists **Expected parameters:**, and you fill them in under **Template parameters**. Ask the person who wrote the template what to enter; the values are written in a technical format.

A template that is no longer available, for example because its feature was turned off, shows the badge **Unavailable**.
