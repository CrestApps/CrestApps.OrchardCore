---
title: AI Tool Instances
description: Configure reusable AI tool instances from registered sources and expose each one to the AI model as its own function.
---

# AI Tool Instances

An **AI tool instance** is a user-configured tool built from a developer-registered *tool instance source*. A source describes the shape of a capability once (for example, "call an HTTP API"), and administrators then create as many named instances of that source as they need. Each instance is exposed to the AI model as its own function, with its own name, description, permission, and settings.

This is the difference between a tool and a tool instance:

| Concept | Registered by | Cardinality | Example |
| --- | --- | --- | --- |
| Tool | Developer, in code | One function per tool | `search_content_items` |
| Tool instance source | Developer, in code | Blueprint only, never callable directly | `http-api-request` |
| Tool instance | Administrator, in the admin UI | Many per source, one function each | `Order Lookup API`, `Shipping Rates API` |

## Enabling the feature

Enable the **AI Tool Instances** feature (`CrestApps.OrchardCore.AI.ToolInstances`). It depends on the **AI Services** feature and adds the **Artificial Intelligence → Tool Instances** admin menu entry.

## Managing tool instances

Navigate to **Artificial Intelligence → Tool Instances**.

1. Select **Add Tool Instance**. A modal lists every registered source.
2. Pick a source. The editor renders the fields the source contributes.
3. Provide a **Name** and a **Description**. These two fields are always rendered first and are required for every source.
4. Fill in the source-specific fields and save.

The **Name** must be unique across all tool instances and cannot be changed after the instance is created, because it is used to derive the function name that the AI model calls. Function names are prefixed and sanitized automatically, so `Order Lookup API` becomes something like `tool_instance_order_lookup_api`.

The **Description** matters more than it looks. When several instances share the same source, the description is the only thing the AI model uses to decide which instance to call, so describe the concrete purpose of the instance rather than the source it was built from.

## The HTTP API request source

The feature always registers the built-in `http-api-request` source, so a usable source is available out of the box. It issues an HTTP request to a configured endpoint and returns the response to the AI model. It captures:

- **Base URL** — the absolute HTTP or HTTPS URL the request targets.
- **HTTP method** — `GET`, `POST`, `PUT`, `PATCH`, or `DELETE`.
- **Timeout** — the per-request timeout in seconds. Leave empty to use the default.
- **Headers** — static headers always added to the request, expressed as a JSON object such as `{ "Accept": "application/json" }`.
- **Model provided values** — whether the AI model may supply a relative path, query string parameters, or a request body. Disable anything the instance should not expose.
- **Authentication type** — `None`, `API Key`, `Bearer Token`, `Basic`, or `OAuth 2.0`. The credential fields shown below the selector change to match the selected type.

All secrets (API key, token, password, and client secret) are encrypted with ASP.NET Core data protection before they are stored. When you edit an existing instance, leaving a secret field empty keeps the previously stored value.

## The documentation search sources

The feature also registers three built-in sources that turn a public documentation site into a searchable tool instance, so the AI model can answer questions from product or framework documentation without indexing it into a vector store. Each configured instance binds one site and is exposed to the model as its own function, so you can offer "search the CrestApps docs" and "search the Orchard Core docs" as two distinct tools.

Pick the source that matches how the site publishes its content:

| Source | Best for | How it works |
| --- | --- | --- |
| **Documentation search (sitemap)** (`sitemap-documentation`) | Any site that publishes a `sitemap.xml`, such as Docusaurus, MkDocs, and most static sites. | Crawls pages, strips the HTML, and ranks passages locally with keyword scoring. |
| **Documentation search (search index)** (`search-index-documentation`) | MkDocs Material and other sites that publish a fetchable `search_index.json`. | Downloads the prebuilt index once and ranks its entries locally. |
| **Documentation search (Algolia)** (`algolia-documentation`) | Docusaurus sites (and others) wired to hosted Algolia DocSearch. | Forwards the query to Algolia, which performs the ranking. |

All three sources carry the **Knowledgebase** category. Each source captures its own fields:

- **Sitemap** — a **Base URL** (the site root, for example `https://core.crestapps.com`), an optional **Sitemap URL** (defaults to `{BaseUrl}/sitemap.xml`), an optional **Maximum results**, and an optional **Maximum pages**.
- **Search index** — a **Base URL** (used to resolve relative links and the default index URL), an optional **Index URL** (defaults to `{BaseUrl}/search/search_index.json`), and an optional **Maximum results**.
- **Algolia** — an **Application id**, a **search-only API key** (never a write key), an **Index name**, and an optional **Maximum results**. The API key is encrypted with ASP.NET Core data protection before it is stored; when you edit an existing instance, leaving the API key field empty keeps the previously stored key.

The first search materializes the corpus (the crawled pages or the downloaded index) and caches it. Later searches reuse the cache until the instance settings change.

These instances are usable anywhere tool instances are — on a profile, a chat interaction, or exposed to external clients through the [MCP server](mcp/server#tool-exposure).

## Assigning instances to a profile

Open an **AI Profile** (or an **AI Profile Template** of the *Profile* source) and go to the **Capabilities** tab. The **Tool Instances** section lists every instance the current user is allowed to access. Selected instances are passed to the AI model alongside the profile's regular tools.

Because AI profile templates copy their properties onto the profiles created from them, instances selected on a template are inherited by every profile created from that template.

## Assigning instances to a chat interaction

Open a **Chat Interaction** and go to the **Capabilities** tab. The **Tool Instances** section works exactly like the profile editor and lists every instance the current user is allowed to access. Cloning an interaction carries the selected instances over to the copy.

## Using instances during post-session processing

AI profiles and profile templates that use post-session processing can also invoke tool instances while analyzing a closed conversation. Open the **Data Processing & Metrics** tab, then open the post-session **Capabilities** tab, where tools and tool instances are selected together.

This selection is stored separately from the **Capabilities** selection, so an instance that a live conversation may call is not automatically available during post-session analysis, and vice versa. Selecting only tool instances is enough to run post-session processing through the tool-enabled path; no regular tools are required.

## Permissions

The feature adds two permissions:

| Permission | Description |
| --- | --- |
| `ManageAIToolInstances` | Create, edit, and delete the tool instances the user owns. |
| `ManageAIToolInstancesCreatedByOthers` | Also manage instances created by other users. Implies `ManageAIToolInstances`. |

Only `ManageAIToolInstances` is ever checked to decide whether the user may reach the management surface. The ownership check is applied afterwards, so a user who does not hold `ManageAIToolInstancesCreatedByOthers` may still manage their own instances.

In addition, every configured instance produces a dynamic `AccessAITool_{functionName}` permission, exactly like a regular AI tool. The feature replaces the default tool instance registry with a permission-aware one, so an instance is only surfaced to the AI model when the current user is authorized for that instance.

## Registering a custom source

A tool instance source is an `IAIToolInstanceSource` that turns an `AIToolInstance` into an `AITool`. Register it with `AddAIToolInstanceSource<TSource>` from your own feature's startup:

```csharp
using CrestApps.Core.AI.Tooling.Instances;

[RequireFeatures(AIConstants.Feature.ToolInstances)]
public sealed class MySourceStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIToolInstanceSource<WeatherToolInstanceSource>("weather", options =>
        {
            options.DisplayName = S["Weather Lookup"];
            options.Description = S["Looks up the forecast for a configured region."];
        });
    }
}
```

Register sources with `AddAIToolInstanceSource` rather than calling `AddToolInstances(...)` yourself. Source registration never decides registry policy, whereas `AddToolInstances` defaults to `useDefaultRegistry: true` and would add the built-in registry provider alongside the permission-aware one this feature installs, surfacing every instance to the model regardless of permissions.

To capture the fields your source needs, add a display driver for `AIToolInstance` and gate it on the source name:

```csharp
internal sealed class WeatherToolInstanceDisplayDriver : DisplayDriver<AIToolInstance>
{
    public override IDisplayResult Edit(AIToolInstance instance, BuildEditorContext context)
    {
        if (!string.Equals(instance.Source, "weather", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Initialize<WeatherToolInstanceViewModel>("WeatherToolInstance_Edit", model =>
        {
            var settings = instance.GetOrCreate<WeatherToolSettings>();

            model.Region = settings.Region;
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIToolInstance instance, UpdateEditorContext context)
    {
        if (!string.Equals(instance.Source, "weather", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var model = new WeatherToolInstanceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        instance.Put(new WeatherToolSettings
        {
            Region = model.Region,
        });

        return Edit(instance, context);
    }
}
```

Register the driver with `services.AddDisplayDriver<AIToolInstance, WeatherToolInstanceDisplayDriver>();`. Use `Content:1` for the shared name and description fields, and anything after it for source-specific fields, so the shared fields always render first.

## Exposing the selector on your own model

`AIToolInstancesDisplayDriverBase<TModel>` renders the instance picker for any `ExtensibleEntity`, filtering the list down to the instances the current user may access. Derive from it to add the selector to your own model:

```csharp
internal sealed class MyModelToolInstancesDisplayDriver : AIToolInstancesDisplayDriverBase<MyModel>
{
    public MyModelToolInstancesDisplayDriver(
        ISourceCatalog<AIToolInstance> instancesCatalog,
        IAIToolAccessEvaluator toolAccessEvaluator,
        IHttpContextAccessor httpContextAccessor)
        : base(instancesCatalog, toolAccessEvaluator, httpContextAccessor)
    {
    }

    protected override string EditorLocation => "Content:7#Capabilities;9";
}
```

The following members are available for customization:

| Member | Purpose |
| --- | --- |
| `EditorShapeType` | The shape rendered for the picker. Defaults to `EditToolInstances_Edit`. Override it to match your editor's layout. |
| `EditorLocation` | Where the picker is placed. Defaults to `Content:7#Capabilities;9`. |
| `CanHandle(TModel)` | Skips the picker entirely for models it should not apply to. |
| `GetSelectedInstanceNames(TModel)` / `SetSelectedInstanceNames(TModel, string[])` | Where the selection is read from and written to. Defaults to `AIToolInstanceMetadata`, which the framework already honors when building the completion context. Override both when the selection belongs somewhere else. |

Register the driver from a startup class gated with `[RequireFeatures(AIConstants.Feature.ToolInstances)]` so it only appears when the feature is enabled.

## Related

- [AI Tools](tools)
- [AI Profiles](overview)
- [AI Profile Templates](profile-templates)
