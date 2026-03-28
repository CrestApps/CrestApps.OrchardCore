# Orchard Core Content Parts Examples

## Example 1: Blog Content Type with Common Parts

```csharp
public sealed class BlogMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public BlogMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        // Create the Blog container type
        await _contentDefinitionManager.AlterTypeDefinitionAsync("Blog", type => type
            .DisplayedAs("Blog")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .WithPart("TitlePart", part => part
                .WithPosition("0")
                .WithSettings(new TitlePartSettings
                {
                    Options = TitlePartOptions.EditableRequired,
                    RenderTitle = true
                })
            )
            .WithPart("AutoroutePart", part => part
                .WithPosition("1")
                .WithSettings(new AutoroutePartSettings
                {
                    AllowCustomPath = true,
                    Pattern = "{{ ContentItem.DisplayText | slugify }}",
                    ShowHomepageOption = true,
                    AllowUpdatePath = true
                })
            )
            .WithPart("ListPart", part => part
                .WithPosition("2")
                .WithSettings(new ListPartSettings
                {
                    PageSize = 10,
                    ContainedContentTypes = new[] { "BlogPost" }
                })
            )
        );

        // Create the BlogPost content type
        await _contentDefinitionManager.AlterTypeDefinitionAsync("BlogPost", type => type
            .DisplayedAs("Blog Post")
            .Creatable()
            .Draftable()
            .Versionable()
            .WithPart("TitlePart", part => part
                .WithPosition("0")
                .WithSettings(new TitlePartSettings
                {
                    Options = TitlePartOptions.EditableRequired
                })
            )
            .WithPart("AutoroutePart", part => part
                .WithPosition("1")
                .WithSettings(new AutoroutePartSettings
                {
                    AllowCustomPath = true,
                    Pattern = "{{ ContentItem.DisplayText | slugify }}",
                    AllowUpdatePath = true
                })
            )
            .WithPart("HtmlBodyPart", part => part
                .WithPosition("2")
                .WithEditor("Wysiwyg")
            )
            .WithPart("SeoMetaPart", part => part
                .WithPosition("10")
            )
        );

        return 1;
    }
}
```

## Example 2: Landing Page with FlowPart and BagPart

```csharp
public sealed class LandingPageMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public LandingPageMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("LandingPage", type => type
            .DisplayedAs("Landing Page")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .WithPart("TitlePart", part => part
                .WithPosition("0")
                .WithSettings(new TitlePartSettings
                {
                    Options = TitlePartOptions.EditableRequired
                })
            )
            .WithPart("AutoroutePart", part => part
                .WithPosition("1")
                .WithSettings(new AutoroutePartSettings
                {
                    AllowCustomPath = true,
                    Pattern = "{{ ContentItem.DisplayText | slugify }}"
                })
            )
            .WithPart("FlowPart", part => part
                .WithPosition("2")
                .WithSettings(new FlowPartSettings
                {
                    ContainedContentTypes = new[] { "HtmlWidget", "ImageWidget", "BlockQuote", "RawHtml" }
                })
            )
            .WithPart("BagPart", "Testimonials", part => part
                .WithDisplayName("Testimonials")
                .WithPosition("3")
                .WithSettings(new BagPartSettings
                {
                    ContainedContentTypes = new[] { "Testimonial" }
                })
            )
            .WithPart("SeoMetaPart", part => part
                .WithPosition("10")
            )
        );

        return 1;
    }
}
```

## Example 3: Content Types via Recipe

```json
{
    "steps": [
        {
            "name": "ContentDefinition",
            "ContentTypes": [
                {
                    "Name": "Article",
                    "DisplayName": "Article",
                    "Settings": {
                        "ContentTypeSettings": {
                            "Creatable": true,
                            "Listable": true,
                            "Draftable": true,
                            "Versionable": true,
                            "Securable": true
                        }
                    },
                    "ContentTypePartDefinitionRecords": [
                        {
                            "PartName": "TitlePart",
                            "Name": "TitlePart",
                            "Settings": {
                                "ContentTypePartSettings": {
                                    "Position": "0"
                                },
                                "TitlePartSettings": {
                                    "Options": "EditableRequired",
                                    "RenderTitle": true
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
                                    "Pattern": "{{ ContentItem.DisplayText | slugify }}",
                                    "ShowHomepageOption": false,
                                    "AllowUpdatePath": true
                                }
                            }
                        },
                        {
                            "PartName": "HtmlBodyPart",
                            "Name": "HtmlBodyPart",
                            "Settings": {
                                "ContentTypePartSettings": {
                                    "Position": "2",
                                    "Editor": "Wysiwyg"
                                },
                                "HtmlBodyPartSettings": {
                                    "SanitizeHtml": true
                                }
                            }
                        },
                        {
                            "PartName": "LocalizationPart",
                            "Name": "LocalizationPart",
                            "Settings": {
                                "ContentTypePartSettings": {
                                    "Position": "5"
                                }
                            }
                        },
                        {
                            "PartName": "SeoMetaPart",
                            "Name": "SeoMetaPart",
                            "Settings": {
                                "ContentTypePartSettings": {
                                    "Position": "10"
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

## Example 4: Taxonomy with TaxonomyPart

```csharp
public sealed class TaxonomyMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public TaxonomyMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        // Create the term content type
        await _contentDefinitionManager.AlterTypeDefinitionAsync("Category", type => type
            .DisplayedAs("Category")
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart("HtmlBodyPart", part => part
                .WithDisplayName("Description")
                .WithPosition("1")
                .WithEditor("Wysiwyg")
            )
        );

        // Create the taxonomy content type
        await _contentDefinitionManager.AlterTypeDefinitionAsync("Categories", type => type
            .DisplayedAs("Categories")
            .Creatable()
            .Listable()
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart("AliasPart", part => part
                .WithPosition("1")
                .WithSettings(new AliasPartSettings
                {
                    Pattern = "{{ ContentItem.DisplayText | slugify }}"
                })
            )
            .WithPart("TaxonomyPart", part => part
                .WithPosition("2")
                .WithSettings(new TaxonomyPartSettings
                {
                    TermContentType = "Category"
                })
            )
        );

        return 1;
    }
}
```

## Example 5: Content Type with Scheduled Publishing

```csharp
public sealed class NewsArticleMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public NewsArticleMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("NewsArticle", type => type
            .DisplayedAs("News Article")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .WithPart("TitlePart", part => part
                .WithPosition("0")
                .WithSettings(new TitlePartSettings
                {
                    Options = TitlePartOptions.EditableRequired
                })
            )
            .WithPart("AutoroutePart", part => part
                .WithPosition("1")
                .WithSettings(new AutoroutePartSettings
                {
                    AllowCustomPath = true,
                    Pattern = "news/{{ ContentItem.DisplayText | slugify }}"
                })
            )
            .WithPart("MarkdownBodyPart", part => part
                .WithPosition("2")
                .WithSettings(new MarkdownBodyPartSettings
                {
                    SanitizeHtml = true
                })
            )
            .WithPart("PublishLaterPart", part => part
                .WithPosition("5")
            )
            .WithPart("PreviewPart", part => part
                .WithPosition("6")
                .WithSettings(new PreviewPartSettings
                {
                    Pattern = "{{ ContentItem | display_url }}"
                })
            )
            .WithPart("LocalizationPart", part => part
                .WithPosition("7")
            )
            .WithPart("SeoMetaPart", part => part
                .WithPosition("10")
            )
        );

        return 1;
    }
}
```
