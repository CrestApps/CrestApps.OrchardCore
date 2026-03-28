# Caching Examples

## Example 1: Dynamic Cache for a Featured Products Widget

A shape that caches featured products and invalidates when any Product content item changes.

### Shape Template (Razor)

```html
<cache expires-after="TimeSpan.FromMinutes(20)"
       vary-by-query="category"
       vary-by-user="true">
    <div class="featured-products">
        @foreach (var product in Model.Products)
        {
            <div class="product-card">
                <h3>@product.DisplayText</h3>
                <p>@product.Content.ProductPart.Price.Value</p>
            </div>
        }
    </div>
</cache>
```

### Shape Template (Liquid)

```liquid
{% cache "featured-products", after: "00:20:00", vary_by: Request.QueryString["category"] %}
    <div class="featured-products">
        {% for product in Model.Products %}
            <div class="product-card">
                <h3>{{ product.DisplayText }}</h3>
                <p>{{ product.Content.ProductPart.Price.Value }}</p>
            </div>
        {% endfor %}
    </div>
{% endcache %}
```

### Display Driver with Cache Dependencies

```csharp
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

public sealed class FeaturedProductsDisplayDriver : DisplayDriver<FeaturedProductsViewModel>
{
    public override IDisplayResult Display(FeaturedProductsViewModel model, BuildDisplayContext context)
    {
        return View("FeaturedProducts", model)
            .Location("Content", "Content:5")
            .Cache("featured-products", cache => cache
                .AddDependency("contenttype:Product")
                .AddContext("query")
                .WithExpiryAfter(TimeSpan.FromMinutes(20))
                .WithExpirySliding(TimeSpan.FromMinutes(5))
            );
    }
}
```

## Example 2: Content Event Handler with Cache Invalidation

Automatically invalidate caches when content is published or removed.

```csharp
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Environment.Cache;

public sealed class ProductCacheInvalidationHandler : ContentHandlerBase
{
    private readonly ISignal _signal;

    public ProductCacheInvalidationHandler(ISignal signal)
    {
        _signal = signal;
    }

    public override Task PublishedAsync(PublishContentContext context)
    {
        return InvalidateIfProductAsync(context.ContentItem.ContentType);
    }

    public override Task RemovedAsync(RemoveContentContext context)
    {
        return InvalidateIfProductAsync(context.ContentItem.ContentType);
    }

    private async Task InvalidateIfProductAsync(string contentType)
    {
        if (string.Equals(contentType, "Product", StringComparison.OrdinalIgnoreCase))
        {
            await _signal.SignalTokenAsync("productcatalog");
            await _signal.SignalTokenAsync("featured-products");
        }
    }
}
```

Register the handler:

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentHandler<ProductCacheInvalidationHandler>();
    }
}
```

## Example 3: Distributed Cache with JSON Serialization

Cache the results of an expensive query using `IDistributedCache` with manual serialization.

```csharp
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using OrchardCore.Queries;

public sealed class CachedQueryService
{
    private readonly IQueryManager _queryManager;
    private readonly IDistributedCache _distributedCache;

    public CachedQueryService(
        IQueryManager queryManager,
        IDistributedCache distributedCache)
    {
        _queryManager = queryManager;
        _distributedCache = distributedCache;
    }

    public async Task<IEnumerable<object>> ExecuteCachedQueryAsync(
        string queryName,
        IDictionary<string, object> parameters)
    {
        var cacheKey = $"query-{queryName}-{string.Join("-", parameters.Values)}";
        var cached = await _distributedCache.GetStringAsync(cacheKey);

        if (cached is not null)
        {
            return JsonSerializer.Deserialize<IEnumerable<object>>(cached) ?? [];
        }

        var query = await _queryManager.GetQueryAsync(queryName);

        if (query is null)
        {
            return [];
        }

        var result = await _queryManager.ExecuteQueryAsync(query, parameters);
        var items = result.Items.ToList();

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
            SlidingExpiration = TimeSpan.FromMinutes(5),
        };

        await _distributedCache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(items),
            options);

        return items;
    }
}
```

## Example 4: IDynamicCacheService for Pre-rendered HTML Fragments

Store and retrieve pre-rendered HTML using `IDynamicCacheService` directly.

```csharp
using OrchardCore.DynamicCache;
using OrchardCore.Environment.Cache;

public sealed class NavigationCacheService
{
    private readonly IDynamicCacheService _dynamicCacheService;
    private readonly ISignal _signal;

    public NavigationCacheService(
        IDynamicCacheService dynamicCacheService,
        ISignal signal)
    {
        _dynamicCacheService = dynamicCacheService;
        _signal = signal;
    }

    public async Task<string?> GetCachedNavigationAsync()
    {
        var context = new CacheContext("main-navigation")
            .AddDependency("contenttype:Menu")
            .AddContext("user")
            .WithExpiryAfter(TimeSpan.FromHours(1));

        return await _dynamicCacheService.GetCachedValueAsync(context);
    }

    public async Task SetCachedNavigationAsync(string html)
    {
        var context = new CacheContext("main-navigation")
            .AddDependency("contenttype:Menu")
            .AddContext("user")
            .WithExpiryAfter(TimeSpan.FromHours(1));

        await _dynamicCacheService.SetCachedValueAsync(context, html);
    }

    public async Task InvalidateNavigationCacheAsync()
    {
        await _signal.SignalTokenAsync("contenttype:Menu");
    }
}
```

## Example 5: Multiple Cache Profiles for Different Response Types

Define multiple cache profiles and apply them to different endpoints.

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddResponseCaching();

        services.AddMvc(options =>
        {
            options.CacheProfiles.Add("Static", new CacheProfile
            {
                Duration = 86400,
                Location = ResponseCacheLocation.Any,
                VaryByHeader = "Accept-Encoding",
            });

            options.CacheProfiles.Add("Personalized", new CacheProfile
            {
                Duration = 60,
                Location = ResponseCacheLocation.Client,
                VaryByHeader = "Cookie",
            });

            options.CacheProfiles.Add("ApiResponse", new CacheProfile
            {
                Duration = 120,
                Location = ResponseCacheLocation.Any,
                VaryByQueryKeys = ["page", "pageSize", "sort"],
            });

            options.CacheProfiles.Add("NeverCache", new CacheProfile
            {
                Duration = 0,
                Location = ResponseCacheLocation.None,
                NoStore = true,
            });
        });
    }
}
```

Apply cache profiles to controllers and actions:

```csharp
[ResponseCache(CacheProfileName = "ApiResponse")]
public sealed class ProductApiController : Controller
{
    [HttpGet]
    public async Task<IActionResult> List(int page = 1, int pageSize = 20)
    {
        // Return paginated product list.
        return Ok(results);
    }

    [HttpGet("{id}")]
    [ResponseCache(CacheProfileName = "Static")]
    public async Task<IActionResult> GetById(string id)
    {
        // Return individual product.
        return Ok(product);
    }

    [HttpPost]
    [ResponseCache(CacheProfileName = "NeverCache")]
    public async Task<IActionResult> Create([FromBody] ProductDto dto)
    {
        // Create product, never cache POST responses.
        return CreatedAtAction(nameof(GetById), new { id = product.ContentItemId }, product);
    }
}
```

## Example 6: Enabling Redis and Dynamic Cache via Recipe

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.DynamicCache",
        "OrchardCore.ResponseCompression",
        "OrchardCore.Redis",
        "OrchardCore.Redis.Cache"
      ],
      "disable": []
    }
  ]
}
```

Configure the Redis connection in `appsettings.json`:

```json
{
  "OrchardCore": {
    "OrchardCore_Redis": {
      "Configuration": "localhost:6379,abortConnect=false,connectTimeout=5000"
    }
  }
}
```

When `OrchardCore.Redis.Cache` is enabled, it replaces the default in-memory distributed cache with Redis, allowing all `IDistributedCache` consumers and dynamic cache entries to be shared across multiple application instances.

## Example 7: Combining Vary-By Attributes in Razor Cache Tag Helper

```html
<!-- Cache a user-specific dashboard widget -->
<cache expires-after="TimeSpan.FromMinutes(5)"
       vary-by-user="true"
       vary-by-route="area,controller,action"
       vary-by-cookie=".AspNetCore.Culture">
    @await Component.InvokeAsync("DashboardWidget")
</cache>

<!-- Cache a public page fragment with query and header variation -->
<cache expires-sliding="TimeSpan.FromMinutes(30)"
       vary-by-query="page,sort,filter"
       vary-by-header="Accept-Language">
    @await DisplayAsync(Model.SearchResults)
</cache>

<!-- Cache with a custom composite key -->
<cache expires-after="TimeSpan.FromHours(1)"
       vary-by="@($"{Model.TenantName}-{Model.CategoryId}")">
    @await DisplayAsync(Model.CategoryProducts)
</cache>
```
