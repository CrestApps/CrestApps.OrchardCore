# Razor View Examples

## Example 1: Complete Theme Layout

### Views/_ViewImports.cshtml

```cshtml
@inherits OrchardCore.DisplayManagement.Razor.RazorPage<TModel>
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
@addTagHelper *, OrchardCore.DisplayManagement
@addTagHelper *, OrchardCore.ResourceManagement
@addTagHelper *, OrchardCore.Menu
@addTagHelper *, OrchardCore.Contents
@addTagHelper *, OrchardCore.Media
@using OrchardCore
@using OrchardCore.DisplayManagement
@using OrchardCore.DisplayManagement.Shapes
@using OrchardCore.ContentManagement
@using Microsoft.AspNetCore.Html
```

### Views/_ViewStart.cshtml

```cshtml
@{
    Layout = "Layout";
}
```

### Views/Layout.cshtml

```cshtml
@{
    var body = await RenderBodyAsync();
}
<!DOCTYPE html>
<html lang="en" dir="ltr">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@RenderTitleSegments(Site.SiteName, " - ")</title>
    <resources type="Meta" />
    <style asp-name="MyTheme"></style>
    <resources type="HeadLink" />
    <resources type="HeadScript" />
    <resources type="StyleSheet" />
</head>
<body>
    <header>
        @if (Model.Navigation != null)
        {
            <nav class="navbar">
                <div class="container">
                    <a class="navbar-brand" href="~/">@Site.SiteName</a>
                    <zone name="Navigation" />
                </div>
            </nav>
        }
    </header>

    @if (Model.BeforeContent != null)
    {
        <div class="before-content">
            <zone name="BeforeContent" />
        </div>
    }

    <main class="container">
        <div class="row">
            <div class="@(Model.Sidebar != null ? "col-md-8" : "col-md-12")">
                <zone name="Content">
                    @body
                </zone>
            </div>

            @if (Model.Sidebar != null)
            {
                <aside class="col-md-4">
                    <zone name="Sidebar" />
                </aside>
            }
        </div>
    </main>

    @if (Model.Footer != null)
    {
        <footer class="site-footer">
            <div class="container">
                <zone name="Footer" />
            </div>
        </footer>
    }

    <resources type="FooterScript" />
</body>
</html>
```

### ResourceManifest.cs

```csharp
public sealed class ResourceManifest : IResourceManifestProvider
{
    public void BuildManifests(IResourceManifestBuilder builder)
    {
        var manifest = builder.Add();

        manifest.DefineStyle("MyTheme")
            .SetUrl("~/MyTheme/css/site.min.css", "~/MyTheme/css/site.css");

        manifest.DefineScript("MyTheme")
            .SetUrl("~/MyTheme/js/site.min.js", "~/MyTheme/js/site.js")
            .SetDependencies("jQuery")
            .SetPosition("Foot");
    }
}
```

### Startup.cs

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddResourceManifest<ResourceManifest>();
    }
}
```

## Example 2: Blog Post Detail Template

This template overrides the shape for a `BlogPost` content type rendered in `Detail` display type.

### Views/Content-BlogPost.Detail.cshtml

```cshtml
@using OrchardCore.ContentManagement

@{
    var contentItem = Model.ContentItem as ContentItem;
    var blogPostPart = contentItem.Content.BlogPost;
    var imagePaths = blogPostPart?.Image?.Paths;
    var subtitle = blogPostPart?.Subtitle?.Text?.ToString();
}

<article>
    <header>
        <h1>@contentItem.DisplayText</h1>

        @if (!string.IsNullOrEmpty(subtitle))
        {
            <p class="lead">@subtitle</p>
        }

        <div class="meta text-muted">
            <date-time utc="@contentItem.PublishedUtc" format="MMMM dd, yyyy" />
        </div>
    </header>

    @if (imagePaths != null && imagePaths.Count > 0)
    {
        <div class="featured-image">
            <img asp-src="@imagePaths[0]"
                 asp-resize-width="1200"
                 asp-resize-height="630"
                 asp-resize-mode="Crop"
                 alt="@contentItem.DisplayText"
                 class="img-fluid rounded" />
        </div>
    }

    <div class="body-content">
        @await DisplayAsync(Model.Content)
    </div>
</article>
```

## Example 3: Blog Post Summary Template

### Views/Content-BlogPost.Summary.cshtml

```cshtml
@using OrchardCore.ContentManagement

@{
    var contentItem = Model.ContentItem as ContentItem;
    var blogPostPart = contentItem.Content.BlogPost;
    var imagePaths = blogPostPart?.Image?.Paths;
    var subtitle = blogPostPart?.Subtitle?.Text?.ToString();
}

<article class="card mb-4">
    @if (imagePaths != null && imagePaths.Count > 0)
    {
        <img asp-src="@imagePaths[0]"
             asp-resize-width="400"
             asp-resize-height="250"
             asp-resize-mode="Crop"
             class="card-img-top"
             alt="@contentItem.DisplayText" />
    }

    <div class="card-body">
        <h2 class="card-title">
            <a href="@Url.DisplayContentItem(contentItem)">@contentItem.DisplayText</a>
        </h2>

        @if (!string.IsNullOrEmpty(subtitle))
        {
            <p class="card-text text-muted">@subtitle</p>
        }

        <div class="card-text">
            <small class="text-muted">
                <date-time utc="@contentItem.PublishedUtc" format="MMM dd, yyyy" />
            </small>
        </div>
    </div>
</article>
```

## Example 4: Widget Template with Caching

### Views/Content-HeroBanner.cshtml

```cshtml
@using OrchardCore.ContentManagement

@{
    var contentItem = Model.ContentItem as ContentItem;
    var heroPart = contentItem.Content.HeroBanner;
    var heading = heroPart?.Heading?.Text?.ToString();
    var bgImagePaths = heroPart?.BackgroundImage?.Paths;
    var ctaUrl = heroPart?.CallToAction?.Url?.ToString();
    var ctaText = heroPart?.CallToAction?.Text?.ToString();
}

<cache expires-after="@TimeSpan.FromMinutes(30)" vary-by="@contentItem.ContentItemVersionId">
    <section class="hero-banner" style="@(bgImagePaths != null && bgImagePaths.Count > 0 ? $"background-image: url('{bgImagePaths[0]}')" : "")">
        <div class="container text-center">
            @if (!string.IsNullOrEmpty(heading))
            {
                <h1 class="display-4">@heading</h1>
            }

            @await DisplayAsync(Model.Content)

            @if (!string.IsNullOrEmpty(ctaUrl))
            {
                <a href="@ctaUrl" class="btn btn-primary btn-lg">@(ctaText ?? "Learn More")</a>
            }
        </div>
    </section>
</cache>
```

## Example 5: Querying Content Items in a Razor View

### Views/RecentBlogPosts.cshtml

```cshtml
@{
    var recentPosts = await Orchard.GetRecentContentItemsByContentTypeAsync("BlogPost", 5);
}

@if (recentPosts.Any())
{
    <div class="recent-posts">
        <h3>Recent Posts</h3>
        <ul class="list-unstyled">
            @foreach (var post in recentPosts)
            {
                <li>
                    <a href="@Url.DisplayContentItem(post)">@post.DisplayText</a>
                    <small class="text-muted">
                        <date-time utc="@post.PublishedUtc" format="MMM dd, yyyy" />
                    </small>
                </li>
            }
        </ul>
    </div>
}
```

## Example 6: Menu Rendering in a Theme

### Views/Layout.cshtml (Navigation Section)

```cshtml
@if (Model.Navigation != null)
{
    <nav class="navbar navbar-expand-lg navbar-light bg-light">
        <div class="container">
            <a class="navbar-brand" href="~/">@Site.SiteName</a>
            <button class="navbar-toggler" type="button"
                    data-bs-toggle="collapse"
                    data-bs-target="#navbarNav">
                <span class="navbar-toggler-icon"></span>
            </button>
            <div class="collapse navbar-collapse" id="navbarNav">
                <zone name="Navigation" />
            </div>
        </div>
    </nav>
}
```

### Custom Menu Shape Override: Views/MenuItemLink.cshtml

```cshtml
@{
    var tag = (TagBuilder)Model.Tag;
}
<li class="nav-item">
    @{
        tag.AddCssClass("nav-link");
    }
    @tag
</li>
```

## Example 7: Shape with Inline Resources

### Views/Parts/ContactForm.cshtml

```cshtml
<style asp-name="ContactFormStyles"></style>
<script asp-name="ContactFormScript" depends-on="jQuery" at="Foot"></script>

<div class="contact-form">
    <h2>Contact Us</h2>
    <form method="post" asp-action="Submit" asp-controller="Contact">
        <div class="mb-3">
            <label for="name" class="form-label">Name</label>
            <input type="text" class="form-control" id="name" name="Name" required />
        </div>
        <div class="mb-3">
            <label for="email" class="form-label">Email</label>
            <input type="email" class="form-control" id="email" name="Email" required />
        </div>
        <div class="mb-3">
            <label for="message" class="form-label">Message</label>
            <textarea class="form-control" id="message" name="Message" rows="5" required></textarea>
        </div>
        <button type="submit" class="btn btn-primary">Send</button>
    </form>
</div>

<script at="Foot">
    document.querySelector('.contact-form form').addEventListener('submit', function (e) {
        var btn = this.querySelector('button[type="submit"]');
        btn.disabled = true;
        btn.textContent = 'Sending...';
    });
</script>
```

## Example 8: Content Picker and Related Items

### Views/Parts/RelatedArticles.cshtml

```cshtml
@using OrchardCore.ContentManagement

@{
    var contentItem = Model.ContentItem as ContentItem;
    var relatedIds = contentItem.Content.ArticlePart?.RelatedArticles?.ContentItemIds?.ToObject<string[]>();
}

@if (relatedIds != null && relatedIds.Length > 0)
{
    <aside class="related-articles">
        <h3>Related Articles</h3>
        <div class="row">
            @foreach (var id in relatedIds)
            {
                var related = await Orchard.GetContentItemByIdAsync(id);
                if (related != null)
                {
                    <div class="col-md-4">
                        @await Orchard.DisplayContentItemAsync(related, "Summary")
                    </div>
                }
            }
        </div>
    </aside>
}
```

## Example 9: Theme Setup Recipe

```json
{
    "steps": [
        {
            "name": "Themes",
            "Site": "MyCustomTheme",
            "Admin": "TheAdmin"
        },
        {
            "name": "Feature",
            "enable": [
                "MyCustomTheme",
                "OrchardCore.Resources",
                "OrchardCore.Menu",
                "OrchardCore.Media"
            ]
        }
    ]
}
```

## Example 10: Alternates and Shape Overrides

Orchard Core resolves shape templates using naming conventions. Override specific shapes by creating Razor views with matching names.

### Shape Naming Convention

| Shape Type | File Name | Description |
|---|---|---|
| `Content` | `Content.cshtml` | Default content item rendering |
| `Content-BlogPost` | `Content-BlogPost.cshtml` | Content type-specific override |
| `Content-BlogPost.Detail` | `Content-BlogPost.Detail.cshtml` | Content type + display type override |
| `Content-BlogPost.Summary` | `Content-BlogPost.Summary.cshtml` | Summary display type override |
| `Content__owned` | `Content-owned.cshtml` | Stereotype-based override |
| `Widget` | `Widget.cshtml` | Default widget wrapper |
| `Widget-HeroBanner` | `Widget-HeroBanner.cshtml` | Widget content type override |

### Custom Placement via Placement.json

Control where parts and fields render within the content shape zones:

```json
[
    {
        "place": "Content:0",
        "contentType": ["BlogPost"],
        "contentPart": ["TitlePart"]
    },
    {
        "place": "Content:1",
        "contentType": ["BlogPost"],
        "contentPart": ["HtmlBodyPart"]
    },
    {
        "place": "Content:2#secondary",
        "contentType": ["BlogPost"],
        "contentPart": ["BlogPost"],
        "differentiator": "Tags"
    },
    {
        "place": "-",
        "contentType": ["BlogPost"],
        "contentPart": ["CommonPart"]
    }
]
```

- `Content:0` places the shape in the `Content` zone at position `0`.
- `Content:2#secondary` places the shape in position `2` within the `secondary` tab group.
- `"-"` hides the shape entirely.
