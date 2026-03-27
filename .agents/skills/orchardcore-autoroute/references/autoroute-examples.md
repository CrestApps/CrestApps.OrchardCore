# AutoroutePart Examples

## Example 1: Article Content Type with SEO-Friendly URLs

### Migration

```csharp
public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public int Create()
    {
        _contentDefinitionManager.AlterTypeDefinition("Article", type => type
            .DisplayedAs("Article")
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
                    ShowHomepageOption = true,
                    Pattern = "articles/{{ ContentItem | display_text | slugify }}"
                })
            )
            .WithPart("HtmlBodyPart", part => part
                .WithPosition("2")
                .WithEditor("Wysiwyg")
            )
            .WithPart("Article", part => part
                .WithPosition("3")
            )
        );

        _contentDefinitionManager.AlterPartDefinition("Article", part => part
            .WithField("Summary", field => field
                .OfType("TextField")
                .WithDisplayName("Summary")
                .WithPosition("0")
                .WithEditor("TextArea")
            )
            .WithField("Image", field => field
                .OfType("MediaField")
                .WithDisplayName("Featured Image")
                .WithPosition("1")
            )
        );

        return 1;
    }
}
```

## Example 2: Blog with Hierarchical Container URLs

This example creates a Blog container that routes its contained BlogPost items under the blog's URL path. A Blog at `/tech-blog` with a post titled "Getting Started" produces `/tech-blog/getting-started`.

### Recipe

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "Blog",
          "DisplayName": "Blog",
          "Settings": {
            "ContentTypeSettings": {
              "Creatable": true,
              "Listable": true,
              "Draftable": true
            }
          },
          "ContentTypePartDefinitionRecords": [
            {
              "PartName": "TitlePart",
              "Name": "TitlePart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "0"
                }
              }
            },
            {
              "PartName": "AutoroutePart",
              "Name": "AutoroutePart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "1"
                },
                "AutoroutePartSettings": {
                  "AllowCustomPath": true,
                  "Pattern": "{{ ContentItem | display_text | slugify }}",
                  "AllowRouteContainedItems": true
                }
              }
            },
            {
              "PartName": "ListPart",
              "Name": "ListPart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "2"
                },
                "ListPartSettings": {
                  "ContainedContentTypes": [
                    "BlogPost"
                  ]
                }
              }
            }
          ]
        },
        {
          "Name": "BlogPost",
          "DisplayName": "Blog Post",
          "Settings": {
            "ContentTypeSettings": {
              "Draftable": true,
              "Versionable": true
            }
          },
          "ContentTypePartDefinitionRecords": [
            {
              "PartName": "TitlePart",
              "Name": "TitlePart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "0"
                }
              }
            },
            {
              "PartName": "AutoroutePart",
              "Name": "AutoroutePart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "1"
                },
                "AutoroutePartSettings": {
                  "AllowCustomPath": true,
                  "Pattern": "{{ ContentItem | display_text | slugify }}"
                }
              }
            },
            {
              "PartName": "HtmlBodyPart",
              "Name": "HtmlBodyPart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "2"
                }
              }
            }
          ]
        }
      ]
    }
  ]
}
```

## Example 3: Date-Prefixed URL Pattern for News Items

### Migration

```csharp
public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public int Create()
    {
        _contentDefinitionManager.AlterTypeDefinition("NewsItem", type => type
            .DisplayedAs("News Item")
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
                    Pattern = "news/{{ ContentItem.CreatedUtc | date: '%Y' }}/{{ ContentItem.CreatedUtc | date: '%m' }}/{{ ContentItem | display_text | slugify }}"
                })
            )
            .WithPart("HtmlBodyPart", part => part
                .WithPosition("2")
                .WithEditor("Wysiwyg")
            )
        );

        return 1;
    }
}
```

This generates URLs such as `news/2025/07/product-launch-announcement`.

## Example 4: Programmatic Route Lookup and Content Retrieval

```csharp
using OrchardCore.Autoroute.Services;
using OrchardCore.ContentManagement;

public sealed class AutorouteContentResolver
{
    private readonly IAutorouteEntries _autorouteEntries;
    private readonly IContentManager _contentManager;

    public AutorouteContentResolver(
        IAutorouteEntries autorouteEntries,
        IContentManager contentManager)
    {
        _autorouteEntries = autorouteEntries;
        _contentManager = contentManager;
    }

    public async Task<ContentItem> ResolveByPathAsync(string path)
    {
        (var found, var entry) = await _autorouteEntries.TryGetEntryByPathAsync(path);

        if (!found)
        {
            return null;
        }

        return await _contentManager.GetAsync(entry.ContentItemId);
    }
}
```

## Example 5: Landing Page with Home Page Option

### Recipe

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "LandingPage",
          "DisplayName": "Landing Page",
          "Settings": {
            "ContentTypeSettings": {
              "Creatable": true,
              "Listable": true,
              "Draftable": true,
              "Versionable": true
            }
          },
          "ContentTypePartDefinitionRecords": [
            {
              "PartName": "TitlePart",
              "Name": "TitlePart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "0"
                }
              }
            },
            {
              "PartName": "AutoroutePart",
              "Name": "AutoroutePart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "1"
                },
                "AutoroutePartSettings": {
                  "AllowCustomPath": true,
                  "ShowHomepageOption": true,
                  "Pattern": "{{ ContentItem | display_text | slugify }}"
                }
              }
            },
            {
              "PartName": "FlowPart",
              "Name": "FlowPart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "2"
                }
              }
            }
          ]
        }
      ]
    }
  ]
}
```

## Example 6: BagPart Container with Routable Contained Items

This example uses BagPart to hold FAQ items as contained content, each receiving its own URL under the parent FAQ page's path.

### Migration

```csharp
public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public int Create()
    {
        _contentDefinitionManager.AlterTypeDefinition("FaqPage", type => type
            .DisplayedAs("FAQ Page")
            .Creatable()
            .Listable()
            .Draftable()
            .WithPart("TitlePart", part => part
                .WithPosition("0")
            )
            .WithPart("AutoroutePart", part => part
                .WithPosition("1")
                .WithSettings(new AutoroutePartSettings
                {
                    AllowCustomPath = true,
                    Pattern = "{{ ContentItem | display_text | slugify }}",
                    AllowRouteContainedItems = true
                })
            )
            .WithPart("BagPart", part => part
                .WithPosition("2")
                .WithSettings(new BagPartSettings
                {
                    ContainedContentTypes = new[] { "FaqItem" }
                })
            )
        );

        _contentDefinitionManager.AlterTypeDefinition("FaqItem", type => type
            .DisplayedAs("FAQ Item")
            .WithPart("TitlePart", part => part
                .WithPosition("0")
            )
            .WithPart("AutoroutePart", part => part
                .WithPosition("1")
                .WithSettings(new AutoroutePartSettings
                {
                    Pattern = "{{ ContentItem | display_text | slugify }}"
                })
            )
            .WithPart("FaqItem", part => part
                .WithPosition("2")
            )
        );

        _contentDefinitionManager.AlterPartDefinition("FaqItem", part => part
            .WithField("Answer", field => field
                .OfType("HtmlField")
                .WithDisplayName("Answer")
                .WithPosition("0")
                .WithEditor("Wysiwyg")
            )
        );

        return 1;
    }
}
```

A FAQ page at `/frequently-asked-questions` with an FAQ item titled "How do I reset my password" generates the URL `/frequently-asked-questions/how-do-i-reset-my-password`.
