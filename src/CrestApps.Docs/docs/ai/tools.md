---
title: AI Tools
description: Shared tool registration and orchestration concepts are documented in CrestApps.Core.
user_manual:
  - user-manual/ai/tools-and-agents
---

# AI Tools

The shared tool system is documented primarily in **CrestApps.Core**:

- [Tools](https://core.crestapps.com/docs/core/tools)
- [Agents](https://core.crestapps.com/docs/core/agents)
- [MCP](https://core.crestapps.com/docs/mcp/index)
- [A2A](https://core.crestapps.com/docs/a2a/index)

This page is the Orchard-specific catalog for the AI functions registered or documented in this repository. Shared framework utilities that are not registered by Orchard features in this repo, along with external MCP and A2A tools, continue to be documented in the shared Core docs.

Within Orchard Core, tools become useful through the modules that register or expose them:

- [AI Services](overview)
- [AI Agents](agent)
- [AI Documents](./documents/)
- [AI Data Sources](./data-sources/)
- [MCP](./mcp/)
- [A2A](./a2a/)

## Orchard-specific AI function catalog

Use this catalog to see which Orchard feature makes each AI function available and what it does. When the codebase adds, removes, renames, or re-describes an AI function, update this page together with the related feature docs so the catalog stays synchronized with the current registrations.

Tools marked as hidden in the shared Core registry are not shown in Orchard Core capability pickers such as AI Profile, AI Profile Template, Chat Interaction, workflow-task, or post-session tool selectors. Hidden tools are reserved for internal orchestration paths such as system agents that reference them explicitly by name.

This catalog only lists functions registered in code. Functions created by administrators from a tool instance source are not listed here because they are tenant data rather than code registrations. See [AI Tool Instances](tool-instances) for that surface.

### System tools

**Available when:** `CrestApps.OrchardCore.AI.Agent`

| Function | Description |
| --- | --- |
| `listTimeZones` | Retrieves a list of the available time zones in the system. |
| `listAIProfiles` | Lists AI profiles with optional filters for type, analytics, data extraction, and post-session processing. |
| `viewAIProfile` | Retrieves detailed configuration for a specific AI profile by ID or name. |
| `listAIConversations` | Lists AI conversations on every channel, newest first, by profile, status and time. |
| `viewAIConversation` | Reads a conversation's transcript, variables, extracted fields, analysis, evaluations and flow stages. |
| `getAIContactMemory` | Reads what the AI remembers about a contact. |

### AI quality tools

With the **AI Quality** feature on, the admin AI agent can also look at QA results and run tests, so an admin can ask questions such as "why did calls fail yesterday?" and get proposed fixes to review.

| Function | Description |
| --- | --- |
| `summarizeAIQuality` | Sums up QA evaluations over a period: count, average score, pass rate, resolution, sentiment, the most common issues, the questions asked most and the trend by day. |
| `listAITestCases` | Lists the simulation test cases. |
| `runAITests` | Queues test cases to run in the background and returns the batch. |
| `listAIAlerts` | Lists the alert rules and which are open. |

### Recipe tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Recipes.Core`

| Function | Description |
| --- | --- |
| `applySiteSettings` | Applies predefined system configurations and settings using AI assistance. |
| `getOrchardCoreRecipeJsonSchema` | Returns the Orchard Core recipe root JSON Schema with a `steps` array. It can limit the schema to one step definition while still exposing every valid recipe step name. Call it first before building recipe JSON. |
| `listOrchardCoreRecipeStepsAndSchemas` | Lists all available Orchard Core recipe steps and returns their JSON schema definitions. |
| `importOrchardCoreRecipe` | Imports and runs Orchard Core recipes within your site. Call `getOrchardCoreRecipeJsonSchema` first and match that schema. |
| `listNonStartupRecipes` | Retrieves all available Orchard Core recipes that are not executed during startup. |
| `executeNonStartupRecipe` | Executes Orchard Core recipes that are not configured to run at application startup. |

### Tenant tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Tenants`

| Function | Description |
| --- | --- |
| `listStartupRecipes` | Retrieves a list of Orchard Core recipes configured to run at application startup. |
| `createTenant` | Creates a new tenant in the Orchard Core application. |
| `getTenant` | Retrieves detailed information about a specific tenant. |
| `listTenant` | Returns information about all tenants in the system. |
| `enableTenant` | Enables a tenant that is currently disabled. |
| `disableTenant` | Disables a tenant that is currently active. |
| `removeTenant` | Removes an existing tenant that can be safely deleted. |
| `reloadTenant` | Reloads the configuration and state of an existing tenant. |
| `setupTenant` | Sets up new tenants. |

### Content tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Contents`

| Function | Description |
| --- | --- |
| `searchForContentItems` | Searches for content items. |
| `getSampleContentItemForContentType` | Generates a structured sample content item for a specified content type. |
| `getContentItemSchema` | Returns the current content-item JSON schema for one or more Orchard Core content types. Call it immediately before `createOrUpdateContentItem` whenever that tool is available. |
| `publishContentItem` | Publishes a draft or previously unpublished content item. |
| `unpublishContentItem` | Unpublishes a currently published content item. |
| `getContentItemById` | Retrieves a specific content item by its ID or type. |
| `deleteContentItem` | Deletes a content item from the system. |
| `cloneContentItem` | Creates a duplicate of an existing content item. |
| `createOrUpdateContentItem` | Creates a new content item or updates an existing one. Before calling it, call `getContentItemSchema` first whenever available, then call it once for the top-level item and include nested or contained items in the same payload. |
| `getLinkForContentItem` | Retrieves a link for a content item. |

`getOrchardCoreRecipeJsonSchema` should be called immediately before `importOrchardCoreRecipe` whenever recipe-backed JSON needs to be generated. Its response always describes the root recipe object with a `steps` array; when you request a specific step, the schema still lists every valid step name in `steps[].name` but only expands the selected step's payload contract. If you ask for an unknown step, the tool returns an error that includes the available step names so the request can be retried with a valid identifier.

When `createOrUpdateContentItem` is available alongside recipe-backed content schema support, call `getContentItemSchema` immediately before it and request the parent content type plus any nested content types that will appear in the payload so the model can inspect the current content-item contract.

`createOrUpdateContentItem` must be called for the parent content item only. When the payload contains nested or contained content items such as `BagPart`, `FlowPart`, widgets, or blocks, include those child items inside the parent JSON instead of invoking the tool once per child item. During creation, the tool now initializes each nested content item with Orchard Core's `NewAsync()` flow before merging the authored payload, so nested handlers run for child items too. The tool also rejects payloads when the submitted JSON does not match the expected Orchard Core content-item shape. By default it uses dropped-value detection after `ContentItem` mapping to catch misplaced or unmapped values. If `CrestApps.OrchardCore.Recipes` is enabled, the tool validates the payload against the content type's recipe-backed JSON schema as authored, uses the registered contained-part schema metadata to find nested child payloads recursively, and returns the same schema contract in its corrective guidance so the model can retry with the correct structure.

### Content item list, version and owner tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Contents`

| Function | Description |
| --- | --- |
| `listContentItems` | Lists content items with the same filters as the admin Content Items page: text, content types, stereotype, status (latest, published, draft, owned by me, all versions), sort, culture, and paging. It returns summaries with IDs, not full bodies. The `culture` filter needs `OrchardCore.ContentLocalization`. |
| `listContentItemVersions` | Lists the stored versions of one content item (version ID, created/modified/published dates, author, latest and published flags). Use it before restoring a version. |
| `restoreContentItemVersion` | Restores an earlier version of a content item as a new draft, keeping the published version live. Takes a content item version ID. Publish the item afterwards to make it live. |
| `discardContentItemDraft` | Discards the unpublished draft of a content item so the published version becomes the latest again. Asks for approval. |
| `changeContentItemOwner` | Changes the owner of a content item to another user, given by user name, user ID or email. The author is not changed. Asks for approval. |

`listContentItems` builds the filter text a person types in the search box of the Content Items page (`type:`, `stereotype:`, `status:`, `sort:`, `culture:`) and runs it through the same filter parser and query service as that page, so it lists what the page lists. It does not filter by parent list, taxonomy term or schedule. `searchForContentItems` stays registered beside it.

These tools, and the content type, localization, nested item and scheduling tools below, act for the signed-in person in the chat. Each one checks the Orchard Core permission the matching admin action checks, on the same content item where the admin checks one: `ListContent` and `ViewContent` to list and read, `PublishContent` to restore a version or schedule, `DeleteContent` to discard a draft, `EditContentOwner` to change the owner, `EditContent` to change nested items, `LocalizeContent` to translate, and `EditContentTypes` to change content definitions. A caller who is not signed in, or who does not hold the permission, gets a refusal and nothing is changed. Dates are returned in the user's or the site's time zone, with the zone named.

### Content definition tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.ContentTypes`

| Function | Description |
| --- | --- |
| `getContentTypeDefinition` | Retrieves the definitions of all available content types. |
| `getContentPartDefinition` | Retrieves the definitions of all available content parts. |
| `listContentTypesDefinitions` | Provides a list of available content type definitions. |
| `listContentPartsDefinitions` | Provides a list of available content part definitions. |
| `listContentFieldDefinitions` | Provides a list of available content fields. |

### Content definition recipe tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.ContentTypes` + `OrchardCore.Recipes.Core`

| Function | Description |
| --- | --- |
| `removeContentTypeDefinition` | Removes the content type definition. |
| `removeContentPartDefinition` | Removes the content part definition. |
| `applyContentTypeDefinitionFromRecipe` | Creates a new content type definition or updates an existing one. |

### Content type part and field tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.ContentTypes`

| Function | Description |
| --- | --- |
| `saveContentTypePart` | Attaches a part to a content type or updates how it is attached: display name and description (named parts), editor, display mode and the part's settings for this type, without resending the whole type definition. |
| `removeContentTypePart` | Removes a part from a content type. Existing items keep the stored data but it is no longer edited or displayed. Asks for approval. |
| `saveContentPartField` | Adds a field to a content part (or to a content type's own part) or updates an existing field: display name, field type on creation, editor, display mode and the field's settings. The field type cannot be changed after creation. |
| `removeContentPartField` | Removes a field from a content part or content type. Existing items keep the stored values but they are no longer edited or displayed. Asks for approval. |
| `reorderContentTypeElements` | Sets the order of the parts on a content type, or of the fields on a part, from an ordered list that names every part or field once. |

These tools call the content definition service the content type editor calls, so the events of that editor run and a part or field the system defines cannot be removed. Only the arguments given are changed. The `settings` argument is a JSON object keyed by settings class name, the shape a content definition recipe uses (for example `{"AutoroutePartSettings": {"AllowCustomPath": true}}`); it is merged into the stored settings and is not checked by the settings editor of the part or field.

### Content localization tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.ContentLocalization`

| Function | Description |
| --- | --- |
| `listContentItemLocalizations` | Lists the translations of a content item: for each site culture, the content item ID, title and published state of its version in that culture, or that it is missing. |
| `localizeContentItem` | Creates the version of a content item in another culture as a draft copy linked to the same localization set, and returns the new content item ID. Translate the copy with `createOrUpdateContentItem` afterwards. |

### Nested content item tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Flows`

| Function | Description |
| --- | --- |
| `listNestedContentItems` | Returns the outline of the items nested in a content item's Flow, Bag and widget-zone parts: for each, its part, zone, position, nested item ID, content type, title and depth. |
| `saveNestedContentItem` | Adds, updates or moves a single widget or contained item inside a content item's Flow, Bag or widget-zone part without resending the rest of the page. |
| `removeNestedContentItem` | Deletes one widget or contained item, and everything nested inside it, from a content item's Flow, Bag or widget-zone part. Asks for approval. |

`createOrUpdateContentItem` replaces a nested list whole, so changing one widget means resending every widget of the page. These tools change one nested item and write only the list it is in. A new nested item must be of a content type the part's settings allow, and gets its own ID. The parent content item is saved as a draft unless `isDraft` is `false`; publish it to make the change live.

### Content scheduling tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.PublishLater` or `OrchardCore.ArchiveLater`

| Function | Description |
| --- | --- |
| `scheduleContentItem` | Schedules a content item to be published or archived (unpublished) at a date and time given in the site's or the user's time zone, or cancels the schedule. The content type must have the Publish Later or Archive Later part; publishing is scheduled on a draft, archiving on a published item. |

The scheduled time is acted on by the Publish Later and Archive Later background tasks, which run every minute and must be enabled.

### Address and home page tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Autoroute`

| Function | Description |
| --- | --- |
| `findContentItemByHandle` | Finds the content item behind a URL or path, an alias or a handle (`slug:about-us`, `alias:main-menu`, `contentitemid:...`), and returns its ID, type, title and state. A path of `/` returns the current home page. |
| `setHomepage` | Makes a published content item the site's home page. The content item must have an Autoroute part with a path, and its route must not be disabled. |

`findContentItemByHandle` asks Orchard Core's content handle manager, so a path is matched by the Autoroute feature and an alias by the Alias feature. A full URL is reduced to its path, without the tenant's prefix. It needs **View content** on the content item it finds.

`setHomepage` follows the content editor: it ticks **Set as homepage** on the Autoroute part and publishes the content item, and the Autoroute feature writes the home route. It needs **Set homepage** and **Publish content** on the content item, and it refuses a content item whose latest version is a draft, because that draft would go live. Every visitor sees the new home page at once. The site does not remember the previous home page, so the result names it.

### Menu tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Menu`

| Function | Description |
| --- | --- |
| `getMenuTree` | Returns a menu's items as a flat, ordered tree: item ID, type, title, URL or linked content item, parent ID, depth and required permissions. The menu is given by content item ID, alias or title; without one it lists the site's menus. |
| `saveMenuItem` | Adds an item to a menu, updates an existing item, or moves it under another parent or to another position, leaving the other items untouched. Supports link, content and HTML menu items. |
| `deleteMenuItem` | Deletes a menu item and all its child items from a menu. Asks for approval. |

`saveMenuItem` and `deleteMenuItem` need **Manage menus**, as the menu editor does. They save the change in a draft of the menu, so the site keeps showing the published menu until the menu is published; pass `publish: true` to publish it in the same call, which also needs **Publish content** on the menu. A URL is checked the way the menu item editor checks it, so a script URL is refused. `permissionNames` hides an item from users who lack the permissions; it does not protect the page the item links to.

### Taxonomy tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Taxonomies`

| Function | Description |
| --- | --- |
| `getTaxonomyTerms` | Returns the terms of a taxonomy as a flat, ordered tree: term ID, title, parent ID, depth and full path. The taxonomy is given by content item ID, alias or title; without one it lists the site's taxonomies. |
| `saveTaxonomyTerm` | Adds a term to a taxonomy, updates an existing term, or moves it under another parent or to another position, without touching the other terms. New terms use the taxonomy's term content type. |
| `deleteTaxonomyTerm` | Deletes a term and all its child terms from a taxonomy. Content items that reference the term keep the stale ID. Asks for approval. |

`saveTaxonomyTerm` and `deleteTaxonomyTerm` need **Manage taxonomies**. Like the menu tools, they save the change in a draft of the taxonomy unless `publish` is `true`. The other parts and fields of a term go in `content`, in the content item JSON shape, and are checked against the term content type.

### Robots.txt tool

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Seo`

| Function | Description |
| --- | --- |
| `getRobotsTxt` | Returns the robots.txt content the site currently serves, and says whether it comes from the robots settings or from a physical robots.txt file that overrides them. |

It needs **Manage SEO related settings**. Use it to read back the effect of a change to the robots settings.

### Sitemap tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Sitemaps`

| Function | Description |
| --- | --- |
| `listSitemaps` | Lists the sitemaps and sitemap indexes with their path, enabled state and sources (content types with frequency and priority, custom paths), and the cached sitemap files. |
| `saveSitemap` | Creates a sitemap, or updates a sitemap or a sitemap index: name, path, enabled, and the sources of a sitemap. The given source list replaces the existing one; the path must be unique and end in `.xml`. |
| `deleteSitemap` | Deletes a sitemap or a sitemap index. Indexes that contain the sitemap are not updated. Asks for approval. |
| `purgeSitemapCache` | Clears the cached sitemap files, one file or all of them, so sitemaps are rebuilt on the next request. |

The four tools need **Manage sitemaps**. `saveSitemap` checks every argument before it changes anything, so a refused call leaves the sitemap as it was. A source listed with its `sourceId` is kept and updated; a stored source that is not listed is removed. The tools cannot create a sitemap index, and they neither list nor change the sitemaps an index contains: that stays on the **Sitemap Indexes** page.

### Media library tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Media`

| Function | Description |
| --- | --- |
| `listMediaFiles` | Lists the sub-folders and files of a media folder (the root when no path is given), with optional name and extension filter, recursion and paging. Each file has its path, size, MIME type, date and public URL. |
| `getMediaFile` | Returns one media file's details and public URL and, when asked, its text content. |
| `getMediaOptions` | Returns the media configuration: allowed and restricted file extensions, maximum upload size, supported image sizes, CDN base URL, the enabled media features and the remaining permitted storage. |
| `importMediaFromUrl` | Downloads a file from a public http or https URL and stores it in a media folder. Asks for approval. |
| `saveMediaTextFile` | Creates a text file (for example `.csv`, `.svg`, `.css` or `.js`) in a media folder from the supplied text. Asks for approval. |
| `moveMediaFile` | Moves or renames up to 50 media files: each file's current path and its new path. |
| `copyMediaFile` | Copies a media file to a new path. |
| `deleteMediaFile` | Permanently deletes up to 50 media files by path. Asks for approval. |

The file tools check what the Media Library checks: **Manage Media** first, then the folder permission with the path as the resource. Orchard Core maps the path to the right permission, so the attached media folder needs **Manage Attached Media Fields Folder**, another user's folder needs **Manage Media For Others**, and with Secure Media the folder's view permission applies too. `listMediaFiles` leaves out the folders the caller may not manage. `getMediaOptions` needs **View Media Options**; it adds the remaining storage only for a caller who may manage the media library.

A path is given from the root of the media library, for example `brand/logo.png`, as the media library stores it and not URL-encoded. A path with a drive or a `..` segment is refused, and so is a path that names another folder or file when it is URL-decoded, because Orchard Core decodes a path before it checks the folder permission. The tools move, copy, import and save only into a folder that exists, and none of them creates or deletes a folder: do that in the Media Library. A new file must have an extension the site allows; a restricted extension (`.css`, `.js` and `.svg` by default) also needs **Upload media file extensions requiring additional permission**. No tool replaces a file unless `overwrite` is set, and `moveMediaFile` and `copyMediaFile` never replace one. `moveMediaFile` and `deleteMediaFile` check every file before they change the first one. Content that references a moved or deleted file is not updated, because media fields and HTML or Markdown bodies store the path as text.

`importMediaFromUrl` and `saveMediaTextFile` store the file through the file creation pipeline of an upload, so the file event handlers of the site (an antivirus scan, for example) can refuse it, and the site's maximum upload size applies. `saveMediaTextFile` writes UTF-8 text and only the text extensions `.css`, `.csv`, `.htm`, `.html`, `.js`, `.json`, `.md`, `.srt`, `.svg`, `.tsv`, `.txt`, `.vtt`, `.xml`, `.yaml` and `.yml`. The file is served from the site, so it must hold no secret and no script the user did not review.

`importMediaFromUrl` fetches an address the model chose, so it is built to never read the site's own network:

- Only `http` and `https` addresses are accepted, without a user name or password.
- The host is resolved before the request, and the request is refused when any of its addresses is a loopback, private, link-local or other internal address. The connection goes through the shared egress guard, which checks the address again when the connection opens.
- Redirects are followed by the tool, at most five, and each one is checked the same way. A redirect to an internal address is not followed.
- The download stops at the site's maximum upload size and after 100 seconds.
- A web page returned in place of the file (a sign-in page, for example) is not stored.

`getMediaFile` returns text only when `includeText` is set, and at most the first 20000 characters. Text, Markdown, PDF, Word and PowerPoint files are read by the text providers of the media indexing features (`OrchardCore.Media.Indexing.Text`, `OrchardCore.Media.Indexing.Pdf`, `OrchardCore.Media.Indexing.OpenXML`) when they are enabled. Without a provider, the tool reads a plain-text file of up to 1 MB.

### Media cache tool

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Media.Cache`

| Function | Description |
| --- | --- |
| `purgeMediaCache` | Purges the local cache of files fetched from remote media storage (Amazon S3 or Azure Blob). Files are fetched again on the next request. |

It needs **Manage Asset Cache Folder**. It says so when no remote media store is enabled and configured, because then there is no cache.

### Shortcode tool

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Shortcodes`

| Function | Description |
| --- | --- |
| `listShortcodes` | Lists every shortcode available on the site (built-in and template-based) with its hint, usage, default value and categories, with an optional search on the name, hint or category. |

The list is the catalog the shortcode picker of the HTML and Markdown editors shows. That picker checks no permission, so the tool asks only for a signed-in user. It does not return the Liquid content of a shortcode template.

### Search index tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Indexing`

| Function | Description |
| --- | --- |
| `listIndexProfiles` | Lists the search index profiles (name, provider, type, index name, indexed content types, created date) with an optional name or provider filter, and the provider and type combinations a new one can be created for. |
| `getIndexProfile` | Returns one index profile, by ID or name, with its settings and the names of its indexed fields. |
| `saveIndexProfile` | Creates an index profile for a provider and type (for example Lucene and Content), or updates the name and the content settings of an existing one. |
| `synchronizeIndexProfile` | Starts the background synchronization of up to 20 indexes, or with `reset` indexes every item again from the beginning while keeping the entries of the index. |
| `rebuildIndexProfile` | Deletes and recreates the physical index of up to 20 index profiles and starts indexing again in the background. Asks for approval. |
| `deleteIndexProfile` | Deletes up to 20 index profiles together with their physical indexes. Asks for approval. |

The six tools need **Manage Indexes**, the permission of the **Search > Indexes** page, and call the index profile manager and the index manager of the provider in the order that page does. A tool takes an index profile by its ID or by its name; both are unique on a site.

`saveIndexProfile` creates an index profile when no profile has the given name: it needs the provider, the type and the index name, validates the profile with the provider's rules, stores it, creates the physical index and starts indexing. When the provider cannot create the index, the profile is removed again. For an existing profile it changes the name (with `id`) and, for a Content index, the indexed content types, the culture and whether the latest or the published version is indexed. The changes are validated before the stored profile is touched, so a refused call leaves it as it was. The provider, the type and the index name cannot be changed, a content type must exist, and the analyzer and the other settings of the provider are changed on the admin page of the index. Items that are already indexed are not indexed again after an update: use `synchronizeIndexProfile` with `reset` for that.

`synchronizeIndexProfile`, `rebuildIndexProfile` and `saveIndexProfile` start indexing in the background and return before it is finished, so their result says what was started. `rebuildIndexProfile` takes the lock the indexing of the index takes, as the rebuild step of a recipe does, and reports an index that is being indexed at that moment as failed. `deleteIndexProfile` keeps the profile when the provider cannot remove the physical index, unless `force` is set, as the force option of the page does. The three tools that take a list do nothing when one of the given indexes does not exist.

`getIndexProfile` returns the analyzer, the default search fields and the names of the indexed fields for Lucene and Elasticsearch indexes. For an index of another provider it returns the general and the content settings only.

### Site search tool

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Search`

| Function | Description |
| --- | --- |
| `searchIndex` | Runs a plain-text search against an index profile, or the default index of the site search settings when none is named, and returns the matching content items with highlights, a page at a time. |

It searches the way the search page of the site does: the search service of the provider of the index runs the terms, and the content items are loaded in the order of the results, published or latest as the index stores them. It checks **Query any index** on the index profile, so the permission to query that one index is enough. It does not call the handlers the search page calls after a search, so a search by the tool is not recorded as a visitor's search. When the provider cannot run the search, because the physical index is missing or the terms are not valid for the index, the tool says so instead of returning no results.

### Search query tool

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Lucene` or `OrchardCore.Elasticsearch`

| Function | Description |
| --- | --- |
| `runSearchQuery` | Runs an ad-hoc JSON query (Elasticsearch-style DSL, Liquid allowed, with optional parameters) against a Lucene or Elasticsearch index profile and returns the matching documents and the number of matches. |

It needs the permission of the **Run Lucene Query** or **Run Elasticsearch Query** page of the provider of the index: **Manage Lucene Indexes** for a Lucene index, **Manage Elasticsearch Indexes** for an Elasticsearch index. The query is not saved. It is run by the query source of the provider, as the query API of the provider runs one, with the query service the "Run query" page uses. The tool refuses an index of another provider (use `searchIndex` for Azure AI Search), an index profile whose physical index does not exist and, for Elasticsearch, a site without an Elasticsearch connection. An invalid query returns the provider's error. Documents are returned a page at a time, as described under [Query tools](#query-tools).

### Query tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Queries`

| Function | Description |
| --- | --- |
| `listQueries` | Lists the saved queries with their name, source, template, index, result schema and whether they return content items, with a name filter and paging, and the query sources enabled on the site. |
| `saveQuery` | Creates or updates a saved Lucene or Elasticsearch query: name, source, template, index, result schema and the return-content-items flag. It can rename a query. Asks for approval. |
| `deleteQuery` | Deletes up to 50 saved queries by name. Asks for approval. |
| `runQuery` | Executes a saved query by name with optional parameters and returns its rows or content items. |

`listQueries`, `saveQuery` and `deleteQuery` need **Manage queries**. `runQuery` checks the permission the query API checks for `api/queries/{name}`: **Execute Api** for that query, which **Execute Api - All queries** and **Manage queries** imply. A caller who may not run a query gets the same refusal for a name that exists and for one that does not.

`saveQuery` saves Lucene and Elasticsearch queries with the checks of the **Search > Queries** page: a technical name of letters and digits that no other query has, a template, an index profile of the provider of the source, and a schema that is JSON. Everything is checked before the query is changed. The source of a saved query cannot be changed. It does not save SQL queries, and it does not change a saved SQL query: the page checks the SQL of a query with a parser that only the Queries module can call, so a SQL query is saved on that page. A saved query can be called from Liquid, GraphQL and the query API, which is why the tool asks for approval.

The tools that run a query (`runQuery`, `runSqlQuery` and `runSearchQuery`) bound what they return:

- Rows are returned a page at a time: 20 by default, 50 at most. `total` is the number of rows the query returned, so limit the rows in the query itself.
- A page stops before about 40000 characters of JSON and says so with `truncated`.
- A text longer than 2000 characters is shortened, and a list keeps its first 50 values.
- A content item is returned as its ID, version ID, content type, title and published and latest flags. Read its parts and fields with the content tools.
- A value whose column or property name says it is a credential (a password, a hash, a token, a secret, a key, a security stamp, a connection string) is replaced with `[redacted]`, also inside a JSON document stored in a text column. This is a safety net that goes by the name: a query that returns a credential under another name still returns it.

When a saved query fails, `runQuery` says that it failed and leaves the error in the log, because the caller may run the query without being allowed to read it.

### SQL query tool

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Queries.Sql`

| Function | Description |
| --- | --- |
| `runSqlQuery` | Runs an ad-hoc read-only SQL SELECT, with optional parameters, against the site's database and returns the rows. Asks for approval. |

It needs **Manage SQL Queries**, the permission of the **Run SQL Query** page, which Orchard Core marks as security critical. The tool opens no database connection: the SQL is the template of an unsaved query that the SQL query source of Orchard Core runs. That source parses the SQL with the parser the page uses, which accepts a SELECT only and applies the table prefix of the tenant, and Liquid in the query is rendered first, as on the page.

The query can read every table of the tenant, including the documents that hold users and settings, and no content permission applies, so attach the tool only to a profile for administrators. Rows are bounded and credentials are left out as described under [Query tools](#query-tools).

The SQL query source logs a query its parser refuses and returns no rows. The tool cannot tell that from a query that matched nothing, so a result without rows says that both are possible and that the site's log has the parse error. An error from the database is returned.

### Feature management tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Features`

| Function | Description |
| --- | --- |
| `disableSiteFeature` | Disables site features. |
| `enableSiteFeature` | Enables site features. |
| `searchSiteFeature` | Searches available features for a match. |
| `listSiteFeature` | Retrieves available site features. |
| `getSiteFeature` | Retrieves info about a feature. |

### Communication tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + one or more communication features

| Function | Required feature | Description |
| --- | --- | --- |
| `sendNotification` | `OrchardCore.Notifications` | Sends a notification message to a user. |
| `sendEmail` | `OrchardCore.Email` | Sends an email message on behalf of the logged-in user. |
| `sendSmsMessage` | `OrchardCore.Sms` | Sends an SMS message to a user. |

### User and role tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + the matching user-management feature

| Function | Required feature | Description |
| --- | --- | --- |
| `getUserInfo` | `OrchardCore.Users` | Gets information about a user. |
| `searchForUsers` | `OrchardCore.Users` | Searches the system for users. |
| `getRoleInfo` | `OrchardCore.Roles` | Gets information about a role. |

### User account tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Users`

| Function | Description |
| --- | --- |
| `listUsers` | Lists user accounts with optional filters (text in the user name, email, role, enabled or disabled), sorting and paging. It returns ID, user name, email, roles and status only. |
| `getUser` | Returns one user by ID, user name or email: profile, roles, enabled, email confirmed, whether a password is set, locked out until, failed sign-in count, two-factor state, external login provider names, time zone, culture and custom user settings. |
| `saveUser` | Updates the user name, email and phone number of a user. It does not create a user and takes no password. Asks for approval. |
| `enableUser` | Enables up to 20 disabled user accounts, for example registrations that wait for moderation, and reports the outcome for each. Asks for approval. |
| `disableUser` | Disables up to 20 user accounts so they can no longer sign in, and reports the outcome for each. Asks for approval. |
| `unlockUser` | Clears the lockout and the failed sign-in count of a user. Asks for approval. |
| `confirmUserEmail` | Marks the email address of a user as confirmed without sending an email. Asks for approval. |
| `deleteUser` | Permanently deletes a user account. Asks for approval. |

These tools are added beside `getUserInfo` and `searchForUsers`. They check, for the person in the chat, the permission the matching action of the **Access Control > Users** page checks, on the user it acts on, so the permissions for the users of one role (for example **Edit users in Editor role**) work as they do on that page:

- `listUsers` needs **List all users** or the permission to list the users of a role. It builds the filter text of the page's search box and runs it through the page's parser and query service. A role filter needs the permission to list the users of that role, and a user the caller may not list is left out of the page.
- `getUser` needs **View user profiles** on the user. A person can always read their own account.
- `saveUser`, `enableUser`, `disableUser`, `unlockUser` and `confirmUserEmail` need **Edit any user** on the user, and `deleteUser` needs **Delete any user**.

A caller who may act on no user gets the same refusal for an ID that exists and for one that does not, so the tools cannot be used to learn which accounts exist.

No tool returns or accepts a credential. Results are built from a fixed list of fields: the password hash, the security stamp, the reset token, the authenticator key, the recovery codes, the user's claims and the keys of external logins are never returned, and `getUser` says only whether a password is set and which external providers are linked. No tool takes a password, a hash or a token. A password is set by a person on the Users page.

`saveUser` follows the Edit page: a field the site locks under **Settings > Security > User Login** is refused, to administrators too, the phone number is validated, and the user manager validates and stores the user (a user name or an email that is taken is reported). A person who changes their own account needs **Edit own user information**. It does not create a user, because an account created from a chat would have no password and the assistant cannot send the email that lets the user choose one; create users on the Users page. It does not change the time zone or the culture of a user.

The tools refuse what the admin page leaves to the person:

- `disableUser` and `deleteUser` refuse the caller's own account, the site owner (the super user of the site settings) and the only enabled user in the administrator role. The name of the administrator role is read from the role service, so a renamed administrator role is protected too.
- `deleteUser` takes the user name with the ID and refuses when they do not match, so the approval shows which account is deleted.
- `unlockUser` resets only the failed sign-in count of a user who is not locked out.

### User role tool

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Users` + `OrchardCore.Roles`

| Function | Description |
| --- | --- |
| `setUserRoles` | Adds roles to a user and removes roles from a user. Asks for approval. |

It needs **Edit any user** on the user and, for every role it adds or removes, **Assign any role to users** on that role, the check the role editor of a user makes (the permission to assign one role is enough for that role). It checks everything before it changes anything, so a refused call leaves the user as it was. It refuses:

- the caller's own account, which the role editor allows;
- a role that cannot be given to a user (Anonymous, Authenticated or a name that is not a role), with the list of roles that can;
- a role the caller may not assign;
- removing the administrator role from the only enabled administrator.

Removing a role changes the security stamp of the user, as the role editor does, which signs the user out of their open sessions.

### User two-factor authentication tool

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Users.2FA`

| Function | Description |
| --- | --- |
| `disableUserTwoFactor` | Switches off two-factor authentication for a user who lost their second factor. Asks for approval. |

It needs **Disable two-factor authentication for any user**, the permission of the **Disable two-factor authentication** action of the Users page. It refuses the caller's own account. The result says whether the site, or a rule for the roles of the user, makes the user set two-factor authentication up again at the next sign-in. It does not read, show or reset an authenticator key or a recovery code.

### Custom user settings tool

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Users.CustomUserSettings`

| Function | Description |
| --- | --- |
| `saveUserCustomSettings` | Sets the values of one custom user settings type on a user, given the type name and the parts and fields to change. |

Custom user settings are stored inside the user, not as content items, so the content tools do not reach them. The tool needs **Edit any user** on the user and the **Manage Own Custom User Settings** permission of the settings type, which the user editor checks for whichever user is edited. It reads the stored settings with the custom user settings service, merges the given parts and fields the way `createOrUpdateContentItem` merges a content item (a field that is not given keeps its value, a list that is given replaces the stored list), validates the result with the content manager and stores the user. `getUser` returns the settings of the types the caller holds the permission for.

### Role and permission tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Roles`

| Function | Description |
| --- | --- |
| `listRoles` | Lists the roles with their description and whether each is a system role or the administrator role. |
| `getRole` | Returns one role with the permissions assigned to it, the permissions a user who holds only that role has in effect, the ones that come from Anonymous and Authenticated, and its security-critical permissions. |
| `listPermissions` | Lists the permissions the enabled features provide, with name, description, group, the permissions that imply them and whether Orchard Core marks them as security critical; with a text filter and paging. |
| `saveRole` | Creates a role, or changes the description and the permissions of a role (add, remove or replace; a new role can copy the permissions of another role). Asks for approval. |
| `deleteRole` | Deletes a role that is not a system role and takes it from the users and OpenID applications that have it. Asks for approval. |

These tools are added beside `getRoleInfo`. `listRoles`, `getRole` and `listPermissions` need **View Roles**; `saveRole` and `deleteRole` need **Manage Roles**, which Orchard Core marks as security critical. They use the role manager and the role service of the **Access Control > Roles** page.

`getRole` asks the authorization service what a user who holds only the role may do, as the View page of a role does, so `effectivePermissions` holds the assigned permissions, the ones they imply and the ones every user gets from Anonymous and Authenticated. A list holds at most 200 names; `filter` narrows the lists. For the administrator role it says that the role holds every permission.

`saveRole` updates a role unless `create` is true, so a mistyped name never creates a role. It keeps the rules of the admin page: a new role needs a name without a slash that no other role has, and the permissions of the administrator role cannot be changed. It adds four rules:

- A permission must be one an enabled feature provides. `listPermissions` returns the exact names.
- A security-critical permission is never given to Anonymous or Authenticated.
- It does not add permissions to a role the caller holds (Anonymous and Authenticated count, because every signed-in user has what they grant), unless the caller is in the administrator role or is the site owner. Removing permissions from such a role is allowed.
- Every security-critical permission the call gives must be named again in `securityCriticalPermissions`. When one is missing, the tool changes nothing and names it, so the assistant tells the person and the approval of the second call shows what is granted.

Everything is checked before the role is changed. `deleteRole` reports how many users had the role.

### OpenID Connect tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.OpenId.Management`; `saveOpenIdApplication` also needs `OrchardCore.OpenId.Server`

| Function | Description |
| --- | --- |
| `listOpenIdApplications` | Lists the client applications with id, client id, display name and type, with a search by display name and paging. |
| `getOpenIdApplication` | Returns one application by id or client id: type, consent type, redirect addresses, allowed flows and endpoints, the PKCE and pushed authorization request requirements, roles and scopes. |
| `saveOpenIdApplication` | Creates a public client, or changes the display name, consent type, redirect addresses, flows, endpoints, requirements, roles and scopes of an application. Asks for approval. |
| `deleteOpenIdApplication` | Deletes an application. The call names its id and its client id. Asks for approval. |
| `listOpenIdScopes` | Lists the scopes with id, name, display name, description and resources. |
| `saveOpenIdScope` | Creates a scope, or changes its name, display name, description and resources. Asks for approval. |
| `deleteOpenIdScope` | Deletes a scope and names the applications that were allowed to request it. Asks for approval. |

The application tools need the `ManageApplications` permission (**View, add, edit and remove the OpenID Connect applications**) and the scope tools need the `ManageScopes` permission (**View, add, edit and remove the OpenID Connect scopes**), the permissions of the **Access Control > OpenID Connect** pages. They read and delete through the application and scope managers of Orchard Core.

No tool returns, takes or writes a client secret. `getOpenIdApplication` returns `hasClientSecret` only. `saveOpenIdApplication` saves through the `OpenIdApplication` recipe step of the OpenID module, which the Authorization Server feature registers and which keeps the stored secret of a confidential client when it is given none. It refuses:

- A new confidential client, and a change of the type of an application. Both set or remove a client secret, which a person does on the **Applications** page.
- A new redirect or post-logout redirect address that is not an absolute `https` address without a fragment. An address that is already stored may stay.
- The administrator role, a role that cannot be given to a user, and a role the caller may not assign (**Assign any role to users**, checked on that role). A role on an application is what its client credentials act with.
- The implicit and the password flow, unless the call sets `confirmDiscouragedFlows`, so the person who approves sees them.
- A scope that does not exist, and a client id another application has.

The lists it is given (`redirectUris`, `allowedFlows`, `allowedEndpoints`, `roles`, `scopes`) replace the stored lists; a field that is left out keeps its value. The tool does not change a client id. `saveOpenIdScope` refuses a name another scope has and a resource that names the tenant itself (`oct:<tenant>`), as the admin page does.

### Rate limit tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.RateLimits`

| Function | Description |
| --- | --- |
| `listRateLimitPolicies` | Lists the policies with scope, path or group, status and limiters, and the built-in route and group limits that modules register in code. Search text, status filter and paging. |
| `saveRateLimitPolicy` | Creates a policy, or changes its name, description, scope (global, endpoint path or group) and limiters (fixed window, sliding window, concurrency, token bucket). Asks for approval. |
| `enableRateLimitPolicy` | Enables one or more policies. The tenant reloads. Asks for approval. |
| `disableRateLimitPolicy` | Disables one or more policies. The tenant reloads. Asks for approval. |
| `deleteRateLimitPolicy` | Deletes one or more policies. The tenant reloads when an enabled policy is deleted. Asks for approval. |

The tools need **Manage Rate Limits** and use the policy store of the **Tools > Rate Limits** page with its rules: a policy name is unique, an endpoint policy needs a path that starts with `/`, a group policy needs a group, and the scope, path, group and limiters of an enabled policy cannot be changed until it is disabled. The numbers of a limiter have the limits of the limiter editors (each at least 1; a queue limit of 0 or more).

`saveRateLimitPolicy` always saves a new policy disabled, so the person can review its limits before `enableRateLimitPolicy` turns it on. The result warns when a policy is global, has no limiters, or names a group that no endpoint of the site is in.

`listRateLimitPolicies` marks with `protectsSignIn` each enabled policy that limits the sign-in and account endpoints: a global policy, a policy on one of the rate-limit groups the Users module puts on those endpoints, and an endpoint policy whose path covers one of them. `disableRateLimitPolicy` and `deleteRateLimitPolicy` refuse such a policy unless the call sets `confirmRemovesSignInProtection`, and then say in the result which protection went. The built-in limits stay on in every case.

### Secret tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Secrets`

| Function | Description |
| --- | --- |
| `listSecrets` | Lists the secrets by name, store, type, description and created, updated and expiry dates, with search, type and expiry filters, and the secret stores. |
| `saveSecret` | Creates an X.509 secret that points to a certificate in a certificate store of the server, or changes the description and the expiry date of any secret. Asks for approval. |
| `deleteSecret` | Deletes a secret from its store. Asks for approval. |
| `moveSecrets` | Moves one secret, or every secret, from one writable store to another and reports the outcome for each. Asks for approval. |

`listSecrets` needs **View secrets**; the other tools need **Manage secrets**. They use the secret manager and the store operations service of the **Tools > Security > Secrets** pages.

No tool returns the value of a secret, a private key or PEM material, and no tool has an argument for one. `listSecrets` reads only what the stores tell about a secret. To change a description or an expiry date, `saveSecret` saves the stored secret back unchanged, as the Edit page does when its value fields are empty. It creates nothing for a text secret or an RSA key secret, and it does not accept a PEM certificate: it returns the address of the Secrets page where a person types the value or has the key pair generated. For a new X.509 secret it says whether a certificate with the thumbprint is in that certificate store of the server. Dates are given and returned in the time zone of the user or the site.

`deleteSecret` and `saveSecret` refuse a read-only store. No lookup tells which settings reference a secret, so `deleteSecret` cannot name them. When a store fails during `moveSecrets`, the tool does not return what the store answered.

### Audit trail tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.AuditTrail`

| Function | Description |
| --- | --- |
| `listAuditTrailEvents` | Lists the events, newest first, with filters for category, event name, user name, user id, correlation id and a date range, and paging. |
| `getAuditTrailEvent` | Returns one event with its IP address and its details: the user snapshot of a user event, or the content item version of a content event. |

Both tools need **View audit trail**. `listAuditTrailEvents` turns its arguments into the filter text of the **Tools > Audit Trail** page and runs it through the filter parser and the query service of that page. Dates are local dates or times in the time zone of the user or the site. A list returns who did what and when, without the IP address.

The admin page shows the data of an event through the display driver of the module that recorded it. `getAuditTrailEvent` knows the user events and the content events of Orchard Core and returns the header only for any other event type. The details of a user event need **View Audit Trail events about users**, or **View Audit Trail events about own user** when the event is about the caller, as on the admin page; without it the tool returns the header and says so. A user snapshot is returned from a fixed list of fields (user id, user name, email, phone number, enabled, roles, the confirmation, two-factor and lockout flags and the failed sign-in count), with the values as the site redacted them when the event was recorded. For a content event the tool returns the ids, type, title, version number, author, comment and whether the version is published, not the content itself.

### Notification tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Notifications`

| Function | Description |
| --- | --- |
| `listNotifications` | Lists the caller's own notifications, newest first, with filters for read or unread, type and text, and paging. |
| `markNotifications` | Marks notifications of the caller as read or unread: the given ids, or all of them for read. |
| `deleteNotifications` | Removes the given notifications of the caller. Asks for approval. |

These tools are added beside `sendNotification`. They need **Manage notifications**, which is about the user's own notifications, and act on the notifications of the signed-in caller only: the id of a notification that was sent to another user is answered like an id that does not exist. `listNotifications` uses the filter parser and the query service of the **Notifications** admin page. The admin page has no action that marks every notification as unread, so `all` works with `read` only.

### Test email and test SMS tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Email` (`sendTestEmail`) or `OrchardCore.Sms` (`sendTestSms`)

| Function | Required feature | Description |
| --- | --- | --- |
| `sendTestEmail` | `OrchardCore.Email` | Sends a test email with a fixed subject and body through a chosen email provider, or the default one, and returns the provider's result. Asks for approval. |
| `sendTestSms` | `OrchardCore.Sms` | Sends the fixed test text message to a phone number through a chosen SMS provider and returns the provider's result. Asks for approval. |

These tools are added beside `sendEmail` and `sendSmsMessage` and do what the **Tools > Testing** pages do. `sendTestEmail` needs **Manage Email Settings**, sends to the email address of the caller unless `to` names one other address, and lists the enabled providers in its result. `sendTestSms` needs **Manage SMS Settings** and sends to the phone number the call names; `provider` can be left out when one provider only is enabled. The subject and the text are fixed, so the tools check the settings and cannot be used to write a message to somebody. When a provider throws, the error goes to the log of the site and is not returned, because it can name the host and the account of the provider settings.

### Site settings tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Settings` (always on)

| Function | Description |
| --- | --- |
| `listSiteSettingsGroups` | Lists the settings pages the caller can open, with group id, title, menu path and link. Search text and paging. |
| `getSiteSettings` | Returns the fields of one settings group, by section, with their current values and a JSON schema of each section. |
| `updateSiteSettings` | Changes fields of one settings group and returns the validation errors, or the saved values and whether the site reloads. Asks for approval. |

The tools replay the settings page instead of writing the stored settings. `listSiteSettingsGroups` reads the admin menu of the caller, which already leaves out the pages the caller may not open, and needs **Access admin panel**. `getSiteSettings` and `updateSiteSettings` check the permission the **Settings** page checks for the group, then build the editor through the site settings display drivers, so each driver applies its own permission and fills its editor as the page does. A section is one editor of the page, named by its form prefix (for example `ISite` for the General page or `ISite.AdminSettings`); its fields are the fields of the editor, not of the stored settings, so the values `getSiteSettings` returns are in the shape `updateSiteSettings` takes.

`updateSiteSettings` merges the changed fields over the values the editor was built with and posts the whole page back through the drivers, with the same model binding and validation as the page. The drivers then protect credentials, run their side effects and ask for the tenant reload as on a save from the page. When a driver reports an error, nothing is saved, the tenant is not reloaded, and the errors are returned as the driver wrote them. A page that has an editor the tool cannot read (a list of objects, an editor without a view model) is not changed, and the result says why.

Secrets are never shown or accepted: a text field whose name holds a password, secret, token, API key, connection string or private key is returned only as whether it is set, and a credential edited with the secret input returns its source and secret name; a call can point it at a secret of the Secrets module by name but cannot set a value. The tools never change the super user, the site salt or the home route. Custom settings content types are not read or changed by these tools; a person edits them on their settings page. The **OpenID Client** settings are a site settings group and can be read and changed with these tools; the OpenID server and validation settings and the CORS settings have their own pages and documents and have no tool yet.

### Admin page search tool

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Admin`

| Function | Description |
| --- | --- |
| `findAdminPage` | Searches the admin menu the caller can see and returns the matching pages with their menu path and link. |

The tool needs **Access admin panel**. It walks the admin menu the same way the quick navigation palette (Ctrl+K) does, for the caller only, so it never returns a page the caller cannot open.

### Admin dashboard tool

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.AdminDashboard`

| Function | Description |
| --- | --- |
| `listAdminDashboardWidgets` | Lists the dashboard widgets with content type, position, width and height, and the content types that can be added as widgets. |

The tool needs **Access the Admin Dashboard**. With **Manage the Admin Dashboard** it lists the latest version of the widgets whose type the caller may edit and the widget types the caller may create, as the **Manage Dashboard** page does; otherwise it lists the published widgets the caller may view. Widgets are content items, so the content tools create, edit and delete them.

### Admin menu tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.AdminMenu` + `OrchardCore.Deployment`

| Function | Description |
| --- | --- |
| `listAdminMenus` | Lists the custom admin menus, optionally with the full node tree of each and the node types the site offers. |
| `saveAdminMenu` | Creates a custom admin menu or replaces one, with its name, enabled state and whole node tree. |

Both tools need **Manage the admin menu**. `listAdminMenus` reads the menus through the admin menu deployment step, which is why the Deployment feature must be on too, and returns the nodes in the shape of the `AdminMenu` recipe step, which `saveAdminMenu` runs. A node must have the `$type` of a node type of the site and the fields that type requires. Nodes that are not sent are removed.

### Tenant feature profile tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Tenants.FeatureProfiles` (Default tenant only)

| Function | Description |
| --- | --- |
| `listTenantFeatureProfiles` | Lists the feature profiles with their include and exclude rules and the tenants that use each one. |
| `saveTenantFeatureProfile` | Creates or replaces a feature profile: a name and an ordered list of `Include` or `Exclude` rules with an expression. |

Both tools need **Manage tenant feature profiles** and work from the Default tenant only. `saveTenantFeatureProfile` runs the `FeatureProfiles` recipe step and, as the **Feature Profiles** page does, refuses a name another profile has.

### Deployment plan tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Deployment`

| Function | Description |
| --- | --- |
| `listDeploymentPlans` | Lists the plans, optionally with the fields of their ordered steps and the step types the site offers. Search text and paging. |
| `saveDeploymentPlan` | Creates a plan or replaces the steps of the plan with the same name. |
| `deleteDeploymentPlan` | Deletes a plan. Asks for approval. |

`listDeploymentPlans` needs **Manage deployment plans** and **Export Data**, as the **Deployment Plans** page does; `saveDeploymentPlan` and `deleteDeploymentPlan` need **Manage deployment plans**. `saveDeploymentPlan` runs the `deployment` recipe step and refuses a step type that no enabled feature provides. No tool runs a plan: an export can hold any data of the site, secrets included, so it is downloaded with **File Download** on the plan page.

### Background task tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.BackgroundTasks`

| Function | Description |
| --- | --- |
| `listBackgroundTasks` | Lists the background tasks with their schedule, enabled state, status, last and next start, and run and failure counts. Given a task name, it also returns the lock settings and the recent runs with their error messages. Search text, a status filter (`enabled`, `disabled`, `running`, `failed`) and paging. |
| `runBackgroundTask` | Asks the background service to run one or more tasks now, outside their schedule, and says for each whether the run was queued. |

Both tools need **Manage background tasks**. Times are in the user's or the site's time zone. The execution state is kept in memory by each server, so on a web farm it shows the runs of the server that answered. `runBackgroundTask` returns as soon as the run is requested; call `listBackgroundTasks` afterwards to see how it went. When the background service is not registered, there is no state and nothing can be run on demand. Schedules and other task settings are changed on the **Background Tasks** page.

### URL rewriting tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.UrlRewriting`

| Function | Description |
| --- | --- |
| `listUrlRewriteRules` | Lists the redirect and rewrite rules in the order they are applied, with their pattern, substitution and options. Search text, a type filter and paging. |
| `saveUrlRewriteRule` | Creates a `Redirect` or `Rewrite` rule, or changes one by ID; fields that are not sent keep their value. A new rule is applied after the existing ones. |
| `deleteUrlRewriteRule` | Deletes one or more rules by ID. Asks for approval. |

All three tools need **Manage URLs rewriting rules**. `saveUrlRewriteRule` validates the rule as the **URL Rewriting** page does and saves nothing when a pattern is not a valid regular expression. After a save or a delete the tenant reloads its rules at the end of the request. Pages under the admin prefix are never rewritten. Rules are reordered by dragging them on the **URL Rewriting** page.

### Health check tool

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.HealthChecks`

| Function | Description |
| --- | --- |
| `getSiteHealth` | Runs the health checks the enabled features register, or one of them by name, and returns the overall status and each check's status, description and duration. |

The tool needs **Access admin panel**. It returns what the health endpoint shows with details on; exceptions and check data are left out, as they can hold connection details.

### Dynamic translation tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.DataLocalization`

| Function | Description |
| --- | --- |
| `listDynamicTranslations` | Lists the translatable dynamic strings of one culture with their translation, and the translation progress of every supported culture. Filters by context, search text and untranslated only, with paging. Without a culture it returns only the progress. |
| `saveDynamicTranslations` | Adds or changes translations of one culture; strings that are not sent keep their translation. |

`listDynamicTranslations` needs **View dynamic translations and statistics**. `saveDynamicTranslations` needs **Manage all dynamic translations** or the culture's own **Manage {culture} translations** permission, as the **Dynamic Translations** page does. The culture must be one of the site's supported cultures, each string must be one that `listDynamicTranslations` returns, and a translation cannot be empty. The tool runs the `Translations` recipe step, which merges the translations into the culture's saved ones. Translations saved in one reply are stored when the reply ends, so `listDynamicTranslations` asks to be called again in the next reply instead of reading them early.

### Workflow tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + the matching workflow feature set

| Function | Required feature(s) | Description |
| --- | --- | --- |
| `getWorkflowType` | `OrchardCore.Workflows` | Gets information about a workflow type. |
| `listWorkflowTypes` | `OrchardCore.Workflows` | Lists information about workflow types. |
| `createOrUpdateWorkflow` | `OrchardCore.Workflows` + `OrchardCore.Recipes.Core` | Creates or updates a workflow. |
| `listWorkflowActivities` | `OrchardCore.Workflows` + `OrchardCore.Recipes.Core` | Lists all available tasks and activities for workflows. |

### Workflow design tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Workflows`

| Function | Description |
| --- | --- |
| `getWorkflowActivity` | For up to 10 activity names, returns the JSON schema of the activity's `Properties`, its outcomes and whether they depend on its settings, its outputs and the values it provides to later activities. |
| `saveWorkflowType` | Creates a workflow from a full definition: settings, variables, activities named by aliases and the transitions between them. It generates the IDs, checks the definition first and saves nothing when there are errors; `validateOnly` only checks. The workflow is disabled unless `isEnabled` is set. Asks for approval. |
| `listWorkflowTypeVersions` | Lists a workflow's published versions, the most recent first, with who published each and when, and marks the version that runs now. |
| `cloneWorkflowType` | Copies a workflow's published activities, transitions, variables and settings under a new name. The copy is always disabled. |
| `enableWorkflowType` | Turns a workflow on at once, without publishing. |
| `disableWorkflowType` | Turns a workflow off at once: its events no longer start instances; instances that already wait are kept. |
| `deleteWorkflowType` | Deletes one or more workflows with their versions, draft, instances and journals. Asks for approval. |

All seven tools need **Manage workflows**. `saveWorkflowType` checks that each activity exists on the site, that only events start the workflow, that each activity's properties match the schema `getWorkflowActivity` returns, that transitions connect activities of the call with an outcome the activity produces, and that variables have a known type; these checks are not the designer's own validation. It asks for approval because what it saves is the published version of the workflow, not a draft, so it can go live without a separate publish step. It does not change an existing workflow: the designer keeps changes in a draft that only the Workflows module can write, so existing workflows are changed in the designer or with `createOrUpdateWorkflow`. The URL of an HTTP Request event is signed for its workflow, so `saveWorkflowType` and `cloneWorkflowType` leave it empty and say to generate it in the designer. Enabling or disabling changes the live workflow and creates no version; a draft someone left open in the designer still holds the earlier state and would bring it back when published.

### Workflow instance tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Workflows`

| Function | Description |
| --- | --- |
| `listWorkflowInstances` | Lists the instances of one workflow or of all, the most recent first, with paging. Each row has the instance ID, workflow, version, status, creation date, fault message, what it waits on and when a retry is due. |
| `getWorkflowInstance` | Returns one instance: status, version, the activities it waits on, its variable values, its fault and the activity that faulted, the pending retry, parent and child instances, and the last 50 journal records. `sequence` returns the data one activity run recorded; `includeState` adds the state as JSON. |
| `runWorkflowType` | Starts an enabled workflow that starts with **Started by Workflow**, with values for its input variables, and returns the instance, status, fault and output variables. For a workflow that starts with an HTTP Request event it returns the URL and method instead. Asks for approval. |
| `rerunWorkflowInstances` | `retry` runs faulted instances again from the activity that faulted, or from a given activity; `restart` starts a new instance with the original input on the published version. Asks for approval. |
| `cancelWorkflowInstances` | Cancels instances that have not ended: they become Aborted, stop waiting and drop a pending retry. Asks for approval. |
| `deleteWorkflowInstances` | Deletes instances and their journals. Asks for approval. |

`runWorkflowType` needs **Execute workflows**; the other tools need **Manage workflows**, and `retry` also needs **Execute workflows**, as on the instances page. The run, retry, restart, cancel and delete follow the admin pages: the same locks and the same check of workflows that run one instance at a time, and the instance that runs a cancelled or deleted one as an activity is resumed. The instance tools take at most 50 instance IDs. Running an instance again runs its activities again, so emails, HTTP calls and content changes can repeat. The status and date filters of the instances page are not offered, because they query an index of the Workflows module; each row returns its status. Times are in the user's or the site's time zone. Variable values and the recorded data of an activity can hold personal data and are cut to 2,000 characters each; the state is returned only when asked for.

### Workflow signal tool

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Workflows.Http`

| Function | Description |
| --- | --- |
| `triggerWorkflowSignal` | Sends a named signal to one instance, or to the instances waiting on a Signal event for a correlation ID, which also starts the enabled workflows that start with a Signal event. Asks for approval. |

The tool needs **Execute workflows**. It does what a signal URL does: only the Signal events whose name matches the signal go on.

### Workflow user task tool

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Workflows` + `OrchardCore.Contents` + `OrchardCore.Roles`

| Function | Description |
| --- | --- |
| `completeWorkflowUserTask` | Chooses a user task action, such as Approve, for a content item a workflow waits on, which resumes the workflow on that outcome. Without an action, or with one the user may not choose, it returns the actions the user may choose. Asks for approval. |

The tool needs **Edit content** on the item, as the action buttons are in its editor, and offers only the actions of the User Task events whose roles the user has (every action when the event names no role). Unlike the buttons, it does not save or publish the item.

### Data pipeline tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.DataPipelines`

| Function | Description |
| --- | --- |
| `listDataPipelineStepTypes` | Lists the step types of the pipeline designer by category (source, transform, file, destination), with their input and output ports: records or files, required, and whether a port accepts many connections. |
| `listDataSets` | Lists the data sets the user may read from every data source (content types, users, saved queries and the data of other modules), with the `Source::DataSet` value a **Read a data source** step uses, the name, description, data source and group. Filter by text or by data source. |
| `getDataSetSchema` | Returns the fields of one data set: name, label, type, group, the field that identifies a row, the fields that can filter by key and what they refer to, and the default date field. It returns no rows. |

`listDataPipelineStepTypes` needs **View data pipelines**; `listDataSets` and `getDataSetSchema` need **Manage data pipelines**, as the designer's **Add a data source** dialog does. The data sources are asked with the user, so they leave out what the user may not read, and `getDataSetSchema` answers the same way for a data set that does not exist and one the user may not read. The step settings schemas are not offered. The pipelines themselves (list, design, preview, publish, run and run history) are not reachable by tools yet: their documents and services live in the Data Pipelines module, which this module does not reference. Use the designer, or the `DataPipelines` recipe step through `importOrchardCoreRecipe`.

### Shared link tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.DataPipelines.Core`

| Function | Description |
| --- | --- |
| `listSharedLinks` | Lists the shared links of every module, the most recent first: title, kind, what shared them, recipients, when they were created and expire, uses, and whether they still work (Active, Expired, UsedUp or Revoked). Filter by title or by what shared them. |
| `revokeSharedLink` | Stops a link from working for all its recipients; the shared file or resource is kept until the link is deleted. Asks for approval. |
| `deleteSharedLink` | Deletes a link together with the resource it shared, such as the stored copy of an exported file. Asks for approval. |

The tools need **Manage shared links**. A link's URL holds a token that is only emailed to its recipients and never stored, so no tool returns it; the tools also never return the token hash, the resource ID or the recipients' user IDs. Times are in the user's or the site's time zone.

### AI profile tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `CrestApps.OrchardCore.AI`

| Function | Description |
| --- | --- |
| `listAITools` | Lists what can be attached to a profile: registered tools by category (whether they ask for approval and whether they are safe for visitors who are not signed in), tool instances and agent profiles. Only the tools and tool instances the user may use are listed, as in the profile editor. |
| `saveAIProfile` | Creates a profile or agent, or changes one by ID: title, description, type, instructions, welcome message, initial prompt, deployments, model parameters, persona and SystemPrompt templates. It never changes the profile's tools, agents or connections, its instruction lock or whether it can be deleted, and refuses new instructions on a profile whose instructions are locked. |
| `setAIProfileCapabilities` | Sets the tools, tool instances and agents a profile can use and, for an agent, whether it is always available and whether it runs its tools. MCP and A2A connections are set on the profile's Capabilities tab. Asks for approval. |
| `createAIProfileFromTemplate` | Creates a profile from a Profile template, as the **New AI profile** wizard does: a title, a technical name and optionally a chat deployment. It names the features to enable when the template's scenario needs them. |
| `deleteAIProfile` | Deletes profiles by ID. Profiles marked not removable and the assistant that is running, with its agents, are skipped and reported. Asks for approval. |
| `listAIProfileVersions` | Lists a profile's saved versions (number, date, author, note), and with `versionId` the settings that differ from the profile as it is now or from another version, as the **Compare** page shows them. |
| `restoreAIProfileVersion` | Restores a profile to a saved version, saved as a new version so it can be undone. Asks for approval. |
| `testAIProfile` | Sends one message to a profile with its tools turned off and returns the reply. It makes a paid model call. Asks for approval. |

The tools need **Manage AI profiles**; `testAIProfile` needs **Query any AI profile** on the profile, so a profile-specific grant counts. Every change is saved as a profile version whose note says the AI assistant made it, and for whom.

Until a site-wide guard for AI configuration exists, the tools that change profiles, templates, personas or tool instances apply three rules themselves:

- **An assistant cannot change itself.** From inside a chat, the profile that runs the tool, the chat session's profile, the admin chat widget's profile, the agents those can call (including every always-available agent), and the persona, SystemPrompt templates, speaking styles, guardrail topics and tool instances they use cannot be changed or deleted. The tool refuses and names the admin page where a person can make the change. This is a refusal, not an approval, because an approval can be clicked through and is not asked inside an agent.
- **No powerful tools on a profile visitors can reach.** A profile is reachable when the Anonymous role may query it, an enabled browser API key names it, a published content item with an AI profile part shows it, or it is an always-available agent. `setAIProfileCapabilities`, `restoreAIProfileVersion`, `createAIProfileFromTemplate` and `saveAIProfile` may then only add tools marked safe for anonymous callers, and no tool instance, MCP or A2A connection or agent that runs tools. Making an agent always available, or letting an always-available agent run its tools, checks all of its tools. Phone numbers and messaging channels are not checked yet.
- **Only tools the user may use.** Every tool and tool instance a change adds needs the user's own **Access AI tool** permission for it, as the profile editor asks.

### AI profile template tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `CrestApps.OrchardCore.AI`

| Function | Description |
| --- | --- |
| `listAIProfileTemplates` | Lists templates with their source (Profile or SystemPrompt), category, title, whether a module ships them (read-only) and, for a Profile template, whether its scenario can be used and which features it still needs. |
| `getAIProfileTemplate` | Returns one template: its instructions and, for a Profile template, the profile settings, tool and agent names it gives a profile created from it. |
| `saveAIProfileTemplate` | Creates or changes a stored template, including a speaking style (category `Speaking style`) or a guardrail topic (category `Guardrail topic`), which are created unlisted as their admin pages do. A Profile template's tools and agents are set on its admin page. Shipped templates cannot be changed. |
| `deleteAIProfileTemplate` | Deletes stored templates by ID; shipped templates are skipped. Asks for approval. |

The tools need **Manage AI profile templates**.

### AI persona tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `CrestApps.OrchardCore.AI.Personas`

| Function | Description |
| --- | --- |
| `listAIPersonas` | Lists the personas, saved and from files, with their assistant name, language, voice and the profiles that link to each. With `name` it returns that persona's instructions too. |
| `saveAIPersona` | Creates a persona or changes one by ID. The technical name never changes, and saving a persona file saves a copy with the same name, which the linked profiles then use. |
| `deleteAIPersona` | Deletes saved personas by ID and says how many profiles now run without a persona. Persona files are skipped. Asks for approval. |

The tools need **Manage AI personas**.

### AI tool instance tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `CrestApps.OrchardCore.AI.ToolInstances`

| Function | Description |
| --- | --- |
| `listAIToolInstances` | Lists tool instances with their source, function name, owner, settings and the profiles that use them. With `includeSources` it lists the sources and the settings each one takes through the tools. |
| `saveAIToolInstance` | Creates a tool instance from a source or changes one by ID. Asks for approval. |
| `deleteAIToolInstance` | Deletes tool instances by ID. Asks for approval. |

The tools need **Manage AI tool instances**, and **Manage AI tool instances created by others** for an instance someone else owns, as the admin page does. Credentials (API keys, bearer tokens, passwords, client secrets and OAuth tokens) and default headers are never returned, only whether each is stored, and never accepted: `saveAIToolInstance` saves the rest and returns the admin page where a person enters them. Stored credentials stay when other settings change, and the address an instance with a stored credential calls cannot be changed from a chat. Addresses must be absolute `http` or `https` addresses outside the server and its internal networks; the host name is not resolved, so the request itself must still be guarded. The settings of the Workflow source are set on its admin page.

### AI provider connection tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `CrestApps.OrchardCore.AI.ConnectionManagement`

| Function | Description |
| --- | --- |
| `listAIProviderConnections` | Lists provider connections with provider, name, title, endpoint, authentication type, whether a key is stored, whether the connection comes from configuration (read-only) and which deployments use it. With `includeProviders` it lists the providers a connection can be created for. |
| `saveAIProviderConnection` | Creates a provider connection or changes one by ID: title, endpoint and, for Azure, the authentication type and managed identity. Saving reloads the site, which can end the current chat. Asks for approval. |
| `deleteAIProviderConnection` | Deletes provider connections by ID, names the deployments that stop working, and reloads the site. Asks for approval. |

The tools need **Manage AI provider connections**. Keys are never returned, only whether one is stored, and never accepted: a connection that cannot exist without a key (an OpenAI connection, or an Azure one with key authentication) is not created, and the result links to the admin page where a person creates it with its key. Connections defined in configuration (appsettings) cannot be changed or deleted. From inside a chat, the connection the running assistant's deployments use cannot be changed or deleted.

### AI deployment and voice tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `CrestApps.OrchardCore.AI`

| Function | Description |
| --- | --- |
| `listAIDeployments` | Lists deployments with provider, connection, model, capabilities, prices, whether a contained key is stored, whether they come from configuration, and which slot defaults and profiles use them. Filter by provider, connection or capability; `includeOptions` lists the providers and capabilities. |
| `saveAIDeployment` | Creates a deployment under a provider or changes one by ID: model, connection, capabilities and, for speech providers that hold their own connection, the endpoint and sign-in. The technical name never changes. Asks for approval. |
| `setAIDeploymentPricing` | Sets or clears what a deployment costs: a currency and prices per meter, with the checks of the **Pricing** card. It only changes the cost estimates of the usage reports. |
| `deleteAIDeployment` | Deletes deployments by ID and names the profiles that named them. The site's default deployments are skipped until another default is picked. Asks for approval. |
| `listAIVoices` | Searches the voices of a text-to-speech deployment by text, language and gender, one page at a time with the provider's cursor. |
| `cloneAIVoice` | Clones a voice at the provider from a recording of up to 10 MB in the media library and returns its ID. The provider may charge for it. Asks for approval. |
| `deleteAIVoice` | Deletes a voice at the provider. It cannot be undone. Asks for approval. |

The tools need **Manage AI deployments**; `cloneAIVoice` also needs the media permission for the recording's folder. A key a speech deployment holds is never returned, only whether one is stored, and never accepted: the result links to the deployment's admin page, where a person enters it. Deployments defined in configuration cannot be changed or deleted. From inside a chat, the deployments the running assistant uses (its profiles' chat and utility deployments, the site defaults they fall back to, and its persona's voice deployment) cannot be changed or deleted; their prices can.

### AI quality management tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `CrestApps.OrchardCore.AI.Quality`

| Function | Description |
| --- | --- |
| `listAITestRuns` | Lists the runs of a batch or a test case with status, score, pass or fail, the judge's findings, the pass rate and how many are still pending. Use it after `runAITests`. |
| `saveAITestCase` | Creates or changes a simulation test case. `fromConversationId` starts from a real conversation, as **Create test from conversation** does. |
| `deleteAITestCases` | Deletes test cases by ID; their runs stay. Asks for approval. |
| `saveAIAlertRule` | Creates or changes an alert rule, or turns one on or off. A changed metric, condition, threshold or profile is checked again at once. |
| `deleteAIAlertRule` | Deletes alert rules by ID, with their incident history. Asks for approval. |
| `listAIQACohorts` | Lists QA cohorts: profiles, sample, criteria, passing score, judge deployment and whether each is on. |
| `saveAIQACohort` | Creates or changes a QA cohort. An enabled cohort makes a paid judge call for every sampled conversation. Asks for approval. |
| `deleteAIQACohort` | Deletes QA cohorts by ID; recorded scores stay. Asks for approval. |

The tools need **Manage AI tests, QA cohorts and alert rules** and apply the same checks as the editors of the **Tests**, **Alert rules** and **QA** pages. Times are in the user's or the site's time zone.

### AI API key tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `CrestApps.OrchardCore.AI.Chat.Api`

| Function | Description |
| --- | --- |
| `listAIApiKeys` | Lists API keys with name, kind, prefix, scopes, roles, allowed origins and profiles, expiry, enabled state and last use. The key value and its hash are never returned. |
| `saveAIApiKey` | Changes an existing key: name, enabled, expiry, scopes and roles of a server key, web sites and profiles of a browser key. It never makes a key; it returns the link to the **API keys** page, where a new key's value is shown once. Asks for approval. |
| `deleteAIApiKey` | Deletes (revokes) keys by ID. Clients using them stop working at once. Asks for approval. |

The tools need **Manage AI API keys** and apply the checks of the key editor. A key's value never passes through the assistant, a browser key's value included. A role must exist and cannot be Anonymous. A change that lets a browser key, which is public, chat with a profile that can run tools not marked safe for visitors (adding the profile, enabling the key again, or extending its expiry) is refused.

### AI conversation tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `CrestApps.OrchardCore.AI.Chat`

| Function | Description |
| --- | --- |
| `deleteAIConversation` | Deletes one of the user's own conversations and its transcript by session ID. It cannot be undone. Asks for approval. |
| `deleteAllAIConversations` | Deletes every one of the user's own conversations with one profile and returns how many were removed. It cannot be undone. Asks for approval. |

Like a profile's chat **History** page, the tools delete the user's own conversations through the chat session manager. `deleteAIConversation` needs **Delete chat session** and `deleteAllAIConversations` **Delete all chat sessions**, and both need to be allowed to chat with the profile. The conversation the assistant is answering in is never deleted, and `deleteAllAIConversations` is refused for the profile of the running chat.

### AI usage tool

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `CrestApps.OrchardCore.AI.UsageAnalytics`

| Function | Description |
| --- | --- |
| `queryAIUsage` | Returns AI usage for a period of local days: requests, input, cached, reasoning, output, audio and image tokens, audio minutes and estimated cost, grouped by model, deployment, AI profile, connection, user, day or kind of request, with totals. |

The tool needs **View AI Chat Analytics**. It reads the usage records the **AI Usage Analytics** report reads, every request sent to an AI provider, and estimates costs from the prices set on each deployment; rows of different currencies are kept apart. Voice call minutes are on the report only.

### Chat interaction tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `CrestApps.OrchardCore.AI.Chat.Interactions`

| Function | Description |
| --- | --- |
| `listChatInteractions` | Lists chat interactions with title, owner, deployment and last activity, newest first, with search and paging. Without **List chat interactions for others** only the user's own are listed. |
| `deleteChatInteractions` | Deletes chat interactions by ID, with their messages and uploaded documents. Interactions the user may not delete are skipped and reported. Asks for approval. |

`listChatInteractions` needs **List chat interactions**. `deleteChatInteractions` checks **Delete chat interaction** on each interaction, as the **Chat Interactions** page does, so **Delete own chat interaction** covers the user's own. Profile Builder sessions and the interaction the assistant is answering in are left alone.

### MCP connection tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `CrestApps.OrchardCore.AI.Mcp`

| Function | Description |
| --- | --- |
| `listMcpConnections` | Lists MCP connections with transport, title, endpoint or command, authentication, which credentials are stored and which profiles use them. |
| `saveMcpConnection` | Creates or changes a remote (SSE) MCP connection: title, endpoint and authentication. Asks for approval. |
| `deleteMcpConnection` | Deletes MCP connections by ID, local ones included, and names the profiles that lose their tools. Asks for approval. |

The tools need **Manage MCP connections**. No credential is returned or accepted: keys, passwords, client secrets, private keys and certificates show only whether one is stored, custom headers and a local server's environment variables show their names only, and a local server's arguments their count only. A new connection is anonymous; `saveMcpConnection` can switch a connection to anonymous access, which removes its credentials, and change the fields of its authentication that are not secret, but a person enters any credential on the admin page the result links to. A connection that stores a credential cannot be moved to another host from a chat, since the credential would be sent there. The endpoint must be an absolute `http` or `https` address. A local (stdio) connection runs a program on the web server, so it is never created or changed by the tool. From inside a chat, the connections the running assistant uses cannot be changed or deleted.

### MCP server prompt and resource tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `CrestApps.OrchardCore.AI.Mcp.Server`

| Function | Description |
| --- | --- |
| `listMcpPrompts` | Lists the prompts the site's MCP server offers, with their arguments and messages. |
| `saveMcpPrompt` | Creates or changes a prompt: name, title, description, arguments and the user and assistant messages. |
| `deleteMcpPrompt` | Deletes prompts by ID. Asks for approval. |
| `listMcpResources` | Lists the resources the site's MCP server offers, with type, URI pattern and MIME type, and whether credentials are stored. `includeTypes` lists the resource types and the URI variables each supports. |
| `saveMcpResource` | Creates or changes a resource of a type with a title, name, path, description and MIME type. Asks for approval. |
| `deleteMcpResource` | Deletes resources by ID. Asks for approval. |

The prompt tools need **Manage MCP prompts** and the resource tools **Manage MCP resources**, and they apply the checks of the editors. An FTP or SFTP resource needs a host and credentials, so it is created on the admin page; its password, private key and passphrase are listed only as stored or not, and its connection is not changed by the tool.

### A2A connection tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `CrestApps.OrchardCore.AI.A2A`

| Function | Description |
| --- | --- |
| `listA2AConnections` | Lists A2A connections with title, endpoint, authentication, whether credentials are stored and which profiles use them. |
| `saveA2AConnection` | Creates or changes a connection to a remote A2A host: title, `http` or `https` endpoint and authentication. Profiles that use it send conversation content to that host. Asks for approval. |
| `deleteA2AConnection` | Deletes A2A connections by ID and names the profiles that lose those remote agents. Asks for approval. |

The tools need **Manage Agent-to-Agent connections** and handle credentials as the MCP connection tools do. From inside a chat, the connections the running assistant uses cannot be changed or deleted.

### AI memory tool

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `CrestApps.OrchardCore.AI.Memory`

| Function | Description |
| --- | --- |
| `clearAIUserMemory` | Deletes everything the AI has remembered about one user, by user name or ID, or about the user in the chat, and returns how many memories were removed. It cannot be undone. Asks for approval. |

The tool needs **Clear AI memory** for the user's own memory and **Clear AI memory for other users** for anyone else's, checked separately, as the Clear AI memory action of a user's page does. Each memory is deleted through the memory manager, so it leaves the memory index too.

### AI data source tools

**Available when:** `CrestApps.OrchardCore.AI.DataSources` with `CrestApps.OrchardCore.AI` on. `saveAIDataSource` and `deleteAIDataSource` also need `CrestApps.OrchardCore.AI.Agent`.

| Function | Description |
| --- | --- |
| `listAIDataSources` | Lists the AI data sources (knowledge bases) with their source type, destination index and field mapping, with name search, a source type filter and paging, or one data source by ID. Also returns the source types that can be created and the AI knowledge base indexes. |
| `saveAIDataSource` | Creates a data source of a source type, or renames and remaps one by ID. A data source whose configuration is locked can only be renamed. Azure AI Search and Elasticsearch data sources are created on a data connection, PostgreSQL ones on the site's connection string. Asks for approval. |
| `deleteAIDataSource` | Deletes a data source, queues the removal of its knowledge base documents and names the profiles that used it. Asks for approval. |
| `syncAIDataSource` | Queues a full synchronization of one data source into its knowledge base index, as **Sync index** on the Data Sources page does. It spends embedding tokens. Asks for approval. |
| `searchAIDataSource` | Runs a test search of one data source for a phrase and returns what an assistant using it would receive. It makes one embedding call. |

The tools need **Manage AI data sources**, and picking a data connection also needs **Manage data connections**. No tool returns or takes an API key, a password or a connection string: the list reports only whether one is stored (`hasApiKey`, `hasPassword`, `hasConnectionString`), and their own endpoint and credentials are entered on the data source's page. Saving a data source queues a full synchronization, as the editor does. From inside a chat, the data sources the running assistant answers from (through its profile, a chat interaction or a data source search tool instance) cannot be changed or deleted.

### AI profile document tool

**Available when:** `CrestApps.OrchardCore.AI.Documents.Profiles` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listAIProfileDocuments` | Lists the documents an AI profile answers from, with file name, type and size, and the profile's document retrieval settings. |

The tool needs **Manage AI profiles**. Documents are added and removed on the profile's **Knowledge** tab.

### AI file source tools

**Available when:** `CrestApps.OrchardCore.AI.FileSources` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listAIFileSources` | Lists the file sources that feed File data sources, with connector, data source, schedule, connector settings and the last run, or one file source by ID. Also returns the connectors enabled on the site. |
| `saveAIFileSource` | Creates a file source for this tenant's folder or an FTP server, or updates one by ID: name, data source, schedule, figure settings and connector settings. Asks for approval. |
| `deleteAIFileSource` | Deletes a file source and queues the removal of what it ingested. Asks for approval. |
| `runAIFileSource` | Queues a run that reads the folder or server and ingests new and changed files after the reply; `listAIFileSources` shows its result once it has run. Asks for approval. |

The tools need **Manage file sources**. No FTP or SFTP password, private key, passphrase or proxy password is returned or taken: the list reports whether one is stored, and the save result links to the page where a person enters it. A folder can only be inside the tenant's own `file-sources` folder. Hosts and ports are checked as the editors check them (a host, and a port from 1 to 65535), and a host or port cannot change while a credential is stored for it. An SFTP file source needs a password or a private key, so it is created on its page. Saving or deleting a file source queues a full synchronization of its data source. From inside a chat, a file source that feeds a data source the running assistant answers from cannot be saved or deleted.

### AI web crawler tools

**Available when:** `CrestApps.OrchardCore.AI.WebCrawlers` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listAIWebCrawlers` | Lists the web crawlers that feed Web data sources, with site, sitemap, limits, URL patterns, data source, enabled state and schedule, or one crawler by ID. Also returns the crawl strategies. |
| `saveAIWebCrawler` | Creates a web crawler for a website, or updates one by ID. Asks for approval, because saving queues a full synchronization of the data source, which crawls the site. |
| `deleteAIWebCrawler` | Deletes a web crawler; its pages stop being refreshed. Asks for approval. |
| `runAIWebCrawler` | Re-crawls one website now and queues its new, changed and removed pages for indexing, returning the counts. Asks for approval. |

The tools need **Manage web crawlers**. Addresses are checked as the crawler editor and the sitemap strategy check them: each given base or sitemap URL must be absolute, at least one of them must use `http` or `https`, and the URL patterns must be valid regular expressions. Nothing stops a crawler from reaching an address on the server's own network, so confirm the address before approving. From inside a chat, a crawler that feeds a data source the running assistant answers from cannot be saved or deleted.

### Data connector tools

**Available when:** `CrestApps.OrchardCore.DataConnectors` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listDataConnections` | Lists the connections to external systems with their connector and non-secret settings, with name search, or one connection by ID. Also returns the connectors enabled on the site. |
| `saveDataConnection` | Creates an HTTP API, Elasticsearch or media files connection, or updates one by ID: name, description and the connector's settings. Credentials are entered on the connection's page. Asks for approval. |
| `testDataConnection` | Checks that a saved connection reaches its system and returns success or the error. |
| `deleteDataConnection` | Deletes a connection and its stored credentials; refused while a data set reads it. Asks for approval. |
| `listDataConnectionResources` | Lists what a connection offers to read, such as tables and views, with search and paging. |
| `listConnectedDataSets` | Lists the connected data sets with connection, resource, access mode, enabled state, row count and refresh state. |
| `getConnectedDataSet` | Returns one data set in full: fields, refresh settings, import state, connector settings and, on request, its latest refreshes. |
| `saveConnectedDataSet` | Creates a data set on a connection, or updates one by ID: resource, live or imported, fields, default date field and refresh schedule. |
| `refreshConnectedDataSet` | Requests a refresh of an imported data set; the refresh task starts it within about a minute. |
| `previewConnectedDataSet` | Returns the first rows of a data set, at most 20 and about 40,000 characters. |
| `deleteConnectedDataSet` | Deletes a data set with its imported rows and refresh history. Asks for approval. |

The connection tools need **Manage data connections**, the data set tools **Manage connected data sets**, `refreshConnectedDataSet` needs **Refresh imported data sets**, and `previewConnectedDataSet` also needs the data set's read permission (`ReadConnectedDataSet_{name}`). No tool returns or takes an API key, password, bearer token, client secret or header value: the list reports whether a secret is stored (`hasApiKey`, `hasPassword`, `hasBearerToken`, `hasClientSecret`) and a header by its name only, and `saveDataConnection` saves the rest and links to the page where a person enters the credential. While a connection stores a credential or headers, its server cannot be moved to another host, and while it stores a credential, its authentication type, grant, token endpoint host and issuer host cannot change. SQL Server connections are never created by a tool, because they sign in with a password or with the identity of the site's host; only their name and description can be changed. The editors' checks apply, and connections are reached through the same reader as the admin pages, so the network rules of `CrestApps:DataConnectors:Network` apply. A new imported data set has its first import requested when the user may refresh data sets. The connector's own settings of a data set (an HTTP request, a query) are set on its page.

### Content transfer tools

**Available when:** `CrestApps.OrchardCore.ContentTransfer` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listContentTransferTypes` | Lists the content types the user may bulk import or export, the file formats enabled, and the import and export columns of one content type. |
| `listContentTransferEntries` | Lists bulk imports, or the user's own exports, with status, file, progress, author and dates, filtered by status, newest first. |
| `getContentTransferEntry` | Returns one entry with its progress, its first 50 failed rows with their errors, and the download link of a completed export. |
| `setContentImportState` | Pauses a processing import, or resumes a pending, paused or failed one; the Imported Files Processor continues it within about a minute. |
| `deleteContentTransferEntry` | Deletes an entry and its stored file in the background after the reply. Content items already imported stay. Asks for approval. |

The tools check the permissions of the Import and Export pages per content type: **Import content items from file** for imports, **Export content items from file** for exports, **List content transfer entries** for the import list and **Delete content transfer entries** for a delete. An export is shown and deleted only for the user who made it. An import that is still processing, running or stalled, is resumed from the Import list. Imports and exports are started on the Import and Export pages.

### Report tools

**Available when:** `CrestApps.OrchardCore.Reports` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listReports` | Lists the reports the user may open: built-in reports with their category, and, when the Report Builder is on, designed reports with their group and owner. Text search and a category or group filter. |
| `getReport` | Describes one report: its filters, default period, export formats and whether the user may export. For a designed report, on request, its definition, versions and share links. |
| `runReport` | Runs a report for a period (and, for a designed report, filter values) and returns its headline numbers and tables, 200 rows per table by default, at most 1,000, and about 60,000 characters. |
| `exportReport` | Returns the download link of a CSV, Excel or PDF export with its file name. The file is built when the link is opened. |

One set of tools serves every report a module registers and every designed report. A built-in report needs its own permission, and its export permission to export, as on the Reports page. A designed report must be one the user may view (its owner, the people and roles it is shared with, and **View all custom reports**), and runs with its owner's access, as on its page; exporting it needs the report to allow export or the right to change it. A built-in report takes only a period (`from`, `to` as days in the user's time zone, or `allTime` when the report offers it): its other filters, such as an AI profile or a queue, are set on its page, because they bind from the page's form. The download link is the report page's own export address, so whoever opens it is checked again and nothing is stored. Dates in results are in the user's or the site's time zone.

### Report Builder tools

**Available when:** `CrestApps.OrchardCore.Reports.Builder` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listReportDataSets` | Lists the data sources and the data sets the user may read, for one source or matching a text. |
| `getReportDataSetSchema` | Returns the fields of one data set and the formula functions for calculated fields. |
| `queryReportData` | Runs an ad hoc query (data sets, joins, calculated fields, filters, grouping, aggregates, sorts, limit) with the user's own access and returns rows. Nothing is saved. |
| `saveReportDesign` | Creates and publishes a designed report, or publishes a change to one by ID: title, description, group, query, visuals, admin menu and export options. Never changes who the report is shared with. |
| `shareReportDesign` | Sets the users and roles a designed report is shared with. Asks for approval. |
| `restoreReportDesignVersion` | Restores and publishes an earlier version of a designed report; who it is shared with does not change. |
| `deleteReportDesign` | Deletes a designed report with its draft, versions and share links, or a report that was never published. Asks for approval. |
| `createReportShareLink` | Creates a share link for named users of the site and emails it to them. The link is never returned. Asks for approval. |
| `revokeReportShareLink` | Revokes one share link of a designed report. Asks for approval. |
| `listReportViews` | Lists the saved report views with owner, refresh interval and snapshot state, or one view with its query and the reports and views that read it. |
| `saveReportView` | Creates a report view from a query, or updates one by ID, with an optional refresh interval. |
| `refreshReportView` | Recomputes the stored rows of a scheduled view now. |
| `deleteReportView` | Deletes a report view that no report or view reads. Asks for approval. |
| `listReportGroups` | Lists the report groups as a tree with the number of groups and reports in each. |
| `saveReportGroup` | Creates a report group under a parent, or renames, moves or reorders one by ID. |
| `deleteReportGroup` | Deletes an empty report group; a group that holds groups or reports is kept. Asks for approval. |

The data set, query and view tools and creating a report need **Build reports and manage own custom reports and views**; changing, sharing, restoring or deleting a report or view needs **Manage all custom reports and views** on it, which its owner holds; share links also need **Share custom reports publicly and through share links**, and so does sharing with the Anonymous role; the group tools need **Manage report groups**. A report that has unpublished changes in the Report Builder is not published over: the tool names who changed it and asks to publish or discard the draft first. Every user and role a report is shared with must exist. A group is given by its path, such as `Sales / Regional`, and must exist. A share link's address holds a secret token, so `createReportShareLink` emails it to its recipients and returns only its ID, recipients and expiry; the Report Builder shows the address once. A report, view or group changed earlier in the same chat message is changed again only in the next reply, because the change is saved when the reply ends.

### Report subscription tools

**Available when:** `CrestApps.OrchardCore.Reports.Subscriptions` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listReportSubscriptions` | Lists the scheduled report emails with their report, frequency, hour, recipients, attachment format, enabled state, next run, last run and last error, with a name search and paging, or one by ID. |
| `saveReportSubscription` | Creates a schedule that emails a built-in report daily, weekly or monthly to up to 50 addresses, or changes one by ID, including enabling or disabling it. Asks for approval. |
| `sendReportSubscription` | Emails a subscription's report now, for the period before now, without moving its schedule, and returns the error when it could not be sent. Asks for approval. |
| `deleteReportSubscription` | Deletes a report subscription. Asks for approval. |

The tools need **Manage report subscriptions**. Saving goes through the subscription editor's checks for the user: a built-in report they may view, at most 50 valid email addresses, an hour from 0 to 23 in the site's time zone and a day of the month from 1 to 28, and an attachment format only when they may export the report. The user becomes the person who last saved the subscription, whose export permission an attachment needs at every run. Sending runs the same checks on what is stored first, as **Save and send** does, so the user must be able to view the report, and export it when a file is attached. Designed reports are emailed with the Email Report workflow task instead. Emails go out only when the site's email is set up; the results say so when it is not.

### Webhook tools

**Available when:** `CrestApps.OrchardCore.Webhooks` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listWebhooks` | Lists the outbound webhooks with name, URL, events, enabled state and the number of failed deliveries, or one by ID. |
| `listWebhookEvents` | Lists the events the site can send to webhooks, by group. |
| `saveWebhook` | Creates a webhook for chosen events, or all events, posting to an https URL, or changes one by ID, including enabling or disabling it. Asks for approval. |
| `rotateWebhookSecret` | Gives a webhook a new signing secret. Asks for approval. |
| `deleteWebhook` | Deletes a webhook and its delivery log. Asks for approval. |
| `sendWebhookPing` | Sends a test `webhook.ping` event now and returns the receiver's status code or the error. Asks for approval. |
| `listWebhookDeliveries` | Lists the deliveries of one webhook with event, status, attempts, last status code, last error and times, by status. Payloads are not returned. |
| `resendWebhookDelivery` | Sends one logged delivery of a webhook again and returns the receiver's answer. Asks for approval. |

The tools need **Manage webhooks** (`ManageCommerceWebhooks`), as every action of **Configuration → Webhooks** does. A webhook sends site data to another system, so every change, ping and resend asks for approval. Addresses are checked as the webhook editor checks them: an absolute `https` address the egress guard allows (plain `http` only to this machine in development), and all events or at least one event an enabled module declares; events no module declares are reported and left out. No tool takes or returns a signing secret: a new or rotated secret is copied from the webhook's admin page, whose address the results give. A ping and a resend are sent during the call, as on the admin page.

### Analytics tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `CrestApps.OrchardCore.AI.Chat.Analytics`

| Function | Description |
| --- | --- |
| `queryChatSessionMetrics` | Queries aggregated chat session analytics metrics with optional date range and profile filters, returning statistics for charts and reports. |

### Memory tools

**Available when:** `CrestApps.OrchardCore.AI.Memory` is enabled and user memory is enabled for the current authenticated user

| Function | Description |
| --- | --- |
| Search User Memories | Semantic search across the current user's saved memories. |
| List User Memories | Enumerates the current user's existing memories. |
| Save User Memory | Creates or updates a named memory entry for the current user. |
| Remove User Memory | Removes a saved memory entry when it should be forgotten. |

### NLWeb tools

**Available when:** `CrestApps.OrchardCore.NLWeb` with `CrestApps.OrchardCore.AI` on

The tool answers as the caller, with the **Ask the site questions with NLWeb** (`QueryNLWeb`) permission, and only returns published content items the caller may view. See [NLWeb](../modules/nlweb.md#the-ask-tool).

| Function | Description |
| --- | --- |
| `ask` | Asks the site a question in natural language (NLWeb) and returns the matching content items as schema.org objects, with a summary or an answer written from them when asked. Selectable on the MCP Server, WebMCP and AI profiles. |

### Search Engine Optimization tools

**Available when:** `CrestApps.OrchardCore.Seo` with `CrestApps.OrchardCore.AI` on

The tools set up schema.org structured data. Changing a content type needs **Edit content types**, and changing the site settings needs **Manage the advanced SEO settings**. See [Advanced SEO](../modules/advanced-seo.md#ai-tools).

| Function | Description |
| --- | --- |
| `getSchemaOrgOptions` | Lists the content types with their schema.org type and the types the editor knows; for a content type, its settings, the values its items can give, a type's properties and the suggested mappings. |
| `configureContentTypeSchemaOrg` | Adds the Schema.org part to a content type if needed, sets its schema.org type and maps its properties, adding the suggested mappings unless told not to. Returns the required properties still missing. |
| `configureSiteStructuredData` | Reads or changes the site's publisher, logo and profiles, and whether pages describe the web site and its search. |

### Calendar tools

**Available when:** `CrestApps.OrchardCore.Calendars` with `CrestApps.OrchardCore.AI` on

These tools act for the signed-in person using the AI, with that person's access to each calendar, and refuse anonymous callers and callers without **Use calendars** (`AccessCalendars`). See [Calendars](../modules/calendars.md#ai-tools).

| Function | Description |
| --- | --- |
| `listCalendars` | Lists the calendars the user can see. |
| `findCalendarEvents` | Lists the events on the user's calendars between two times (at most 62 days apart). |
| `findFreeTime` | Finds times when the user and others are free, optionally together with calendars such as a room (at most 31 days). |
| `createCalendarEvent` | Adds an event to one of the user's calendars, or to their personal calendar when none is named. |
| `moveCalendarEvent` | Moves an event, or one occurrence of a repeating event, to another time. |
| `cancelCalendarEvent` | Cancels an event, or one occurrence of a repeating event. |

### Scheduling tools

**Available when:** `CrestApps.OrchardCore.Scheduling` with `CrestApps.OrchardCore.AI` on

These tools are meant for customer-facing assistants and also work for anonymous visitors. They offer only appointment types that are enabled and allow self-service, unless the caller has **Manage appointments**, and hold customers to the appointment type's booking policies. See [Scheduling](../modules/scheduling.md#ai-tools).

| Function | Description |
| --- | --- |
| `listAppointmentTypes` | Lists the kinds of appointments that can be booked. |
| `findAppointmentSlots` | Finds open times for an appointment. |
| `bookAppointment` | Books an appointment for the customer at a time `findAppointmentSlots` returned, and says which staff, rooms and equipment it is with. |
| `changeAppointment` | Cancels or moves a customer's appointment. The customer proves it is theirs with the manage code, or the email or phone number on the appointment. |

### Commerce tools

**Available when:** `CrestApps.OrchardCore.AgenticCommerce` with `CrestApps.OrchardCore.AI`, `CrestApps.OrchardCore.Products.Listing` and `CrestApps.OrchardCore.Products.Feeds` on

These tools are meant for a customer-facing shopping assistant and also work for anonymous visitors. They read the same products as the store's product feeds, and only products agents may see (**Show to AI agents**). The assistant never pays: `startStoreCheckout` returns a link where the buyer enters their address and pays in the store's own checkout. A signed-in visitor's checkout is theirs. The cart tools only use the caller's own cart. The tools are in the **Commerce** category and selectable on AI profiles, the MCP Server and WebMCP. They are marked safe for anyone, so WebMCP can offer them to visitors who aren't signed in (see [storefront tools](mcp/webmcp.md#storefront-tools)). See [Agentic Commerce](../modules/agentic-commerce.md#ai-tools).

| Function | Description |
| --- | --- |
| `searchStoreProducts` | Searches the store's products (`query`, `limit` 1 to 20, 10 by default). Returns each product with its variants, the item id to buy each with, its price and whether it is in stock. |
| `getStoreProduct` | Returns one of the store's products with its description, photos and variants, each with its item id, price and availability. Takes a product id or a variant's item id. |
| `startStoreCheckout` | Starts a checkout for the buyer's chosen items (item ids and quantities) and optional e-mail address, and returns its total, its items, any problems and the `checkout_url` where the buyer pays. |
| `addToStoreCart` | Adds an item (item id and quantity) to the caller's own cart and returns the cart. |
| `getStoreCart` | Returns the caller's own cart: its items, prices, subtotal and the cart page link. |

## Tool permissions

Tool permissions decide who may **attach** a tool, and, for chat interactions, who may **use** it. They do not limit who may use the tools of an AI profile: for a profile, the profile itself is the authorization boundary.

| Permission | Description |
| --- | --- |
| `QueryAnyAIProfile` (or `QueryAIProfile_{profileName}`) | Allows the identity to query the AI profile, and with it to use every tool the profile is configured with. |
| `AccessAnyAITool` | Allows the identity to use every registered AI function. This permission is security critical and is granted only to the `Administrator` role by default. |
| `AccessAITool_{toolName}` | Allows the identity to use one specific AI function. |

The tool permissions are checked in two places:

- **When tools are selected.** The tools editors of AI profiles, AI profile templates, chat interactions and the MCP server settings list only the tools the editing user holds `AccessAnyAITool` or `AccessAITool_{toolName}` for.
- **When a chat interaction sends a message.** The tools selected on the interaction are checked again against the person chatting. A tool they are not authorized for is excluded from the request instead of failing it. The model then answers without that capability, which usually looks like an incomplete or degraded answer. Each excluded tool is reported in the logs with a `Warning` entry that lists the tool names and the permissions to grant, so check the application log first when an interaction behaves correctly for an administrator but not for another user.

AI profile chats run the profile exactly as it is configured. This covers the admin chat, the frontend chat widget, and API or OpenID clients that query a profile. Anyone allowed to query the profile can use all of its tools, without holding any tool permission. That includes anonymous visitors when the `Anonymous` role has `QueryAIProfile_{profileName}`.

:::warning[Choose a profile's tools for its least-privileged user]
Do not attach tools that change data or site configuration, such as tools that create or edit content, enable features, manage tenants, run recipes, change settings, or send email or SMS, to a profile that less-privileged or anonymous users can query. Remember that text the model reads, such as uploaded documents, crawled pages and content items, can steer it toward calling those tools.
:::

:::tip
Custom roles and OpenID applications that query a profile need only the profile query permission to use its tools. To use the tools of a chat interaction, a user needs `AccessAnyAITool`, or the matching `AccessAITool_{toolName}` permission for every tool the interaction uses.
:::

### How the caller is resolved

The identity used for the chat interaction check comes from the framework's `IUserAccessor` rather than from `IHttpContextAccessor`. This matters for SignalR: `HttpContext` is frequently unavailable inside hub invocations on long-lived transports, on backplane-delivered messages, and behind hosted SignalR services, so resolving the caller from the HTTP request could not be relied upon there. The AI chat and chat interaction hubs publish the connection's principal for the duration of every invocation, which means tool permissions are evaluated the same way over SignalR as they are over a regular HTTP request.

Two cases are treated differently:

- **No caller at all**, such as a background task, a workflow, or a recipe. Permission checks are skipped and every configured tool stays available, because trusted server-side code is not a security boundary.
- **An unauthenticated caller of a chat interaction.** The permission check still runs, so whatever you grant the `Anonymous` role continues to apply exactly as it does elsewhere in Orchard Core. An anonymous visitor using a public chat widget is querying an AI profile, so the profile's query permission alone decides what they can use.

## Invocation Context (AIInvocationScope)

`AIInvocationScope` is the shared per-request context for references, tool state, and other invocation-scoped data. For the framework-level explanation, see the shared Core documentation:

- [Tools](https://core.crestapps.com/docs/core/tools)
