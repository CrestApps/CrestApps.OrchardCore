# Orchard Core Shapes Examples

## Example 1: Blog Theme Shape Overrides

Overriding the Content shape for BlogPost content type with Liquid templates.

### Views/Content__BlogPost.liquid

```liquid
<article class="blog-post">
    <header class="post-header">
        <h1>{{ Model.ContentItem.DisplayText }}</h1>
        <div class="post-meta">
            <time datetime="{{ Model.ContentItem.PublishedUtc | date: '%Y-%m-%d' }}">
                {{ Model.ContentItem.PublishedUtc | date: "%B %d, %Y" }}
            </time>
            <span class="author">by {{ Model.ContentItem.Author }}</span>
        </div>
    </header>

    {% if Model.Content %}
        <div class="post-body">
            {{ Model.Content | shape_render }}
        </div>
    {% endif %}

    {% if Model.Footer %}
        <footer class="post-footer">
            {{ Model.Footer | shape_render }}
        </footer>
    {% endif %}
</article>
```

### Views/Content__BlogPost__Summary.liquid

```liquid
<article class="blog-summary">
    <h3>
        <a href="{{ Model.ContentItem | display_url }}">
            {{ Model.ContentItem.DisplayText }}
        </a>
    </h3>
    <div class="post-meta">
        <time datetime="{{ Model.ContentItem.PublishedUtc | date: '%Y-%m-%d' }}">
            {{ Model.ContentItem.PublishedUtc | date: "%B %d, %Y" }}
        </time>
    </div>
    {% if Model.Content %}
        {{ Model.Content | shape_render }}
    {% endif %}
</article>
```

## Example 2: Custom Widget Wrapper with Liquid

### Views/Widget.liquid

```liquid
<div class="widget widget-{{ Model.ContentItem.ContentType | downcase }}">
    {% if Model.Header %}
        <div class="widget-header">
            {{ Model.Header | shape_render }}
        </div>
    {% endif %}
    <div class="widget-body">
        {{ Model.Content | shape_render }}
    </div>
</div>
```

### Views/Widget__HtmlWidget.liquid

```liquid
<div class="html-widget">
    {{ Model.Content | shape_render }}
</div>
```

## Example 3: Custom Shape Table Provider for Product Alternates

```csharp
using OrchardCore.DisplayManagement.Descriptors;

public sealed class ProductShapeTableProvider : IShapeTableProvider
{
    public void Discover(ShapeTableBuilder builder)
    {
        builder.Describe("Content")
            .OnDisplaying(context =>
            {
                var contentType = context.Shape.GetProperty<string>("ContentType");

                // Add category-specific alternates for Product content type
                if (contentType == "Product")
                {
                    var category = context.Shape.GetProperty<string>("Category");
                    if (!string.IsNullOrEmpty(category))
                    {
                        context.Shape.Metadata.Alternates.Add($"Content__Product__{category}");
                    }
                }
            });

        // Add a wrapper to all Widget shapes
        builder.Describe("Widget")
            .OnDisplaying(context =>
            {
                context.Shape.Metadata.Wrappers.Add("Widget_CardWrapper");
            });
    }
}
```

### Views/Widget_CardWrapper.liquid

```liquid
<div class="card">
    <div class="card-body">
        {{ Model | shape_render }}
    </div>
</div>
```

### Registration

```csharp
using OrchardCore.DisplayManagement.Descriptors;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IShapeTableProvider, ProductShapeTableProvider>();
    }
}
```

## Example 4: Creating Shapes from Code in a Controller

```csharp
using Microsoft.AspNetCore.Mvc;
using OrchardCore.DisplayManagement;

public sealed class DashboardController : Controller
{
    private readonly IShapeFactory _shapeFactory;
    private readonly IDisplayHelper _displayHelper;

    public DashboardController(
        IShapeFactory shapeFactory,
        IDisplayHelper displayHelper)
    {
        _shapeFactory = shapeFactory;
        _displayHelper = displayHelper;
    }

    public async Task<IActionResult> Index()
    {
        // Create shapes programmatically
        var statsShape = await _shapeFactory.CreateAsync("Dashboard_Stats", Arguments.From(new
        {
            TotalPosts = 42,
            TotalComments = 128,
            TotalUsers = 15
        }));

        var recentPostsShape = await _shapeFactory.CreateAsync("Dashboard_RecentPosts", Arguments.From(new
        {
            Count = 5
        }));

        var model = await _shapeFactory.CreateAsync("Dashboard", Arguments.From(new
        {
            Stats = statsShape,
            RecentPosts = recentPostsShape
        }));

        return View(model);
    }
}
```

### Views/Dashboard.liquid

```liquid
<div class="dashboard">
    <div class="row">
        <div class="col-md-4">
            {{ Model.Stats | shape_render }}
        </div>
        <div class="col-md-8">
            {{ Model.RecentPosts | shape_render }}
        </div>
    </div>
</div>
```

### Views/Dashboard_Stats.liquid

```liquid
<div class="stats-card">
    <h3>Site Statistics</h3>
    <ul>
        <li>Posts: {{ Model.TotalPosts }}</li>
        <li>Comments: {{ Model.TotalComments }}</li>
        <li>Users: {{ Model.TotalUsers }}</li>
    </ul>
</div>
```

## Example 5: Field Shape Templates

### Views/TextField.liquid

```liquid
{% if Model.Field.Text and Model.Field.Text != "" %}
    <p class="text-field text-field-{{ Model.PartFieldDefinition.Name | downcase }}">
        {{ Model.Field.Text }}
    </p>
{% endif %}
```

### Views/MediaField.liquid

```liquid
{% if Model.Field.Paths and Model.Field.Paths.size > 0 %}
    {% for path in Model.Field.Paths %}
        <img src="{{ path | asset_url }}"
             alt="{{ Model.PartFieldDefinition.DisplayName }}"
             class="img-fluid" />
    {% endfor %}
{% endif %}
```

### Views/ContentPickerField.liquid

```liquid
{% if Model.Field.ContentItemIds and Model.Field.ContentItemIds.size > 0 %}
    <ul class="content-picker-list">
        {% for id in Model.Field.ContentItemIds %}
            {% assign item = id | content_item %}
            {% if item %}
                <li>
                    <a href="{{ item | display_url }}">{{ item.DisplayText }}</a>
                </li>
            {% endif %}
        {% endfor %}
    </ul>
{% endif %}
```
