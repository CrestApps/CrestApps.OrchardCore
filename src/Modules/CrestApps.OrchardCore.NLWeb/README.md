# CrestApps.OrchardCore.NLWeb

Lets people and AI agents ask the site questions in natural language with the [NLWeb](https://github.com/nlweb-ai/NLWeb) protocol. The site answers at `/ask` with the matching content items, each described with the schema.org object its page publishes (from the Structured Data module).

## Feature

One feature, **NLWeb** (`CrestApps.OrchardCore.NLWeb`). It depends on Structured Data. Everything else switches on by itself through `[RequireFeatures]` startups:

| Enabled module | What it adds |
| --- | --- |
| *(always)* | `POST /ask` (protocol 0.5x, JSON or server-sent events), `GET /ask` (original protocol), `POST /await`, settings and permissions |
| Orchard Core Search | Keyword search of the selected Lucene, Elasticsearch or Azure AI Search indexes |
| AI | Follow-up question rewriting, AI ranking, `summarize` and `generate` modes, and the `ask` AI tool for the MCP server, WebMCP and AI profiles |
| AI Data Sources | Search by meaning in an AI data source's vector index, merged with the keyword results |
| Orchard Core Rate Limits | 30 questions a minute for each visitor |

## Configuration

**Settings > Search > NLWeb**: the search indexes and content types to answer from, the number of results, the default mode, cross-site calls, the original protocol, and, with AI, the deployment and the AI steps.

## Extending

The protocol and the ask pipeline live in `CrestApps.OrchardCore.NLWeb.Core`, which has no Orchard Core dependency. Register an `INLWebRetriever`, `INLWebQueryProcessor`, `INLWebResultRanker`, `INLWebResponseGenerator` or `INLWebAskHandler` to extend it.

See the [NLWeb](https://orchardcore.crestapps.com/docs/modules/nlweb) documentation.
