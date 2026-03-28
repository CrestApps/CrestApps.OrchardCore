# Orchard Core SEO Examples

## Example 1: Full SEO Setup for a Blog Content Type

A complete migration that configures a BlogPost content type with title, autoroute, body, and SEO meta support:

```csharp
using OrchardCore.Autoroute.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

public sealed class BlogPostSeoMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public BlogPostSeoMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("BlogPost", type => type
            .DisplayedAs("Blog Post")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .WithPart("TitlePart", part => part
                .WithPosition("0")
            )
            .WithPart("AutoroutePart", part => part
                .WithPosition("1")
                .WithSettings(new AutoroutePartSettings
                {
                    AllowCustomPath = true,
                    AllowUpdatePath = true,
                    Pattern = "blog/{{ Model.ContentItem | display_text | slugify }}",
                    ShowHomepageOption = false
                })
            )
            .WithPart("HtmlBodyPart", part => part
                .WithPosition("2")
                .WithEditor("Wysiwyg")
            )
            .WithPart("SeoMetaPart", part => part
                .WithPosition("3")
            )
        );

        return 1;
    }
}
```

## Example 2: Product Page with JSON-LD Structured Data

A recipe that creates a product page with full SEO metadata and Product schema:

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentType": "Product",
          "ContentItemId": "product-wireless-headphones-001",
          "DisplayText": "Wireless Headphones Pro",
          "Latest": true,
          "Published": true,
          "TitlePart": {
            "Title": "Wireless Headphones Pro"
          },
          "AutoroutePart": {
            "Path": "products/wireless-headphones-pro"
          },
          "SeoMetaPart": {
            "PageTitle": "Wireless Headphones Pro - Premium Audio Quality",
            "MetaDescription": "Experience premium sound quality with Wireless Headphones Pro. Noise cancellation, 30-hour battery, and comfortable design.",
            "MetaKeywords": "wireless headphones, noise cancellation, bluetooth headphones",
            "Canonical": "",
            "MetaRobots": "index, follow",
            "OpenGraphType": "product",
            "OpenGraphTitle": "Wireless Headphones Pro",
            "OpenGraphDescription": "Premium wireless headphones with active noise cancellation and 30-hour battery life.",
            "OpenGraphImage": {
              "Paths": ["/media/products/headphones-pro-og.jpg"]
            },
            "TwitterCard": "summary_large_image",
            "TwitterTitle": "Wireless Headphones Pro",
            "TwitterDescription": "Premium wireless headphones with active noise cancellation.",
            "TwitterImage": {
              "Paths": ["/media/products/headphones-pro-twitter.jpg"]
            },
            "TwitterCreator": "@audiostore",
            "TwitterSite": "@audiostore",
            "GoogleSchema": "{\"@context\":\"https://schema.org\",\"@type\":\"Product\",\"name\":\"Wireless Headphones Pro\",\"description\":\"Premium wireless headphones with active noise cancellation and 30-hour battery life.\",\"image\":\"https://example.com/media/products/headphones-pro.jpg\",\"brand\":{\"@type\":\"Brand\",\"name\":\"AudioStore\"},\"offers\":{\"@type\":\"Offer\",\"price\":\"149.99\",\"priceCurrency\":\"USD\",\"availability\":\"https://schema.org/InStock\"}}"
          }
        }
      ]
    }
  ]
}
```

## Example 3: Sitemap with Multiple Content Types

A recipe that creates a sitemap covering several content types with different priorities:

```json
{
  "steps": [
    {
      "name": "Sitemaps",
      "Sitemaps": [
        {
          "SitemapId": "main-sitemap-4a7b",
          "Name": "Main Sitemap",
          "Path": "main-sitemap.xml",
          "Enabled": true,
          "SitemapSources": [
            {
              "Type": "SitemapSource",
              "ContentTypes": [
                {
                  "ContentTypeName": "Page",
                  "ChangeFrequency": "weekly",
                  "Priority": 8
                },
                {
                  "ContentTypeName": "BlogPost",
                  "ChangeFrequency": "daily",
                  "Priority": 6
                },
                {
                  "ContentTypeName": "Product",
                  "ChangeFrequency": "weekly",
                  "Priority": 7
                }
              ]
            }
          ]
        }
      ]
    }
  ]
}
```

## Example 4: Sitemap Index Aggregating Multiple Sitemaps

A sitemap index that combines content and product sitemaps under a single entry point:

```json
{
  "steps": [
    {
      "name": "Sitemaps",
      "Sitemaps": [
        {
          "SitemapId": "content-sitemap-8c3d",
          "Name": "Content Sitemap",
          "Path": "content-sitemap.xml",
          "Enabled": true,
          "SitemapSources": [
            {
              "Type": "SitemapSource",
              "ContentTypes": [
                {
                  "ContentTypeName": "Page",
                  "ChangeFrequency": "weekly",
                  "Priority": 8
                },
                {
                  "ContentTypeName": "BlogPost",
                  "ChangeFrequency": "daily",
                  "Priority": 6
                }
              ]
            }
          ]
        },
        {
          "SitemapId": "product-sitemap-9f1a",
          "Name": "Product Sitemap",
          "Path": "product-sitemap.xml",
          "Enabled": true,
          "SitemapSources": [
            {
              "Type": "SitemapSource",
              "ContentTypes": [
                {
                  "ContentTypeName": "Product",
                  "ChangeFrequency": "weekly",
                  "Priority": 7
                }
              ]
            }
          ]
        }
      ]
    },
    {
      "name": "SitemapIndexes",
      "SitemapIndexes": [
        {
          "SitemapIndexId": "sitemap-index-ab12",
          "Name": "Sitemap Index",
          "Path": "sitemap.xml",
          "Enabled": true,
          "ContainedSitemapIds": [
            "content-sitemap-8c3d",
            "product-sitemap-9f1a"
          ]
        }
      ]
    }
  ]
}
```

## Example 5: Dynamic Robots.txt Middleware with Tenant Support

A middleware that generates robots.txt content and references the sitemap URL dynamically:

```csharp
using Microsoft.AspNetCore.Http;
using OrchardCore.Settings;

public sealed class RobotsTxtMiddleware
{
    private readonly RequestDelegate _next;

    public RobotsTxtMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/robots.txt"))
        {
            await _next(context);
            return;
        }

        var siteService = context.RequestServices.GetRequiredService<ISiteService>();
        var siteSettings = await siteService.GetSiteSettingsAsync();
        var baseUrl = siteSettings.BaseUrl.TrimEnd('/');

        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync(
            $"""
            User-agent: *
            Allow: /
            Disallow: /admin/
            Disallow: /api/

            Sitemap: {baseUrl}/sitemap.xml
            """);
    }
}
```

Register it in Startup:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Modules;

public sealed class Startup : StartupBase
{
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        app.UseMiddleware<RobotsTxtMiddleware>();
    }
}
```

## Example 6: Organization JSON-LD Structured Data

A homepage content item with Organization schema for brand recognition in search results:

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentType": "Page",
          "ContentItemId": "homepage-001",
          "DisplayText": "Home",
          "Latest": true,
          "Published": true,
          "TitlePart": {
            "Title": "Home"
          },
          "AutoroutePart": {
            "Path": "home"
          },
          "SeoMetaPart": {
            "PageTitle": "AudioStore - Premium Audio Equipment",
            "MetaDescription": "Discover premium audio equipment at AudioStore. Shop headphones, speakers, and accessories with free shipping.",
            "OpenGraphType": "website",
            "OpenGraphTitle": "AudioStore - Premium Audio Equipment",
            "OpenGraphDescription": "Discover premium audio equipment with free shipping on all orders.",
            "OpenGraphImage": {
              "Paths": ["/media/site/og-home.jpg"]
            },
            "TwitterCard": "summary_large_image",
            "GoogleSchema": "{\"@context\":\"https://schema.org\",\"@type\":\"Organization\",\"name\":\"AudioStore\",\"url\":\"https://example.com\",\"logo\":\"https://example.com/media/site/logo.png\",\"sameAs\":[\"https://twitter.com/audiostore\",\"https://facebook.com/audiostore\"],\"contactPoint\":{\"@type\":\"ContactPoint\",\"telephone\":\"+1-800-555-0199\",\"contactType\":\"customer service\"}}"
          }
        }
      ]
    }
  ]
}
```

## Example 7: Article with BreadcrumbList Structured Data

A blog post with combined Article and BreadcrumbList JSON-LD for enhanced search result display:

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentType": "BlogPost",
          "ContentItemId": "blogpost-seo-guide-001",
          "DisplayText": "Complete Guide to SEO in Orchard Core",
          "Latest": true,
          "Published": true,
          "TitlePart": {
            "Title": "Complete Guide to SEO in Orchard Core"
          },
          "AutoroutePart": {
            "Path": "blog/complete-guide-to-seo-in-orchard-core"
          },
          "SeoMetaPart": {
            "PageTitle": "Complete Guide to SEO in Orchard Core | AudioStore Blog",
            "MetaDescription": "Learn how to configure SEO features in Orchard Core including meta tags, sitemaps, structured data, and social sharing.",
            "MetaKeywords": "orchard core seo, meta tags, sitemaps, structured data",
            "MetaRobots": "index, follow",
            "OpenGraphType": "article",
            "OpenGraphTitle": "Complete Guide to SEO in Orchard Core",
            "OpenGraphDescription": "Learn how to configure SEO features in Orchard Core.",
            "TwitterCard": "summary_large_image",
            "GoogleSchema": "[{\"@context\":\"https://schema.org\",\"@type\":\"Article\",\"headline\":\"Complete Guide to SEO in Orchard Core\",\"author\":{\"@type\":\"Person\",\"name\":\"Jane Smith\"},\"datePublished\":\"2025-01-15\",\"dateModified\":\"2025-01-20\"},{\"@context\":\"https://schema.org\",\"@type\":\"BreadcrumbList\",\"itemListElement\":[{\"@type\":\"ListItem\",\"position\":1,\"name\":\"Home\",\"item\":\"https://example.com\"},{\"@type\":\"ListItem\",\"position\":2,\"name\":\"Blog\",\"item\":\"https://example.com/blog\"},{\"@type\":\"ListItem\",\"position\":3,\"name\":\"Complete Guide to SEO in Orchard Core\"}]}]",
            "CustomMetaTags": [
              {
                "Name": "article:author",
                "Content": "Jane Smith"
              },
              {
                "Name": "article:published_time",
                "Content": "2025-01-15T08:00:00Z"
              }
            ]
          }
        }
      ]
    }
  ]
}
```

## Example 8: Enabling All SEO-Related Features via Recipe

A comprehensive recipe step that enables all features needed for full SEO support:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Seo",
        "OrchardCore.Sitemaps",
        "OrchardCore.Autoroute",
        "OrchardCore.Title",
        "OrchardCore.Media"
      ],
      "disable": []
    }
  ]
}
```
