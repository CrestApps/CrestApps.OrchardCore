# Orchard Core Content Queries Examples

## Example 1: Blog Post Query Service

A service that queries blog posts using ContentItemIndex:

```csharp
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;

public sealed class BlogPostQueryService
{
    private readonly ISession _session;

    public BlogPostQueryService(ISession session)
    {
        _session = session;
    }

    public async Task<IEnumerable<ContentItem>> GetRecentPostsAsync(int count = 10)
    {
        return await _session
            .Query<ContentItem, ContentItemIndex>(x =>
                x.ContentType == "BlogPost" && x.Published)
            .OrderByDescending(x => x.PublishedUtc)
            .Take(count)
            .ListAsync();
    }

    public async Task<IEnumerable<ContentItem>> SearchPostsAsync(string searchText)
    {
        return await _session
            .Query<ContentItem, ContentItemIndex>(x =>
                x.ContentType == "BlogPost" &&
                x.Published &&
                x.DisplayText.Contains(searchText))
            .ListAsync();
    }
}
```

## Example 2: Custom Product Index

Creating a custom YesSql index for a Product content type:

```csharp
using OrchardCore.ContentManagement;
using YesSql.Indexes;

public sealed class ProductPartIndex : MapIndex
{
    public string ContentItemId { get; set; }
    public string Sku { get; set; }
    public decimal Price { get; set; }
    public string Category { get; set; }
    public bool Published { get; set; }
}

public sealed class ProductPartIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<ProductPartIndex>()
            .When(contentItem => contentItem.Has<ProductPart>())
            .Map(contentItem =>
            {
                var part = contentItem.As<ProductPart>();
                return new ProductPartIndex
                {
                    ContentItemId = contentItem.ContentItemId,
                    Sku = part.Sku,
                    Price = part.Price,
                    Category = part.Category,
                    Published = contentItem.Published
                };
            });
    }
}
```

### Migration for the Product Index

```csharp
using OrchardCore.Data.Migration;
using YesSql.Sql;

public sealed class Migrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ProductPartIndex>(table => table
            .Column<string>(nameof(ProductPartIndex.ContentItemId), col => col.WithLength(26))
            .Column<string>(nameof(ProductPartIndex.Sku), col => col.WithLength(100))
            .Column<decimal>(nameof(ProductPartIndex.Price))
            .Column<string>(nameof(ProductPartIndex.Category), col => col.WithLength(256))
            .Column<bool>(nameof(ProductPartIndex.Published))
        );

        await SchemaBuilder.AlterIndexTableAsync<ProductPartIndex>(table => table
            .CreateIndex("IDX_ProductPartIndex_Sku",
                nameof(ProductPartIndex.Sku),
                nameof(ProductPartIndex.Published))
        );

        return 1;
    }
}
```

### Querying with the Custom Index

```csharp
public sealed class ProductQueryService
{
    private readonly ISession _session;

    public ProductQueryService(ISession session)
    {
        _session = session;
    }

    public async Task<IEnumerable<ContentItem>> GetBySkuAsync(string sku)
    {
        return await _session
            .Query<ContentItem, ProductPartIndex>(x => x.Sku == sku && x.Published)
            .ListAsync();
    }

    public async Task<IEnumerable<ContentItem>> GetByCategoryAsync(string category, int page, int pageSize)
    {
        return await _session
            .Query<ContentItem, ProductPartIndex>(x => x.Category == category && x.Published)
            .OrderBy(x => x.Price)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ListAsync();
    }

    public async Task<IEnumerable<ContentItem>> GetPriceRangeAsync(decimal minPrice, decimal maxPrice)
    {
        return await _session
            .Query<ContentItem, ProductPartIndex>(x =>
                x.Published && x.Price >= minPrice && x.Price <= maxPrice)
            .OrderBy(x => x.Price)
            .ListAsync();
    }
}
```
