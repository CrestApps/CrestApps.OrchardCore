---
sidebar_label: Theme Designer
title: Theme Designer
description: Per-user theme drafts over Orchard Core's front-end and admin templates and the site resources, previewed by their designer and published in one step, with AI tools that design through the admin chat widget.
user_manual: user-manual/administration/theme-designer
---

| | |
| --- | --- |
| **Feature Name** | Theme Designer |
| **Feature ID** | `CrestApps.OrchardCore.ThemeDesigner` |
| **Dependencies** | `OrchardCore.Templates`, `CrestApps.OrchardCore.SiteResources` |
| **Permissions** | Orchard Core's **Manage templates** and **Manage admin templates**, and **Manage site stylesheets and scripts** |
| **Admin menu** | Design > Theme Draft |

The Theme Designer gives each designer a draft of changes to three things Orchard Core and CrestApps already own:

- front-end shape templates (Orchard Core's **Templates** feature, `TemplatesDocument`),
- admin shape templates (Orchard Core's **Admin Templates** feature, `AdminTemplatesDocument`),
- [site resources](site-resources.md) (stylesheets and scripts).

The designer sees the draft while browsing the site and the admin; nobody else does. Publishing applies every change at once. The [AI tools](#ai-tools) write to the same draft, so a user can ask the admin chat widget to restyle the site and look at the result before anyone else sees it.

## Drafts

A draft (`ThemeDesignDraft`) belongs to one user and holds up to 200 changes. Each change adds, replaces or removes a template or a resource. The drafts are kept in one cached document (`ThemeDesignDraftsDocument`), so a page finds its viewer's draft without a query.

`IThemeDesignDraftService` is the only way to change a draft, and checks the permission of the module that owns what changes: **Manage templates** for front-end templates, **Manage admin templates** for admin templates (which also need the Admin Templates feature on), and **Manage site stylesheets and scripts** for resources. A designer can only draft what they could change by hand.

- A template's Liquid is validated (`ILiquidTemplateManager.Validate`) before it is saved. Names are shape types or alternates: letters, digits, underscores, dashes and dots.
- A resource is validated with `SiteResourceValidator` against the published resources and the draft's own, and minified; a resource that can't be minified is refused so the draft never holds broken CSS or JavaScript.
- `PublishAsync` checks every permission and validates every resource before it applies anything, then writes the templates documents once each and creates, updates or deletes the resources through the site resources catalog manager, so their handlers run. The draft is deleted afterwards.
- `RevertAsync` drops one change, `DiscardAsync` the whole draft, and `SetPreviewAsync` turns the preview off without losing the draft.

## Preview

While a designer with a draft browses (and has not turned the preview off):

- `ThemeDesignPreviewShapeBindingResolver` renders the shapes the draft changes with the draft's Liquid. It is registered before Orchard Core's template resolvers, so a drafted template wins over a published one. A template the draft removes falls back to the theme's own binding.
- `ThemeDesignSiteResourcesHandler` (an `ISiteResourcesHandler`) replaces, adds and removes the page's site resources with the draft's, served to the designer only from `/theme-designer/resources/{name}.css` or `.js`.
- `ThemeDesignPreviewCacheEvents` adds the `user` cache context and the draft's tag to every cached shape, before Dynamic Cache looks it up. Shapes rendered with the draft are cached for the designer alone and dropped whenever the draft changes, so a draft never leaks into what other visitors get.
- A small bar at the bottom left of each page says the draft is shown, with **Review** and **Stop preview**.

The Theme Designer's own admin page always renders without the draft, so a draft that breaks the admin layout can still be discarded there.

## Theme shapes

`IThemeShapeExplorer` tells a designer, or the AI, what can be replaced: the active front-end and admin themes, the shape types and alternates their shape tables bind (with the file that renders each one), and the source of that file (Liquid or Razor, read through `IShapeTemplateFileProviderAccessor`), so a new template can keep the theme's markup.

## Page previews

`IThemeDraftPagePreviewer` renders a page of the site as the signed-in designer by requesting it with their own cookies, so the page has their draft, permissions and theme. It returns the status code, the error of a failed page, the stylesheets it loads with their size and whether they are the site's own resources, the scripts, its body markup (without scripts, styles and SVG paths) and the templates that rendered it. For those, the request carries a one-time trace id, and `ThemeDesignShapeTraceEvents` records each shape's winning template (the draft's, a published template, or a theme or module file) with the name a replacement must take, such as `Content__Blog`. Redirects, such as to the login page, are reported rather than followed.

## AI tools

With **Orchard Core AI Agent** on, the Theme Designer adds the **Theme Design** tools. They act as the signed-in user, through `IThemeDesignDraftService`, and everything they save goes to that user's draft. See the [AI function catalog](../ai/tools.md#theme-design-tools).

The tools are built to keep a model's theme working, not only valid:

- `getThemeDesignOverview` returns a design guide (the `theme-design-guide` prompt template in AI Agent, which a site can override like any prompt template). It tells the model to decide first between a **restyle** (keep the theme and its stylesheets, override on top) and a **new theme** (own Layout and class names, the theme's stylesheets dropped for Bootstrap and the icons, every content template on the home and item pages replaced, and a complete stylesheet), and covers the shape names to use and the Liquid that works in Orchard Core.
- `saveShapeTemplate` refuses a Layout that stops loading the stylesheets and scripts the theme's own Layout loads, and quotes the lines to add back; a Layout that loads Bootstrap itself counts as a new theme and is saved with a warning naming what it left out, and a model can also name resources in `dropThemeResources`. It warns about heavy inline styling.
- `previewThemeDraftPage` lets the model check each change on the real page, and tells it which template drew each part, so it replaces the right one.

The **Theme Designer Agent** profile template gives an assistant the design tools as an agent: add it on the assistant's **Capabilities** tab, for example on the profile the admin widget uses, and ask the widget things like "make the site look like a modern bakery, warm colors and rounded cards" or "move the menu into a dark header with the logo on the left". The agent has no publish or discard tool, because tools an agent runs on behalf of an assistant don't ask for approval: the user publishes from **Design > Theme Draft**. To let the assistant publish or discard, tick `publishThemeDesign` and `discardThemeDesign` on the assistant itself; the chat then asks the user to **Allow** or **Deny** each call.
