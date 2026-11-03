# NLWeb + schema.org / JSON-LD for Orchard Core — design

Status: phases 1–5 implemented on branch `claude/nlweb-search-orchardcore-46cb08` (2026-11-02). Phase 6 is still to do.

## 0. What was built, and where it differs from the proposal below

| Area | Built |
| --- | --- |
| Features | **One feature per module.** `CrestApps.OrchardCore.Seo` (Advanced SEO, first called Structured Data) and `CrestApps.OrchardCore.NLWeb`, which depends on it. Every integration is a `[RequireFeatures]` startup, so nothing else has to be enabled by hand: Orchard Core SEO, Search, Rate Limits, AI, AI Data Sources, Products and Reviews. |
| Projects | `src/Abstractions/CrestApps.OrchardCore.Seo.Abstractions`, `src/Modules/CrestApps.OrchardCore.Seo`, `src/Core/CrestApps.OrchardCore.NLWeb.Core` and `src/Modules/CrestApps.OrchardCore.NLWeb`. |
| Protocol location | The protocol is in `CrestApps.OrchardCore.NLWeb.Core`, which has **no Orchard Core dependency**, instead of a new CrestApps.Core package. Moving it to CrestApps.Core later is a namespace change. It avoids publishing a Core preview first. |
| Sites (sub-corpora) | **No separate catalog or admin list.** The settings select the search indexes, and `query.site` picks one of them by name. The first index is the default. |
| MCP | `ask` is a selectable **AI tool**, so the MCP server, WebMCP and AI profiles can offer it. The anonymous public MCP binding (option A, optional authentication on the MCP server) is **not built yet**. |
| Content types | **Opt-in Schema.org part** (`SchemaOrgPart`) instead of type settings with a Liquid map. Its settings editor lists the types, their properties with Google's required/recommended levels, and a suggested source for each (field, item value, fixed, editor-entered or Liquid); `SchemaOrgSuggestions` mirrors the editor's rules for the AI tools. Types without the part publish nothing about their items, except parts in `StructuredDataOptions.SelfDescribingParts` (products). NLWeb answers still describe every item. |
| AI tools and code | `getSchemaOrgOptions`, `configureContentTypeSchemaOrg` and `configureSiteStructuredData` when AI is on. Pages that are not content items describe themselves with `IPageStructuredData.MainEntity`/`AddJsonLd`, the `<structured-data>` tag helper or the `structured_data` Liquid filter; the script is built on the `Layout` shape's display so views can still add to it. |
| UI | **No "Ask this site" widget yet.** The settings screens are done. |
| Index handler | The structured data fields in content indexes are **not built**, because Azure AI Search indexes reject fields their schema does not declare. NLWeb filters by `itemType` after it loads the results. |
| Not built yet (phase 6) | `/await` promises (`/await` answers `UNKNOWN_PROMISE`), the `chatgpt_app` format, the Who endpoint, AG-UI/A2UI writers, the upstream Orchard Core PRs, events and breadcrumb providers. |

## 1. Short answers

| Question | Answer |
|---|---|
| Do we need a `CrestApps.OrchardCore.Seo` module first? | Yes. NLWeb answers are schema.org objects, so the site needs one engine that turns any content item into JSON-LD. The same engine feeds the `<script type="application/ld+json">` on pages and the NLWeb `/ask` results. Pages and agents then always see the same data. |
| Does it extend `OrchardCore.Seo`? | It builds on it and does not replace it. `OrchardCore.Seo` keeps meta tags, Open Graph, Twitter cards and robots.txt. Our module takes over JSON-LD and merges the editor's hand-written `SeoMetaPart.GoogleSchema` into one `@graph`. |
| Is NLWeb a search provider? | No. NLWeb is an answer protocol that sits on top of retrieval. It needs a retriever (keyword, vector or hybrid) plus schema.org output. It does not need the `/search` page. |
| Does it need the `OrchardCore.Search` feature? | Only for keyword retrieval. Lucene, Elasticsearch and Azure AI register their keyed `ISearchService` only under `[RequireFeatures("OrchardCore.Search")]`. Vector retrieval through our AI data sources does not need it. |
| Can it be done without patching OC? | Yes. Everything uses existing extension points: content aspects, `IResourceManager.RegisterHeadScript`, `IAsyncResultFilter`, `IDocumentIndexHandler`, keyed `ISearchService`, and minimal API endpoints. Four small upstream OC PRs (section 8) would make it cleaner, but none blocks us. |

## 2. NLWeb: what we must implement

Sources: the NLWeb reference implementation (`nlweb-ai/NLWeb`), `nlweb-ai/NLWeb_Core/docs/NLWeb Protocol Specification (v 0.54).md`, `http_example.md` and `mcp_example.md` from the same repo, `nlweb-ai/nlweb-typespec/main.tsp` (v0.5), and the Lumar agentic-readiness NLWeb checks, which already validate v0.55.

The spec is still moving. A W3C Community Group formed in October 2025.

| Version | Request | Response |
|---|---|---|
| v0.1 (original Python) | `GET/POST /ask?query=&site=&prev=&decontextualized_query=&mode=list\|summarize\|generate&streaming=&query_id=` | `{ query_id, results: [{ url, name, site, score, description, schema_object }] }` |
| v0.5 (TypeSpec) | `{ query{text,site[],location,price,type,…}, context{prev[],text,memory[]}, return_response{streaming,format,mode,lang,client_type}, meta{conversation_id,api_version,request_id} }` | `{ _meta{response_type,version,…}, content: [ {type:"text"} \| {type:"resource", resource{uri,mimeType,data}} ] }` |
| v0.54 / v0.55 (current) | `{ query{text, site, itemType, …filters}, context{@type:"ConversationalContext", prev[], text, memory}, prefer{streaming, response_format, mode, accept-language, user-agent}, meta{version, session_context} }` | `{ _meta{response_type: answer\|elicitation\|promise\|failure, version, session_context, …}, results:[schema.org objects + grounding + actions] }`. With `response_format: chatgpt_app` the response is `content[] + structuredData[]` instead. Also `elicitation{text,questions[]}`, `promise{token,estimated_time}` and `error{code,message}`. |

Required surface:

- `POST /ask`. JSON by default. SSE when `Accept: text/event-stream` or `prefer.streaming` is true. The event sequence is `start` (carries `_meta`), then `result` and `content` events, then `complete` (carries `_meta` + `session_context`), then `end`.
- `POST /await` with `{ promise_token, action: checkin|cancel }`.
- MCP `ask` and `await` tools. The tool's `inputSchema` takes `query.text`; a plain `query` string is also accepted. The result is the same envelope.
- Conformance points that validators check:
  - Anonymous queries answer with HTTP 200. Errors also return 200 with a `failure` envelope. The exception is 429 with `Retry-After`.
  - Every streamed frame is valid JSON.
  - `_meta.response_type` is lowercase. `_meta.version` is present.
  - Items carry schema.org types. This is recommended, not required.
  - The MCP `ask` tool has a description and a string query input.
- The Who endpoint (agent finder) is optional and later.

Because the wire format has changed three times in about 18 months, **the pipeline works on a version-neutral model**, and each wire version is a small adapter. A new spec revision then means a new adapter, not a pipeline change.

## 3. Architecture

```
 Clients:  site widget   AI agents (HTTP)   MCP clients   later: AG-UI / A2UI clients
              │                │                 │                  │
     ┌────────▼────────────────▼──┐   ┌──────────▼────────┐  ┌──────▼──────────┐
     │ HTTP binding /ask, /await  │   │ MCP binding       │  │ AG-UI writer    │
     │ JSON or SSE                │   │ ask / await tools │  │ (later)         │
     └────────────┬───────────────┘   └──────────┬────────┘  └──────┬──────────┘
                  └── wire adapters (v0.1, v0.5x) → NLWebAskRequest ┘
                                       │
                 INLWebAskService.AskAsync() → IAsyncEnumerable<NLWebEvent>
                                       │  ordered INLWebAskStep pipeline
  validate → resolve site → decontextualize* → analyze* (itemType/filters/elicit)
  → retrieve → hydrate + authorize → build schema.org → rank* → mode* (summarize/generate)
                                       │                       (* = NLWeb.AI feature)
       ┌───────────────────────────────┼─────────────────────────────┐
  INLWebRetriever (keyed)       IStructuredDataManager         CrestApps AI
  • index  (OC ISearchService)  (CrestApps.OrchardCore.Seo)    (IAICompletionService,
  • vector (AI data sources)                                    utility deployment,
  • hybrid (RRF)                                                ITemplateService prompts)
```

The response writers are keyed by transport and `response_format`: JSON aggregate, SSE, MCP `CallToolResult`, `chatgpt_app`, and later AG-UI and A2UI. Every writer consumes the same event stream.

## 4. `CrestApps.OrchardCore.Seo` — the structured data engine

### Projects (repo conventions)

- `src/Abstractions/CrestApps.OrchardCore.Seo.Abstractions`: `IStructuredDataProvider`, `IStructuredDataManager`, `StructuredDataContext`, `StructuredDataPurpose`, `JsonLdGraph`, `JsonLdNode` helpers, `SchemaOrgTypes` constants and `ISchemaActionProvider`.
- `src/Core/CrestApps.OrchardCore.Seo.Core`:
  - `DefaultStructuredDataManager` and the graph merger (merges on `@id`, drops duplicates, resolves `@id` references).
  - A safe serializer that escapes `<`, `>` and `&` inside `<script>`. OC currently writes `GoogleSchema` raw.
  - Validation rules per type, such as Google's required properties for rich results.
- `src/Modules/CrestApps.OrchardCore.Seo`: the module.

### Data model: JSON, not typed classes

schema.org has about 800 types, and both JSON-LD and NLWeb extend it through `@context`. Typed libraries such as Schema.NET lock us to one vocabulary and pull in a very large assembly. Nodes are therefore `JsonObject`s, with a thin fluent helper on top.

```csharp
public enum StructuredDataPurpose { Page, Answer }   // Answer = NLWeb / agents (may add body text, actions)

public sealed class StructuredDataContext
{
    public ContentItem ContentItem { get; }          // null for site/route-level pages
    public StructuredDataPurpose Purpose { get; }
    public JsonLdGraph Graph { get; }                // nodes keyed by @id
    public JsonObject MainEntity { get; set; }       // node that represents ContentItem
    public string BaseUrl { get; }                   // from site settings, works outside a request
}

public interface IStructuredDataProvider
{
    int Order => 0;
    Task BuildAsync(StructuredDataContext context);  // add or enrich nodes; never replace others' work
}

public interface IStructuredDataManager
{
    Task<JsonLdGraph> BuildPageGraphAsync(ContentItem primary, CancellationToken ct = default);
    Task<JsonObject> BuildEntityAsync(ContentItem item, StructuredDataPurpose purpose, CancellationToken ct = default);
}
```

### Features

There is one feature, `CrestApps.OrchardCore.Seo`. It holds the engine, the **Structured data** site settings, the page renderer, and the schema.org type and property mapping on content types.

Everything that relies on another module lights up by itself through `[RequireFeatures]` startups, so nobody has to know about extra features:

- `[RequireFeatures("OrchardCore.Seo")]`: imports `SeoMetaPart.GoogleSchema` and suppresses OC's duplicate script.
- `[RequireFeatures("OrchardCore.Indexing")]`: adds the structured data fields to content indexes.
- Domain providers live in their own modules (Products, Reviews and so on), each in a `[RequireFeatures("CrestApps.OrchardCore.Seo")]` startup.

Later SEO additions such as `llms.txt` go into the same feature as settings, not as new features.

### Providers that ship

1. **Site:** `Organization` or `Person` (publisher, logo, `sameAs`), and `WebSite` with a `SearchAction` that points at `/search?terms={search_term_string}`. These come from site settings and render on every front-end page.
2. **Content type mapping:** set in the content type definition settings (`SchemaOrgTypeSettings`). It holds a `SchemaType` such as `Article` and a property map whose values are Liquid expressions, for example `headline: {{ Model.ContentItem.DisplayText }}`. Sensible defaults need no configuration:
   - `name` and `url` (absolute, through `ContentItemMetadata` and `BaseUrl`)
   - `datePublished`, `dateModified` and `inLanguage`
   - `description` from `SeoAspect.MetaDescription`
   - `image` from `SeoAspect.OpenGraphImage`
3. **Parts and fields:** a `ContentPartStructuredDataProvider<TPart>` base class, the same shape as `ContentPartIndexHandler<T>`.
   - TitlePart → `name`
   - Autoroute → `url`
   - HtmlBody / Markdown → `articleBody` (`Answer` purpose only, trimmed)
   - TaxonomyField → `keywords` and `about`
   - MediaField → `image`
   - ListPart → `isPartOf` and `ItemList`
   - Bag and Flow parts → `hasPart`
4. **`SeoMetaPart.GoogleSchema` import:** the editor's raw JSON-LD, after shortcode processing, is parsed and merged into the graph. Editors keep the field they know, and the page gets one script instead of two.
5. **Breadcrumbs:** `BreadcrumbList` from the ListPart container chain or the menu.
6. **Domain providers** live in their owning modules, in a `[RequireFeatures("CrestApps.OrchardCore.Seo")]` startup. This is the same pattern as `ProductListingSeoHandler`.
   - Products: `Product`/`ProductGroup`/`Offer`/`AggregateOffer`.
     - `ProductPart` gives `Sku`, `Price`, `Currency` and `SalePrice`.
     - `ProductVariantsPart` gives `hasVariant`.
     - `IPriceResolver` gives the price and `IProductAvailabilityProvider` gives `availability`.
     - `ProductFeedPart` gives `brand`, `gtin`, `mpn` and `itemCondition`. `ProductFeedBuilder` already does most of this mapping.
   - Reviews: `AggregateRating`, through `ProductRatingReader`.
   - Calendars and Bookings: `Event` and `Service`.
   - Companies: `Organization` and `LocalBusiness`.
   - Downloads: `SoftwareApplication` and `DigitalDocument`.
   - Fundraising: `DonateAction`.
   - Classifieds: `Product`/`Offer`.
7. **Actions** (`ISchemaActionProvider`, `Answer` purpose): NLWeb `actions[]`, such as `AddToCartAction` against the Commerce API or `ReserveAction` for Bookings. Only public, documented endpoints may be listed.

### Rendering on pages (no OC change)

- A scoped `IStructuredDataAccumulator` collects the primary content item. A `ContentDisplayDriver` captures it for the `Detail` display type of the first item, which is the same rule `SeoContentDriver` uses, and returns `null`.
- An `IAsyncResultFilter` emits one `<script type="application/ld+json">{"@context":"https://schema.org","@graph":[…]}</script>` through `RegisterHeadScript`. It runs for front-end `ViewResult`/`PageResult` only, skipping admin through `AdminAttribute.IsApplied`. This is the pattern `WebMcpScriptFilter` and OC's `FacebookPixelFilter` use. Because a result filter runs, site-level nodes also appear on pages that have no content item, such as the home page, lists and custom controllers.
- To avoid duplicate output, a `ContentHandlerBase.GetContentItemAspectAsync` handler for `SeoAspect` sets `aspect.GoogleSchema = null` after `SeoMetaPartHandler` has filled it. Our graph already contains that JSON (provider 4). This is ordinary aspect composition, the same mechanism `ProductListingSeoHandler` uses for `MetaRobots`. It goes away once upstream PR #1 lands.
- Theme authors get a `<structured-data for="…" />` tag helper and a `structured_data` Liquid filter for custom pages.

### Admin UX

- **Settings → Search engine optimization → Structured data:** organization, logo, social profiles, the WebSite search action, and on/off switches for page rendering.
- **Content type settings:** a schema.org type picker and a property map editor (Liquid) with the defaults shown.
- **Content item editor:** a "Structured data" preview tab that shows the generated JSON-LD and warnings for missing required properties.
- Recipe and deployment steps for the settings, plus a recipe schema in `CrestApps.OrchardCore.Recipes.Core`.

### Indexing (optional fast path, also used by NLWeb)

`StructuredDataDocumentIndexHandler : IDocumentIndexHandler` adds two fields to content indexes:

- `Content.StructuredData.Type`: a keyword, such as `Recipe` or `Product`.
- `Content.StructuredData.Json`: stored, not analyzed.

NLWeb can then filter by `itemType` at query time. On public indexes it can also skip loading every item from the database. Changing a type mapping queues a re-index of that content type.

## 5. `CrestApps.OrchardCore.NLWeb`

### Where the protocol code lives

The recommendation is to put the framework-neutral protocol in CrestApps.Core as `CrestApps.Core.NLWeb`: the wire models, version adapters, SSE framing, `INLWebAskService`, step and retriever contracts, and writers. The OC module adds the Orchard pieces: retrievers over `IndexProfile`, structured data, settings, permissions, admin and UI. This is the same split as `CrestApps.Core.AI.WebMcp` and `CrestApps.OrchardCore.AI.WebMcp`.

NLWeb is a top-level module, not `AI.*`, because list mode works with no AI at all.

### Features

There is one feature, `CrestApps.OrchardCore.NLWeb`. It depends on `CrestApps.OrchardCore.Seo`, which is enabled automatically. It provides:

- `/ask` (JSON + SSE) and `/await`
- list mode
- sites (sub-corpora)
- permissions
- settings
- the "Ask this site" UI

Everything else lights up through `[RequireFeatures]` startups as soon as the needed module is on:

| Startup gate | What it adds |
|---|---|
| `[RequireFeatures("OrchardCore.Search")]` | The keyword retriever through keyed `ISearchService`, which covers Lucene, Elasticsearch and Azure AI. |
| `[RequireFeatures(AIConstants.Feature.Area)]` | Decontextualization, LLM ranking with a per-item `description`, and `summarize`/`generate` modes. It also registers `ask` as an AI tool (`AddCoreAITool`), so the MCP server, WebMCP and AI profiles can use it. |
| `[RequireFeatures(AIConstants.Feature.DataSources)]` | The vector and hybrid retrievers over AI data sources. |
| `[RequireFeatures("OrchardCore.Cors")]` / `("OrchardCore.RateLimits")` | Applies the CORS policy and rate limit policy picked in settings. |

The admin page warns when no retriever is available, for example "enable a search provider or AI data sources".

Later: `/await` promises, an `ask` skill on the A2A agent card, the `chatgpt_app` format, the Who endpoint, and AG-UI/A2UI writers.

### Canonical model

```csharp
public sealed class NLWebAskRequest
{
    public NLWebQuery Query { get; init; }          // Text, Sites, ItemType, Location, Price, Filters (JsonObject: unknown members kept)
    public NLWebContext Context { get; init; }      // Type ("ConversationalContext"), Prev, Text, Memory
    public NLWebPreferences Prefer { get; init; }   // Streaming, ResponseFormat, Mode, Language, UserAgent
    public NLWebMeta Meta { get; init; }            // Version, ConversationId, SessionContext (JsonObject)
}

public abstract record NLWebEvent;
public sealed record NLWebStarted(NLWebResponseMeta Meta) : NLWebEvent;
public sealed record NLWebResult(int Index, JsonObject Item, double? Score) : NLWebEvent;
public sealed record NLWebText(string Text) : NLWebEvent;
public sealed record NLWebElicitation(string Text, IReadOnlyList<NLWebQuestion> Questions) : NLWebEvent;
public sealed record NLWebPromise(string Token, int EstimatedSeconds) : NLWebEvent;
public sealed record NLWebFailure(string Code, string Message) : NLWebEvent;
public sealed record NLWebCompleted(NLWebResponseMeta Meta) : NLWebEvent;

public interface INLWebProtocolAdapter   { string Version { get; } bool CanRead(NLWebWireRequest r); NLWebAskRequest Read(NLWebWireRequest r); }
public interface INLWebResponseWriter    { bool CanWrite(NLWebResponseTarget t); Task WriteAsync(IAsyncEnumerable<NLWebEvent> events, NLWebResponseTarget t, CancellationToken ct); }
public interface INLWebAskStep           { int Order { get; } Task ExecuteAsync(NLWebAskContext context, CancellationToken ct); } // may short-circuit with an elicitation or failure
public interface INLWebRetriever         { string Name { get; } Task<IReadOnlyList<NLWebCandidate>> RetrieveAsync(NLWebRetrievalRequest r, CancellationToken ct); }
public interface INLWebAskHandler        { Task AskedAsync(NLWebAskedContext context); } // analytics, auditing (like OC's ISearchHandler)
```

### Pipeline steps

1. **Validate:** `query.text` is required. Length limits, a cap on `prev` entries and a request size limit apply.
2. **Resolve site:** `query.site` maps to an NLWeb *site*, which is a sub-corpus record holding an index profile, a retriever, allowed content types or schema types, and a result cap. Without a `site`, the default site is used. Tenants are already isolated from each other.
3. **Decontextualize** (AI): when `context.prev`/`text` is present, an LLM rewrites the query into a standalone query. Results are cached per conversation.
4. **Analyze** (AI): infers `itemType` from the schema types this site actually publishes and extracts filters. Elicitation is opt-in per site.
5. **Retrieve:**
   - `index` uses `ISearchService` keyed by `IndexProfile.ProviderName`.
   - `vector` uses data source embeddings.
   - `hybrid` merges the two with reciprocal rank fusion.
   - Retrieval over-fetches about 3×N because `ISearchService` cannot filter (see upstream PR #3).
6. **Hydrate and authorize:**
   - Load published items in one batch.
   - Check `ViewContent` for the *caller*, who is anonymous by default. ContentAccessControl rules apply.
   - Filter by culture.
   - Drop items whose types are not allowed for the site.
7. **Build schema.org:** call `IStructuredDataManager.BuildEntityAsync(item, Answer)`. The step ensures `@context`, `url` and `name` are present, then adds `grounding` (url, `dateModified`, source) and `actions`.
8. **Rank:**
   - Without AI, retriever order is used.
   - With AI, an LLM scores relevance in parallel within a bounded budget and writes the item's `description`. Items below a threshold are dropped.
   - Results stream out as soon as they are ranked.
9. **Mode:**
   - `list`: nothing more.
   - `summarize`: an LLM summary of the top results, written as `_meta.summary` or as a text content item, depending on the writer.
   - `generate`: a RAG answer that cites result URLs only. URLs come from the structured data, never from the model.
10. **Handlers:** analytics, AI usage metering through the existing `AIUsageScope`, and audit.

All prompts are AI templates (`Templates/Prompts/*.md` + `AITemplateIds` + `ITemplateService`), as AGENTS.md requires. They run on the utility deployment by default, or on a chosen AI profile. Ranking and analysis use typed structured output (`GetResponseAsync<T>`).

### HTTP binding

- Minimal APIs in `Startup.Configure`, as in `WebMcp/Endpoints`:
  - `routes.MapPost("ask", …)` and `MapPost("await", …)`.
  - Optionally `MapGet("ask", …)` for v0.1 query-string clients.
  - `.DisableAntiforgery()`, plus the CORS policy from the OC Cors module and a rate limiter from OC RateLimits.
- Paths are configurable but default to the tenant root `/ask` and `/await`, because that is where validators and agents look.
- Content negotiation:
  - `Accept: text/event-stream` or `prefer.streaming=true` gives SSE.
  - Otherwise the response is JSON.
  - The protocol version comes from `meta.version`, defaulting to the newest adapter.
- Errors return 200 with a `failure` envelope. Exceptions are 429 with `Retry-After`, and 400 only for a body that is not JSON.

### MCP binding: a decision for us

The CrestApps MCP server (`routes.MapMcp("mcp")`) uses one authentication mode for every tool (`McpServerAuthenticationType`: `None` | `ApiKey` | `OpenId`). Public NLWeb needs an anonymous `ask` without exposing admin tools. There are two clean options:

- **A (recommended):** add an "optional authentication" mode to the MCP server. Callers that present credentials authenticate. Callers that present none run as Anonymous. Tool visibility is decided per tool by the existing `IAIToolAccessEvaluator`, and only tools whose permission Anonymous holds are listed (`ask`, `await`). This also suits future public tools.
- **B:** NLWeb maps its own small JSON-RPC endpoint (`initialize`, `tools/list`, `tools/call` for `ask`/`await` only) at `nlweb/mcp`. It also maps `/mcp` when the MCP server feature is off. This is simpler, but it is a second MCP surface to maintain.

### Settings, permissions, admin

- **Site settings group "NLWeb":**
  - endpoints on/off, paths, legacy GET support
  - allow anonymous
  - CORS policy and rate limit policy names
  - default and allowed modes and formats, max results
  - AI deployment or profile
  - whether AI steps are on
- **Admin list:** **NLWeb sites** (sub-corpora) as catalog records on the standard `AdminList` (name, index profile, retriever, content types, cap), following CLAUDE.md and the admin-index skill.
- **Permissions:** `QueryNLWeb`, granted to Anonymous through a stereotype and needed by `/ask` and the tools, and `ManageNLWeb`.
- Recipe and deployment steps for the settings and sites.

### Discovery

There is no standard discovery yet. We will do what is defined today:

- An `ask` skill on the A2A agent card (`/.well-known/agent-card.json`) when the A2A host is on.
- An entry in `llms.txt` (the Seo.LlmsTxt feature).
- An optional, clearly labelled non-standard `<link rel="nlweb" href="/ask">`.

We will not invent a `.well-known` path.

## 6. A2UI / AG-UI readiness (do not build yet, keep the seams)

- **AG-UI** (agent ↔ UI runtime events over SSE). The ask pipeline is already an `IAsyncEnumerable<NLWebEvent>`, so an AG-UI writer is a mapping:
  - `NLWebStarted` → `RUN_STARTED`
  - `NLWebText` → `TEXT_MESSAGE_START/CONTENT/END`
  - `NLWebResult` → `STATE_DELTA` (JSON Patch append to `/results`)
  - `NLWebCompleted` → `RUN_FINISHED`
  - `NLWebFailure` → `RUN_ERROR`

  The AI chat should get the same transport later. That points to a shared `CrestApps.Core.AI.AGUI` package rather than one inside NLWeb.
- **A2UI** (declarative UI: `createSurface`, `updateComponents`, `updateDataModel`). A2UI results are UI for a schema.org `@type`. The seam is an `ISchemaTypePresenter` registry keyed by `@type` and output kind:
  - Orchard shape (`NLWebResult__Recipe`)
  - ChatGPT Apps widget (`openai/outputTemplate`)
  - A2UI component tree

  Data stays in JSON-LD and presentation stays in presenters. This is why JSON-LD must remain the single canonical form.
- What must hold now:
  1. Event-stream pipeline.
  2. Writers keyed by transport and `response_format`.
  3. A presenter registry keyed by schema type.
  4. No HTML inside result objects.

## 7. Security and quality

- Only published content that the caller may view is returned. Tenants are isolated. Culture is respected.
- Prompt injection: the query text and retrieved content are untrusted. Use CrestApps prompt security templates. Model output never supplies URLs or actions, which come only from structured data.
- Cost and abuse controls:
  - rate limits, timeouts and token budgets
  - a cap on `prev` entries and request size
  - AI steps off by default, and decontextualization cached
- JSON-LD XSS: serialize with escaping. OC's `GoogleSchema` path writes the processed string raw, so a `</script>` inside it would break out of the script tag.
- Logs pass user input through `SanitizeLogValue()`.

## 8. Upstream Orchard Core PRs (optional, each removes one interim step)

1. **Composable JSON-LD in OrchardCore.Seo.**
   - Move `SeoAspect` to `OrchardCore.Seo.Abstractions`.
   - Add a structured JSON-LD collection to `SeoAspect`, keeping the `GoogleSchema` string for compatibility.
   - Have `SeoContentDriver` render one merged, escaped `@graph`.
   - This removes our "null out GoogleSchema" handler.
2. **Register the search service with the provider feature.**
   - Today `AddSearchService<LuceneSearchService>` (and the Elasticsearch and Azure AI equivalents) is gated on `RequireFeatures("OrchardCore.Search")`.
   - The search service is a provider capability; the Search feature is only the `/search` UI.
   - This removes NLWeb.IndexSearch's dependency on `OrchardCore.Search`.
3. **A richer `ISearchService` contract.**
   - Add `SearchAsync(IndexProfile, SearchRequest)` with filters (content types, culture, extra fields), scores and stored-field projection.
   - A default interface method falls back to the term-only overload.
   - This removes over-fetching and gives real scores.
4. **Absolute content URLs outside a request.** Add a helper that builds a content item's absolute URL from `ContentItemMetadata` and the site `BaseUrl`, without `IHttpContextAccessor`. Indexing and background work need it. `SeoMetaPartHandler` needs `HttpContext` today.

## 9. Testing

- **Unit tests:**
  - Wire adapters against the NLWeb_Core fixtures (`test_v054_request.json`, `test_v054_minimal.json`) and v0.1 query strings.
  - Golden JSON for every writer, and SSE framing.
  - Graph merging, `@id` dedupe and escaping.
  - Type mapping defaults.
- **Integration tests:** a test site with a Lucene index and published, draft and restricted items:
  - `/ask` in JSON and SSE
  - anonymous vs. authenticated results
  - MCP `tools/list` and `tools/call`
- **External checks:** Google Rich Results Test and validator.schema.org on pages, and the Lumar agentic-readiness NLWeb checks on `/ask` and `/mcp`.

## 10. Phases (one branch/PR for the plan, one commit per slice)

1. **Seo engine:**
   - abstractions, core and module
   - site, content type mapping, part and field, and GoogleSchema import providers
   - page renderer and duplicate suppression
   - settings, type mapping editor and preview
   - recipes, deployment steps and docs
2. **Domain providers:** Products/Offers/Reviews, Events (Calendars, Bookings), Organization, breadcrumbs and actions.
3. **NLWeb core:**
   - `CrestApps.Core.NLWeb` (model, v0.5x and v0.1 adapters, JSON and SSE writers, pipeline)
   - the OC module (`/ask`, keyword retriever, hydrate and authorize, list mode)
   - sites admin list, permissions, CORS and rate limits
   - the structured data index handler
4. **NLWeb.AI:** decontextualize, analyze, LLM rank and descriptions, summarize and generate, vector and hybrid retrievers, opt-in elicitation.
5. **NLWeb.Mcp + widget + A2A skill:** the MCP server's optional-auth mode (option A).
6. **Later:** `/await` promises, `chatgpt_app`, Who, AG-UI and A2UI writers.

In parallel: the upstream OC PRs 1–4.

## 11. Decisions needed (original list; see section 0 for what was decided)

1. Should the protocol layer live in CrestApps.Core (`CrestApps.Core.NLWeb`, recommended) or only in this repo?
2. MCP exposure: option A (optional auth on the existing MCP server, recommended) or option B (a dedicated NLWeb MCP endpoint)?
3. Endpoint paths: tenant-root `/ask` and `/await` (recommended, for conformance) or a prefix such as `/nlweb/ask`?
4. Which upstream OC PRs to open now. PR #1 and PR #2 are small and low-risk.
