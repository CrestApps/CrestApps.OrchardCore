---
sidebar_label: NLWeb
title: NLWeb (Natural Language Questions)
description: Let people and AI agents ask the site questions in natural language with the NLWeb protocol, and answer with content items described as schema.org objects.
user_manual:
  - user-manual/administration/nlweb
---

| | |
| --- | --- |
| **Module** | `CrestApps.OrchardCore.NLWeb` |
| **Feature** | NLWeb (`CrestApps.OrchardCore.NLWeb`) |
| **Category** | Search |
| **Depends on** | [Advanced SEO](advanced-seo.md) (`CrestApps.OrchardCore.Seo`), enabled with it |

[NLWeb](https://github.com/nlweb-ai/NLWeb) is an open protocol that lets agents ask a web site questions in natural language. It is being standardized in a W3C Community Group. The site answers with the matching items as schema.org objects.

This module implements NLWeb for Orchard Core:

- **Endpoints.** It answers at the root of the tenant: `POST /ask`, `GET /ask` (the original protocol), and `POST /await`.
- **Search.** It searches with Orchard Core's search providers, and with AI data sources by meaning.
- **Results.** It describes each result with the same schema.org data the content item's page publishes, through the [Advanced SEO](advanced-seo.md) structured data engine.

The settings screen is described in the User Manual under [NLWeb](../user-manual/administration/nlweb.md).

## One feature, with the rest switching on by itself

| Enabled module | What it adds |
| --- | --- |
| *(always)* | `/ask` and `/await`, list mode, the content item resolver, the settings and the permissions |
| Orchard Core **Search** (`OrchardCore.Search`) | The keyword retriever over the search indexes you select. It covers Lucene, Elasticsearch and Azure AI Search. Their search services are only registered while Orchard Core's Search feature is on. |
| **AI** (`CrestApps.OrchardCore.AI`) | Query rewriting for follow-up questions, AI ranking, `summarize` and `generate` modes, and the `ask` AI tool. |
| **AI Data Sources** (`CrestApps.OrchardCore.AI.DataSources`) | The vector retriever. It searches an AI data source's knowledge base index by meaning. |
| Orchard Core **Rate Limits** (`OrchardCore.RateLimits`) | Limits each visitor (IP address) to 30 questions a minute, in the `NLWeb` rate limit group. |

## Protocol support

The protocol has changed between versions, so requests are read into one model whatever version sent them (`NLWebRequestReader`):

| Version | Request | Response |
| --- | --- | --- |
| 0.54 and 0.55 (current) | `POST /ask`, body `{ query: { text, site, itemType, ... }, context: { prev, text, memory }, prefer: { streaming, response_format, mode, accept-language }, meta: { version, session_context } }` | `{ _meta: { response_type, version, ... }, results: [ schema.org objects ], content: [ { type: "text", text } ] }` |
| 0.5 | `POST /ask`, the same body with `return_response` instead of `prefer` | The same as 0.54 |
| 0.1 (original) | `GET /ask?query=&site=&prev=&mode=&streaming=&query_id=`, or the same fields at the top of a JSON body | `{ query_id, results: [ { url, name, site, score, description, schema_object } ] }` |

Notes on the response:

- `_meta.response_type` is `answer`, `elicitation`, `promise` or `failure`.
- `_meta.summary` holds the summary in `summarize` mode.
- `_meta.session_context` is echoed back to the caller for its next request.
- A failure response has an `error` object with a `code` and a `message`.

### Streaming

A request is streamed as server-sent events when its `streaming` preference is `true`. Without that preference, it is streamed when the `Accept` header holds `text/event-stream`. The events are:

| Event | Data |
| --- | --- |
| `start` | `{ "_meta": { "response_type": "answer", "version", "query_id", "conversation_id" } }` |
| `result` | `{ "index", "item" }`, one per result |
| `content` | `{ "type": "text", "text" }`, the summary or the answer |
| `elicitation` / `failure` | The metadata and the elicitation or the error |
| `complete` | `{ "_meta": { "total_items", "processing_time_ms", "session_context" } }` |
| `end` | `{}` |

### Status codes

| Response | Status |
| --- | --- |
| An answer, or a failure to answer | 200, with a `failure` response for the failure |
| A body that is not JSON, or a query with no text | 400 |
| A caller without the **Ask the site questions with NLWeb** permission | 401 (anonymous) or 403 |
| Too many requests, when Rate Limits is enabled | 429 |

`POST /await` answers `UNKNOWN_PROMISE`, because the site answers every question at once.

### Examples

```bash
curl -X POST https://example.com/ask \
  -H "Content-Type: application/json" \
  -d '{"query":{"text":"vegetarian recipes with cinnamon","itemType":"Recipe"},"prefer":{"mode":"summarize"},"meta":{"version":"0.54"}}'
```

```bash
curl -N -X POST https://example.com/ask \
  -H "Content-Type: application/json" -H "Accept: text/event-stream" \
  -d '{"query":{"text":"with cinnamon?"},"context":{"prev":["vegetarian muffin recipes"]}}'
```

## How a question is answered

`NLWebAskService` runs these steps and produces a stream of events (`NLWebEvent`). The JSON, server-sent event and AI tool responses are all written from that stream.

1. **Query processors** (`INLWebQueryProcessor`, by `Order`):
   - The settings processor caps the results, picks the mode and maps `query.site` to a search index.
   - The AI query rewriter rewrites a follow-up question with the conversation's context.

   A processor can end the question with a failure or an elicitation.
2. **Retrievers** (`INLWebRetriever`). Every enabled retriever finds candidates, three times as many as the results. When more than one runs, their lists are merged with reciprocal rank fusion.
3. **The resolver** (`INLWebResultResolver`):
   - loads the published content items;
   - keeps those the caller may view (`ViewContent`) and those of the allowed content types;
   - describes each one with `IStructuredDataManager.BuildEntityAsync(item, StructuredDataPurpose.Answer)`.

   When the question asks for a schema.org type, results of other types are dropped, unless none has that type.
4. **Rankers** (`INLWebResultRanker`). The AI ranker scores each result from 0 to 100 and drops those under 25. A ranker that fails leaves the order as it was.
5. **Generators** (`INLWebResponseGenerator`). In `summarize` or `generate` mode, they write the text of the answer.
6. **Handlers** (`INLWebAskHandler`). They are told about every question, for analytics or auditing.

The protocol layer is the `CrestApps.OrchardCore.NLWeb.Core` project, which has no Orchard Core dependency. Any module can add a retriever, a processor, a ranker, a generator or a handler by registering the interface.

## AI

The AI steps use the deployment picked in the settings, or the default utility deployment. They record usage under the `NLWeb` purpose. Their prompts are templates you can override like any other prompt template:

| Template | Used to |
| --- | --- |
| `nlweb-decontextualize` | Rewrite a follow-up question to stand on its own, and guess the schema.org type asked for |
| `nlweb-rank` | Score the results |
| `nlweb-summarize` | Write the summary (`summarize` mode) |
| `nlweb-generate` | Write the answer, citing the URLs of the results (`generate` mode) |

### The ask tool

The `ask` AI tool takes the same `query`, `context` and `prefer` arguments as `POST /ask`, and returns the NLWeb JSON response. It answers as the user who calls it. Select it like any AI tool:

- on the **MCP Server** card, to offer it to MCP clients (see [MCP Server](../ai/mcp/server.md));
- on the **WebMCP** card;
- in an AI profile.

## Settings

`NLWebSettings` is a site setting:

```json
{
  "name": "Settings",
  "NLWebSettings": {
    "IndexProfileNames": [ "Main", "Blog" ],
    "ContentTypes": [],
    "MaxResults": 10,
    "DefaultMode": "list",
    "AllowLegacyRequests": true,
    "AllowCrossOrigin": true,
    "RewriteQueries": true,
    "RankResults": false,
    "AllowSummarize": true,
    "AllowGenerate": true,
    "DeploymentName": null,
    "DataSourceId": null
  }
}
```

A caller picks one of the selected indexes by its name in `query.site`. Without a match, the first index is used.

## Permissions

| Permission | Key | Default roles |
| --- | --- | --- |
| Ask the site questions with NLWeb | `QueryNLWeb` | Anonymous, Authenticated, Administrator |
| Manage the NLWeb settings | `ManageNLWebSettings` | Administrator |

Answers only include the published content items the caller may view. With **Allow calls from other sites**, the endpoints send `Access-Control-Allow-Origin: *`. Browsers then send no credentials, so a cross-site call always gets the answer an anonymous visitor would get.
