# Widget Examples

## Example 1: Hero Banner Widget

### Migration

```csharp
public int Create()
{
    _contentDefinitionManager.AlterTypeDefinition("HeroBanner", type => type
        .DisplayedAs("Hero Banner")
        .Stereotype("Widget")
        .WithPart("HeroBanner", part => part
            .WithPosition("0")
        )
    );

    _contentDefinitionManager.AlterPartDefinition("HeroBanner", part => part
        .WithField("Heading", field => field
            .OfType("TextField")
            .WithDisplayName("Heading")
            .WithPosition("0")
        )
        .WithField("Subheading", field => field
            .OfType("TextField")
            .WithDisplayName("Subheading")
            .WithPosition("1")
        )
        .WithField("BackgroundImage", field => field
            .OfType("MediaField")
            .WithDisplayName("Background Image")
            .WithPosition("2")
        )
        .WithField("CallToAction", field => field
            .OfType("LinkField")
            .WithDisplayName("Call to Action")
            .WithPosition("3")
        )
    );

    return 1;
}
```

### Liquid Template (Views/Widget-HeroBanner.liquid)

```liquid
<section class="hero-banner"
         style="background-image: url('{{ Model.ContentItem.Content.HeroBanner.BackgroundImage.Paths[0] | asset_url | append: '?width=1920&height=600&rmode=crop' }}')">
    <div class="container text-center">
        <h1>{{ Model.ContentItem.Content.HeroBanner.Heading.Text }}</h1>
        {% if Model.ContentItem.Content.HeroBanner.Subheading.Text %}
            <p class="lead">{{ Model.ContentItem.Content.HeroBanner.Subheading.Text }}</p>
        {% endif %}
        {% if Model.ContentItem.Content.HeroBanner.CallToAction.Url %}
            <a href="{{ Model.ContentItem.Content.HeroBanner.CallToAction.Url }}"
               class="btn btn-primary btn-lg">
                {{ Model.ContentItem.Content.HeroBanner.CallToAction.Text | default: "Learn More" }}
            </a>
        {% endif %}
    </div>
</section>
```

## Example 2: Layer and Widget Placement Recipe

```json
{
  "steps": [
    {
      "name": "Layers",
      "Layers": [
        {
          "Name": "Always",
          "LayerRule": {
            "Conditions": [
              {
                "Name": "BooleanCondition",
                "Value": true
              }
            ]
          },
          "Description": "Always visible"
        },
        {
          "Name": "Homepage",
          "LayerRule": {
            "Conditions": [
              {
                "Name": "UrlCondition",
                "Value": "^/$"
              }
            ]
          },
          "Description": "Homepage only"
        }
      ]
    }
  ]
}
```

### Widget Content Item

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "widget-hero-home",
          "ContentType": "HeroBanner",
          "DisplayText": "Welcome Banner",
          "Latest": true,
          "Published": true,
          "LayerMetadata": {
            "Layer": "Homepage",
            "Zone": "Header",
            "Position": 0
          },
          "HeroBanner": {
            "Heading": { "Text": "Welcome to Our Site" },
            "Subheading": { "Text": "Discover amazing content" },
            "CallToAction": {
              "Url": "/about",
              "Text": "Learn More"
            }
          }
        }
      ]
    }
  ]
}
```
