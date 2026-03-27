# GraphQL Examples

## Example 1: Common Content Queries

### Query All Blog Posts

```graphql
{
  blogPost(status: PUBLISHED, orderBy: {publishedUtc: DESC}, first: 10) {
    contentItemId
    displayText
    publishedUtc
    titlePart {
      title
    }
    autoroutePart {
      path
    }
    htmlBodyPart {
      html
    }
  }
}
```

### Search Blog Posts

```graphql
{
  blogPost(where: {displayText_contains: "orchard"}, first: 20) {
    contentItemId
    displayText
    publishedUtc
    autoroutePart {
      path
    }
  }
}
```

### Query Single Content Item

```graphql
{
  contentItem(contentItemId: "4abc123def456") {
    contentItemId
    contentType
    displayText
    publishedUtc
    owner
    ... on BlogPost {
      titlePart {
        title
      }
      autoroutePart {
        path
      }
    }
  }
}
```

## Example 2: Custom GraphQL Part Type

```csharp
using GraphQL.Types;
using OrchardCore.ContentManagement;

public sealed class ProductPart : ContentPart
{
    public string ProductName { get; set; }
    public decimal Price { get; set; }
    public string SKU { get; set; }
    public bool InStock { get; set; }
}

public sealed class ProductPartQueryObjectType : ObjectGraphType<ProductPart>
{
    public ProductPartQueryObjectType()
    {
        Name = "ProductPart";

        Field(x => x.ProductName).Description("The product name.");
        Field(x => x.Price).Description("The product price.");
        Field(x => x.SKU).Description("The stock keeping unit.");
        Field(x => x.InStock).Description("Whether the product is in stock.");
    }
}

// Registration in Startup.cs
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddObjectGraphType<ProductPart, ProductPartQueryObjectType>();
    }
}
```

### Querying the Custom Type

```graphql
{
  product(status: PUBLISHED) {
    displayText
    productPart {
      productName
      price
      sku
      inStock
    }
  }
}
```
