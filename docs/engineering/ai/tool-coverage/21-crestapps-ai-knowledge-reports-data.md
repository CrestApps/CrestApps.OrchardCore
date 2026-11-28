# CrestApps: AI knowledge, reports and data platform

Part of the [AI tool coverage plan](README.md). Nothing in this file is built yet. The design rules, the permission and approval model and the test plan that apply to every tool below are in [01-foundation.md](01-foundation.md) and [03-testing.md](03-testing.md); the final agent roster is in [02-agents.md](02-agents.md) and wins over the agent notes in this file.

Scope: 25 module folders under `src/Modules/`. None of them registers an AI tool today (`AddCoreAITool`, `AIToolsStartup` and `AIFunction` have no match in any of the 25 folders). All tools below are new and would live in each module's own `AIToolsStartup` gated by `[RequireFeatures("CrestApps.OrchardCore.AI")]`.

Conventions used in this section:

- `listX` returns the full non-secret settings of each entry when the entry is small (data source, file source, crawler, connection, webhook) and takes an optional `id` to return one entry; a separate `getX` exists only where the detail is much larger than a list row (report, connected data set, content transfer entry).
- `saveX` creates when no `id` is given and otherwise merges the given values into the existing entry (the pattern `AISourceCatalogApi` already uses: validate a merged copy, then `UpdateAsync(item, body)`).
- Secret fields are write-only: a tool never returns a secret value, only whether one is stored (the editors already expose `HasPassword`, `HasApiKey` and similar flags). An empty secret keeps the stored one, as the editors and the recipe steps do.
- Kind `External` is used for operations whose cost is unbounded (re-embedding a whole source, crawling a site) or that send data to a third party. A single read against an external system that an administrator already configured (test a connection, preview a live data set) is `Read`. See Open questions.

## AI Data Sources — `CrestApps.OrchardCore.AI.DataSources` (dependency only), source providers `CrestApps.OrchardCore.AI.DataSources.AzureAI`, `CrestApps.OrchardCore.AI.DataSources.Elasticsearch`, `CrestApps.OrchardCore.AI.DataSources.PostgreSQL`

**What an admin can do in the UI:**

- Artificial Intelligence → Data Sources (`ai/data-sources`, `DataSourcesController`): list with search and paging; Add (pick a source type from `AIDataSourceSourceOptions.Sources`: `SearchIndexProfile`, `AzureAISearch`, `Elasticsearch`, `PostgreSQL`, and `Web` / `File` when Web Crawlers / File Sources are enabled); Edit; Delete; bulk Delete; Sync index per row (queues a background synchronization of the knowledge base index).
- Data source editor: name (`DisplayText`), destination AI knowledge base index profile (`AIKnowledgeBaseIndexProfileName`, an index profile of type `DataSourceConstants.IndexingTaskType`), key/title/content field mapping (required for `SearchIndexProfile`, `AzureAISearch`, `Elasticsearch`, `PostgreSQL`), then the provider's own fields: source index profile; Azure AI Search endpoint, authentication type, index name, API key, identity client id, or a data connection; Elasticsearch environment, URL or cloud id, authentication, index name, user name/password/API key/Base64 API key/key id/certificate fingerprint, or the default connection, or a data connection; PostgreSQL connection string and table name, or the default connection string. When `AIDataSourceExtensions.IsConfigurationLocked` is true only the name can be changed.
- Settings → Artificial Intelligence → Data Sources tab: default strictness (clamped 1–5) and default top N documents (clamped 3–20).
- AI profile and AI profile template editors: attach data sources to a profile (`AIProfileDataSourceDisplayDriver`; belongs to the AI profile area).
- Search → Indexing: the providers add the index source "AI Knowledge Base Index (Azure AI Search)" / "(Elasticsearch)" (belongs to the Orchard Core indexing area).
- AI tool instances: a "data source search" tool instance source (`AddDataSourceSearchSource`, when the Tool Instances feature is on).
- Background task "AI Data Source Alignment", daily at 02:00 (`IAIDataSourceIndexingService.SyncAllAsync`).
- REST API when the AI Chat WebAPI feature is on: `api/ai/v1/data-sources` list/get/create/update/delete (`MapAISourceCatalogApi<AIDataSource>`), and `GET ai/data-sources/fields/{indexProfileName}` for the field picker.

**Already covered:** recipe step `AIDataSource` (create or update by item id; usable through `importOrchardCoreRecipe`, schema `AIDataSourceRecipeStep` when the CrestApps Recipes feature is on) and the deployment step of the same name. Nothing lists, deletes, synchronizes or test-searches a data source. The framework's `SearchDataSources` system tool (`DataSourceSearchTool`) searches the data sources of the profile in the conversation; it is not an admin tool.

**Settings groups:** `AIDataSourceSettings` (`CrestApps.Core.AI.Models`) / group `ai-settings` / permission `ManageAIProfiles` (the driver and `AddSiteSettingsPermission` both use it, not `ManageAIDataSources`) / secrets: none.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listAIDataSources` | Read | no | `ManageAIDataSources` | Lists the AI data sources (knowledge bases) with their source type, destination index and field mapping, with optional name search, source type filter, paging, or one data source by id. Also returns the source types that can be created on this site. Secrets are never returned. | AIKnowledgeAgent |
| `saveAIDataSource` | Security | yes | `ManageAIDataSources` | Creates an AI data source of a given source type, or updates an existing one by id, from the same JSON its `AIDataSource` recipe entry takes. Use it after `listAIDataSources` shows the source types; an existing data source whose configuration is locked can only be renamed. Prefer a data connection or the default connection over typing credentials. | AIKnowledgeAgent |
| `deleteAIDataSource` | Destructive | yes | `ManageAIDataSources` | Deletes an AI data source. Profiles, web crawlers and file sources that point at it stop having knowledge to search; this cannot be undone. | AIKnowledgeAgent |
| `syncAIDataSource` | External | yes | `ManageAIDataSources` | Queues a full synchronization of one data source into its AI knowledge base index, which re-reads the source and generates embeddings in the background. Use it when answers are stale; it costs embedding tokens in proportion to the size of the source. | AIKnowledgeAgent |
| `searchAIDataSource` | Read | no | `ManageAIDataSources` | Runs a test vector search of one data source for a query and returns the best matching chunks with their titles, scores and references. Use it to check what a knowledge base returns before or after a change; it makes one embedding call. | AIKnowledgeAgent |

**Implementation notes:**

- Services: `ISourceCatalogManager<AIDataSource>` (`PageAsync`, `FindByIdAsync`, `NewAsync(source, JsonNode)`, `ValidateAsync`, `CreateAsync`, `UpdateAsync(model, JsonNode)`, `DeleteAsync`), `IOptions<AIDataSourceSourceOptions>` for the source types, `IAIDataSourceIndexingQueue.QueueSyncDataSourceAsync` for the sync (exactly what the `SyncIndex` action calls). `AISourceCatalogApi` (in `CrestApps.OrchardCore.AI.Core/ApiKeys`) already shows the JSON create/merge-update/validate flow to copy.
- Field mapping: the suggested key/title/content fields come from `AIDataSourceOptions.GetFieldMapping(providerName, type)`, and the list of index fields is computed inside the private static `GetDataSourceFieldsEndpoint.GetFieldNamesFromProfile`. Extract that into a service so `saveAIDataSource` can default the mapping and return the valid field names in its validation error instead of adding a separate "list fields" tool.
- Secrets: the provider editors hold API keys, passwords and connection strings (`EditAzureAISearchAIDataSourceViewModel.ApiKey`, `EditElasticsearchAIDataSourceViewModel.Password/ApiKey/Base64ApiKey`, `EditPostgreSQLAIDataSourceViewModel.ConnectionString`). The REST API returns the stored model as is; whether those values are protected or stripped in that JSON is unverified, so the list tool must mask them explicitly.
- `searchAIDataSource`: reuse the retrieval the framework tool uses (`DataSourceRetrieval.SearchDetailedAsync`, called from `DataSourceSearchToolFunction` in `CrestApps.Core`); its exact signature is unverified.
- Deleting: the controller only calls `DeleteAsync`; whether the documents are removed from the knowledge base index by a catalog handler is unverified (`IAIDataSourceIndexingQueue.QueueDeleteDataSourceAsync` exists for it).
- The three provider modules add no menu, controller or permission of their own; they are fully reached through the tools above.

**Test prompts:**

- "Which knowledge bases do we have and which index does each one write to?"
- "Create a data source named Product Docs from the Articles search index and store it in the KnowledgeBase index."
- "Re-sync the Help Center data source."
- "What does the Product Docs knowledge base return for 'refund policy'?"

## AI Documents — `CrestApps.OrchardCore.AI.Documents` (dependency only), `CrestApps.OrchardCore.AI.Documents.Profiles`, `CrestApps.OrchardCore.AI.Documents.ChatSessions`, `CrestApps.OrchardCore.AI.Documents.ChatInteractions`

**What an admin can do in the UI:**

- AI profile editor → Knowledge tab (feature `...Documents.Profiles`, `AIProfileDocumentsDisplayDriver`): upload documents the profile answers from, remove documents, download a document; set top N, retrieval mode, maximum indexable characters and "describe figures in uploads" for the profile. The same card exists on AI profile templates.
- AI profile editor (feature `...Documents.ChatSessions`): allow session documents and allow session image uploads.
- Chat interactions and chat sessions: end users upload and remove their own documents through endpoints (`ChatInteractionUploadDocument`, `ChatSessionUploadDocument` and their remove counterparts). Not an admin action.
- Settings → Artificial Intelligence → Documents tab: index profile for AI documents, retrieval mode, allow document uploads, allow image uploads, maximum indexable characters, describe figures in uploads. Saving a change releases the tenant (`IShellReleaseManager.RequestRelease`).
- Index profile editor: extra fields for the AI documents index type (`ChatInteractionIndexProfileDisplayDriver`; Orchard Core indexing area).
- REST API when the AI Chat WebAPI feature is on: `api/ai/v1/profiles/{profileId}/documents` GET, POST (multipart), DELETE.

**Already covered:** nothing for documents. `viewAIProfile` shows a profile; the per-profile knowledge parameters (top N, retrieval mode and so on) are profile fields and belong to whatever "save AI profile" tool the AI profile area proposes.

**Settings groups:** `InteractionDocumentSettings` (`CrestApps.Core.AI.Documents.Models`) / group `ai-settings` / permission `ManageChatInteractionSettings` / secrets: none. A change reloads the tenant.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listAIProfileDocuments` | Read | no | `ManageAIProfiles` | Lists the documents an AI profile answers from, with file name, type and size. Use it to see what knowledge a profile has before adding or removing a document. | AIKnowledgeAgent |
| `addAIProfileDocument` | Write | no | `ManageAIProfiles` | Adds a file from the media library to an AI profile's documents: extracts its text, splits it into chunks, generates embeddings and indexes it. The file type must be one the site can read (for example .pdf and .docx need their reader features), and a documents index must be configured in the AI settings. | AIKnowledgeAgent |
| `removeAIProfileDocument` | Destructive | yes | `ManageAIProfiles` | Removes one document from an AI profile and deletes its chunks from the index. The profile stops answering from it and the stored file is gone. | AIKnowledgeAgent |

**Implementation notes:**

- The add and remove logic exists twice, inline, in `AIProfileDocumentsDisplayDriver.UpdateAsync` and in `AIProfileDocumentsApiEndpoints` (`UploadAsync`, `DeleteAsync`). Extract one service (add from a stream, remove by document id) before adding a third copy in a tool. Pieces: `IAIProfileManager`, `DocumentsMetadata` on the profile, `IAIDocumentProcessingService.ProcessFileAsync` (takes an `IFormFile`, so the media stream must be wrapped), `IAIDocumentStore`, `IAIDocumentChunkStore`, `ProfileDocumentChunkIndexer.ScheduleIndexing`, `ChatDocumentsOptions.EmbeddableFileExtensions`, and the deferred chunk removal from every `AIDocumentsIndexingTaskType` index profile.
- Source of the file: a media library path is the only source a tool can reach today. Taking a file the administrator attached to the chat would need the chat session document store to hand the stream over (unverified whether the admin widget keeps attachments).
- Adding a document makes embedding calls for one file; it is classed Write, not External (see Open questions).

**Test prompts:**

- "Which documents does the Support Assistant profile use?"
- "Add /docs/returns-policy.pdf from the media library to the Support Assistant profile."
- "Remove the old price list from the Sales Assistant's documents."

## AI File Sources — `CrestApps.OrchardCore.AI.FileSources`, connectors `CrestApps.OrchardCore.AI.FileSources.Ftp`, `CrestApps.OrchardCore.AI.FileSources.Sftp`

**What an admin can do in the UI:**

- Artificial Intelligence → File Sources (`ai/file-sources`, `FileSourcesController`): list with search and paging; Add (pick a connector from `IngestionConnectorOptions.Connectors`: the tenant's file system folder, FTP, SFTP); Edit; Delete (knowledge base cleanup is queued); bulk Delete; Run (reads the folder now and reports items read, discovered, removed and failed).
- File source editor: name, the File AI data source it feeds (`AIDataSourceId`), enabled, re-index interval in minutes, figure mode, vision deployment, utility deployment, maximum figure descriptions per document, maximum items per run, language. File system connector: root path (always inside the tenant's own `file-sources` folder, `ITenantFileSourceRoot`), recursive, maximum items. FTP: host, port, user name, password, encryption mode, data connection type, validate any certificate, timeouts, retry attempts, remote root path, recursive, maximum items. SFTP: host, port, user name, password, private key, passphrase, proxy type/host/port/user name/password, connection timeout, keep-alive interval, remote root path, recursive, maximum items.
- Data source editor: adds the `File` source type (`FileAIDataSourceDisplayDriver`).
- Background task "File Source Ingestion", hourly (`IFileSourceScheduler.RunDueAsync`).
- REST API when the AI Chat WebAPI feature is on: `api/ai/v1/file-sources`.

**Already covered:** recipe step `FileSources` (entries in `Sources`; usable through `importOrchardCoreRecipe`, schema `FileSourcesRecipeStepSchema`) and its deployment step, which exports file sources without passwords and keys. Nothing lists, runs or deletes a file source.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listAIFileSources` | Read | no | `ManageFileSources` | Lists the file sources that feed File AI data sources, with connector, data source, enabled state, schedule and connector settings, with optional name search or one file source by id. Also returns the connectors enabled on this site. Passwords and keys are never returned. | AIKnowledgeAgent |
| `saveAIFileSource` | Security | yes | `ManageFileSources` | Creates a file source for a connector (file system, FTP or SFTP), or updates one by id, from the JSON its `FileSources` recipe entry takes. It needs an existing File AI data source; a file system path can only be inside the tenant's own file-sources folder. | AIKnowledgeAgent |
| `deleteAIFileSource` | Destructive | yes | `ManageFileSources` | Deletes a file source and queues the removal of everything it ingested from the knowledge base. | AIKnowledgeAgent |
| `runAIFileSource` | External | yes | `ManageFileSources` | Reads a file source's folder now and ingests new and changed files, returning how many items were discovered, indexed, removed and failed. It connects to the folder or server and spends embedding and vision tokens, and can take minutes on a large folder. | AIKnowledgeAgent |

**Implementation notes:**

- Services: `ISourceCatalogManager<FileSource>` (`GetAllAsync`, `FindByIdAsync`, `NewAsync`, `ValidateAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`), `IOptions<IngestionConnectorOptions>` for connectors, `IIngestionRunService.RunAsync(fileSource, ct)` which returns a `FileSourceRunSummary` (status, discovered, indexed, deleted, failed, `DiscoveryCompleted`, error).
- `RunAsync` runs inline in the request in the UI. A tool call on a large folder will exceed a chat turn; run it as a background job and return the summary when it is short, or a "started" answer otherwise. Whether the last run's summary is stored on the file source is unverified, so `listAIFileSources` may not be able to show "last run" without a change.
- Secrets: FTP password; SFTP password, private key, passphrase and proxy password. They must be write-only in the tool.
- The FTP and SFTP modules only register a connector and its editor; they need no tools of their own.

**Test prompts:**

- "Which folders are we ingesting into the knowledge base, and how often?"
- "Add a file source that reads the 'manuals' folder into the Manuals data source every 12 hours."
- "Run the Policies file source now and tell me how many files failed."

## AI Web Crawlers — `CrestApps.OrchardCore.AI.WebCrawlers`

**What an admin can do in the UI:**

- Artificial Intelligence → Web Crawlers (`ai/web-crawlers`, `WebCrawlersController`): list with search and paging; Add (pick a crawl strategy; sitemap is the one registered); Edit; Delete; bulk Delete; Sync (re-crawls the site now and queues the new, changed and removed pages for indexing, reporting the three counts).
- Crawler editor: name, the Web AI data source it feeds, enabled, re-index interval in minutes; sitemap strategy: base URL, sitemap URL, maximum pages, maximum concurrent requests, request timeout, user agent, include URL patterns, exclude URL patterns.
- Data source editor: adds the `Web` source type (`WebAIDataSourceDisplayDriver`).
- Background task "Web Crawler Re-index", hourly (`IWebCrawlerReindexService.ReindexDueAsync`).
- REST API when the AI Chat WebAPI feature is on: `api/ai/v1/web-crawlers`.

**Already covered:** recipe step `WebCrawlers` (entries in `Crawlers`; schema `WebCrawlersRecipeStepSchema`) and its deployment step. Nothing lists, runs or deletes a crawler.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listAIWebCrawlers` | Read | no | `ManageWebCrawlers` | Lists the web crawlers that feed Web AI data sources, with site, sitemap, limits, URL patterns, data source, enabled state and schedule, with optional name search or one crawler by id. | AIKnowledgeAgent |
| `saveAIWebCrawler` | Write | no | `ManageWebCrawlers` | Creates a web crawler for a public website, or updates one by id, from the JSON its `WebCrawlers` recipe entry takes. It needs an existing Web AI data source; nothing is crawled until the crawler is due or `runAIWebCrawler` is called. | AIKnowledgeAgent |
| `deleteAIWebCrawler` | Destructive | yes | `ManageWebCrawlers` | Deletes a web crawler. Its pages stop being refreshed in the knowledge base. | AIKnowledgeAgent |
| `runAIWebCrawler` | External | yes | `ManageWebCrawlers` | Re-crawls one website now and queues its new, changed and removed pages for indexing, returning the three counts. It sends requests to the website and spends embedding tokens for every changed page. | AIKnowledgeAgent |

**Implementation notes:** `ISourceCatalogManager<WebCrawler>` for CRUD; `IWebCrawlerReindexPlanner.PlanAndEnqueueAsync(crawler, ct)` for the run (returns `Status`, `Message`, `NewCount`, `ChangedCount`, `RemovedCount`; `DiscoveryFailed` and `NoPagesDiscovered` are errors). What deleting a crawler does to pages already indexed is unverified.

**Test prompts:**

- "Which websites are we crawling for the assistant?"
- "Crawl https://docs.example.com from its sitemap into the Docs data source, at most 500 pages, every day."
- "Re-crawl the documentation site now."

## Data Connectors — `CrestApps.OrchardCore.DataConnectors`, `CrestApps.OrchardCore.DataConnectors.Http`, `CrestApps.OrchardCore.DataConnectors.Elasticsearch`, `CrestApps.OrchardCore.DataConnectors.Files`, `CrestApps.OrchardCore.DataConnectors.SqlServer`

**What an admin can do in the UI:**

- Tools → Data Connectors → Connections (`data-connectors/connections`, `DataConnectionsController`): list with search; Add (pick a connector from `IDataConnectorManager.GetConnectors()`: HTTP API, Elasticsearch, Media files, SQL Server); Edit; Save and test; Test; Delete (refused while a data set reads the connection).
- Connection editor: display name, technical name, description, then the connector's fields. HTTP/Elasticsearch: base URL, timeout, headers, authentication type (API key with name, location and prefix; Basic; bearer token; OAuth 2.0 / OpenID Connect with token URL or authority, client id, client secret, scope, audience, grant type, client authentication). Media files: folder. SQL Server: server, database, authentication, user name, password, encrypt, trust server certificate, read-only intent, connect timeout.
- Tools → Data Connectors → Data Sets (`data-connectors/data-sets`, `ConnectedDataSetsController`): list with search; Add (pick a connection); Edit; Discover fields; Preview rows; History (refresh runs); Refresh (imported data sets only); Delete (also drops the imported rows and the run history).
- Data set editor: display name, technical name, description, resource (picked from what the connection discovers, such as tables and views), access mode Live or Imported, enabled, fields (name, source name, display name, type, identifier, included), default date field, refresh interval in minutes, refresh mode Full or Incremental with its incremental field, schema change policy Accept or Fail; connector fields: HTTP method, body, records path, format, pagination; Elasticsearch query and scroll minutes; relational query and command timeout; media file delimiter, header row, sheet name.
- A data set appears to reports and data pipelines through the `ConnectedData` (live) and `ImportedData` (imported) data sources.
- Background task "Connected Data Set Refresh", every minute (`IDataSetRefreshService.RefreshDueAsync`).
- Not in the UI: limits and network rules come from `CrestApps:DataConnectors` and `CrestApps:DataConnectors:Network` in configuration.

**Already covered:** recipe steps `DataConnection` (may carry `Secrets`) and `ConnectedDataSet` (entries in `DataSets`), usable through `importOrchardCoreRecipe`, and the two deployment steps of the same names. Nothing tests a connection, lists resources, previews, refreshes or deletes.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listDataConnections` | Read | no | `ManageDataConnections` | Lists the connections to external systems (HTTP APIs, Elasticsearch, SQL Server, media files) with their connector and non-secret settings, with optional name search or one connection by id. Also returns the connectors enabled on this site. Secret values are never returned, only which secrets are stored. | DataConnectorsAgent |
| `saveDataConnection` | Security | yes | `ManageDataConnections` | Creates a connection for a connector, or updates one by id: name, description, the connector's settings and its secrets. A connection's connector cannot be changed, and an empty secret keeps the stored one. | DataConnectorsAgent |
| `testDataConnection` | Read | no | `ManageDataConnections` | Checks that a saved connection reaches its system and returns success or the error message. Use it after saving a connection or when a data set stops refreshing. | DataConnectorsAgent |
| `deleteDataConnection` | Destructive | yes | `ManageDataConnections` | Deletes a connection and its stored credentials. It is refused while any data set still reads the connection. | DataConnectorsAgent |
| `listDataConnectionResources` | Read | no | `ManageConnectedDataSets` | Lists what a connection offers to read, such as the tables and views of a database or the files of a folder. Use it to pick the `resource` of a new data set. | DataConnectorsAgent |
| `listConnectedDataSets` | Read | no | `ManageConnectedDataSets` | Lists the connected data sets with their connection, resource, access mode (live or imported), enabled state, row count, last refresh status and next refresh, with optional name search. | DataConnectorsAgent |
| `getConnectedDataSet` | Read | no | `ManageConnectedDataSets` | Returns one connected data set in full: fields, refresh settings, import state, connector settings, and optionally its latest refresh runs with rows read and errors. Use it before changing a data set or to explain why a refresh failed. | DataConnectorsAgent |
| `saveConnectedDataSet` | Write | no | `ManageConnectedDataSets` | Creates a data set on a connection, or updates one by id: resource or query, live or imported, fields, default date field and refresh schedule. Fields are read from the source when none are given, and a new imported data set starts its first import when the user may refresh data sets. | DataConnectorsAgent |
| `refreshConnectedDataSet` | Write | no | `RefreshConnectedDataSets` | Starts a refresh of an imported data set in the background and returns immediately. Only imported data sets are refreshed; a live one is read from its source every time. | DataConnectorsAgent |
| `previewConnectedDataSet` | Read | no | `ManageConnectedDataSets` and `ReadConnectedDataSet_{name}` (implied by `ReadAllConnectedDataSets`) | Returns the first rows of a connected data set with its field names. Use it to check that a data set reads what was intended; a disabled data set cannot be read. | DataConnectorsAgent |
| `deleteConnectedDataSet` | Destructive | yes | `ManageConnectedDataSets` | Deletes a connected data set with its imported rows and its refresh history. Reports and data pipelines that read it will find it missing. | DataConnectorsAgent |

**Implementation notes:**

- Services (all in `.Abstractions` / `.Core`, so a tool in the module can use them): `IDataConnectionStore`, `IConnectedDataSetStore`, `IDataSetRefreshRunStore.PageAsync`, `IDataSetRowStore.DeleteAllAsync`, `IDataConnectorManager`, `ConnectedDataReader` (`TestAsync`, `DiscoverAsync`, `DescribeAsync`), `IDataSetRefreshService.RefreshAsync`, `IDataSourceManager.GetDataSource("ConnectedData" | "ImportedData").ReadRowsAsync` for the preview, `IDataConnectorSecrets` for secrets, `ConnectedDataSetFields.Create/Merge` for field discovery.
- Logic that only exists in controllers or drivers and must be extracted first: connection and data set validation and mapping live in the display drivers (`DataConnectionDisplayDriver`, `HttpConnectionDisplayDriver`, `SqlServerConnectionDisplayDriver`, `ConnectedDataSetDisplayDriver` and the per-connector data set drivers) and bind from the form; `RequestRefreshAsync` (sets `Import.RefreshRequestedUtc`, then a deferred background job) is private in `ConnectedDataSetsController`; the "delete rows, prune runs, delete data set" sequence and the "refuse while data sets read it" check are in the controllers. The two recipe steps (`DataConnectionStep`, `ConnectedDataSetStep`) already map from JSON and are the better model for `saveX`.
- Risk: `ManageDataConnections` is declared `isSecurityCritical`. A connection stores credentials and defines where the site connects to, so saving one is Security with approval. `DataConnectorNetworkOptions` (configuration) restricts egress; the tool must go through the same reader so those rules apply.
- A live preview and a connection test call the external system. They are classed Read because the target was approved when the connection was saved.
- The SQL Server module only registers a connector and its connection editor; it needs no tools of its own.

**Test prompts:**

- "Which external systems are we connected to, and do they all still work?"
- "Add a data set for the dbo.Invoices table of the ERP connection, imported every hour."
- "Why did the last refresh of the Invoices data set fail?"
- "Show me the first rows of the Shipments data set."

## Content Transfer — `CrestApps.OrchardCore.ContentTransfer`, `CrestApps.OrchardCore.ContentTransfer.OpenXml`, `CrestApps.OrchardCore.ContentTransfer.Azure`

**What an admin can do in the UI:**

- Content → Import (`content-transfer-entries`, `AdminController.List`): list the import entries with filter by status and content type, sort, search and paging; bulk Remove; per entry Pause, Resume (pending, paused, failed or stalled imports), Delete, Download errors (the failed rows with an Errors column).
- Import a content type (`import/contents/{contentTypeId}`): see the importable columns, download a template file, upload a .csv file (or .xlsx with Content Transfer (OpenXml)) with the option to publish the imported items; the upload is chunked and the import runs in the background.
- Content → Export (`export/contents`): choose a content type and file format, optionally a partial export (created from/to, modified from/to, owners, published only, latest only, all versions) and options contributed by other modules; a small export downloads at once, a large one (above `ExportQueueThreshold`) is queued; list the user's own export entries, download a completed one, bulk Remove. An export dashboard (`export/dashboard`) lists the same entries.
- Content type editor: "Allow bulk import" and "Allow bulk export" (`ContentTypeTransferSettings`).
- Background tasks: "Imported Files Processor" (every minute), "Export Files Processor" (every 5 minutes), "Content Transfer Upload Cleanup" (hourly).
- Not in the UI: limits come from `CrestApps:ContentTransfer` in configuration (`ContentImportOptions`); Content Transfer - Azure Blob Storage moves the files to blob storage from `CrestApps:ContentTransfer:AzureBlobStorage`.

**Already covered:** the content tools create, update and export single items but not in bulk and not to a file. The `AllowBulkImport` / `AllowBulkExport` content type settings are type settings, so `applyContentTypeDefinitionFromRecipe` covers turning them on. No recipe or deployment step exists for transfers.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listContentTransferTypes` | Read | no | `ImportContentFromFile` or `ExportContentFromFile` (per content type) | Lists the content types the user may bulk import or bulk export, the file formats enabled, and, for one content type, the column names an import file takes and an export file contains. Use it before an import or export to know the columns. | ContentTransferAgent |
| `listContentTransferEntries` | Read | no | `ListContentTransferEntries` (imports), `ExportContentFromFile` (the user's own exports) | Lists bulk import or export entries with content type, file name, status, who started them and when, filtered by direction, status and content type, with paging. | ContentTransferAgent |
| `getContentTransferEntry` | Read | no | `ImportContentFromFile` for the entry's content type (import), `ExportContentFromFile` (own export) | Returns one import or export entry with its progress: total rows, processed, imported, failed rows with their error messages, and the download link of a completed export. Use it to report progress or explain failures. | ContentTransferAgent |
| `exportContentItems` | Write | no | `ExportContentFromFile` for the content type | Starts an export of all or a filtered part of a content type's items (created or modified date range, owners, published, latest or all versions) to a file and returns the entry with its download link once ready. The content type must allow bulk export. | ContentTransferAgent |
| `importContentItems` | Destructive | yes | `ImportContentFromFile` for the content type | Queues a bulk import of a .csv or .xlsx file from the media library into one content type, optionally publishing the items, and returns the entry to follow. Rows create or update content items in bulk and cannot be rolled back; the content type must allow bulk import. | ContentTransferAgent |
| `setContentImportState` | Write | no | `ImportContentFromFile` for the entry's content type | Pauses a running import or resumes a pending, paused, failed or stalled one. A resumed import continues from its last saved row. | ContentTransferAgent |
| `deleteContentTransferEntry` | Destructive | yes | `DeleteContentTransferEntries` | Deletes an import or export entry and its stored file in the background. Content items already imported are not removed. | ContentTransferAgent |

**Implementation notes:**

- Services: `IContentImportManager.GetColumnsAsync` (columns), `IContentTransferEntryManager` (`PauseImportAsync`, `ResumeImportAsync`, `MarkAsDeletingAsync`), `IContentTransferEntryAdminListQueryService.QueryAsync` (list), `IContentTransferFileStore`, `IEnumerable<IContentTransferFileFormatProvider>` (formats), `ContentTypeTransferSettings`, `ImportFileProcessStatsPart` on the entry (progress and up to 1000 error messages). Permissions are checked per content type with the content type name as the resource (`ContentTypeAuthorizationHandler`, dynamic `ImportContentFromFile_{type}` / `ExportContentFromFile_{type}`).
- Gap, export: the whole export decision and the inline writer live in `AdminController.DownloadExport` (build the `ContentTransferEntry`, run the `ExportRequest` option drivers, count, queue or stream). A tool should always create a queued entry (`TriggerExportProcessing`) and return its download route (`DownloadExportFile`, `export/dashboard/{entryId}/download`), which needs that creation code extracted from the controller. Exports contributed with option drivers (`IDisplayDriver<ExportRequest>`) bind from the form and cannot be set by a tool without a change.
- Gap, import: an import is created only by the chunked upload in `ImportPOST` (store the file, build the entry, copy the `ImportContent` properties, `TriggerImportProcessing`). A tool needs that extracted into a service that takes a stream, and a file source: a media library path is proposed. The "publish imported content" flag is bound by `ImportContentDisplayDriver` into the entry's properties.
- The tools check `DeleteContentTransferEntries` for every delete, single or bulk, and for an export also that the entry is the caller's own, as the export list does.
- The OpenXml module only adds the .xlsx format and the Azure module only swaps the file store; they need no tools.

**Test prompts:**

- "Export all published Blog Posts modified this year to Excel."
- "Which columns does the Product import file need?"
- "How is the product import going, and which rows failed?"
- "Import /imports/products-march.xlsx from the media library as Products and publish them."

## Reports — `CrestApps.OrchardCore.Reports`, export formats `CrestApps.OrchardCore.Reports.OpenXml`, `CrestApps.OrchardCore.Reports.Pdf`

**How reports and data sets are registered (from code):**

- A coded report is a class that implements `IReport` (static `Name`, `DisplayName`, `Description`, `Category`, `Permission`, optional `ExportPermission` and `DefaultDateRange`; `RunAsync(ReportContext)` returns a `ReportDocument`) and is registered with `services.AddReport<TReport>()`, which adds a `ReportDescriptor` to `ReportOptions`. A module that runs a family of reports with one class adds one `ReportDescriptor` per report with a factory. `IReportManager` lists descriptors (`GetReports`), finds one (`GetDescriptor(name)`) and creates the runner (`GetReport(name)`). There are about 54 registrations across 12 modules (AI, AI.Chat, ContactCenter, Downloads, Inventory, Omnichannel.Managements, Orders, PhoneNumbers.Verifications, Reviews, Shipping, Subscriptions, TimeTracker).
- A `ReportDocument` is an ordered list of `ReportSection` (kind `Metrics`, `Table`, `Bars` or `Chart`) with labelled metrics, columns and string rows, so every report can be flattened the same way.
- A coded report's filter is a `ReportFilter` (an `Entity` property bag with `ReportName`). Each filter is contributed by an `IDisplayDriver<ReportFilter>`: the built-in `ReportDateRangeFilterDisplayDriver` (keys `FromUtc`, `ToUtc`, `DateRangeKey`; presets such as `today`, `last7`, `last30`, `last90`, `thisWeek`, `all`, `custom`) and drivers in AI, AI.Chat, ContactCenter, Omnichannel.Managements and TimeTracker. A report can list the filter names it supports through `IReportFilterMetadata.FilterNames`, but the property keys, value types and allowed values are known only to each driver.
- Export formats implement `IReportExportFormat` (`Serialize(ReportDocument)` returns bytes) and are registered with `AddReportExportFormat<T>()`: `csv` is built in, `xlsx` comes from Reports (OpenXml), `pdf` from Reports (PDF). `IReportExportManager` lists and resolves them.
- Data sets (used by the Report Builder, not by coded reports) come from Orchard Core's `OrchardCore.DataSources` (`IDataSourceManager.GetDataSources()`, `IDataSource.GetDataSetsAsync`, `GetSchemaAsync`, `ReadAsync`). CrestApps modules expose their records by deriving from `IndexedReportDataSet<TDocument, TIndex, TRow>` (a YesSql map index read a page at a time, readable only with the permissions the data set names); 24 modules do. Other sources: `ReportViews` (saved report views), `ConnectedData` / `ImportedData` (Data Connectors), and Orchard Core's `Users` and `Contents`.

**What an admin can do in the UI:**

- Reports (top-level menu) → category → report (`Reports/view/{id}`, `ReportsController.Display`): pick a date range and the report's other filters, read the metric cards, tables, bars and charts. `Reports` lists every report the user may see.
- Export a report with its current filter (`Reports/view/{id}/export/{format?}`): CSV always; Excel and PDF when their features are on; a report can require an extra `ExportPermission`.
- Admin dashboard: a `ReportWidgetPart` widget shows a coded report (when the admin dashboard feature is on).

**Already covered:** `summarizeAIQuality` and `queryChatSessionMetrics` answer AI questions without the Reports framework; no tool lists, runs or exports a report. The dashboard widget is a content part, so the content tools cover placing one.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listReports` | Read | no | each report's own `ReportDescriptor.Permission`; designed reports: `ViewAllReportDesigns` checked on the report | Lists the reports the user may open: built-in reports with their category and description, and, when the Report Builder is on, designed reports with their group and owner. Supports a text search and a category or group filter. Use it first to find the report that answers a question. | ReportsAgent |
| `getReport` | Read | no | the report's `Permission`; designed: `ViewAllReportDesigns` (definition, versions and share links need `ManageAllReportDesigns` on the report) | Describes one report: what it shows, the filters it accepts with their allowed values, its default period, the export formats and whether the user may export. For a designed report it can also return the full definition, its saved versions and its share links. | ReportsAgent |
| `runReport` | Read | no | the report's `Permission`; designed: `ViewAllReportDesigns` on the report | Runs a report for a period and filter values and returns its headline numbers and its tables as columns and rows, capped at a row limit with the total row count. Use it to answer questions from a report; use `exportReport` when the user wants a file. | ReportsAgent |
| `exportReport` | Read | no | the report's `Permission` and its `ExportPermission` when it has one; designed: `ViewAllReportDesigns` and either the report's `AllowExport` or `ManageAllReportDesigns` | Exports a report for a period and filter values as CSV, Excel or PDF and returns a download link with the file name. Excel and PDF need their features to be enabled. | ReportsAgent |

**Implementation notes:**

- One set of four tools serves both kinds of report. The id is the coded report's `Name` or the designed report's `ItemId`; the tool tries `IReportManager.GetDescriptor` first and `ReportDesignService.FindAsync` second (only when the Builder feature is on), and says in the result which kind it is.
- Coded reports, run: `ReportTelemetry.RunCodedReportAsync(_reportManager.GetReport(name), descriptor, new ReportContext(filter), ct)`, then `ReportDisplayValueResolver.ResolveAsync(document)` so ids become names, then flatten the sections. Export: the same with `ReportRunMode.Export` and `IReportExportManager.GetFormat(name).Serialize(document)` (`ReportTelemetry.Serialize`).
- Designed reports, run: `ReportDesignRunner.RunAsync(design, filterValues, period, options)` returns a `ReportRunResult` with `Columns`, `Rows`, `RowCount`, `Errors`, `Warnings`, `Filters` (the exposed filters with id, label, control, data type, current values and options) and `Document`. `ReportWorkflowService.ToOutput` already turns a run into rows by column label and caps it at 1000 rows; reuse it. Export: `DesignedReportPresenter.ExportAsync(design, filterValues, format, ct)` returns a `ReportExportFile` (content, content type, file name). These overloads run the report with its owner's access, as the UI and the workflow tasks do, so the tool must check `ViewAllReportDesigns` on the design for the caller first. Pass a `ReportRunOptions.Source` for telemetry (`ReportRunSources` has no value for an assistant yet).
- Gap, coded report filters: `ReportsController.BuildFilterAsync` fills the filter with `IDisplayManager<ReportFilter>.UpdateEditorAsync`, which binds from the HTTP request. A tool has no form. The date range can be set directly (`filter.SetDateRange`, after the normalization that is `internal` in `ReportDateRangeFilterDisplayDriver.NormalizeAsync` and should move to a service). The other filters (AI profile, usage grouping, contact center, omnichannel, hours by category, QA cohort) have no machine-readable description: add a small contract next to `IReportFilterMetadata` (key, label, type, options) that each filter driver implements, so `getReport` can list a coded report's filters and `runReport` can set them. Until then `runReport` can only set the period on coded reports.
- Gap, the exported file: the tool has bytes, not a URL. Options: (a) return a link to the existing export route with the filter in the query string (designed reports: `ReportDesignsExport` with `ReportFilterValueBinder.FieldName(filterId)` names, `f.{filterId}`; coded reports: `ReportsExport`, whose query names depend on each filter driver's prefix, unverified), which re-checks permissions and stores nothing; (b) a shared short-lived download store for AI tools. See Open questions.
- Row cap and privacy: results go to the model, so cap rows (for example 200 by default, 1000 at most) and return `rowCount` and `truncated`. The data is what the user can already see in the UI.
- The OpenXml and PDF modules only register a format; they are reached through the `format` argument of `exportReport`.

**Test prompts:**

- "Show me sales by month for this year."
- "Which reports do we have about agents?"
- "Run the AI usage analytics report for last month and tell me the total cost."
- "Export the agents report for last week to Excel."

## Report Builder — `CrestApps.OrchardCore.Reports.Builder`

**What an admin can do in the UI:**

- Reports → Report Builder (or "Shared Reports" for a user who cannot design) (`reports/designs`, `ReportDesignsController`): list published reports and unpublished drafts with search and status filter; Run (`reports/designs/{id}`) with the report's exposed filters and click-to-filter visuals; Export; Clone; Delete; Delete an unpublished draft.
- The designer (`reports/builder/create`, `reports/builder/edit/{id}`): pick data sets from any data source, join them, add calculated fields with formula functions, filters (fixed or exposed to viewers), columns with aggregate, transform and format, sorts and a limit, extra named queries; add visuals (table, chart, metrics, pivot) and containers; preview; save a draft (autosave), discard a draft, list versions, view a version, restore a version; publish; choose the report group, pin the report to the admin menu, allow export, share with users and roles; create, list and revoke share links (recipients, expiry in days, maximum uses, allow export, note, notify by email).
- Reports → Report Views (`reports/views`): list saved views (reusable queries exposed as data sets through the `ReportViews` data source); create and edit in the designer; set a refresh interval for a stored snapshot; Refresh now; Delete.
- Reports → Report Groups (`reports/groups`): tree of groups with search; create under a parent, rename, reorder, delete.
- Shared report page for people a report is shared with (`reports/view/{id}` and its export, `SharedReportsController`).
- Admin dashboard: a `ReportDesignWidgetPart` widget shows a designed report.
- Workflows (with Orchard Core Workflows): events Report Run, Report Exported, Report Published; tasks Run Report, Export Report, Create Report Share Link, and Email Report (with Email).
- Background task: report view snapshot refresh (`ReportViewSnapshotBackgroundTask`). Limits come from `CrestApps:Reports:Builder:Limits` and `...:Versions` in configuration.

**Already covered:** recipe step `ReportDesigns` (imports report groups from `Groups`, views and reports matched by `ItemId`; share links are never imported; schema `ReportDesignsRecipeStepSchema`), usable through `importOrchardCoreRecipe`, and its deployment step. The workflow activities are reachable through `listWorkflowActivities` and `createOrUpdateWorkflow`. The dashboard widget is a content part. `listReports`, `getReport`, `runReport` and `exportReport` above already cover finding, reading, running and exporting designed reports. Cloning is `getReport` with the definition followed by `saveReportDesign` without an id, so it needs no tool.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listReportDataSets` | Read | no | `ManageOwnReportDesigns` | Lists the data sources and the data sets the user may read (orders, customers, content types, users, saved views, connected data sets and so on), optionally for one data source or matching a text. Use it to find where the data for a question lives. | ReportBuilderAgent |
| `getReportDataSetSchema` | Read | no | `ManageOwnReportDesigns` | Returns the fields of one data set with their names, types, groups, descriptions and the data sets they reference, and the formula functions available for calculated fields. Use it before writing a query so the field names are exact. | ReportBuilderAgent |
| `queryReportData` | Read | no | `ManageOwnReportDesigns` | Runs an ad hoc query over one or more data sets with the user's own access, with joins, filters, grouping by a field or by year, quarter, month, week or day, aggregates (count, sum, average, min, max, median), sorting and a limit, and returns rows. Use it when no existing report answers the question; nothing is saved. | ReportBuilderAgent |
| `saveReportDesign` | Write | no | `ManageOwnReportDesigns` to create; `ManageAllReportDesigns` on the report to change it | Creates and publishes a designed report, or publishes a change to an existing one by id, from a definition with its query, visuals, group, export and admin menu options. Returns the planner's errors and warnings; it does not change who the report is shared with. Test the query with `queryReportData` first. | ReportBuilderAgent |
| `shareReportDesign` | Security | yes | `ManageAllReportDesigns` on the report (`ShareReportsPublicly` for public sharing) | Sets the users and roles a designed report is shared with. The people it is shared with see the report's data with its owner's access. | ReportBuilderAgent |
| `restoreReportDesignVersion` | Write | no | `ManageAllReportDesigns` on the report | Restores an earlier saved version of a designed report. Use `getReport` with versions to find the version number. | ReportBuilderAgent |
| `deleteReportDesign` | Destructive | yes | `ManageAllReportDesigns` on the report | Deletes a designed report with its drafts and versions, or an unpublished draft that was never published. People it was shared with lose it. | ReportBuilderAgent |
| `createReportShareLink` | Security | yes | `ManageAllReportDesigns` on the report and `ShareReportsPublicly` | Creates a link that opens a designed report for named recipients, with an expiry in days, an optional maximum number of uses, whether export is allowed, a note, and optionally an email to the recipients. Returns the link. | ReportBuilderAgent |
| `revokeReportShareLink` | Security | yes | `ManageAllReportDesigns` on the report and `ShareReportsPublicly` | Revokes one share link of a designed report so it stops working. Use `getReport` with share links to find the link id. | ReportBuilderAgent |
| `listReportViews` | Read | no | `ManageOwnReportDesigns` | Lists the saved report views (reusable queries that reports read as data sets) with owner, refresh interval and snapshot state, or one view by id with its query and the reports that use it. | ReportBuilderAgent |
| `saveReportView` | Write | no | `ManageOwnReportDesigns` to create; `ManageAllReportDesigns` on the view to change it | Creates a report view from a query, or updates one by id, with an optional refresh interval for its stored snapshot. Returns the planner's errors and warnings. | ReportBuilderAgent |
| `refreshReportView` | Write | no | `ManageAllReportDesigns` on the view | Recomputes the stored snapshot of a report view now. Only views with a refresh interval keep a snapshot. | ReportBuilderAgent |
| `deleteReportView` | Destructive | yes | `ManageAllReportDesigns` on the view | Deletes a report view. Reports and other views that read it stop working, so check its usages first. | ReportBuilderAgent |
| `listReportGroups` | Read | no | `ManageReportGroups` | Lists the report groups as a tree with the number of groups and reports in each. | ReportBuilderAgent |
| `saveReportGroup` | Write | no | `ManageReportGroups` | Creates a report group under a parent, or renames, moves or reorders one by id. | ReportBuilderAgent |
| `deleteReportGroup` | Destructive | yes | `ManageReportGroups` | Deletes a report group. Check what it contains first; the service refuses or reports what blocks the deletion. | ReportBuilderAgent |

**Implementation notes:**

- Data sets: `IDataSourceManager.GetDataSources()`, `IDataSource.GetDataSetsAsync(new DataSourceContext { User })`, `GetSchemaAsync(dataSet, context)` and the formula function catalog are exactly what `ReportDesignerApiController.Sources`, `DataSets`, `Schema` and `Functions` return; move that projection (`DescribeDataSet`) to a service. These three read tools overlap with whatever the Orchard Core Data Sources / Data Pipelines area proposes; see Open questions.
- `queryReportData`: build an unsaved `ReportDesign` whose `Query` is the argument (`ReportQueryDefinition`: `DataSets` with alias, source and data set; `Joins`; `CalculatedFields`; `Filters` with field, operator and values; `Columns` with field, `Aggregate` (`None, Count, CountDistinct, Sum, Average, Min, Max, Median`) and `Transform` (`Year, Quarter, Month, Week, Day, DayOfWeek, Hour, MonthOfYear` and others); `Sorts`; `Limit`), normalize it with `ReportDesignNormalizer.Normalize`, and run it with `ReportDesignRunner.RunAsync(design, User, filterValues, period: null, options)` as `ReportDesignerController.Preview` does. That overload reads with the caller's access, so each data set's own permissions apply. `ReportQueryLimits` (50,000 rows per data set, 10,000 result rows, 4 concurrent runs by default) protect the site. A simpler argument shape than the full definition (one data set, group-by list, measures, filters) would be easier for a model; the full definition can stay available for joins.
- `saveReportDesign`: publish through `ReportDesignHistoryService.PublishAsync(incoming, existing, revision, force, user, canSharePublicly)` so a version is recorded, as `ReportDesignerController.Save` does (it returns `Saved`, `Errors`, `Warnings`, the revision and version number, and a `Conflict` or `Busy` status when someone else is editing). The group can be given as a path and resolved with `ReportGroupService.FindOrCreatePathAsync`, as the recipe step does. Keep `SharedUserNames` and `SharedRoles` out of this tool so sharing always passes through `shareReportDesign` and its approval.
- Authorization is resource based: `ReportDesignAuthorizationHandler` grants `ViewAllReportDesigns` on a design to its owner and to the users and roles it is shared with, and `ManageAllReportDesigns` to its owner when they can design. Always call `AuthorizeAsync(user, permission, design)` with the design or view as the resource. `ManageAllReportDesigns` and `ShareReportsPublicly` are declared `isSecurityCritical`.
- Share links: `ReportDesignerApiController.CreateLink` builds a `SharedLinkRequest` (kind `Reports.Report`) for Orchard Core's shared link service and can email recipients; `RevokeLink` and `Links` are next to it. This code is in the controller and must be extracted. Recipients must be users of the site (the service throws otherwise).
- Views: `ReportDesignService.GetAllViewsAsync`, `FindViewAsync`, `SaveViewAsync`, `DeleteViewAsync`, `FindViewUsagesAsync`; `ReportViewSnapshotRefresher.RefreshAsync(view, onlyWhenDue: false)`. Groups: `ReportGroupService.GetTreeAsync`, `FindAsync`, `SaveAsync`, `CountContentsAsync`, `DeleteAsync`. Versions: `ReportDesignHistoryService.ListVersionsAsync`, `FindVersionAsync`, `RestoreAsync`, `DeleteAsync`, `FindUnpublishedAsync`, `DeleteUnpublishedAsync`.
- Drafts (create, autosave, discard) are an editing aid of the browser designer and are not exposed as tools; `saveReportDesign` publishes directly.

**Test prompts:**

- "Show me order totals by month for the last 12 months." (no matching report: `listReportDataSets`, `getReportDataSetSchema`, `queryReportData`)
- "Save that as a report called Monthly Sales in the Sales group with a bar chart."
- "Share the Monthly Sales report with the Sales Manager role."
- "Create a link to the Monthly Sales report for jane that expires in 7 days."
- "Roll the Inventory report back to the previous version."

## Report Subscriptions — `CrestApps.OrchardCore.Reports.Subscriptions`

**What an admin can do in the UI:**

- Reports → Subscriptions (`Reports/subscriptions`, `ReportSubscriptionsController`): list with search and paging; create (optionally pre-selecting a report); edit; delete; "Save and send" emails the report now for the period before now without moving the schedule.
- Subscription fields: name, report (a coded report the user may view), frequency (daily, weekly, monthly), day of week, day of month (1–28), hour (0–23, in the site's time zone), up to 50 recipient email addresses, attachment format (none, CSV, Excel, PDF; needs the report's `ExportPermission` when it has one), enabled.
- Background task "Report subscriptions", every 5 minutes, emails the due ones.

**Already covered:** nothing. Subscriptions only cover coded reports (`IReportManager`); designed reports are emailed with the Email Report workflow task instead.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listReportSubscriptions` | Read | no | `ManageReportSubscriptions` | Lists the scheduled report emails with their report, frequency, recipients, attachment format, enabled state, next run, last run and last error, with optional search and paging. | ReportsAgent |
| `saveReportSubscription` | External | yes | `ManageReportSubscriptions` and the report's `Permission` (and its `ExportPermission` to attach a file) | Creates a schedule that emails a built-in report daily, weekly or monthly to up to 50 addresses, or updates one by id, including enabling or disabling it. The report is emailed for the period before each run; it needs the Email feature to be configured. | ReportsAgent |
| `sendReportSubscription` | External | yes | `ManageReportSubscriptions` | Emails a subscription's report to its recipients now, for the period before now, without changing its schedule. Returns the error when the email could not be sent. | ReportsAgent |
| `deleteReportSubscription` | Destructive | yes | `ManageReportSubscriptions` | Deletes a report subscription so its report is no longer emailed. | ReportsAgent |

**Implementation notes:** `IReportSubscriptionStore` (`PageAsync`, create, update, delete), `ReportSubscriptionService.ScheduleAsync` (computes `NextRunUtc`), `SendAsync(subscription, runUtc, reschedule: false)` and `CanSend`. The validation (report exists and is viewable, recipients, hour and day ranges, attachment format and export permission) is the private `ApplyAsync` of the controller and must be extracted. The subscription classes live in `Subscriptions/SubscriptionsStartup.cs` of the Reports module.

**Test prompts:**

- "Email the sales report to finance@example.com every Monday at 7 with an Excel attachment."
- "Which reports are emailed automatically, and did any fail last time?"
- "Send the weekly agents report now."

## Webhooks — `CrestApps.OrchardCore.Webhooks` (outbound), `CrestApps.OrchardCore.ContactCenter.ProviderInbox` (Provider Webhook Inbox, dependency only)

**What an admin can do in the UI:**

- Settings → Webhooks (`webhooks`, `WebhooksAdminController`): list the webhook subscriptions with paging; Add; Edit (the page shows the signing secret to copy into the receiver); Rotate secret; Send a ping (creates a `ping` delivery and sends it at once, showing the receiver's status code); Delete (its delivery log goes with it); Deliveries (log of every delivery with a status filter and paging); Resend a delivery.
- Webhook fields: name, URL (https, checked by `EgressNetwork.IsAllowedUrl`), enabled, all events or a selection from the event catalog (`IWebhookEventCatalog.GetEvents()`, grouped; other modules declare the events).
- Background task "Webhook deliveries", every minute: retries failed deliveries with backoff and deletes old ones.
- The Provider Webhook Inbox feature has no admin page: only the background task "Provider Webhook Inbox Dispatch" and storage.

**Already covered:** recipe step `WebhookSubscriptions` (entries in `Subscriptions`, matched by `ItemId`, may carry `Secret`; usable through `importOrchardCoreRecipe`) and its deployment step. Nothing lists deliveries, pings, resends or rotates.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listWebhooks` | Read | no | `ManageCommerceWebhooks` | Lists the outbound webhook subscriptions with name, URL, events and enabled state, with paging or one webhook by id. The signing secret is never returned. | WebhooksAgent |
| `listWebhookEvents` | Read | no | `ManageCommerceWebhooks` | Lists the events the site can send to webhooks, grouped, with their names and descriptions. Use it to choose the events of a webhook. | WebhooksAgent |
| `saveWebhook` | Security | yes | `ManageCommerceWebhooks` | Creates a webhook that posts signed JSON for chosen events, or all events, to an https URL, or updates one by id, including enabling or disabling it. A new webhook gets a signing secret that the administrator copies from its admin page. | WebhooksAgent |
| `rotateWebhookSecret` | Security | yes | `ManageCommerceWebhooks` | Gives a webhook a new signing secret. Deliveries are signed with it from then on, so the receiver rejects them until the new secret is entered there. | WebhooksAgent |
| `deleteWebhook` | Destructive | yes | `ManageCommerceWebhooks` | Deletes a webhook and its delivery log. Nothing more is sent to it. | WebhooksAgent |
| `sendWebhookPing` | External | yes | `ManageCommerceWebhooks` | Sends a test `ping` event to a webhook now and returns the receiver's status code or the error. Use it to check that a receiver is reachable and accepts the signature. | WebhooksAgent |
| `listWebhookDeliveries` | Read | no | `ManageCommerceWebhooks` | Lists the deliveries of one webhook with event, status, attempts, last status code, last error and times, filtered by status, with paging. Use it to find out why a receiver is not getting events. | WebhooksAgent |
| `resendWebhookDelivery` | External | yes | `ManageCommerceWebhooks` | Sends one logged delivery to its webhook again and returns the receiver's status code or the error. | WebhooksAgent |

**Implementation notes:**

- The permission object is `WebhookPermissions.ManageWebhooks`, but its name in code is `ManageCommerceWebhooks` and it is declared `isSecurityCritical`.
- Services: `IWebhookSubscriptionStore` (`PageAsync`, `FindByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`), `IWebhookDeliveryStore` (`PageAsync(subscriptionId, status, skip, take)`, `FindByIdAsync`, `CreateAsync`, `DeleteBySubscriptionAsync`), `IWebhookEventCatalog`, `WebhookDispatcher` in `CrestApps.OrchardCore.Webhooks.Core` (`NewProtectedSecret`, `SendAsync`, `ResendAsync`, `GetSecret`).
- In the controller and to extract: `Validate` and `Apply` (name, URL allow-list, known events), the ping delivery construction, and "delete the subscription then its deliveries".
- The edit page shows the signing secret (`_dispatcher.GetSecret`). No tool should return it; the answer should point the administrator to the webhook's admin page.
- A webhook sends site data (orders, AI conversations and so on) to a third party, which is why saving one is Security with approval.

**Test prompts:**

- "Which webhooks do we have and are any of them failing?"
- "Add a webhook for order.paid events to https://erp.example.com/hooks."
- "Ping the ERP webhook."
- "Resend the failed deliveries of the ERP webhook from today." (one `resendWebhookDelivery` call per delivery)

## Wizard — `CrestApps.OrchardCore.Wizard`

**What an admin can do in the UI:**

- No admin menu, settings page or admin controller. `WizardController` is a public front-end host (`Wizard/Start/{wizardType}`, `Wizard/Step/{sessionId}/{step?}`, `Wizard/Confirmation/{sessionId}`) guarded by `StartWizard` and `ContinueWizard`, which are granted to Anonymous and Authenticated by default.
- Content type editor: attach `WizardPart` to a content type and set its part settings (contained content types or stereotypes, display type, collapse contained items, requires authenticated user, completion policy).
- Content item editor: compose the wizard's steps as contained content items (`WizardPart.Steps`, edited like a `BagPart`).
- Workflows: events Wizard Started, Wizard Step Displayed, Wizard Completed, Wizard Failed.

**Already covered:** content tools cover a wizard content item (`createOrUpdateContentItem`, with `getContentItemSchema` / `getSampleContentItemForContentType` for the nested `Steps`); `applyContentTypeDefinitionFromRecipe` covers attaching `WizardPart` and its settings; the workflow tools cover the four events. The nested steps are the same shape as a `BagPart`, so no dedicated tool is proposed.

No tools proposed. See Open questions for the wizard's start link.

## No tools needed

| Module | Reason | Settings group (if any) |
| --- | --- | --- |
| `CrestApps.OrchardCore.AI.DataSources.AzureAI` | Source provider: adds the `AzureAISearch` source type, its editor fields and an index source. Reached through the data source tools. | none |
| `CrestApps.OrchardCore.AI.DataSources.Elasticsearch` | Source provider: adds the `Elasticsearch` source type, its editor fields and an index source. Reached through the data source tools. | none (default connection comes from `ElasticsearchDataSourceOptions`, configuration) |
| `CrestApps.OrchardCore.AI.DataSources.PostgreSQL` | Source provider: adds the `PostgreSQL` source type and its editor fields. Reached through the data source tools. | none (default connection string comes from `PostgreSQLDataSourceOptions`, configuration) |
| `CrestApps.OrchardCore.AI.DataSources.WebCrawlers` | Empty folder (only `bin` and `obj`); the feature lives in `CrestApps.OrchardCore.AI.WebCrawlers`. | none |
| `CrestApps.OrchardCore.AI.Documents.Azure` | Storage provider for uploaded AI documents, configured only in `CrestApps:AI:AzureDocuments`. | none |
| `CrestApps.OrchardCore.AI.Documents.AzureAI` | Index provider for AI documents (index profile handlers and vector search). Index profiles are managed in the Orchard Core indexing area. | none |
| `CrestApps.OrchardCore.AI.Documents.Elasticsearch` | Index provider for AI documents (index profile handlers and vector search). | none |
| `CrestApps.OrchardCore.AI.Documents.OpenXml` | Adds .docx, .xlsx and .pptx readers. No UI. | none |
| `CrestApps.OrchardCore.AI.Documents.Pdf` | Adds the PDF reader. No UI. | none |
| `CrestApps.OrchardCore.AI.FileSources.Ftp` | Connector and its editor fields. Reached through the file source tools. | none |
| `CrestApps.OrchardCore.AI.FileSources.Sftp` | Connector and its editor fields. Reached through the file source tools. | none |
| `CrestApps.OrchardCore.DataConnectors.SqlServer` | Connector and its connection editor. Reached through the data connector tools. | none |
| `CrestApps.OrchardCore.ContentTransfer.Azure` | Storage provider for import and export files, configured only in `CrestApps:ContentTransfer:AzureBlobStorage`. | none |
| `CrestApps.OrchardCore.ContentTransfer.OpenXml` | Adds the .xlsx file format. Reached through the `format` of the content transfer tools. | none |
| `CrestApps.OrchardCore.Reports.OpenXml` | Adds the `xlsx` export format. Reached through `exportReport`. | none |
| `CrestApps.OrchardCore.Reports.Pdf` | Adds the `pdf` export format. Reached through `exportReport`. | none |
| `CrestApps.OrchardCore.Webhooks` — Provider Webhook Inbox feature (`CrestApps.OrchardCore.ContactCenter.ProviderInbox`) | Dependency-only infrastructure: durable inbox and a background task, no admin page. | none |
| `CrestApps.OrchardCore.Wizard` | Front-end wizard host plus a content part; content, content definition and workflow tools cover it. | none |

Settings pages found in this group (both on the shared AI settings page, for the generic settings trio): `AIDataSourceSettings` / `ai-settings` / `ManageAIProfiles` / no secrets; `InteractionDocumentSettings` / `ai-settings` / `ManageChatInteractionSettings` / no secrets, reloads the tenant on change.

## Proposed agents for this group

| Agent | Purpose (one sentence) | Tools | Could merge with |
| --- | --- | --- | --- |
| `AIKnowledgeAgent` | Manages what the AI assistants know: data sources, file sources, web crawlers and profile documents, including synchronizing and test-searching them. | `listAIDataSources`, `saveAIDataSource`, `deleteAIDataSource`, `syncAIDataSource`, `searchAIDataSource`, `listAIProfileDocuments`, `addAIProfileDocument`, `removeAIProfileDocument`, `listAIFileSources`, `saveAIFileSource`, `deleteAIFileSource`, `runAIFileSource`, `listAIWebCrawlers`, `saveAIWebCrawler`, `deleteAIWebCrawler`, `runAIWebCrawler` (16) | An AI configuration agent for profiles, deployments and connections if one is proposed; `AIOperationsAgent` only if it stays small. Needs the Orchard Core index profile tools nearby, because every data source needs a knowledge base index. |
| `ReportsAgent` | Answers questions from the site's reports and data, exports reports to files, and manages scheduled report emails. | `listReports`, `getReport`, `runReport`, `exportReport`, `listReportDataSets`, `getReportDataSetSchema`, `queryReportData`, `listReportSubscriptions`, `saveReportSubscription`, `sendReportSubscription`, `deleteReportSubscription` (11) | The `data-analyst` generic agent (give it these tools instead of a new agent). `listReports`, `runReport` and `exportReport` are also good candidates to tick directly on the main assistant. |
| `ReportBuilderAgent` | Builds and maintains designed reports, report views and report groups, and controls who they are shared with. | `listReports`, `getReport`, `runReport`, `listReportDataSets`, `getReportDataSetSchema`, `queryReportData`, `saveReportDesign`, `shareReportDesign`, `restoreReportDesignVersion`, `deleteReportDesign`, `createReportShareLink`, `revokeReportShareLink`, `listReportViews`, `saveReportView`, `refreshReportView`, `deleteReportView`, `listReportGroups`, `saveReportGroup`, `deleteReportGroup` (19, six shared with `ReportsAgent`) | `ReportsAgent` cannot absorb it (30 tools together). Could take the Data Connectors tools if the Data Pipelines area has its own agent. |
| `DataConnectorsAgent` | Connects external systems and exposes their tables, APIs and files as data sets for reports and data pipelines. | `listDataConnections`, `saveDataConnection`, `testDataConnection`, `deleteDataConnection`, `listDataConnectionResources`, `listConnectedDataSets`, `getConnectedDataSet`, `saveConnectedDataSet`, `refreshConnectedDataSet`, `previewConnectedDataSet`, `deleteConnectedDataSet` (11) | The Orchard Core Data Pipelines / Data Sources agent (one "DataAgent"), or `ContentTransferAgent`. |
| `ContentTransferAgent` | Bulk imports and exports content items from and to CSV or Excel files and follows their progress. | `listContentTransferTypes`, `listContentTransferEntries`, `getContentTransferEntry`, `exportContentItems`, `importContentItems`, `setContentImportState`, `deleteContentTransferEntry` (7) | `ContentAgent` (17 tools today, 24 with these), or a deployment / import-export agent, or `DataConnectorsAgent`. |
| `WebhooksAgent` | Manages outbound webhooks: subscriptions, signing secrets, test pings and the delivery log. | `listWebhooks`, `listWebhookEvents`, `saveWebhook`, `rotateWebhookSecret`, `deleteWebhook`, `sendWebhookPing`, `listWebhookDeliveries`, `resendWebhookDelivery` (8) | Any agent for API keys, MCP, A2A, commerce webhooks or other integrations. |

Total: 66 new tools in 10 of the 11 sections (Wizard has none); 25 module folders covered.

## Open questions

- Files produced by tools. `exportReport` (and any other export tool) ends with bytes. Should the tool return a link to the existing admin export route with the filter in the query string (no storage, permission re-checked, but the query names of coded report filters are unverified), save into the media library, or use a new shared short-lived download store for AI tools? Content Transfer already has its own entry and download route, so it is not affected.
- Coded report filters have no schema. Only the date range can be set by a tool today; every other filter binds from the HTTP form in its `IDisplayDriver<ReportFilter>`. Is adding a small filter description contract (key, label, type, options) to the filter drivers in AI, AI.Chat, ContactCenter, Omnichannel.Managements and TimeTracker acceptable, or should `runReport` build a fake form for `UpdateEditorAsync`?
- Overlap with Orchard Core Data Sources and Data Pipelines. `listReportDataSets`, `getReportDataSetSchema` and `queryReportData` read Orchard Core's `IDataSourceManager`. If that area proposes generic data set tools, keep one set and one permission (these use `ManageOwnReportDesigns` because the report builder is what exposes them today).
- Secrets typed in chat. `saveDataConnection`, `saveAIFileSource`, `saveAIDataSource` and the recipe-based alternatives can carry passwords, keys and connection strings. Should tools accept write-only secret arguments (they then pass through the model and the conversation history), or refuse them and send the administrator to the admin page for the secret fields?
- `External` versus `Write` for AI cost. A full sync, a crawl and a file source run are classed External (unbounded embedding and vision cost, outbound requests); adding one document and one test search are not, and reads against an already configured external system (test connection, preview a live data set) are Read. Confirm or tighten.
- Long-running actions. `runAIFileSource` runs inline in the UI (`IIngestionRunService.RunAsync`). Should the tool wait, or start a background job and report later? The same question applies to large inline report runs.
- Designed reports run with their owner's access for everyone they are shared with (UI, workflow tasks and the proposed `runReport`), while `queryReportData` runs with the caller's access. This matches the UI, but it means an assistant can show a user data the user could not query directly. Acceptable?
- Content Transfer import source. Is a media library path the right source for `importContentItems`, or should the admin chat widget pass an uploaded attachment to the tool? Both need the entry creation extracted from `AdminController.ImportPOST`. Export options contributed by other modules (`IDisplayDriver<ExportRequest>`) cannot be set by a tool without a change.
- Content Transfer placement. `ContentAgent` would reach 24 tools with the seven transfer tools; keep a separate `ContentTransferAgent` or merge?
- Webhook signing secret. The admin page shows it; the tools deliberately do not. Is "open the webhook's page to copy the secret" acceptable after `saveWebhook` and `rotateWebhookSecret`?
- Wizard start link. A wizard content item is started at `Wizard/Start/{wizardType}?definitionId=...`, which is not its content display URL. Should `getLinkForContentItem` return the start link for items with a `WizardPart` instead of adding a tool?
- Unverified items to confirm during implementation: whether stored data source secrets are protected in the JSON the REST API returns; what deleting a data source or a web crawler removes from the knowledge base index; whether a file source keeps its last run summary; the exact signature of `DataSourceRetrieval.SearchDetailedAsync`; what `ShareReportsPublicly` gates inside `ReportDesignService.SaveAsync` (`canSharePublicly`); the behaviour of `ReportGroupService.DeleteAsync` on a group that still has reports.
