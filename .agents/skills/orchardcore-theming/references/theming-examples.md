# Theming Examples

## Example 1: Simple Blog Theme

### Manifest.cs

```csharp
using OrchardCore.DisplayManagement.Manifest;

[assembly: Theme(
    Name = "CrestApps.BlogTheme",
    Author = "CrestApps",
    Website = "https://crestapps.com",
    Version = "1.0.0",
    Description = "A clean and responsive blog theme.",
    BaseTheme = "TheTheme"
)]
```

### Views/Layout.liquid

```liquid
<!DOCTYPE html>
<html lang="{{ Culture.Name }}">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>{{ Model.Title }} - {{ Site.SiteName }}</title>
    {% resources type: "HeadMeta" %}
    {% resources type: "HeadLink" %}
    {% style src: "~/CrestApps.BlogTheme/css/site.css" %}
    {% resources type: "Stylesheet" %}
</head>
<body>
    <header class="site-header">
        {% zone "Navigation" %}
        <div class="container">
            <h1 class="site-title">{{ Site.SiteName }}</h1>
        </div>
    </header>

    <main role="main" class="container">
        <div class="row">
            <div class="col-md-8">
                {% zone "Content" %}
            </div>
            <aside class="col-md-4">
                {% zone "Sidebar" %}
            </aside>
        </div>
    </main>

    <footer class="site-footer">
        {% zone "Footer" %}
        <div class="container">
            <p>&copy; {{ "now" | date: "%Y" }} {{ Site.SiteName }}</p>
        </div>
    </footer>

    {% resources type: "FooterScript" %}
</body>
</html>
```

## Example 2: Content Shape Override (Liquid)

### Views/Content-BlogPost.liquid

```liquid
<article class="blog-post">
    <header>
        <h2>{{ Model.ContentItem.DisplayText }}</h2>
        <time datetime="{{ Model.ContentItem.PublishedUtc | date: "%Y-%m-%d" }}">
            {{ Model.ContentItem.PublishedUtc | date: "%B %d, %Y" }}
        </time>
    </header>

    <div class="post-body">
        {{ Model.Content | shape_render }}
    </div>
</article>
```

## Example 3: Content Shape Override (Razor)

### Views/Content-BlogPost.cshtml

```cshtml
<article class="blog-post">
    <header>
        <h2>@Model.ContentItem.DisplayText</h2>
        <time datetime="@Model.ContentItem.PublishedUtc?.ToString("yyyy-MM-dd")">
            @Model.ContentItem.PublishedUtc?.ToString("MMMM dd, yyyy")
        </time>
    </header>

    <div class="post-body">
        @await DisplayAsync(Model.Content)
    </div>
</article>
```

## Example 4: Summary Display Type Override

### Views/Content-BlogPost.Summary.liquid

```liquid
<article class="blog-summary">
    <h3>
        <a href="{{ Model.ContentItem | display_url }}">
            {{ Model.ContentItem.DisplayText }}
        </a>
    </h3>
    <time datetime="{{ Model.ContentItem.PublishedUtc | date: "%Y-%m-%d" }}">
        {{ Model.ContentItem.PublishedUtc | date: "%B %d, %Y" }}
    </time>
</article>
```

## Example 5: Media Field Rendering in a Template

### Razor — Image from MediaField

```cshtml
@{
    var imgPath = Model.ContentItem.Content.ArticlePart.HeroImage.Paths?[0]?.ToString();
    var imgAlt = Model.ContentItem.Content.ArticlePart.HeroImage.MediaTexts?[0]?.ToString();
}
@if (!string.IsNullOrEmpty(imgPath))
{
    <figure>
        <img asset-src="@imgPath" asp-append-version="true" alt="@imgAlt" class="img-fluid" />
    </figure>
}
```

### Liquid — Image from MediaField

```liquid
{% assign img = Model.ContentItem.Content.ArticlePart.HeroImage.Paths[0] %}
{% assign alt = Model.ContentItem.Content.ArticlePart.HeroImage.MediaTexts[0] %}
{% if img %}
    <figure>
        <img src="{{ img | asset_url | resize_url: width: 1200 }}" alt="{{ alt }}" class="img-fluid" />
    </figure>
{% endif %}
```

## Example 6: Widget Shape Override

### Views/Widget-HeroBanner.liquid

```liquid
<section class="hero-banner">
    <div class="container">
        {{ Model.Content | shape_render }}
    </div>
</section>
```

## Example 7: Placement.json

A `placement.json` that customizes how Article content type is rendered:

```json
{
    "TitlePart": [
        { "place": "Header:1", "displayType": "Detail", "contentType": "Article" },
        { "place": "Header:1", "displayType": "Summary", "contentType": "Article" }
    ],
    "HtmlBodyPart": [
        { "place": "Content:5", "displayType": "Detail", "contentType": "Article" },
        { "place": "-", "displayType": "Summary", "contentType": "Article" }
    ]
}
```

## Example 8: Resource Manifest

```csharp
using OrchardCore.ResourceManagement;

public sealed class ResourceManifest : IResourceManifestProvider
{
    public void BuildManifests(IResourceManifestBuilder builder)
    {
        var manifest = builder.Add();

        manifest
            .DefineStyle("CrestApps.BlogTheme")
            .SetUrl("~/CrestApps.BlogTheme/css/site.min.css", "~/CrestApps.BlogTheme/css/site.css")
            .SetVersion("1.0")
            .SetDependencies("bootstrap");

        manifest
            .DefineScript("CrestApps.BlogTheme")
            .SetUrl("~/CrestApps.BlogTheme/js/site.min.js", "~/CrestApps.BlogTheme/js/site.js")
            .SetDependencies("jquery");
    }
}
```

## Example 9: Razor Layout with Resource Tag Helpers

### Views/Layout.cshtml

```cshtml
@inject OrchardCore.IOrchardHelper Orchard

<!DOCTYPE html>
<html lang="@Orchard.CultureName()">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@RenderTitleSegments(Site.SiteName)</title>
    @await RenderSectionAsync("HeadMeta", required: false)
    <style asp-name="CrestApps.BlogTheme"></style>
    @RenderSection("Styles", required: false)
</head>
<body>
    @await DisplayAsync(ThemeLayout.Header)

    <div class="container">
        <div class="row">
            <main class="col-md-8">
                @await DisplayAsync(ThemeLayout.Content)
            </main>
            <aside class="col-md-4">
                @await DisplayAsync(ThemeLayout.Sidebar)
            </aside>
        </div>
    </div>

    @await DisplayAsync(ThemeLayout.Footer)

    <script asp-name="CrestApps.BlogTheme" at="Foot"></script>
    @RenderSection("Scripts", required: false)
</body>
</html>
```

## Example 10: BagPart / FlowPart Rendering

### Liquid — Render FlowPart Items

```liquid
{% for item in Model.ContentItem.Content.FlowPart.Widgets %}
    {{ item | shape_build_display: "Detail" | shape_render }}
{% endfor %}
```

### Razor — Render BagPart Items

```cshtml
@foreach (var item in Model.ContentItem.Content.BagPart.ContentItems)
{
    @await Orchard.DisplayAsync(item, "Detail")
}
```

## Example 11: Admin Theme Child

### Manifest.cs

```csharp
using OrchardCore.DisplayManagement.Manifest;

[assembly: Theme(
    Name = "CrestApps.CustomAdmin",
    Author = "CrestApps",
    Version = "1.0.0",
    Description = "Custom admin theme with branding.",
    BaseTheme = "TheAdmin",
    Tags = new[] { "Admin" }
)]
```

Override the admin header by creating `Views/Header.cshtml` in your admin theme:

```cshtml
<header class="ta-navbar">
    <div class="d-flex align-items-center">
        <a class="ta-navbar-brand" href="~/admin">
            <img src="~/CrestApps.CustomAdmin/images/logo.svg" alt="Admin" height="32" />
        </a>
    </div>
</header>
```
