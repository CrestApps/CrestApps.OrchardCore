# Media Examples

## Example 1: Image Gallery Content Type with Media Fields

### Migration

```csharp
public int Create()
{
    _contentDefinitionManager.AlterTypeDefinition("ImageGallery", type => type
        .DisplayedAs("Image Gallery")
        .Creatable()
        .Listable()
        .Draftable()
        .WithPart("TitlePart", part => part
            .WithPosition("0")
        )
        .WithPart("ImageGallery", part => part
            .WithPosition("1")
        )
    );

    _contentDefinitionManager.AlterPartDefinition("ImageGallery", part => part
        .WithField("Images", field => field
            .OfType("MediaField")
            .WithDisplayName("Gallery Images")
            .WithSettings(new MediaFieldSettings
            {
                Multiple = true,
                AllowMediaText = true
            })
            .WithPosition("0")
        )
        .WithField("CoverImage", field => field
            .OfType("MediaField")
            .WithDisplayName("Cover Image")
            .WithSettings(new MediaFieldSettings
            {
                Multiple = false,
                AllowAnchors = true
            })
            .WithPosition("1")
        )
    );

    return 1;
}
```

## Example 2: Media Profiles Recipe

```json
{
  "steps": [
    {
      "name": "MediaProfiles",
      "MediaProfiles": {
        "Thumbnail": {
          "Hint": "Small thumbnail for listings",
          "Width": 150,
          "Height": 150,
          "Mode": "Crop",
          "Format": "webp",
          "Quality": 75
        },
        "Banner": {
          "Hint": "Full-width banner image",
          "Width": 1920,
          "Height": 600,
          "Mode": "Crop",
          "Format": "webp",
          "Quality": 85
        },
        "Avatar": {
          "Hint": "User avatar",
          "Width": 200,
          "Height": 200,
          "Mode": "Crop",
          "Format": "webp",
          "Quality": 80
        }
      }
    }
  ]
}
```

## Example 3: Displaying Media in Liquid

```liquid
{% for path in Model.ContentItem.Content.ImageGallery.Images.Paths %}
    <div class="gallery-item">
        <img src="{{ path | asset_url | append: '?profile=Thumbnail' }}"
             data-full="{{ path | asset_url }}"
             alt="{{ Model.ContentItem.Content.ImageGallery.Images.MediaTexts[forloop.index0] }}" />
    </div>
{% endfor %}
```
