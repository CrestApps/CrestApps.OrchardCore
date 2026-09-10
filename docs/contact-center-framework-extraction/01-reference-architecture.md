# 01 - Reference architecture: how the AI suite is built in `CrestApps.Core`

Everything in this plan copies the structure that already exists at `C:\Code\CrestApps\CrestApps.Core`. Read this document first; when in doubt during implementation, open the referenced file in the Core repository and copy its shape.

## 1. Repository layout

| Folder | Purpose | Examples |
| --- | --- | --- |
| `src/Abstractions` | Public contracts, models, builder types. No implementations beyond trivial helpers. | `CrestApps.Core.Abstractions`, `CrestApps.Core.AI.Abstractions`, `CrestApps.Core.Infrastructure.Abstractions` |
| `src/Primitives` | Concrete framework features and providers. Each feature is its own project and package. | `CrestApps.Core`, `CrestApps.Core.AI`, `CrestApps.Core.AI.Chat`, `CrestApps.Core.AI.OpenAI`, `CrestApps.Core.SignalR`, `CrestApps.Core.Templates` |
| `src/Stores` | Persistence implementations. Optional; consumers can bring their own. | `CrestApps.Core.Data.YesSql`, `CrestApps.Core.Data.EntityCore` |
| `src/Utilities` | Shared helpers with no framework dependencies. | `CrestApps.Core.Support` |
| `src/Resources` | Razor class library that ships compiled JS/CSS (`Assets.json` + gulp) as static web assets. | `CrestApps.AI.Resources` |
| `src/Startup` | Runnable sample hosts and shared sample plumbing. | `CrestApps.Core.Mvc.Web`, `CrestApps.Core.Blazor.Web`, `CrestApps.Core.Startup.Shared`, `CrestApps.Core.Aspire.AppHost` |
| `src/CrestApps.Core.Docs` | Docusaurus docs site. | `docs/core/*.md`, `sidebars.js`, `docs/changelog/2.0.0.md` |
| `tests/CrestApps.Core.Tests` | xunit v3 tests, folders by area. | `Core/Chat`, `Framework/AI`, `Modules/AI.Memory`, `Support/TestOptionsMonitor.cs` |

The solution file is `CrestApps.Core.slnx` with folders `/src/Abstractions/`, `/src/Primitives/`, `/src/Stores/`, `/src/Resources/`, `/src/Startup/`, `/src/Utilities/`, `/tests/`.

## 2. Project and package conventions

Every `csproj` under `src` looks like this (from `src/Primitives/CrestApps.Core.AI.Chat/CrestApps.Core.AI.Chat.csproj`):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>CrestApps.Core.AI.Chat</RootNamespace>
    <Title>CrestApps AI Chat Core</Title>
    <Description>
      $(CrestAppsDescription)

      Chat services and SignalR hub contracts for CrestApps AI.
      Framework-independent, usable in any ASP.NET Core application.
    </Description>
    <PackageTags>$(PackageTags) ai chat signalr sessions interactions</PackageTags>
  </PropertyGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="CrestApps.Core.Tests" />
  </ItemGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../CrestApps.Core.AI/CrestApps.Core.AI.csproj" />
  </ItemGroup>
</Project>
```

`Directory.Build.props` in the Core repository sets: `net10.0`, `ImplicitUsings` enabled, `IsPackable` true, `TreatWarningsAsErrors` true, `EnforceCodeStyleInBuild` true, `AnalysisLevel` latest-Recommended, `VersionPrefix` 2.0.0, SourceLink, and a `NoWarn` list. Package versions are central (`Directory.Packages.props`). Abstractions projects reference only `Microsoft.Extensions.*` abstractions packages; primitives use `<FrameworkReference Include="Microsoft.AspNetCore.App" />` when they need ASP.NET Core types.

## 3. Composition model (the builder pattern)

Sample host composition (`src/Startup/CrestApps.Core.Mvc.Web/Program.cs`):

```csharp
builder.Services
    .AddCrestAppsCore(crestApps => crestApps
        .AddAISuite(ai => ai
            .AddYesSqlStores()
            .AddMarkdown()
            .AddChatInteractions(chatInteractions => chatInteractions
                .AddYesSqlStores()
                .ConfigureChatHubOptions<ChatInteractionHub>())
            .AddDocumentProcessing(documentProcessing => documentProcessing
                .AddYesSqlStores()
                .AddOpenXml()
                .AddPdf())
            .AddSignalR(addStoreCommitterFilter: true)
            .AddOpenAI()
            .AddAzureOpenAI())
        .AddIndexingServices(indexing => indexing
            .AddYesSqlStores()
            .AddElasticsearch(builder.Configuration.GetSection("CrestApps:Elasticsearch"), es => es.AddAIDocuments())));
```

Mechanics, all of which the Contact Center Suite must reproduce:

1. **Builder types** are sealed classes with a single `IServiceCollection Services` property, defined in `src/Abstractions/CrestApps.Core.Abstractions/Builders/CrestAppsBuilder.cs` (`CrestAppsCoreBuilder`, `CrestAppsAISuiteBuilder`, `CrestAppsChatInteractionsBuilder`, `CrestAppsDocumentProcessingBuilder`, `CrestAppsMcpServerBuilder`, ...). A builder exists for every feature that has its own store or sub-options.
2. **Entry points** live in the primitive that owns the feature: `AddCrestAppsCore` and `AddIndexingServices` in `src/Primitives/CrestApps.Core/ServiceCollectionExtensions.cs`; `AddAISuite` in `src/Primitives/CrestApps.Core.AI/ServiceCollectionExtensions.cs`; `AddChatInteractions` in `CrestApps.Core.AI.Chat`; `AddOpenAI` in `CrestApps.Core.AI.OpenAI`; `AddSignalR(this CrestAppsAISuiteBuilder ...)` in `CrestApps.Core.SignalR`.
3. **Every builder method is sugar over a plain `IServiceCollection` method** prefixed `AddCore` (`AddCoreAIChatInteractions`, `AddCoreAIChatSessionProcessing`, `AddCoreAIChatNotifications`, `AddCoreSignalR`, `AddCoreAIMemory`, ...). Hosts that cannot use the builder (Orchard Core, whose feature system decides what is enabled) call the `AddCore*` methods directly. Both must exist for every feature.
4. **Store registration is separate from feature registration.** `AddYesSqlStores()` extension methods on each builder type are defined in `src/Stores/CrestApps.Core.Data.YesSql/ServiceCollectionExtensions.cs`, next to `AddCoreAIChatInteractionStoresYesSql()`-style `IServiceCollection` methods. The store package references the primitives (for the builder and model types), never the reverse.
5. **Registration style:** `TryAddScoped` / `TryAddSingleton` for defaults that hosts may replace (`services.Replace(...)` on the Orchard side), `TryAddEnumerable(ServiceDescriptor.Scoped<IHandler, Impl>())` for handler chains, `services.AddOptions<T>()` plus `Configure<T>` for options, `services.Configure<TemplateOptions>` for Fluid member registration.
6. **Providers** register through named entries (`AddCoreAIDeploymentProvider(name, o => ...)`, `AddCoreAIConnectionSource`), not through hard-coded lists.

## 4. Store model

- Store **interfaces** are in Abstractions (`IAIProfileStore`, `IAIChatSessionStore`, `IWebCrawlerStore`). Models derive from `CatalogItem` and implement marker interfaces (`INameAwareModel`, `IDisplayTextAwareModel`, `ISourceAwareModel`, `IModifiedUtcAwareModel`, `ICloneable<T>`).
- Generic catalog plumbing is in `CrestApps.Core.Abstractions` (`ICatalog<T>`, `INamedCatalog<T>`, `ICatalogManager<T>`, `ICatalogEntryHandler<T>`, handler contexts) and `CrestApps.Core` (`CatalogManager<T>`, `NamedCatalogManager<T>`, `CatalogEntryHandlerBase<T>`, `AddCatalogManagers()`).
- `CrestApps.Core.Data.YesSql` provides:
  - `Services/DocumentCatalog<T, TIndex>` (base for YesSql stores), `NamedDocumentCatalog`, `SourceDocumentCatalog`.
  - `Indexes/<Area>/<Model>Index.cs` (`MapIndex` subclasses, `ItemId` column) and `Indexes/<Area>/<Model>IndexSchemaBuilderExtensions.cs` with `public static async Task Create<Model>IndexSchemaAsync(this ISchemaBuilder schemaBuilder, YesSqlStoreOptions options)` that calls `CreateMapIndexTableAsync` and `AlterIndexTableAsync` with `collection: options.AICollectionName`.
  - `YesSqlStoreOptions` with collection names (`DefaultCollectionName`, `AICollectionName = "AI"`, `AIMemoryCollectionName`, `AIDocsCollectionName`).
  - `AddCoreYesSqlDataStore(Func<Configuration, IConfiguration>)` for standalone hosts: creates the `IStore`, initializes collections, registers every `IIndexProvider` grouped by collection, registers a scoped `ISession`, `IStoreCommitter` (`YesSqlStoreCommitter`) and the catalog managers.
  - `Services/YesSql<Model>Store.cs` implementations and the `Add<Feature>StoresYesSql()` / `AddYesSqlStores()` extensions.
- `IStoreCommitter` is committed by `StoreCommitterActionFilter` (MVC), `StoreCommitterEndpointFilter` (minimal APIs) and `StoreCommitterHubFilter` (SignalR) from `CrestApps.Core`, wired with `AddCrestAppsStoreCommitterFilter()`.

Orchard Core does **not** call `AddCoreYesSqlDataStore`. It uses Orchard's own `ISession`, registers index providers with `services.AddIndexProvider<T>()`, adds collections through `StoreCollectionOptions`, and runs `DataMigration` classes whose `CreateAsync` calls the framework schema extension, for example `await SchemaBuilder.CreateAIChatSessionIndexSchemaAsync(_option);` in `src/Modules/CrestApps.OrchardCore.AI/Migrations/AIChatSessionIndexMigrations.cs`.

### Stored document type names (critical)

YesSql writes the CLR type name (`Namespace.Type, Assembly`) into the `Document.Type` column. Moving a model to a new namespace or assembly makes existing rows unreadable unless the column is rewritten. The AI move solved this with `src/Modules/CrestApps.OrchardCore.AI/Migrations/AILegacyDocumentTypeNameMigrations.cs`: a `DataMigration` that schedules a deferred task (`ShellScope.AddDeferredTask`) which runs `UPDATE <prefix><Collection>_Document SET Type = REPLACE(REPLACE(Type, '<old namespace prefix>', '<new namespace prefix>'), '<old assembly>', '<new assembly>') WHERE Type LIKE '<old namespace prefix>%' AND Type LIKE '%, <old assembly>%'` per collection, using `IStore.Configuration` for table prefix, schema, dialect quoting and `TableNameConvention.GetDocumentTable(collection)`. Generic documents (`DictionaryDocument<T>`) contain the argument type name too and are handled by `AIDeploymentIndexMigrations`. Every moved document type in this plan needs the same treatment (see appendix B for the exact prefix/assembly table).

## 5. Background work

The framework never registers Orchard's `IBackgroundTask`. The pattern (from `CrestApps.Core.AI.Chat`):

- `AIChatSessionCloseCycleService` - does one pass of the work in a scoped unit of work.
- `AIChatSessionCloseRunner` - the loop (interval, cancellation, start/stop) that calls the cycle service through `IServiceProvider.CreateAsyncScope()`.
- `AIChatSessionCloseBackgroundService : IHostedService` - registered with `TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ...>())` by `AddCoreAIChatSessionProcessing()`.
- Orchard (`ChatCoreStartup` in `src/Modules/CrestApps.OrchardCore.AI/Startup.cs`) registers its own `IBackgroundTask` that calls the cycle service, and removes the framework hosted service:

```csharp
services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, AIChatSessionCloseBackgroundTask>());
services.AddCoreAIChatSessionProcessing();
// OC uses IBackgroundTask with distributed locking instead of IHostedService,
// so remove the framework's hosted service and runner.
services.RemoveAll<AIChatSessionCloseRunner>();
var hostedServiceDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(AIChatSessionCloseBackgroundService));
if (hostedServiceDescriptor is not null) { services.Remove(hostedServiceDescriptor); }
```

For the Contact Center Suite this is improved slightly: the hosted runners are registered by an explicit opt-in (`AddBackgroundWorkers()` on the builder) so Orchard never has to remove them.

## 6. SignalR

- `CrestApps.Core.SignalR`: `AddCoreSignalR(pathPrefix)` (adds SignalR with camelCase JSON) and `HubRouteManager` (`MapHub<T>`, `GetPathByHub<T>()`, absolute URI building).
- Hub logic lives in framework base classes (`Hubs/AIChatHubCore.cs`, `Hubs/ChatInteractionHubBase.cs`, typed client interfaces `IAIChatHubClient`, `IChatInteractionHubClient` in `CrestApps.Core.AI.Chat`). Concrete hubs (`[Authorize] public sealed class ChatInteractionHub : ChatInteractionHubBase`) are in the host (`Mvc.Web/Areas/ChatInteractions/Hubs`, Orchard module `Hubs/`).
- `ConfigureCrestAppsChatHubOptions<THub>()` configures `HubOptions<THub>` per concrete hub.
- Orchard maps hubs with `routes.MapHub<THub>(SignalRHubRoutes.GetHubPath<THub>())` and prefixes groups with the tenant name (`TenantSignalRGroupName` in `CrestApps.OrchardCore.SignalR.Core`).

## 7. Cross-cutting host services already abstracted by the framework

| Concern | Framework type | Orchard adapter | Sample host |
| --- | --- | --- | --- |
| Current time | `System.TimeProvider` | `CrestApps.OrchardCore.Core/Services/ClockTimeProviderAdapter` registered as `TimeProvider` | `TimeProvider.System` |
| Current user | `CrestApps.Core.Security.IUserAccessor` (`ClaimsPrincipal User`) | default `UserAccessor` over `IHttpContextAccessor` | same |
| Unit of work | `CrestApps.Core.Services.IStoreCommitter` | Orchard commits the session itself; filters are not used | `YesSqlStoreCommitter` + filters |
| Authorization | `IAuthorizationService` with `OperationAuthorizationRequirement` and resource objects (`AIChatSessionDocumentAuthorizationContext`, `ChatInteraction`) | permission-based `AuthorizationHandler<...>` | `Startup.Shared/Areas/AIChat/Services/Sample*AuthorizationHandler.cs` |
| Settings | options pattern (`IOptions<T>`, `IOptionsMonitor<T>`) | `IConfigureOptions<T>` / `IPostConfigureOptions<T>` reading `ISiteService` (e.g. `TelephonySettingsConfiguration`), `AddSignalOptionsChangeTokenSource<T>()` for refresh | `SiteSettingsStore` + `SiteSettingsConfigureStoredOptions<T>` + `SiteSettingsOptionsChangeTokenSource<T>` in `Startup.Shared` |
| Endpoints | extension methods on `IEndpointRouteBuilder` (`AddChatApiEndpoints`, `AddDownloadAIDocumentEndpoint`) | called from `StartupBase.Configure` | called from `Program.cs` |
| Templates | `CrestApps.Core.Templates` (Fluid), embedded `Templates/Prompts/**` | `AddTemplatesFromAssembly` | same |

Concerns that the framework does **not** abstract today and that the Contact Center Suite introduces (see [03-host-seams.md](03-host-seams.md)): distributed locks, tenant identity, scoped/deferred execution, user directory lookups, SMS providers, contacts and subjects, WebSocket connection registries.

## 8. Coding conventions (digest of `.github/copilot-instructions.md` in the Core repository)

- Constructor injection; no `ArgumentNullException.ThrowIf...` in constructors; null guards in public methods for required non-nullable inputs, followed by a blank line.
- Blank line before `return` (unless first statement in a block), before/after `if`/`switch`/loops (unless preceded by `{`), never two consecutive blank lines; exactly one trailing newline.
- `var` everywhere; expression-bodied members only for one short line; multi-line conditional operators with `?` and `:` on their own lines.
- No `global using`; explicit `using` directives; no fully qualified type names in code.
- Avoid `DateTime.UtcNow`; inject `TimeProvider`.
- XML `<summary>` on every public type and member, `<param>` for every parameter in signature order, constructors documented, blank line before an XML block unless preceded by `{`.
- Public classes sealed by default.
- Warnings are errors.
- Catalog entry models get an authoritative `CatalogEntryHandlerBase<T>` with `PopulateAsync` from `JsonNode`, create-time defaults in `InitializedAsync`/`CreatingAsync`, required-field checks in `ValidatingAsync`; duplicate-name validation in the handler with store-level uniqueness as the final safeguard.
- UI-only validation stays in the web layer, not in handlers.
- Framework code must not use `Sample*` names; analytics/shared services belong in Abstractions/Primitives, not sample hosts.

## 9. How Orchard Core consumes the AI suite today (the model for Phase 1 rewiring)

`src/Core/CrestApps.OrchardCore.AI.Core/ServiceCollectionExtensions.cs` wraps the framework entry point once (`services.AddCrestAppsCore(crestApps => crestApps.AddAISuite(ai => ai...))`) and each module `Startup` adds its feature:

```csharp
// src/Modules/CrestApps.OrchardCore.AI.Chat.Interactions/Startup.cs
services
    .AddCoreAIChatInteractions()
    .AddCoreAIChatInteractionStoresYesSql()
    .AddDataMigration<ChatInteractionIndexMigrations>();
services.AddDisplayDriver<ChatInteraction, ChatInteractionDisplayDriver>();
services.AddNavigationProvider<AdminMenu>();
services.AddPermissionProvider<PermissionProvider>();
```

What stays on the Orchard side in the AI suite, and therefore in the Contact Center Suite: `Manifest.cs`, permissions and permission providers, admin menus, display drivers, controllers, views, view models, resource manifests, recipes, deployment steps, workflows, `DataMigration` classes (calling framework schema extensions), `IBackgroundTask` wrappers, `IModularTenantEvents`, site-settings-to-options bridges, `IAuthorizationHandler`s that map framework requirements to permissions.

## 10. Tests in the Core repository

`tests/CrestApps.Core.Tests/CrestApps.Core.Tests.csproj`: xunit v3, `Moq`, `Microsoft.NET.Test.Sdk`, `TestingPlatformDotnetTestSupport`, `UseMicrosoftTestingPlatformRunner`, project references to the primitives under test plus the sample hosts, `<Using Include="Xunit" />`, embedded templates. Folders group by area (`Core/Chat`, `Core/Realtime`, `Framework/AI`, `Modules/AI.Memory`, `Support`). Run with:

```bash
dotnet test .\tests\CrestApps.Core.Tests\CrestApps.Core.Tests.csproj -c Release /p:NuGetAudit=false
```

Orchard tests in this repository run under the Microsoft Testing Platform (`dotnet <Tests>.dll --filter ...` or `dotnet test` with `-filterVSTest`).
