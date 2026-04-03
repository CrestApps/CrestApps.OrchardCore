# Orchard Core Content Fields Examples

## Example 1: Blog Post with Multiple Fields

Migration adding various fields to a BlogPost content type:

```csharp
public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("BlogPost", part => part
            .WithField("Subtitle", field => field
                .OfType("TextField")
                .WithDisplayName("Subtitle")
                .WithPosition("1")
                .WithSettings(new TextFieldSettings
                {
                    Hint = "An optional subtitle for the post",
                    Placeholder = "Enter subtitle..."
                })
            )
            .WithField("FeaturedImage", field => field
                .OfType("MediaField")
                .WithDisplayName("Featured Image")
                .WithPosition("2")
                .WithSettings(new MediaFieldSettings
                {
                    Required = false,
                    Multiple = false,
                    AllowMediaText = true,
                    AllowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" },
                    Hint = "Upload a featured image for this post"
                })
            )
            .WithField("PublishDate", field => field
                .OfType("DateTimeField")
                .WithDisplayName("Publish Date")
                .WithPosition("3")
                .WithSettings(new DateTimeFieldSettings
                {
                    Required = true,
                    Hint = "When should this post be published?"
                })
            )
            .WithField("IsFeatured", field => field
                .OfType("BooleanField")
                .WithDisplayName("Featured Post")
                .WithPosition("4")
                .WithSettings(new BooleanFieldSettings
                {
                    Label = "Feature this post on the homepage",
                    DefaultValue = false
                })
            )
            .WithField("RelatedPosts", field => field
                .OfType("ContentPickerField")
                .WithDisplayName("Related Posts")
                .WithPosition("5")
                .WithSettings(new ContentPickerFieldSettings
                {
                    Multiple = true,
                    DisplayedContentTypes = new[] { "BlogPost" },
                    Hint = "Select related blog posts"
                })
            )
        );

        return 1;
    }
}
```

## Example 2: Product Content Type with Numeric and Link Fields

```csharp
public sealed class ProductMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ProductMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("ProductPart", part => part
            .WithField("Price", field => field
                .OfType("NumericField")
                .WithDisplayName("Price")
                .WithPosition("0")
                .WithSettings(new NumericFieldSettings
                {
                    Required = true,
                    Minimum = 0,
                    Scale = 2,
                    Placeholder = "0.00",
                    Hint = "Product price in USD"
                })
            )
            .WithField("SKU", field => field
                .OfType("TextField")
                .WithDisplayName("SKU")
                .WithPosition("1")
                .WithSettings(new TextFieldSettings
                {
                    Required = true,
                    Placeholder = "PROD-001",
                    Hint = "Stock Keeping Unit"
                })
            )
            .WithField("Status", field => field
                .OfType("TextField")
                .WithDisplayName("Status")
                .WithEditor("PredefinedList")
                .WithPosition("2")
                .WithSettings(new TextFieldPredefinedListEditorSettings
                {
                    Editor = EditorOption.Dropdown,
                    DefaultValue = "active",
                    Options = new[]
                    {
                        new ListValueOption("Active", "active"),
                        new ListValueOption("Out of Stock", "out-of-stock"),
                        new ListValueOption("Discontinued", "discontinued")
                    }
                })
            )
            .WithField("ExternalLink", field => field
                .OfType("LinkField")
                .WithDisplayName("Product Page")
                .WithPosition("3")
                .WithSettings(new LinkFieldSettings
                {
                    LinkTextMode = LinkTextMode.Optional,
                    UrlPlaceholder = "https://example.com/product",
                    TextPlaceholder = "View product page",
                    DefaultTarget = "_blank",
                    Hint = "Link to the external product page"
                })
            )
            .WithField("Gallery", field => field
                .OfType("MediaField")
                .WithDisplayName("Product Gallery")
                .WithPosition("4")
                .WithSettings(new MediaFieldSettings
                {
                    Multiple = true,
                    AllowMediaText = true,
                    AllowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" },
                    Hint = "Upload product images"
                })
            )
        );

        return 1;
    }
}
```

## Example 3: Event Content Type with Date and Time Fields

```csharp
public sealed class EventMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public EventMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("EventPart", part => part
            .WithField("EventDate", field => field
                .OfType("DateField")
                .WithDisplayName("Event Date")
                .WithPosition("0")
                .WithSettings(new DateFieldSettings
                {
                    Required = true,
                    Hint = "When does the event take place?"
                })
            )
            .WithField("StartTime", field => field
                .OfType("TimeField")
                .WithDisplayName("Start Time")
                .WithPosition("1")
                .WithSettings(new TimeFieldSettings
                {
                    Required = true,
                    Step = "00:15:00",
                    Hint = "Event start time"
                })
            )
            .WithField("EndTime", field => field
                .OfType("TimeField")
                .WithDisplayName("End Time")
                .WithPosition("2")
                .WithSettings(new TimeFieldSettings
                {
                    Required = false,
                    Step = "00:15:00",
                    Hint = "Event end time"
                })
            )
            .WithField("VideoUrl", field => field
                .OfType("YoutubeField")
                .WithDisplayName("Event Video")
                .WithPosition("3")
                .WithSettings(new YoutubeFieldSettings
                {
                    Width = 640,
                    Height = 360,
                    Placeholder = "https://www.youtube.com/watch?v=...",
                    Hint = "Optional video recording of the event"
                })
            )
            .WithField("Organizer", field => field
                .OfType("UserPickerField")
                .WithDisplayName("Organizer")
                .WithPosition("4")
                .WithSettings(new UserPickerFieldSettings
                {
                    Required = true,
                    Multiple = false,
                    DisplayAllUsers = false,
                    DisplayedRoles = new[] { "EventOrganizer", "Administrator" },
                    Hint = "Select the event organizer"
                })
            )
            .WithField("Tags", field => field
                .OfType("MultiTextField")
                .WithDisplayName("Tags")
                .WithPosition("5")
                .WithSettings(new MultiTextFieldSettings
                {
                    Options = new[]
                    {
                        new MultiTextFieldValueOption { Name = "Conference", Value = "conference" },
                        new MultiTextFieldValueOption { Name = "Workshop", Value = "workshop" },
                        new MultiTextFieldValueOption { Name = "Webinar", Value = "webinar" },
                        new MultiTextFieldValueOption { Name = "Meetup", Value = "meetup" }
                    },
                    Hint = "Select event tags"
                })
            )
        );

        return 1;
    }
}
```

## Example 4: Adding Fields via Recipe

```json
{
    "steps": [
        {
            "name": "ContentDefinition",
            "ContentParts": [
                {
                    "Name": "ArticlePart",
                    "Settings": {},
                    "ContentPartFieldDefinitionRecords": [
                        {
                            "FieldName": "TextField",
                            "Name": "Subtitle",
                            "Settings": {
                                "ContentPartFieldSettings": {
                                    "DisplayName": "Subtitle",
                                    "Position": "0"
                                },
                                "TextFieldSettings": {
                                    "Required": false,
                                    "Hint": "An optional subtitle",
                                    "Placeholder": "Enter subtitle..."
                                }
                            }
                        },
                        {
                            "FieldName": "MediaField",
                            "Name": "HeroImage",
                            "Settings": {
                                "ContentPartFieldSettings": {
                                    "DisplayName": "Hero Image",
                                    "Position": "1"
                                },
                                "MediaFieldSettings": {
                                    "Required": true,
                                    "Multiple": false,
                                    "AllowMediaText": true,
                                    "Hint": "Upload a hero image"
                                }
                            }
                        },
                        {
                            "FieldName": "NumericField",
                            "Name": "ReadingTime",
                            "Settings": {
                                "ContentPartFieldSettings": {
                                    "DisplayName": "Reading Time (minutes)",
                                    "Position": "2"
                                },
                                "NumericFieldSettings": {
                                    "Required": false,
                                    "Minimum": 1,
                                    "Maximum": 120,
                                    "Scale": 0,
                                    "Hint": "Estimated reading time in minutes"
                                }
                            }
                        },
                        {
                            "FieldName": "ContentPickerField",
                            "Name": "Author",
                            "Settings": {
                                "ContentPartFieldSettings": {
                                    "DisplayName": "Author",
                                    "Position": "3"
                                },
                                "ContentPickerFieldSettings": {
                                    "Required": true,
                                    "Multiple": false,
                                    "DisplayedContentTypes": [ "Author" ],
                                    "Hint": "Select the article author"
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
