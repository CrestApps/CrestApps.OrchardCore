# CrestApps Orchard Core Theme Designer

Theme drafts over Orchard Core's front-end templates, admin templates and the site resources: only their designer sees them, until they publish.

## Features

| Feature | Id | What it adds |
| --- | --- | --- |
| Theme Designer | `CrestApps.OrchardCore.ThemeDesigner` | A per-user draft of template and site resource changes, previewed by its designer on the front end and the admin (kept out of Dynamic Cache for everyone else), and a review page to compare, drop, publish or discard changes. |

The feature depends on `OrchardCore.Templates` and `CrestApps.OrchardCore.SiteResources`. Admin templates also need `OrchardCore.AdminTemplates`.

## Admin

- **Design > Theme Draft**: the signed-in user's draft. The page always shows the published admin, so a draft that breaks the admin can still be discarded.

## AI

With Orchard Core AI Agent on, the **Theme Design** tools read the active themes' shapes and sources and write to the user's draft, and the **Theme Designer Agent** profile template bundles the design tools for the admin chat widget. The agent can't publish or discard (tools an agent runs aren't put to the user for approval); the user publishes on the review page, or from an assistant that has those tools itself, which asks for approval.

## Extending

Inject `IThemeDesignDraftService` to read or change the signed-in user's draft (every call checks the owning module's permission), and `IThemeShapeExplorer` for the active themes, their shape bindings and template sources.

## Documentation

- User Manual: [Theme Designer](../../CrestApps.Docs/docs/user-manual/administration/theme-designer.md)
- Technical Manual: [Theme Designer](../../CrestApps.Docs/docs/modules/theme-designer.md)
