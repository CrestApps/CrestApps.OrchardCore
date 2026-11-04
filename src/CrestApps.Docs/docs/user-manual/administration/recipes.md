---
sidebar_label: Recipes and Shared Resources
title: Recipes and Shared Resources
description: Set a site up from a ready-made recipe, check a pasted recipe, and see which common building blocks the CrestApps screens share.
technical_manual:
  - modules/recipes
  - modules/resources
---

A **recipe** is a ready-made setup that creates content types, settings, roles or sample content in one go. Run one to set up a new or training site the same way every time, or to try a feature with sample content. The **CrestApps Recipes** feature adds a description to each recipe and checks recipes that you paste in. The **CrestApps Resources** feature keeps the small building blocks that CrestApps screens share in one place.

| | |
| --- | --- |
| **Menu** | Tools > Recipes; Tools > Deployments > JSON Import |
| **Permission** | Manage Recipes (to run one); Import Data (JSON Import) |
| **Feature** | CrestApps Recipes; CrestApps Resources |

<AskYourAdmin />

<YouTube video="site-wizard-recipes" title="Video overview of wizards, recipes and shared resources" />

## Run a recipe

<YouTube video="site-wizard-recipes" title="Video of the recipe list and the JSON import editor" start={229} end={264} />

1. Open **Tools > Recipes**. The recipes are grouped by feature. Each has a name, tags and a description of what it creates.
2. Type in the **Search** box to find a recipe by name or tag.
3. Read the description, then click **Run** and confirm. The page reports the result when the recipe is done.

:::caution
A recipe changes the site: it can create content types, settings, roles and content. Read the description first, and run a recipe on a copy of the site when you are not sure.
:::

## Paste a recipe

1. Open **Tools > Deployments > JSON Import**.
2. Paste or type the recipe in the **Recipe JSON** editor. It suggests the names of the steps and their settings as you type, and underlines mistakes before anything runs.
3. Click **Import**.

## Shared resources

The **CrestApps Resources** feature has no screen of its own. It is turned on by the CrestApps features that need it, and keeps these building blocks in one place, so they look and behave the same on every screen:

- The **date range menu**, with presets such as *Last 7 Days* and *This Month*, a custom range, and *On or before* and *On or after* dates. The report filters use it.
- The **phone box with country flags**, used by the [Phone Field](content-fields.md) and by other screens that ask for a phone number.
- **Select all** checkboxes on admin lists, collapsible panels, date and time pickers, and item selectors.
