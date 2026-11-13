---
sidebar_label: Site Resources
title: Site Resources
description: Stylesheets and scripts kept in the site, minified, served from versioned addresses, and added to every page, to the pages of chosen themes, or only where a template asks for them.
user_manual: user-manual/administration/site-resources
---

| | |
| --- | --- |
| **Feature Name** | Site Resources |
| **Feature ID** | `CrestApps.OrchardCore.SiteResources` |
| **Permission** | Manage site stylesheets and scripts (`ManageSiteResources`, security critical) |
| **Admin menu** | Design > Site Resources |

Site Resources keeps CSS and JavaScript in the site itself, so a theme can be restyled, or a template can get the script it needs, without deploying files. Each resource is minified when it is saved (with [NUglify](https://github.com/trullock/NUglify)), served from its own address with a content hash, and registered with Orchard Core's resource manager.

## What a resource is

| Member | What it does |
| --- | --- |
| `Name` | Unique. Letters, digits, dots, dashes and underscores, starting with a letter or digit (up to 100). It is the name templates ask for and the end of the resource's address. It can't be a name a module or theme already defines, such as `bootstrap`. |
| `Type` | `Stylesheet` or `Script`. |
| `Content` | The CSS or JavaScript as written (up to 1,048,576 characters). |
| `Minify` | Serve it minified (on by default). When the content can't be minified, it is served as written and the error is logged. |
| `Target` | `Frontend`, `Admin` or `Everywhere`. |
| `Themes` | The identifiers of the themes the resource is limited to, such as `TheBlogTheme` or `TheAdmin`. A page gets the resource only while one of them renders it, so a stylesheet can restyle one theme and stay out of the others, including after the site switches themes. Empty for every theme. |
| `Placement` | `Global` adds it to every page of its target. `OnDemand` only defines it, so a template adds it by name. |
| `Location` | `Foot` (default) or `Head`, for scripts. Stylesheets always go in the head. |
| `Dependencies` | Resources that must be on the page first, such as `jQuery`, `bootstrap` or another site resource. |
| `Position` | The order global resources are added, smallest first. |
| `Enabled` | Turn a resource off without removing it. |

The resources are a document-backed named catalog (`INamedCatalog<SiteResource>`, one cached document), so pages read them without a database query.

## How pages get them

`SiteResourcesFilter` runs before every full page (`ViewResult` and `PageResult`). It asks `ISiteResourceService.GetPageResourcesAsync(isAdmin, themeId)` for the enabled resources offered to the page's side and to the theme that renders it (`IThemeManager`: the admin theme on admin pages, the site's theme elsewhere), defines each one in the request's inline resource manifest with its address and dependencies, and registers the global ones. Because every resource is defined, a template can ask for an on-demand one by name:

```liquid
{% style name: "pricing-table" %}
{% script name: "image-slider", at: "Foot" %}
```

```html
<script asp-name="image-slider" at="Foot"></script>
```

## Addresses and caching

A published resource is served at `/site-resources/{name}.css` or `/site-resources/{name}.js` (under the tenant's prefix), with `?v=` and the first 12 hex characters of the SHA-256 of what is served. An address with the current version is cached for a year (`immutable`); any other address is revalidated with an ETag. Front-end resources are public. A resource offered only to the admin is served to users with **Access admin panel**.

## Extending it

| Contract | Use |
| --- | --- |
| `ISiteResourceService` | Gets the resources a page gets, a resource's address, and prepares (minifies and versions) a resource. |
| `ISiteResourcesHandler` | Changes the resources offered to the page being rendered (its `IsAdmin` and `ThemeId` are on the context): add, replace or remove `SiteResourceEntry` items. |
| `SiteResourceValidator` | The rules every editor applies: name, uniqueness, names modules define, content, self-dependency. |
| `SiteResourceResponses` | Parses a requested file name and writes a resource with the caching headers above, for another endpoint that serves resources. |

## Recipes and deployment

The `SiteResources` recipe step creates or updates resources, matched by `ItemId`. A deployment plan's **Site resources** step exports them all; what is served is worked out again on the receiving site. With CrestApps Recipes on, the step describes its JSON schema.

```json
{
  "steps": [
    {
      "name": "SiteResources",
      "Resources": [
        {
          "Name": "site-theme",
          "Type": "Stylesheet",
          "Content": ":root { --brand: #0f766e; } a { color: var(--brand); }",
          "Target": "Frontend",
          "Themes": ["TheBlogTheme"],
          "Placement": "Global"
        }
      ]
    }
  ]
}
```
