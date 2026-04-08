---
sidebar_label: Data Storage
sidebar_position: 12
title: Data Storage
description: Pluggable catalog pattern for persistent data storage with first-party YesSql and Entity Framework Core implementations.
---

:::info Canonical framework docs
The shared framework guidance now lives in **[CrestApps.Core](https://core.crestapps.com/docs/framework/data-storage)**. This Orchard Core page is kept for Orchard-specific integration context and cross-links.
:::

# Data Storage

> A pluggable catalog pattern for CRUD operations on framework models, with first-party `CrestApps.Core.Data.YesSql` and `CrestApps.Core.Data.EntityCore` packages plus support for custom implementations.

## Quick Start

```csharp
// YesSql + SQLite
builder.Services.AddYesSqlDataStore(configuration => configuration
    .UseSqLite("Data Source=app.db;Cache=Shared")
    .SetTablePrefix("CA_"));

builder.Services.AddNamedSourceDocumentCatalog<AIProfile, AIProfileIndex>();

// Entity Framework Core + SQLite
builder.Services.AddEntityCoreSqliteDataStore("Data Source=app.db");
builder.Services.AddEntityCoreCoreStores();
```

## Problem & Solution

The framework defines many model types (profiles, deployments, connections, sessions, documents) that need persistent storage. Rather than coupling to a specific ORM, it uses the **catalog pattern**:

- **`ICatalog<T>`** — Basic CRUD operations
- **`INamedCatalog<T>`** — Adds name-based lookup
- **`ISourceCatalog<T>`** — Adds source-based filtering
- **`INamedSourceCatalog<T>`** — Combines both

The repository now ships two first-party persistence flavors:

| Package | Backing technology | Typical fit |
|---------|--------------------|-------------|
| `CrestApps.Core.Data.YesSql` | YesSql document store | Hosts that want YesSql collections, indexes, and a request-scoped unit of work |
| `CrestApps.Core.Data.EntityCore` | Entity Framework Core | Hosts that already standardize on EF Core and want SQLite or another EF-supported relational provider |

You can also implement the same interfaces with another ORM, a remote service, or any custom storage approach.

## Catalog Interfaces

### `ICatalog<T>`

Basic CRUD:

```csharp
public interface ICatalog<T> : IReadCatalog<T>
{
    ValueTask CreateAsync(T entry);
    ValueTask UpdateAsync(T entry);
    ValueTask<bool> DeleteAsync(T entry);
}
```

The YesSql implementation stages writes and expects the host to own the session flush boundary. The Entity Framework Core implementation commits inside its store methods, so it does not require a separate unit-of-work middleware.

### `INamedCatalog<T>`

Adds name-based lookup for models implementing `INameAwareModel`:

```csharp
public interface INamedCatalog<T> : ICatalog<T> where T : INameAwareModel
{
    ValueTask<T> FindByNameAsync(string name);
}
```

### `ISourceCatalog<T>`

Adds source-based filtering for models implementing `ISourceAwareModel`:

```csharp
public interface ISourceCatalog<T> : ICatalog<T> where T : ISourceAwareModel
{
    ValueTask<IReadOnlyCollection<T>> GetAsync(string source);
}
```

### `INamedSourceCatalog<T>`

Combines both capabilities:

```csharp
public interface INamedSourceCatalog<T> : INamedCatalog<T>, ISourceCatalog<T>
    where T : INameAwareModel, ISourceAwareModel
{
    ValueTask<T> GetAsync(string name, string source);
}
```

## DI Extension Methods

| Method | Registers | Requires |
|--------|-----------|----------|
| `AddDocumentCatalog<TModel, TIndex>()` | `ICatalog<T>` | `CatalogItem` + `CatalogItemIndex` |
| `AddNamedDocumentCatalog<TModel, TIndex>()` | `ICatalog<T>` + `INamedCatalog<T>` | + `INameAwareModel` + `INameAwareIndex` |
| `AddSourceDocumentCatalog<TModel, TIndex>()` | `ICatalog<T>` + `ISourceCatalog<T>` | + `ISourceAwareModel` + `ISourceAwareIndex` |
| `AddNamedSourceDocumentCatalog<TModel, TIndex>()` | All four interfaces | Both `INameAware*` + `ISourceAware*` |

The Entity Framework Core package exposes the same service-registration shape without YesSql indexes:

| Method | Registers | Requires |
|--------|-----------|----------|
| `AddDocumentCatalog<TModel>()` | `ICatalog<T>` | `CatalogItem` |
| `AddNamedDocumentCatalog<TModel>()` | `ICatalog<T>` + `INamedCatalog<T>` | `CatalogItem` + `INameAwareModel` |
| `AddSourceDocumentCatalog<TModel>()` | `ICatalog<T>` + `ISourceCatalog<T>` | `CatalogItem` + `ISourceAwareModel` |
| `AddNamedSourceDocumentCatalog<TModel>()` | All four interfaces | `CatalogItem` + both awareness interfaces |

`AddEntityCoreCoreStores()` registers the built-in CrestApps store interfaces (`IAIChatSessionManager`, prompt stores, document stores, memory stores, search index profile store, and related catalog registrations) against the Entity Framework Core package.

## Catalog Entry Handlers

React to lifecycle events on catalog entries:

```csharp
public sealed class ProfileCreatedHandler : CatalogEntryHandlerBase<AIProfile>
{
    public override Task CreatedAsync(CreatedContext<AIProfile> context)
    {
        // React to profile creation (e.g., initialize defaults, send notification)
        return Task.CompletedTask;
    }
}

// Register
builder.Services.AddScoped<ICatalogEntryHandler<AIProfile>, ProfileCreatedHandler>();
```

### Lifecycle Events

| Phase | Context Types |
|-------|--------------|
| Initialize | `InitializingContext<T>`, `InitializedContext<T>` |
| Validate | `ValidatingContext<T>`, `ValidatedContext<T>` |
| Create | `CreatingContext<T>`, `CreatedContext<T>` |
| Update | `UpdatingContext<T>`, `UpdatedContext<T>` |
| Delete | `DeletingContext<T>`, `DeletedContext<T>` |
| Load | `LoadingContext<T>`, `LoadedContext<T>` |

## YesSql Index Pattern

Each model needs a corresponding YesSql index:

```csharp
public sealed class AIProfileIndex : CatalogItemIndex, INameAwareIndex, ISourceAwareIndex
{
    public string Name { get; set; }
    public string Source { get; set; }
}

public sealed class AIProfileIndexProvider : IndexProvider<AIProfile>
{
    public override void Describe(DescribeContext<AIProfile> context)
    {
        context.For<AIProfileIndex>()
            .Map(profile => new AIProfileIndex
            {
                ItemId = profile.Id,
                Name = profile.Name,
                Source = profile.Source,
            });
    }
}
```

## Model Types in the Framework

| Model | Catalog Type | Used By |
|-------|-------------|---------|
| `AIProfile` | Named + Source | AI profiles and configuration |
| `AIProviderConnection` | Named + Source | Provider credentials |
| `AIDeployment` | Named + Source | Model deployment mappings |
| `AIProfileTemplate` | Named + Source | Profile templates |
| `AIChatSession` | Basic | Chat sessions |
| `ChatInteraction` | Basic | Chat interactions |
| `McpConnection` | Source | MCP server connections |
| `McpPrompt` | Named | MCP prompts |
| `McpResource` | Source | MCP resources |
| `A2AConnection` | Source | A2A connections |

## Using a Different Backend

To use Entity Framework Core instead of YesSql, implement the catalog interfaces:

```csharp
public sealed class EfCatalog<T> : ICatalog<T> where T : CatalogItem
{
    private readonly MyDbContext _db;

    public EfCatalog(MyDbContext db) => _db = db;

    public async ValueTask CreateAsync(T entry) => await _db.Set<T>().AddAsync(entry);
    public async ValueTask UpdateAsync(T entry) => _db.Set<T>().Update(entry);
    public async ValueTask<bool> DeleteAsync(T entry) { _db.Set<T>().Remove(entry); return true; }
    public async ValueTask SaveChangesAsync() => await _db.SaveChangesAsync();
    // ... implement IReadCatalog<T> methods
}
```

## Composite Catalogs

When models need to be loaded from multiple sources (e.g., code-defined defaults merged with database entries), use the **CatalogManager** pattern. The `CatalogManager<T>` delegates reads across all registered `ICatalog<T>` instances and merges the results:

```csharp
public sealed class CatalogManager<T>(
    ICatalog<T> primaryCatalog,
    IEnumerable<IReadCatalog<T>> additionalSources) where T : CatalogItem
{
    public async ValueTask<IReadOnlyCollection<T>> GetAllAsync()
    {
        var results = new List<T>();

        // Load from the primary (writable) catalog
        results.AddRange(await primaryCatalog.GetAllAsync());

        // Merge from read-only additional sources
        foreach (var source in additionalSources)
        {
            var entries = await source.GetAllAsync();
            foreach (var entry in entries)
            {
                if (!results.Any(r => r.Id == entry.Id))
                {
                    results.Add(entry);
                }
            }
        }

        return results;
    }
}
```

Register additional read-only sources alongside the primary catalog:

```csharp
// Primary writable catalog (YesSql-backed)
builder.Services.AddNamedSourceDocumentCatalog<AIProfile, AIProfileIndex>();

// Additional read-only source (e.g., code-defined defaults)
builder.Services.AddScoped<IReadCatalog<AIProfile>, DefaultProfilesCatalog>();
```

:::tip
The framework's in-memory `Catalog<T>` (backed by Orchard Core's `IDocumentManager`) is ideal for read-heavy, write-rare data such as configuration entries. Use YesSql-backed `DocumentCatalog<T, TIndex>` for transactional, queryable data.
:::

## Pagination

Catalogs support paginated queries through the `PageAsync` method. The YesSql-backed `DocumentCatalog` uses `.Skip()` and `.Take()` on the underlying query:

```csharp
// In a controller or service
public async Task<IActionResult> List(int page = 1, int pageSize = 20)
{
    var catalog = HttpContext.RequestServices.GetRequiredService<ICatalog<AIProfile>>();

    // PageAsync returns a PageResult<T> with Count and Entries
    var result = await catalog.PageAsync(page, pageSize);

    // result.Count  — total number of matching entries
    // result.Entries — the current page of items
    return View(new ListViewModel
    {
        Items = result.Entries,
        TotalCount = result.Count,
        Page = page,
        PageSize = pageSize,
    });
}
```

Under the hood, the YesSql implementation computes the skip value and applies it to the query:

```csharp
var skip = (page - 1) * pageSize;
var entries = await query.Skip(skip).Take(pageSize).ListAsync();
var count = await query.CountAsync();

return new PageResult<T>
{
    Count = count,
    Entries = entries.ToArray(),
};
```

:::info
YesSql translates `.Skip()` and `.Take()` into database-native `OFFSET`/`LIMIT` (SQLite, PostgreSQL) or `OFFSET`/`FETCH` (SQL Server) clauses. No in-memory paging is performed.
:::

## Bulk Operations

When inserting or updating many entries at once, use a **batch loop** pattern with periodic `SaveChangesAsync()` calls to avoid excessive memory use:

```csharp
private const int _batchSize = 50;

public async Task ImportProfilesAsync(IStore store, IList<AIProfile> profiles)
{
    for (var batchStart = 0; batchStart < profiles.Count; batchStart += _batchSize)
    {
        var batch = profiles.Skip(batchStart).Take(_batchSize).ToList();

        // Create a fresh session per batch to control memory
        using var session = store.CreateSession();

        foreach (var profile in batch)
        {
            await session.SaveAsync(profile, collection: AIConstants.AICollectionName);
        }

        await session.SaveChangesAsync();
    }
}
```

:::warning
YesSql does not support SQL-level `INSERT ... VALUES (...), (...)` bulk inserts. Each `SaveAsync` call tracks the entity in the session's identity map. Flushing per batch (via `SaveChangesAsync()` and disposing the session) prevents the identity map from growing unbounded.
:::

For scenarios where you process existing records in batches (e.g., recipe imports), combine pagination with batch updates:

```csharp
private const int _batchSize = 250;

public async Task UpdateAllUsersAsync(ISession session)
{
    var currentBatch = 0;

    while (true)
    {
        var users = await session.Query<User, UserIndex>(u => u.IsEnabled)
            .OrderBy(x => x.DocumentId)
            .Skip(currentBatch)
            .Take(_batchSize)
            .ListAsync();

        if (!users.Any())
        {
            break;
        }

        foreach (var user in users)
        {
            // Apply updates
            user.Properties["MigratedAt"] = DateTime.UtcNow.ToString("O");
            await session.SaveAsync(user);
        }

        await session.SaveChangesAsync();
        currentBatch += _batchSize;
    }
}
```

## Migration Strategy

When your model's schema changes (e.g., a new column is added to an index), you need a YesSql **index migration** to update the database. Migrations use `SchemaBuilder` to alter index tables.

### Creating an Initial Index

```csharp
public sealed class MyModelIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<MyModelIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<string>("Source", column => column.WithLength(255)),
            collection: MyConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<MyModelIndex>(table => table
            .CreateIndex("IDX_MyModelIndex_DocumentId",
                "DocumentId",
                "ItemId",
                "Name",
                "Source"),
            collection: MyConstants.CollectionName
        );

        return 1;
    }
}
```

### Adding a Column in a Later Version

Use `UpdateFromNAsync` methods to add columns or indexes incrementally:

```csharp
public async Task<int> UpdateFrom1Async()
{
    // Add a new column to an existing index
    await SchemaBuilder.AlterIndexTableAsync<MyModelIndex>(table => table
        .AddColumn<string>("DeploymentName", column => column.Nullable().WithLength(255)),
        collection: MyConstants.CollectionName
    );

    return 2;
}

public async Task<int> UpdateFrom2Async()
{
    // Add a compound index for new query patterns
    await SchemaBuilder.AlterIndexTableAsync<MyModelIndex>(table => table
        .CreateIndex("IDX_MyModelIndex_Deployment",
            "DocumentId",
            "DeploymentName",
            "Source"),
        collection: MyConstants.CollectionName
    );

    return 3;
}
```

:::tip
Always mark new columns as `.Nullable()` so existing rows are not affected. Backfill data in a separate migration step if needed.
:::

### Data Backfill During Migration

When a schema change requires backfilling existing data, combine `SchemaBuilder` with a batch update:

```csharp
public async Task<int> UpdateFrom3Async()
{
    // Step 1: Add the new column
    await SchemaBuilder.AlterIndexTableAsync<AIProfileIndex>(table => table
        .AddColumn<bool>("IsActive", column => column.Nullable().WithDefault(true)),
        collection: AIConstants.AICollectionName
    );

    // Step 2: Backfill existing records
    var store = HttpContext.RequestServices.GetRequiredService<IStore>();

    using var session = store.CreateSession();

    var profiles = await session.Query<AIProfile, AIProfileIndex>(
        collection: AIConstants.AICollectionName).ListAsync();

    foreach (var profile in profiles)
    {
        profile.IsActive = true;
        await session.SaveAsync(profile, collection: AIConstants.AICollectionName);
    }

    await session.SaveChangesAsync();

    return 4;
}
```

## Using a Different Backend (Expanded)

To replace YesSql with Entity Framework Core, implement all four catalog interfaces. Here is a more complete example:

```csharp
public sealed class EfCatalog<T> : ICatalog<T> where T : CatalogItem
{
    private readonly MyDbContext _db;

    public EfCatalog(MyDbContext db) => _db = db;

    // IReadCatalog<T>
    public async ValueTask<T> FindAsync(string id)
        => await _db.Set<T>().FirstOrDefaultAsync(x => x.Id == id);

    public async ValueTask<IReadOnlyCollection<T>> GetAllAsync()
        => await _db.Set<T>().ToListAsync();

    public async ValueTask<PageResult<T>> PageAsync(int page, int pageSize)
    {
        var query = _db.Set<T>().AsQueryable();
        var count = await query.CountAsync();
        var entries = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PageResult<T> { Count = count, Entries = entries.ToArray() };
    }

    // ICatalog<T>
    public async ValueTask CreateAsync(T entry)
        => await _db.Set<T>().AddAsync(entry);

    public ValueTask UpdateAsync(T entry)
    {
        _db.Set<T>().Update(entry);
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> DeleteAsync(T entry)
    {
        _db.Set<T>().Remove(entry);
        return ValueTask.FromResult(true);
    }

    public async ValueTask SaveChangesAsync()
        => await _db.SaveChangesAsync();
}
```

For named and source-aware models, extend with the additional interfaces:

```csharp
public sealed class EfNamedSourceCatalog<T> : EfCatalog<T>, INamedSourceCatalog<T>
    where T : CatalogItem, INameAwareModel, ISourceAwareModel
{
    private readonly MyDbContext _db;

    public EfNamedSourceCatalog(MyDbContext db) : base(db) => _db = db;

    public async ValueTask<T> FindByNameAsync(string name)
        => await _db.Set<T>().FirstOrDefaultAsync(x => x.Name == name);

    public async ValueTask<IReadOnlyCollection<T>> GetAsync(string source)
        => await _db.Set<T>().Where(x => x.Source == source).ToListAsync();

    public async ValueTask<T> GetAsync(string name, string source)
        => await _db.Set<T>().FirstOrDefaultAsync(x => x.Name == name && x.Source == source);
}
```

Register in DI:

```csharp
// Replace the YesSql-backed catalogs with EF Core equivalents
builder.Services.AddScoped<ICatalog<AIProfile>, EfNamedSourceCatalog<AIProfile>>();
builder.Services.AddScoped<INamedCatalog<AIProfile>, EfNamedSourceCatalog<AIProfile>>();
builder.Services.AddScoped<ISourceCatalog<AIProfile>, EfNamedSourceCatalog<AIProfile>>();
builder.Services.AddScoped<INamedSourceCatalog<AIProfile>, EfNamedSourceCatalog<AIProfile>>();
```

## Orchard Core Integration

Orchard Core modules use its built-in YesSql infrastructure and register catalogs in each module's `Startup` class. The catalog pattern remains the same — only the store initialization differs.
