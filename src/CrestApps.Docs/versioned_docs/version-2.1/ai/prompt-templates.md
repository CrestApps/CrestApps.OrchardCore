---
sidebar_label: Prompt Templates
sidebar_position: 10
title: AI Prompt Templates
description: Orchard Core module guidance for reusable AI prompt templates.
---

# AI Prompt Templates

| | |
| --- | --- |
| **Feature Name** | AI Prompt Templates |
| **Feature ID** | `CrestApps.OrchardCore.AI.Prompting` |

This module brings the shared CrestApps.Core template system into Orchard Core and makes prompt templates available to Orchard-managed AI profiles and chat experiences.

## When to enable it

Enable **AI Prompt Templates** when you want Orchard-managed AI experiences to reuse prompt fragments instead of duplicating large system-message blocks in every profile or interaction.

## Orchard setup

After enabling the feature:

- AI profile editors can select prompt templates
- chat interaction editors can select prompt templates
- AI profile templates can select prompt templates
- templates discovered from enabled Orchard features become available automatically

The module replaces the default template service with an Orchard-aware implementation and scans enabled modules for prompt files.

## Where templates are discovered

Prompt templates are discovered from module files and other registered Orchard template providers. The common Orchard pattern is to place them in a module under:

```text
Templates\Prompts\
```

Feature-aware discovery means a template is only available when the owning feature is enabled in the tenant.

## How Orchard users work with prompt templates

In Orchard editors, prompt templates appear through a picker in the relevant AI editor. Use them to:

- standardize system prompts across profiles
- compose reusable instructions for chat interactions
- keep profile templates consistent across environments

## Core documentation

The template engine itself is shared across hosts, so the detailed guidance is published in **CrestApps.Core**:

- [AI templates](https://core.crestapps.com/docs/core/ai-templates)

That includes:

- template file conventions
- front matter metadata
- Liquid rendering behavior
- template composition and shared rendering concepts

## Orchard usage notes

Use the Orchard docs here to understand **where** prompt templates show up and how they fit into the Orchard editors. Use the Core docs when you need to author complex Liquid templates or extend the shared template engine.

## Using `renderAITemplate` in Orchard scripting

When both `CrestApps.OrchardCore.AI` and `OrchardCore.Recipes.Core` are enabled, Orchard scripting gets a `renderAITemplate` global method for prompt-template files discovered by `ITemplateService`.

Use the template **ID** (the file name without the extension), not an `AIProfileTemplate` record name:

```javascript
renderAITemplate("customer-support-intro")
```

To render the template with variables, pass a JSON-style object as the second argument:

```javascript
renderAITemplate("customer-support-intro", {
  siteName: "Contoso",
  audience: "support agents"
})
```

Behavior:

- `renderAITemplate("template-id")` returns the template content as stored by `ITemplateService`
- `renderAITemplate("template-id", { ... })` renders that template and passes the supplied variables into the Liquid context
- the second argument must be an object (or JSON object string) whose property names match the variables used by the template

This method is intended for prompt-template files under `Templates\Prompts\`. It does not read runtime `AIProfileTemplate` catalog entries.

This is especially useful with **SystemPrompt** templates because it lets recipes and Orchard scripting compose reusable system-message fragments without duplicating large prompt blocks.
