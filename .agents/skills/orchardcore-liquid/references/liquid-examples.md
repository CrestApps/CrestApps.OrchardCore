# Liquid Template Examples

## Example 1: Blog Post Detail Template

### Views/Content-BlogPost.liquid

```liquid
<article class="blog-post">
    <header class="post-header">
        <h1>{{ Model.ContentItem.DisplayText }}</h1>
        <div class="post-meta">
            <time datetime="{{ Model.ContentItem.PublishedUtc | date: '%Y-%m-%d' }}">
                {{ Model.ContentItem.PublishedUtc | date: "%B %d, %Y" }}
            </time>
            <span class="author">{{ Model.ContentItem.Author }}</span>
        </div>
    </header>

    {% unless Model.ContentItem.Content.BlogPost.Image.Paths == empty %}
        <div class="featured-image">
            <img src="{{ Model.ContentItem.Content.BlogPost.Image.Paths[0] | asset_url | append: '?width=1200' }}"
                 alt="{{ Model.ContentItem.Content.BlogPost.Image.MediaTexts[0] }}" />
        </div>
    {% endunless %}

    <div class="post-body">
        {{ Model.Content.HtmlBodyPart | shape_render }}
    </div>

    {% if Model.ContentItem.Content.BlogPost.Categories.TermContentItemIds.size > 0 %}
        <div class="post-categories">
            <strong>{% t "Categories" %}:</strong>
            {% for termId in Model.ContentItem.Content.BlogPost.Categories.TermContentItemIds %}
                {% assign term = termId | content_item %}
                <a href="/category/{{ term.DisplayText | slugify }}">{{ term.DisplayText }}</a>
            {% endfor %}
        </div>
    {% endif %}

    {% if User | has_permission: "EditContent" %}
        <div class="post-actions">
            <a href="/Admin/Contents/ContentItems/{{ Model.ContentItem.ContentItemId }}/Edit"
               class="btn btn-sm btn-outline-primary">
                {% t "Edit" %}
            </a>
        </div>
    {% endif %}
</article>
```

## Example 2: Blog Post Summary Template

### Views/Content-BlogPost.Summary.liquid

```liquid
<article class="blog-post-summary">
    <div class="row">
        {% unless Model.ContentItem.Content.BlogPost.Image.Paths == empty %}
            <div class="col-md-4">
                <img src="{{ Model.ContentItem.Content.BlogPost.Image.Paths[0] | asset_url | append: '?width=400&height=300&rmode=crop' }}"
                     alt="{{ Model.ContentItem.DisplayText }}"
                     class="img-fluid" />
            </div>
        {% endunless %}
        <div class="col">
            <h2>
                <a href="{{ Model.ContentItem.Content.AutoroutePart.Path }}">
                    {{ Model.ContentItem.DisplayText }}
                </a>
            </h2>
            <time datetime="{{ Model.ContentItem.PublishedUtc | date: '%Y-%m-%d' }}">
                {{ Model.ContentItem.PublishedUtc | date: "%B %d, %Y" }}
            </time>
            {% if Model.ContentItem.Content.BlogPost.Subtitle.Text %}
                <p class="lead">{{ Model.ContentItem.Content.BlogPost.Subtitle.Text }}</p>
            {% endif %}
            <a href="{{ Model.ContentItem.Content.AutoroutePart.Path }}" class="btn btn-link">
                {% t "Read more" %} &rarr;
            </a>
        </div>
    </div>
</article>
```

## Example 3: Custom Liquid Filter

```csharp
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.DependencyInjection;

// Filter implementation
public sealed class ReadingTimeFilter : ILiquidFilter
{
    public ValueTask<FluidValue> ProcessAsync(
        FluidValue input,
        FilterArguments arguments,
        LiquidTemplateContext context)
    {
        var html = input.ToStringValue();
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", "");
        var wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var minutes = Math.Max(1, wordCount / 200);
        return new ValueTask<FluidValue>(
            new StringValue($"{minutes} min read"));
    }
}

// Registration
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddLiquidFilter<ReadingTimeFilter>("reading_time");
    }
}
```

Usage in template:

```liquid
<span class="reading-time">
    {{ Model.ContentItem.Content.HtmlBodyPart.Html | reading_time }}
</span>
```
