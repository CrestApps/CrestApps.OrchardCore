# CrestApps Orchard Core Site Resources

Stylesheets and scripts kept in the site, minified and served from versioned addresses.

## Features

| Feature | Id | What it adds |
| --- | --- | --- |
| Site Resources | `CrestApps.OrchardCore.SiteResources` | CSS and JavaScript resources, minified with NUglify when saved, served from `/site-resources/{name}.css` or `.js` with a content hash, and added to every front-end or admin page (optionally only those of chosen themes), or defined so a template asks for them by name. A `SiteResources` recipe and deployment step. |

## Admin

- **Design > Site Resources**: the resources, with the **Manage site stylesheets and scripts** permission (security critical: a script runs in every visitor's browser).

## Extending

Inject `ISiteResourceService` for the resources a page gets, a resource's address, and to minify and version one. Implement `ISiteResourcesHandler` to change the resources offered to the page being rendered. `SiteResourceValidator` holds the rules every editor applies, and `SiteResourceResponses` serves a resource with the same caching headers from another endpoint.

## Documentation

- User Manual: [Site Resources](../../CrestApps.Docs/docs/user-manual/administration/site-resources.md)
- Technical Manual: [Site Resources](../../CrestApps.Docs/docs/modules/site-resources.md)
