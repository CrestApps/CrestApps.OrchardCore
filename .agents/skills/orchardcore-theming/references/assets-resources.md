# Assets and Resources

How to register and require scripts/styles via Orchard Core's resource manager.

## Define Resources in a Manifest

Create a class implementing `IResourceManifestProvider` in a module or theme:

```csharp
using OrchardCore.ResourceManagement;

internal sealed class ResourceManifest : IResourceManifestProvider
{
    public void BuildManifests(IResourceManifestBuilder builder)
    {
        var manifest = builder.Add();

        manifest
            .DefineStyle("MyTheme")
            .SetUrl("~/MyTheme/css/site.min.css", "~/MyTheme/css/site.css")
            .SetVersion("1.0")
            .SetDependencies("bootstrap");

        manifest
            .DefineScript("MyTheme")
            .SetUrl("~/MyTheme/js/site.min.js", "~/MyTheme/js/site.js")
            .SetDependencies("jquery");
    }
}
```

## Require Resources in Razor

Using tag helpers:

```cshtml
<!-- From manifest (by name) -->
<style asp-name="MyTheme"></style>
<script asp-name="MyTheme" at="Foot"></script>

<!-- Direct file inclusion -->
<style asp-src="~/MyTheme/css/extra.css" at="Head"></style>
<script asp-src="~/MyTheme/js/extra.js" at="Foot"></script>

<!-- With dependency ordering -->
<style asp-name="MyTheme" depends-on="bootstrap"></style>
```

Placement options: `at="Head"` or `at="Foot"` controls rendering location.

## Require Resources in Liquid

```liquid
{% style src: "~/MyTheme/css/site.css" %}
{% script src: "~/MyTheme/js/site.js" at: "Foot" %}
{% resources type: "Stylesheet" %}
{% resources type: "FooterScript" %}
```

For simple cases in Liquid, link static assets directly:

```liquid
<link rel="stylesheet" href="{{ '~/MyTheme/css/site.css' | href }}">
```

## Built-In Orchard Core Resources

Common style/script names you can require without adding CDNs yourself (from `OrchardCore.Resources`):

| Resource Name | Type | Description |
|---|---|---|
| `bootstrap` | CSS/JS | Bootstrap framework |
| `bootstrap-rtl` | CSS | Bootstrap RTL support |
| `font-awesome` | CSS | Font Awesome icons |
| `jQuery` | JS | jQuery library |
| `jQuery-ui` | JS | jQuery UI |
| `trumbowyg` | JS/CSS | WYSIWYG editor |
| `codemirror` | JS/CSS | Code editor |
| `bootstrap-select` | JS/CSS | Bootstrap select component |
| `nouislider` | JS/CSS | Range slider |
| `vue-multiselect` | JS/CSS | Vue multiselect component |

Prefer using these names in `asp-name`/`depends-on` instead of adding CDN links manually.

## Injecting Resources for Admin Pages

Use a resource filter to include styles/scripts on admin pages:

```csharp
using OrchardCore.ResourceManagement;

[RequireFeatures("OrchardCore.Admin")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddResourceConfiguration<AdminResourceFilter>();
    }
}

public sealed class AdminResourceFilter : IResourceFilterProvider
{
    public void AddResourceFilter(ResourceFilterBuilder builder)
    {
        builder
            .WhenAdmin()
            .IncludeStyle("MyModule.AdminStyles");
    }
}
```

## Static File Serving

- Static files live under `wwwroot/` in your theme or module.
- Files are served at `~/ModuleOrThemeName/path/to/file`.
- Orchard Core's module file provider handles routing automatically.
- Use `asset-src` tag helper in Razor for cache-busting: `<img asset-src="~/MyTheme/images/logo.png" asp-append-version="true">`.

## Tips

- Keep resource names stable; use versioning to bust caches.
- Prefer manifest resources so dependencies are tracked and deduplicated.
- Use `at="Head"` for critical CSS; default to `Foot` for scripts.
- For quick theme prototyping, you can use a CDN (e.g., Tailwind Play CDN) directly in the layout head. Prefer a build pipeline for production.
