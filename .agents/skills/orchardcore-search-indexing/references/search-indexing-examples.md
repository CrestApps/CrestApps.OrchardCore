# Search & Indexing Examples

## Example 1: Lucene Search Index Recipe

```json
{
  "steps": [
    {
      "name": "lucene-index",
      "Indices": [
        {
          "Search": {
            "AnalyzerName": "standardanalyzer",
            "IndexLatest": false,
            "IndexedContentTypes": [
              "Article",
              "BlogPost",
              "Page"
            ],
            "Culture": "",
            "StoreSourceData": false
          }
        }
      ]
    }
  ]
}
```

## Example 2: Search Query Recipe

```json
{
  "steps": [
    {
      "name": "Queries",
      "Queries": [
        {
          "Source": "Lucene",
          "Name": "RecentArticles",
          "Index": "Search",
          "Template": "{\"query\":{\"bool\":{\"filter\":[{\"term\":{\"Content.ContentItem.ContentType\":\"Article\"}}]}},\"sort\":{\"Content.ContentItem.PublishedUtc\":{\"order\":\"desc\"}},\"size\":10}",
          "ReturnContentItems": true,
          "Schema": "[]"
        },
        {
          "Source": "Lucene",
          "Name": "SearchByKeyword",
          "Index": "Search",
          "Template": "{\"query\":{\"multi_match\":{\"query\":\"{{term}}\",\"fields\":[\"Content.ContentItem.FullText\"]}},\"size\":20}",
          "ReturnContentItems": true,
          "Schema": "[{\"name\":\"term\",\"type\":\"String\"}]"
        }
      ]
    }
  ]
}
```

## Example 3: Custom Index Handler

```csharp
using OrchardCore.Indexing;

public sealed class ProductPartIndexHandler : ContentPartIndexHandler<ProductPart>
{
    public override Task BuildIndexAsync(
        ProductPart part,
        BuildPartIndexContext context)
    {
        context.DocumentIndex.Set(
            $"{nameof(ProductPart)}.{nameof(ProductPart.ProductName)}",
            part.ProductName,
            DocumentIndexOptions.Store | DocumentIndexOptions.Analyze);

        context.DocumentIndex.Set(
            $"{nameof(ProductPart)}.{nameof(ProductPart.Price)}",
            part.Price,
            DocumentIndexOptions.Store);

        context.DocumentIndex.Set(
            $"{nameof(ProductPart)}.{nameof(ProductPart.SKU)}",
            part.SKU,
            DocumentIndexOptions.Store | DocumentIndexOptions.Analyze);

        return Task.CompletedTask;
    }
}
```

## Example 4: Using Search in Liquid

```liquid
{% assign results = Queries.RecentArticles | query %}
<div class="article-list">
    {% for item in results %}
        <article>
            <h2><a href="{{ item | display_url }}">{{ item.DisplayText }}</a></h2>
            <time datetime="{{ item.PublishedUtc | date: '%Y-%m-%d' }}">
                {{ item.PublishedUtc | date: "%B %d, %Y" }}
            </time>
        </article>
    {% endfor %}
</div>
```
