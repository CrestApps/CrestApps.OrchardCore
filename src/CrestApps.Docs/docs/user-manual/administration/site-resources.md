---
sidebar_label: Site Resources
title: "Site Resources: Add Your Own Stylesheets and Scripts"
description: Keep stylesheets and scripts in the site and add them to every page, to the pages of one theme, or only where a template asks for them.
technical_manual:
  - modules/site-resources
---

Site resources are stylesheets and scripts you keep in the site instead of in a theme's files. Use them to restyle a theme, to add a small script to every page, or to give a template a stylesheet it asks for by name. They are made smaller (minified) when you save them, and browsers keep them until they change.

| | |
| --- | --- |
| **Menu** | Design > Site Resources |
| **Permission** | Manage site stylesheets and scripts |
| **Feature** | Site Resources |

<AskYourAdmin />

## Add a stylesheet or a script

1. Open **Design > Site Resources** and click **Add resource**.
2. Give it a **Name**, such as `site-colors`. Use letters, digits, dots, dashes and underscores.
3. Choose **Stylesheet (CSS)** or **Script (JavaScript)**, and enter its content.
4. Under **Where it goes**, choose the pages that get it.
5. Click **Save**.

Saving changes the published site straight away.

| Field | What it does |
| --- | --- |
| **Minify** | Serve it without comments and extra spaces. On at first. When it can't be made smaller, it is served as you wrote it. |
| **Enabled** | Turn it off to take it off every page without removing it. |
| **Pages** | The front end, the admin, or both. |
| **Themes** | Tick themes to add the resource only to the pages they render, so a stylesheet written for one theme stays out of the others, even after you switch themes. Tick none for every theme. |
| **Added to** | **Every page**, or **Only pages whose templates ask for it by name**. |
| **Location** | For scripts: in the head or at the end of the page. |
| **Depends on** | Resources that must be on the page first, such as `bootstrap` or `jQuery`. |
| **Order** | Resources added to every page are added smallest first. |

## Restyle one theme

1. Add a stylesheet with your changes.
2. Under **Themes**, tick the theme it is written for.
3. Set **Pages** to the front end (or the admin, for an admin theme) and **Added to** to **Every page**.

The stylesheet is added while that theme is active, and left out if you switch to another theme.
