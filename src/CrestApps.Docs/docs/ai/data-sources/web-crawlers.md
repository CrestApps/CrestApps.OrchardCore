---
sidebar_label: Web Crawlers
sidebar_position: 5
title: AI Data Sources - Web Crawlers
description: A Web AI data source populated by strategy-based web crawlers that scrape public websites and index each page into the AI Knowledge Base for RAG.
---

| | |
| --- | --- |
| **Feature Name** | AI Data Sources - Web Crawlers |
| **Feature ID** | `CrestApps.OrchardCore.AI.DataSources.WebCrawlers` |

Adds a **Web** AI data source that is populated by strategy-based web crawlers. Crawlers scrape public websites (starting with sitemap discovery), clean each page to text, and index it into the AI Knowledge Base for Retrieval-Augmented Generation (RAG).

## Overview

Unlike the other data sources, which read from an index or database you already maintain, the **Web** source builds its knowledge base by scraping live websites. A **Web** data source is a target bucket, and one or more **web crawlers** point at it. Each crawler owns a site and a crawl **strategy**:

- **Web data source** — A knowledge-base target with no connection settings of its own. It aggregates every enabled crawler that targets it.
- **Web crawler** — A configured site to scrape: a strategy (for example **Sitemap**), the strategy's settings, and the target Web data source. Many crawlers can point at one data source.
- **Crawl strategy** — Defines how a site is discovered and how a page is fetched and cleaned. The built-in strategy is **Sitemap**; the model is extensible, so future strategies (for example depth-limited link following) plug in without changing the data source or the UI.

Each scraped page becomes one knowledge-base document keyed by its URL, and the URL is kept for citations.

## Getting Started

1. **Enable the feature** — Enable **AI Data Sources - Web Crawlers** in the Orchard Core admin dashboard. This also enables the **AI Data Sources** feature it depends on.
2. **Create a Knowledge Base Index** — In **Search > Indexing**, add an **AI Knowledge Base Index** (Elasticsearch or Azure AI Search) with an embedding connection configured. See the [AI Data Sources overview](index.md) for the embedding requirements.
3. **Add a Web data source** — Under **Artificial Intelligence > Data Sources**, click **Add Data Source**, choose **Web**, then configure the destination knowledge-base index and field mappings. The Web source itself has no connection settings.
4. **Add a web crawler** — Under **Artificial Intelligence > Web Crawlers**, click **Add Web Crawler**, choose the **Sitemap** strategy, and configure the site to scrape and its target Web data source.
5. **Synchronize** — Use the **Synchronize now** action on a crawler to scrape immediately, or wait for the hourly background task to re-index crawlers that are due.

## Managing Web Crawlers

The **Web Crawlers** admin screen mirrors the other source-based catalogs (AI Templates, Data Sources): a searchable list, an **Add Web Crawler** button that opens a modal of available strategies, and an **Actions** menu per crawler.

### Shared fields

| Field | Description |
| --- | --- |
| **Name** | A human-readable name that identifies the crawler in the list. |
| **Target Web data source** | The Web AI data source whose knowledge base receives the scraped pages. Multiple crawlers can target the same data source. |
| **Enabled** | Disabled crawlers are skipped by the hourly re-index task and are not read during a full data source synchronization. |
| **Re-index interval (minutes)** | How often the background task re-crawls this site. Leave empty to use the global default (24 hours). The task evaluates crawlers hourly, so intervals are honored to the nearest hour. |

### Sitemap strategy settings

| Field | Description |
| --- | --- |
| **Base URL** | The site to scrape. The crawler discovers the sitemap from `robots.txt` and the conventional locations (for example `/sitemap.xml`). Provide this or an explicit sitemap URL. |
| **Sitemap URL** | An explicit sitemap or sitemap-index URL. When set, discovery starts here. Supports nested sitemap indexes, gzip, plain-text, and RSS/Atom feeds. |
| **Max pages** | The most pages to scrape per crawl. Default: 500. |
| **Max concurrent requests** | How many pages are fetched in parallel. Default: 4. Keep this low to be polite to the target site. |
| **Request timeout (seconds)** | Per-request fetch timeout. Default: 30. |
| **Include URL patterns** | One regular expression per line. A discovered page is scraped only when its full URL matches **at least one** pattern. Empty allows every discovered page. |
| **Exclude URL patterns** | One regular expression per line. A page is skipped when its URL matches **any** pattern. Applied **after** the include patterns, so exclude wins. |
| **User-Agent** | The `User-Agent` header sent while crawling. Leave empty to use the default. |

### Include and exclude patterns

Both boxes take one **regular expression** per line and are matched against the full page URL.

- **Include** acts as an allow-list. When empty, every discovered page is eligible. When set, a page must match at least one include pattern to be scraped. For example, `^https://example\.com/docs/` keeps only pages under `/docs/`.
- **Exclude** acts as a deny-list and runs after include, so exclusions win. For example, `/tag/|/author/` drops taxonomy and author pages, and `\.pdf$` drops PDF links.

Invalid regular expressions are rejected in the editor, and a crawler must define a base URL or an explicit sitemap URL before it can be saved.

## Synchronization

- **Manual** — The **Synchronize now** action on a crawler re-crawls just that site: it discovers the current pages, diffs them against the recorded crawl state, and queues only the new, changed, and removed pages for indexing into the target data source. Unchanged pages are skipped.
- **Scheduled** — An hourly Orchard background task (`WebCrawlerReindexBackgroundTask`) evaluates every enabled crawler and re-indexes the ones whose re-index interval is due. Disabled crawlers are skipped.
- **On save** — Creating, updating, or deleting a crawler queues a full synchronization of its target Web data source so the knowledge base stays aligned.

Because a crawler tracks per-page crawl state (last-modified, change-frequency, and a content hash), re-indexing is incremental: only pages that actually changed are re-fetched and re-embedded, and a transient block that returns no pages leaves the existing knowledge base untouched rather than wiping it.

## Citations

Scraped pages are indexed with their URL kept as the knowledge-base reference id, and the feature registers a link resolver for the **Web** reference type so citations link back to the original page. See [AI Data Sources — Citation & Reference Tracking](index.md#citation--reference-tracking) for how `[doc:N]` markers are produced.

## Storage

The feature persists crawlers and per-page crawl state in the AI YesSql collection through the shared `CrestApps.Core` stores (`IWebCrawlerStore` and `IWebCrawlStateStore`). Migrations create the `WebCrawlerIndex` and `WebCrawlStateIndex` tables via the Core schema builders (`CreateWebCrawlerIndexSchemaAsync` and `CreateWebCrawlStateIndexSchemaAsync`).

## Permissions

The feature adds the **Manage web crawlers** (`ManageWebCrawlers`) permission, granted to the Administrator role by default. It controls access to the entire Web Crawlers admin area, including the synchronize action.
