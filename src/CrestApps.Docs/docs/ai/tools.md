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

A function marked **Asks for approval** waits for the user to click **Allow** or **Deny** before it runs. Most of them ask because of their [kind](https://core.crestapps.com/docs/core/tools#tool-kinds): they delete something or cannot be undone (Destructive), send something outside the site, call a paid service or move money (External), or change access, sign-in, secrets or what the site runs (Security). A profile's tool approval setting can let those run without asking. A few change data that can be changed back but ask anyway, such as `setHomepage` and `saveCoupon`; they always ask. Where nobody can click **Allow** (a workflow, a voice call, an MCP client), a function that asks does not run.

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
| `applySiteSettings` | Applies predefined system configurations and settings using AI assistance. It needs **Import data**, the permission of the JSON import page. Asks for approval. |
| `getOrchardCoreRecipeJsonSchema` | Returns the Orchard Core recipe root JSON Schema with a `steps` array. It can limit the schema to one step definition while still exposing every valid recipe step name. Call it first before building recipe JSON. |
| `listOrchardCoreRecipeStepsAndSchemas` | Lists all available Orchard Core recipe steps and returns their JSON schema definitions. |
| `importOrchardCoreRecipe` | Imports and runs Orchard Core recipes within your site. Call `getOrchardCoreRecipeJsonSchema` first and match that schema. It needs **Import data**, the permission of the JSON import page. |
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
| `getContentItem` | Gets one content item by its ID or by a URL, path, alias or handle (`slug:about-us`, `alias:main-menu`, `contentitemid:...`), and returns its ID, type, title, version and state; a path of `/` returns the home page. `include` adds `localizations` (its translation in each site culture) and `nested` (the outline of the widgets and items in its Flow, Bag and widget-zone parts, with IDs and positions). |
| `listContentItems` | Lists content items with the same filters as the admin Content Items page: text, content types, stereotype, status (latest, published, draft, owned by me, all versions), sort, culture, and paging. It returns summaries with IDs, not full bodies. The `culture` filter needs `OrchardCore.ContentLocalization`. |
| `listContentItemVersions` | Lists the stored versions of one content item (version ID, created/modified/published dates, author, latest and published flags). Use it before restoring a version. |
| `compareContentItemVersions` | Shows what changed between two versions of a content item, field by field, with each value before and after. By default it compares the draft with the published version, or the latest version with the one before it. Use it before `restoreContentItemVersion`. |
| `restoreContentItemVersion` | Restores an earlier version of a content item as a new draft, keeping the published version live. Takes a content item version ID. Publish the item afterwards to make it live. |
| `discardContentItemDraft` | Discards the unpublished draft of a content item so the published version becomes the latest again. Asks for approval. |
| `changeContentItemOwner` | Changes the owner of a content item to another user, given by user name, user ID or email. The author is not changed. Asks for approval. |

`getContentItem` takes `contentItemId` or `handle`, not both, and `version` (`latest`, the default, or `published`). A handle is looked up through Orchard Core's content handle manager, so a path is matched by the Autoroute feature and an alias by the Alias feature; a full URL is reduced to its path, without the tenant's prefix. It needs **View content** on the content item; a translation the caller may not view is reported as existing, without its details. `localizations` needs `OrchardCore.ContentLocalization`; without it the result says translations are not available on this site. `nested` reads the item's own JSON, so it works whenever the content type has a Flow, Bag or widgets list part. It replaces `findContentItemByHandle`, `listContentItemLocalizations` and `listNestedContentItems`. `getContentItemById` stays beside it unchanged.

`listContentItems` builds the filter text a person types in the search box of the Content Items page (`type:`, `stereotype:`, `status:`, `sort:`, `culture:`) and runs it through the same filter parser and query service as that page, so it lists what the page lists. It does not filter by parent list, taxonomy term or schedule. `searchForContentItems` stays registered beside it.

`compareContentItemVersions` serializes both versions the way the audit trail's diff does and lists every value that was added, removed or changed, by its path (`TitlePart.Title`, `BagPart.ContentItems[0].HtmlBodyPart.Html`), at most 100 changes and 500 characters a value, with `truncated` when there are more. The version ID, flags and dates are reported once per version instead of as changes. Both versions must belong to the item. The values are the item's own text, returned as data.

These tools, and the content type, localization, nested item and scheduling tools below, act for the signed-in person in the chat. Each one checks the Orchard Core permission the matching admin action checks, on the same content item where the admin checks one: `ListContent` and `ViewContent` to list and read, `EditContent` on the latest version to compare versions (drafts and earlier versions are what the item's editor sees), `PublishContent` to restore a version or schedule, `DeleteContent` to discard a draft, `EditContentOwner` to change the owner, `EditContent` to change nested items, `LocalizeContent` to translate, and `EditContentTypes` to change content definitions. A caller who is not signed in, or who does not hold the permission, gets a refusal and nothing is changed. Dates are returned in the user's or the site's time zone, with the zone named.

### Content definition tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.ContentTypes`

| Function | Description |
| --- | --- |
| `getContentTypeDefinition` | Retrieves the definitions of all available content types. |
| `getContentPartDefinition` | Retrieves the definitions of all available content parts. |
| `listContentTypesDefinitions` | Provides a list of available content type definitions. |
| `listContentPartsDefinitions` | Provides a list of available content part definitions. |
| `listContentFieldDefinitions` | Provides a list of available content fields. |

### Shape alternates tool

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.ContentTypes`

| Function | Description |
| --- | --- |
| `listShapeAlternates` | Lists the template names Orchard Core looks for when it shows items of a content type in a display type (`Detail`, the default, or `Summary`, ...): the content item shape and the shapes of each part and field, each with its shape name (for the **Templates** admin page) and its theme file name (add `.cshtml` or `.liquid`). |

It needs **View content types**. The part and field names come from Orchard Core's own alternate factories (`ContentPartAlternatesFactory`, `ContentFieldAlternatesFactory`) for the parts and fields that have a display driver, with the shape type those drivers use (the part or field type, or `[Type]_Display__[DisplayMode]` with a display mode). The content item names follow Orchard Core's content item conventions (`Content`, or the type's stereotype, then `_[DisplayType]` and `__[ContentType]`), because those factories are internal to Orchard Core. The file names are what Orchard Core's template harvester reads as each shape name (`Content_Summary__BlogPost` is `Content-BlogPost.Summary`). Not listed: the per-item alternates (`Content__[ContentItemId]`, named in the result), extra shapes some parts render (a list part's feed, for example), and the ContentPart shape of a part made only of fields, whose fields are listed. It does not say which templates already exist.

### Content definition recipe tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.ContentTypes` + `OrchardCore.Recipes.Core`

| Function | Description |
| --- | --- |
| `removeContentTypeDefinition` | Removes the content type definition. |
| `removeContentPartDefinition` | Removes the content part definition. |
| `applyContentTypeDefinitionFromRecipe` | Creates a new content type definition or updates an existing one. It needs **Edit content types** and **Import data**, the permission of the JSON import page, because the recipe can hold any step. |

### Content type part and field tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.ContentTypes`

| Function | Description |
| --- | --- |
| `saveContentTypePart` | Attaches a part to a content type or updates how it is attached: display name and description (named parts), editor, display mode and the part's settings for this type, without resending the whole type definition. |
| `removeContentTypePart` | Removes a part from a content type. Existing items keep the stored data but it is no longer edited or displayed. Asks for approval. |
| `saveContentPartField` | Adds a field to a content part (or to a content type's own part) or updates an existing field: display name, field type on creation, editor, display mode and the field's settings. The field type cannot be changed after creation. |
| `removeContentPartField` | Removes a field from a content part or content type. Existing items keep the stored values but they are no longer edited or displayed. Asks for approval. |

These tools call the content definition service the content type editor calls, so the events of that editor run and a part or field the system defines cannot be removed. Only the arguments given are changed. The `settings` argument is a JSON object keyed by settings class name, the shape a content definition recipe uses (for example `{"AutoroutePartSettings": {"AllowCustomPath": true}}`); it is merged into the stored settings and is not checked by the settings editor of the part or field.

### Content localization tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.ContentLocalization`

| Function | Description |
| --- | --- |
| `localizeContentItem` | Creates the version of a content item in another culture as a draft copy linked to the same localization set, and returns the new content item ID. Translate the copy with `createOrUpdateContentItem` afterwards. |

To list the translations of a content item, call `getContentItem` with `include: ["localizations"]`.

### Nested content item tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Flows`

| Function | Description |
| --- | --- |
| `saveNestedContentItem` | Adds, updates or moves a single widget or contained item inside a content item's Flow, Bag or widget-zone part without resending the rest of the page. `afterId` places it right after a sibling in the target list (an empty `afterId` puts it first); without it a new item goes at the end and a moved item keeps its place in the same list. |
| `removeNestedContentItem` | Deletes one widget or contained item, and everything nested inside it, from a content item's Flow, Bag or widget-zone part. Asks for approval. |

`createOrUpdateContentItem` replaces a nested list whole, so changing one widget means resending every widget of the page. These tools change one nested item and write only the list it is in. A new nested item must be of a content type the part's settings allow, and gets its own ID. The parent content item is saved as a draft unless `isDraft` is `false`; publish it to make the change live. To see the nested items with their IDs, call `getContentItem` with `include: ["nested"]`.

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
| `setHomepage` | Makes a published content item the site's home page. The content item must have an Autoroute part with a path, and its route must not be disabled. Asks for approval. |

To find the content item behind a URL, a path, an alias or a handle, call `getContentItem` with `handle` (see the content item tools above).

`setHomepage` follows the content editor: it ticks **Set as homepage** on the Autoroute part and publishes the content item, and the Autoroute feature writes the home route. It needs **Set homepage** and **Publish content** on the content item, and it refuses a content item whose latest version is a draft, because that draft would go live. Every visitor sees the new home page at once. The site does not remember the previous home page, so the result names it. It asks for approval because the home page is a site-wide change every visitor sees.

### Menu tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Menu`

| Function | Description |
| --- | --- |
| `getMenuTree` | Returns a menu's items as a flat, ordered tree: item ID, type, title, URL or linked content item, parent ID, depth and required permissions. The menu is given by content item ID, alias or title; without one it lists the site's menus. |
| `saveMenuItem` | Adds an item to a menu, updates an existing item, or moves it under another parent (`parentMenuItemId`) or after another item (`afterId`), leaving the other items untouched. Supports link, content and HTML menu items. |
| `deleteMenuItem` | Deletes a menu item and all its child items from a menu. Asks for approval. |

`saveMenuItem` and `deleteMenuItem` need **Manage menus**, as the menu editor does. They save the change in a draft of the menu, so the site keeps showing the published menu until the menu is published; pass `publish: true` to publish it in the same call, which also needs **Publish content** on the menu. A URL is checked the way the menu item editor checks it, so a script URL is refused. `permissionNames` hides an item from users who lack the permissions; it does not protect the page the item links to. An item is placed by its parent and the sibling it goes after, never by a number: `afterId` names an item under the same parent (an empty `afterId` puts the item first); without it a new item goes at the end and a moved item keeps its place.

### Taxonomy tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Taxonomies`

| Function | Description |
| --- | --- |
| `getTaxonomyTerms` | Returns the terms of a taxonomy as a flat, ordered tree: term ID, title, parent ID, depth and full path. The taxonomy is given by content item ID, alias or title; without one it lists the site's taxonomies. |
| `saveTaxonomyTerm` | Adds a term to a taxonomy, updates an existing term, or moves it under another parent (`parentTermId`) or after another term (`afterId`), without touching the other terms. New terms use the taxonomy's term content type. |
| `deleteTaxonomyTerm` | Deletes a term and all its child terms from a taxonomy. Content items that reference the term keep the stale ID. Asks for approval. |

`saveTaxonomyTerm` and `deleteTaxonomyTerm` need **Manage taxonomies**. Like the menu tools, they save the change in a draft of the taxonomy unless `publish` is `true`. The other parts and fields of a term go in `content`, in the content item JSON shape, and are checked against the term content type. `afterId` places the term right after a term under the same parent (an empty `afterId` puts it first); without it a new term goes at the end and a moved term keeps its place.

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

The three tools need **Manage sitemaps**. `saveSitemap` checks every argument before it changes anything, so a refused call leaves the sitemap as it was. A source listed with its `sourceId` is kept and updated; a stored source that is not listed is removed. The tools cannot create a sitemap index, and they neither list nor change the sitemaps an index contains: that stays on the **Sitemap Indexes** page.

### Media library tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Media`

| Function | Description |
| --- | --- |
| `listMediaFiles` | Lists the sub-folders and files of a media folder (the root when no path is given), with optional name and extension filter, recursion and paging. Each file has its path, size, MIME type, date and public URL. |
| `getMediaFile` | Returns one media file's details and public URL and, when asked, its text content. |
| `importMediaFromUrl` | Downloads a file from a public http or https URL and stores it in a media folder. Asks for approval. |
| `moveMediaFiles` | Moves, renames or copies up to 50 media files: `action` (`move`, the default, or `copy`) and each file's current path and new path. |
| `deleteMedia` | Permanently deletes up to 50 media files by path. Folders are not deleted: delete them in the Media Library. Asks for approval. |

The file tools check what the Media Library checks: **Manage Media** first, then the folder permission with the path as the resource. Orchard Core maps the path to the right permission, so the attached media folder needs **Manage Attached Media Fields Folder**, another user's folder needs **Manage Media For Others**, and with Secure Media the folder's view permission applies too. `listMediaFiles` leaves out the folders the caller may not manage.

A path is given from the root of the media library, for example `brand/logo.png`, as the media library stores it and not URL-encoded. A path with a drive or a `..` segment is refused, and so is a path that names another folder or file when it is URL-decoded, because Orchard Core decodes a path before it checks the folder permission. The tools move, copy and import only into a folder that exists, and none of them creates or deletes a folder: do that in the Media Library. A new file must have an extension the site allows; a restricted extension (`.css`, `.js` and `.svg` by default) also needs **Upload media file extensions requiring additional permission**. No tool replaces a file unless `overwrite` is set, and `moveMediaFiles` never replaces one. `moveMediaFiles` and `deleteMedia` check every file before they change the first one. Content that references a moved or deleted file is not updated, because media fields and HTML or Markdown bodies store the path as text.

`importMediaFromUrl` stores the file through the file creation pipeline of an upload, so the file event handlers of the site (an antivirus scan, for example) can refuse it, and the site's maximum upload size applies.

`importMediaFromUrl` fetches an address the model chose, so it is built to never read the site's own network:

- Only `http` and `https` addresses are accepted, without a user name or password.
- The host is resolved before the request, and the request is refused when any of its addresses is a loopback, private, link-local or other internal address. The connection goes through the shared egress guard, which checks the address again when the connection opens.
- Redirects are followed by the tool, at most five, and each one is checked the same way. A redirect to an internal address is not followed.
- The download stops at the site's maximum upload size and after 100 seconds.
- A web page returned in place of the file (a sign-in page, for example) is not stored.

`getMediaFile` returns text only when `includeText` is set, and at most the first 20000 characters. Text, Markdown, PDF, Word and PowerPoint files are read by the text providers of the media indexing features (`OrchardCore.Media.Indexing.Text`, `OrchardCore.Media.Indexing.Pdf`, `OrchardCore.Media.Indexing.OpenXML`) when they are enabled. Without a provider, the tool reads a plain-text file of up to 1 MB.

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

### Query tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Queries`

| Function | Description |
| --- | --- |
| `listQueries` | Lists the saved queries with their name, source, template, index, result schema and whether they return content items, with a name filter and paging, and the query sources enabled on the site. |
| `runQuery` | Executes a saved query by name with optional parameters and returns its rows or content items. |

`listQueries` needs **Manage queries**. `runQuery` checks the permission the query API checks for `api/queries/{name}`: **Execute Api** for that query, which **Execute Api - All queries** and **Manage queries** imply. A caller who may not run a query gets the same refusal for a name that exists and for one that does not.

Queries are created, changed and deleted on the **Search > Queries** page.

`runQuery` bounds what it returns:

- Rows are returned a page at a time: 20 by default, 50 at most. `total` is the number of rows the query returned, so limit the rows in the query itself.
- A page stops before about 40000 characters of JSON and says so with `truncated`.
- A text longer than 2000 characters is shortened, and a list keeps its first 50 values.
- A content item is returned as its ID, version ID, content type, title and published and latest flags. Read its parts and fields with the content tools.
- A value whose column or property name says it is a credential (a password, a hash, a token, a secret, a key, a security stamp, a connection string) is replaced with `[redacted]`, also inside a JSON document stored in a text column. This is a safety net that goes by the name: a query that returns a credential under another name still returns it.

When a saved query fails, `runQuery` says that it failed and leaves the error in the log, because the caller may run the query without being allowed to read it.

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
| `setUserAccountState` | Enables, disables or unlocks up to 20 user accounts, or marks their email as confirmed without sending an email (`action`: `enable`, `disable`, `unlock`, `confirmEmail`), and reports the outcome for each. Use `enable` to approve registrations that wait for moderation. |
| `deleteUser` | Permanently deletes a user account. Asks for approval. |

These tools are added beside `getUserInfo` and `searchForUsers`. They check, for the person in the chat, the permission the matching action of the **Access Control > Users** page checks, on the user it acts on, so the permissions for the users of one role (for example **Edit users in Editor role**) work as they do on that page:

- `listUsers` needs **List all users** or the permission to list the users of a role. It builds the filter text of the page's search box and runs it through the page's parser and query service. A role filter needs the permission to list the users of that role, and a user the caller may not list is left out of the page.
- `getUser` needs **View user profiles** on the user. A person can always read their own account.
- `saveUser` and `setUserAccountState` need **Edit any user** on the user, and `deleteUser` needs **Delete any user**.

A caller who may act on no user gets the same refusal for an ID that exists and for one that does not, so the tools cannot be used to learn which accounts exist.

No tool returns or accepts a credential. Results are built from a fixed list of fields: the password hash, the security stamp, the reset token, the authenticator key, the recovery codes, the user's claims and the keys of external logins are never returned, and `getUser` says only whether a password is set and which external providers are linked. No tool takes a password, a hash or a token. A password is set by a person on the Users page.

`saveUser` follows the Edit page: a field the site locks under **Settings > Security > User Login** is refused, to administrators too, the phone number is validated, and the user manager validates and stores the user (a user name or an email that is taken is reported). A person who changes their own account needs **Edit own user information**. It does not create a user, because an account created from a chat would have no password and the assistant cannot send the email that lets the user choose one; create users on the Users page. It does not change the time zone or the culture of a user.

The tools refuse what the admin page leaves to the person:

- `setUserAccountState` with `disable`, and `deleteUser`, refuse the caller's own account, the site owner (the super user of the site settings) and the only enabled user in the administrator role. The name of the administrator role is read from the role service, so a renamed administrator role is protected too.
- `deleteUser` takes the user name with the ID and refuses when they do not match, so the approval shows which account is deleted.
- `setUserAccountState` with `unlock` resets only the failed sign-in count of a user who is not locked out (outcome `notLockedOut`).

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

### User access explanation tool

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Users` + `OrchardCore.Roles`

| Function | Description |
| --- | --- |
| `explainUserAccess` | Explains why a user can or cannot do something ("why can't Jane edit this page?"): it asks the site's own authorization for the user and a permission, optionally on a content item, and returns the answer, the user's roles that hold the permission, the roles of the site that would grant it, and whether the account is disabled or locked out. |

It builds the user's own principal with the claims principal factory, as sign-in does, and asks the authorization service for the permission with the content item as the resource, so the answer (`allowed`) is the site's own, including the content type's own permissions, ownership and item access rules. It then names the user's roles that hold the permission (Authenticated and Anonymous count for every signed-in user, and the administrator role holds every permission), every role of the site that grants it, whether the user is the site owner, and a disabled or locked-out account (the lockout end in the user's or the site's time zone). When a role holds the permission but the check on the item refuses it, the explanation says that a rule of the item decided, without guessing which one. It needs **View roles** and **View users** on the user (a person may always ask about their own account) and, with a content item, **View content** on that item. An unknown permission name returns the closest names. The user's claims are never returned. It changes nothing.

### User sign-in factor tool

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Users.2FA`

| Function | Description |
| --- | --- |
| `resetUserSignInFactors` | Resets the second sign-in factors of a user who lost them: `twoFactor` switches off two-factor authentication, and `authenticatorApp` also forgets their authenticator app key so the codes of a lost device stop working. Asks for approval. |

It needs **Disable two-factor authentication for any user**, the permission of the **Disable two-factor authentication** action of the Users page, and refuses the caller's own account. `twoFactor` does what that action does. `authenticatorApp` does what the user's own **Reset authenticator app** page does (a new key, so the user scans a new code when they set the app up again); it is refused while two-factor authentication stays on, so it is given together with `twoFactor` unless two-factor authentication is already off. Both change the user's security stamp, which signs the user out of their other sessions. The result says whether the site, or a rule for the roles of the user, makes the user set two-factor authentication up again at the next sign-in. No key and no recovery code is read, returned or accepted. Passkeys cannot be removed: the Orchard Core version this site runs has no passkeys.

### Role and permission tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Roles`

| Function | Description |
| --- | --- |
| `listRoles` | Lists the roles with their description and whether each is a system role or the administrator role. |
| `getRole` | Returns one role with the permissions assigned to it, the permissions a user who holds only that role has in effect, the ones that come from Anonymous and Authenticated, and its security-critical permissions. With `compareWith`, it compares two roles instead: the assigned and effective permissions only the role has, only the other role has, and both have. |
| `listPermissions` | Lists the permissions the enabled features provide, with name, description, group, the permissions that imply them and whether Orchard Core marks them as security critical; with a text filter and paging. |
| `saveRole` | Creates a role, or changes the description and the permissions of a role (add, remove or replace; a new role can copy the permissions of another role). A permission name no enabled feature provides is refused with the closest names that exist. Asks for approval. |
| `deleteRole` | Deletes a role that is not a system role and takes it from the users and OpenID applications that have it. Asks for approval. |

These tools are added beside `getRoleInfo`. `listRoles`, `getRole` and `listPermissions` need **View Roles**; `saveRole` and `deleteRole` need **Manage Roles**, which Orchard Core marks as security critical. They use the role manager and the role service of the **Access Control > Roles** page.

`getRole` asks the authorization service what a user who holds only the role may do, as the View page of a role does, so `effectivePermissions` holds the assigned permissions, the ones they imply and the ones every user gets from Anonymous and Authenticated. A list holds at most 200 names; `filter` narrows the lists. For the administrator role it says that the role holds every permission.

With `compareWith`, `getRole` answers "how do these roles differ": `effectivePermissions` and `assignedPermissions` each hold `onlyInRole`, `onlyInComparedRole` and `inBoth`. When one of the roles is the administrator role, it says that role holds every permission instead.

`saveRole` updates a role unless `create` is true, so a mistyped name never creates a role. It keeps the rules of the admin page: a new role needs a name without a slash that no other role has, and the permissions of the administrator role cannot be changed. It adds four rules:

- A permission must be one an enabled feature provides. An unknown name is refused, and the answer names up to three close permission names (names that contain it or that it contains, or a few edits away, ignoring case). `listPermissions` returns the exact names.
- A security-critical permission is never given to Anonymous or Authenticated.
- It does not add permissions to a role the caller holds (Anonymous and Authenticated count, because every signed-in user has what they grant), unless the caller is in the administrator role or is the site owner. Removing permissions from such a role is allowed.
- Every security-critical permission the call gives must be named again in `securityCriticalPermissions`. When one is missing, the tool changes nothing and names it, so the assistant tells the person and the approval of the second call shows what is granted.

Everything is checked before the role is changed. `deleteRole` reports how many users had the role.

### OpenID Connect tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.OpenId.Management`

| Function | Description |
| --- | --- |
| `listOpenIdApplications` | Lists the client applications with id, client id, display name and type, with a search by display name and paging. |
| `getOpenIdApplication` | Returns one application by id or client id: type, consent type, redirect addresses, allowed flows and endpoints, the PKCE and pushed authorization request requirements, roles and scopes. |
| `listOpenIdScopes` | Lists the scopes with id, name, display name, description and resources. |

The application tools need the `ManageApplications` permission (**View, add, edit and remove the OpenID Connect applications**) and the scope tools need the `ManageScopes` permission (**View, add, edit and remove the OpenID Connect scopes**), the permissions of the **Access Control > OpenID Connect** pages. They read through the application and scope managers of Orchard Core. No tool returns a client secret: `getOpenIdApplication` returns `hasClientSecret` only. Applications and scopes are created, changed and deleted on those pages.

### Rate limit tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.RateLimits`

| Function | Description |
| --- | --- |
| `listRateLimitPolicies` | Lists the policies with scope, path or group, status and limiters, and the built-in route and group limits that modules register in code. Search text, status filter and paging. |
| `setRateLimitPolicyEnabled` | Enables (`enabled: true`) or disables (`enabled: false`) one or more policies. The tenant reloads. Asks for approval. |

The tools need **Manage Rate Limits** and use the policy store of the **Tools > Rate Limits** page. Policies are created, changed and deleted on that page.

`listRateLimitPolicies` marks with `protectsSignIn` each enabled policy that limits the sign-in and account endpoints: a global policy, a policy on one of the rate-limit groups the Users module puts on those endpoints, and an endpoint policy whose path covers one of them. `setRateLimitPolicyEnabled` refuses to disable such a policy unless the call sets `confirmRemovesSignInProtection`, and then say in the result which protection went. The built-in limits stay on in every case.

### Secret tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Secrets`

| Function | Description |
| --- | --- |
| `listSecrets` | Lists the secrets by name, store, type, description and created, updated and expiry dates, with search, type and expiry filters, and the secret stores. |

`listSecrets` needs **View secrets** and uses the secret manager of the **Tools > Security > Secrets** pages, where secrets are created, changed, moved and deleted. It never returns the value of a secret, a private key or PEM material.

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

Secrets are never shown or accepted: a text field whose name holds a password, secret, token, API key, connection string or private key is returned only as whether it is set, and a credential edited with the secret input returns its source and secret name; a call can point it at a secret of the Secrets module by name but cannot set a value. The tools never change the super user, the site salt or the home route. Custom settings content types are not read or changed by these tools; a person edits them on their settings page. The **OpenID Client** settings are a site settings group and can be read and changed with these tools; the OpenID server and validation settings and the CORS settings have their own pages and documents and have no tool yet. `updateSiteSettings` refuses the **User registration**, **User login**, **HTTPS** and **Reverse proxy** settings pages, and the sections of the admin chat widget, the MCP server, WebMCP and the Service Desk's AI profile (`TicketAISettings`) on any page: one wrong value there locks people out or lets the assistant rewire itself. A person changes them on the settings page.

### Generic record tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `CrestApps.OrchardCore.AI`, for the record kinds the enabled features register

| Function | Description |
| --- | --- |
| `listRecords` | Lists the records of one kind, a page at a time (20 by default, at most 50), optionally filtered by a text in their names. Returns each record's id and the fields the kind allow-lists, the total and whether more pages follow. |
| `getRecord` | Gets one record of a kind by its id, with the fields the kind allow-lists. |
| `saveRecord` | Creates a record of a kind, or changes one when an id is given, through the record's admin editor. Returns the saved record, or the errors and the field schema. With `dryRun` it only checks the values. |
| `deleteRecord` | Deletes records of a kind by their ids (at most 50). A record that is still in use is kept and the reason is returned. Asks for approval. |

The four tools work on every **record kind** an enabled feature registers: a setup catalog such as tax categories, exposed under a short camelCase name that the model passes as `kind`. The `kind` argument lists the registered kinds and their descriptions. Each call checks the kind's permission for the signed-in caller (the read permission for `listRecords` and `getRecord`, the write permission for `saveRecord` and `deleteRecord`) and refuses when nobody is signed in. An unknown kind returns the kinds the caller can use.

`listRecords` and `getRecord` return only the fields the kind's owner allow-listed; the record and its `Properties` are never serialized. `saveRecord` replays the record's admin editor, as `updateSiteSettings` does for a settings page: it builds the editor through the record's display drivers, merges the given values over the fields the editor shows, posts them back through the same drivers (so their binding, trimming and other normalization run), then checks the record's own rules, the ones the admin page checks before it saves. The values are first posted to a copy of the record; only when the drivers and the rules accept them, and it is not a dry run, are they posted to the record itself, which is then saved, as the admin page saves it. Nothing is saved when a driver or a rule reports an error; the errors are returned with the JSON schema of the editor's fields, so the model can correct the call. A save, and a dry run, return the fields that changed (`changed`) and the requested fields that kept their value (`unchanged`): some editors set a field only when the record is created, or normalize the value. `values` is keyed by the editor's field names (for example `{ "Name": "Food", "Code": "FOOD" }`); an editor with several sections takes the values keyed by section. Call `saveRecord` with empty `values` to get the schema. A field that holds a secret (its name contains password, secret, token, API key, connection string or private key, or the kind names it) is never returned or accepted. A record changed or deleted earlier in the same chat message is read back as changed or deleted.

Kinds registered in this repository:

| Kind | Records | Permission | Available when |
| --- | --- | --- | --- |
| `taxCategory` | Tax categories. A category the tax rules or child categories still use is not deleted. | **Manage taxation** | `CrestApps.OrchardCore.Taxation` |
| `taxType` | Tax types. A type the tax rules still name is not deleted. | **Manage taxation** | `CrestApps.OrchardCore.Taxation` |
| `taxJurisdiction` | Tax jurisdictions. A jurisdiction the tax rules or child jurisdictions still use is not deleted. | **Manage taxation** | `CrestApps.OrchardCore.Taxation` |
| `currency` | Currencies the store can price products in: the ISO 4217 code (`Name`, set when the currency is created; changing it later has no effect, as on the admin page) and the display name. A code another currency has is refused, as on **Commerce › Currencies**, and a currency products are priced in is not deleted (the products are named). | **Manage currencies** | `CrestApps.OrchardCore.Products` |
| `market` | Markets, read-only: the markets page binds its own view model, so the editor replay cannot write one yet. | **Manage markets** | `CrestApps.OrchardCore.Markets` |
| `shippingZone` | Shipping zones with their methods and rate settings, read-only: the methods are a list of objects the replay cannot post yet. | **Manage shipping** | `CrestApps.OrchardCore.Shipping` |
| `taxRule` | Tax rules, read-only: a rule is created for a calculation method the record tools cannot pass, and its editor is two drivers on a source catalog. | **Manage taxation** | `CrestApps.OrchardCore.Taxation` |
| `taxTable` | Tax tables with their rows, read-only: the rows are a list of objects. | **Manage taxation** | `CrestApps.OrchardCore.Taxation` |
| `posRegister` | Point of sale registers, read-only: the registers page binds its own view model. | **Manage registers** | `CrestApps.OrchardCore.PointOfSale` |
| `posDepartment` | Point of sale departments, read-only: the departments page binds its own view model. | **Manage registers** | `CrestApps.OrchardCore.PointOfSale` |
| `vendor` | Marketplace vendors: name, email, status, commission, country, the user names that manage it and whether its payout account is set up. Read only: vendors are added and changed on **Commerce › Vendors**, whose editor the record tools cannot replay. | **Manage the marketplace** | `CrestApps.OrchardCore.Marketplace` |
| `marketingSegment` | Customer segments: name, description and purchase rules. Read-only: listed and read, changed on **Commerce › Segments**. | **Manage marketing** | `CrestApps.OrchardCore.Marketing` |
| `marketingCampaign` | Email campaigns: name, segment, subject, the first 500 characters of the body, status, whether it has a send time, and the sent, skipped and failed counts. Read-only: listed and read, changed on **Commerce › Campaigns**. | **Manage marketing** | `CrestApps.OrchardCore.Marketing` |
| `agentPlatform` | AI agent platforms allowed to buy from the store. The API key and webhook secret are issued on the AI agents page only. | **Manage AI agent platforms** | `CrestApps.OrchardCore.AgenticCommerce` |
| `timeStage` | Time Tracker stages, with the categories each counts toward. Read-only: changed on the Stages page. | **Manage time stages and categories** | `CrestApps.OrchardCore.TimeTracker` |
| `timeCategory` | Time Tracker categories, with their factor. Read-only: changed on the Categories page. | **Manage time stages and categories** | `CrestApps.OrchardCore.TimeTracker` |
| `assetType` | Asset types, with their category, attributes and meters. Read-only: changed on the Asset Types page. | **Manage assets** | `CrestApps.OrchardCore.Assets` |
| `laborRuleSet` | Labor rule sets: name, jurisdiction, whether enabled, the teams, workers and employment types they apply to, and each rule's key, enforcement and parameters. Read-only: the Labor Rules page binds its own form, so `saveRecord` and `deleteRecord` refuse it. | **Manage labor rules** | `CrestApps.OrchardCore.Workforce.LaborRules` |
| `activityCode` | Workforce activity codes (work, break, meal, training...). Read-only. | **Manage activity codes** | `CrestApps.OrchardCore.Workforce` |
| `shiftTemplate` | Shift templates: start, length, breaks, teams, required skills. Read-only. | **Manage shift templates** | `CrestApps.OrchardCore.Workforce.ShiftPlanning` |
| `rotation` | Team rotations (shift patterns): the template of each weekday over a number of weeks and the workers on them. Only the teams the caller manages. Read-only. | **Plan team schedules** | `CrestApps.OrchardCore.Workforce.ShiftPlanning` |
| `coverageRequirement` | Coverage requirements: people needed by weekday or date and time of day. Only the teams the caller manages. Read-only. | **Plan team schedules** | `CrestApps.OrchardCore.Workforce.ShiftPlanning` |
| `timeOffType` | Time-off types. Read-only. | **Manage time-off policies** | `CrestApps.OrchardCore.Workforce.TimeOff` |
| `timeOffPolicy` | Time-off policies: accrual, limits and assignments. Read-only (assign a policy to one worker with `assignTimeOffPolicy`). | **Manage time-off policies** | `CrestApps.OrchardCore.Workforce.TimeOff` |
| `timeOffAllotment` | Time-off allotments: how many may be off per weekday or date. Read-only. | **Manage time-off policies** | `CrestApps.OrchardCore.Workforce.TimeOff` |
| `blackoutPeriod` | Blackout dates. Read-only. | **Manage time-off policies** | `CrestApps.OrchardCore.Workforce.TimeOff` |
| `timeClockPolicy` | Time clock policies: rounding, grace, meals, sources, geofence, overtime, holidays, attendance points. The pay period calendar is left out. Read-only. | **Manage time clock policies** | `CrestApps.OrchardCore.Workforce.TimeClock` |
| `planningGroup` | Forecasting planning groups (queues, channel, team, service goal, occupancy, shrinkage, history). Read-only. | **Manage forecasts** | `CrestApps.OrchardCore.Workforce.Forecasting` |
| `forecastEvent` | Forecast events (holidays, promotions, outages... with their volume multiplier). Read-only. | **Manage forecasts** | `CrestApps.OrchardCore.Workforce.Forecasting` |
| `capacityPlan` | Capacity plans (start, weeks, workload, growth, FTE, attrition, hiring classes). Read-only; `getCapacityPlanProjection` gives the projection. | **Manage forecasts** | `CrestApps.OrchardCore.Workforce.Forecasting` |
| `adherenceRuleSet` | Adherence rule sets (teams, and per scheduled activity the adherent states and reasons, grace, minimum minutes out and alert time). Read-only. | **Manage adherence rules and exceptions** | `CrestApps.OrchardCore.Workforce.Adherence` |
| `agentStateReasonCode` | Agent state reason codes: name, the state they are offered with (Break, Away, Do not disturb, Meeting, Training, After-hours unavailable; no other state is accepted, as on the editor), order and enabled. | **Manage Contact Center agents** | `CrestApps.OrchardCore.ContactCenter.Agents` |
| `contactCenterQueueGroup` | Contact Center queue groups. | **Manage Contact Center queue groups** | `CrestApps.OrchardCore.ContactCenter.Queues` |
| `contactCenterQueue` | Contact Center queues in full: routing, SLA, skills and skill requirements, overflow hops, wait and size limits and in-queue treatment. Read-only: written with `saveContactCenterQueue` and deleted with `deleteContactCenterRecord`. | **Manage Contact Center queues** | `CrestApps.OrchardCore.ContactCenter.Queues` |
| `contactCenterEntryPoint` | Inbound entry points: channel, the business numbers they answer (`addressIds`), target, hours, call settings and the whole phone menu; the settings other features keep on an entry point are named only. Read-only: written with `saveContactCenterEntryPoint` and deleted with `deleteContactCenterRecord`. | **Manage Contact Center queues** | `CrestApps.OrchardCore.ContactCenter.EntryPoints` |
| `businessHoursCalendar` | Business-hours calendars, which are Business Hours schedules. Each has a time zone and opening hours per weekday: a day with several windows is listed once per window. `holidays` lists the closed dates. `specialDays` lists every holiday and special day in full, with its hours and name. Times are wall-clock times of the schedule's own time zone. Read-only: written with `saveBusinessHoursCalendar` and deleted with `deleteContactCenterRecord`. | **Manage business hours schedules** | `CrestApps.OrchardCore.ContactCenter.BusinessHours` |
| `dialerProfile` | Dialer profiles: mode and pace, attempts and retry delay, caller ID and every compliance setting (do-not-call, calling window and calendar, abandonment cap, abandoned-call message) and predictive pacing. A calling calendar that is given must exist, and a caller ID must be one of the business's own phone numbers (an address with the phone capability), as on the editor; a caller ID or calendar the profile already had is kept. A save that relaxes a compliance rule (stops respecting the do-not-call lists, stops enforcing the calling window, the abandonment cap or the abandoned-call message, or raises the cap) is refused: the record tools cannot ask for approval and the calls it would allow cannot be taken back, so a person makes that change on the Dialer profiles page. Making the rules stricter is allowed. What a profile measured is shown on its page. | **Manage the Contact Center dialer** | `CrestApps.OrchardCore.ContactCenter.Dialer` |
| `omnichannelCampaign` | Campaigns: name, description and group, which the editor changes, and, read-only, how their automated AI conversations are set up. A group that is given must exist. Never deleted: the Campaigns page has no delete. | **Manage campaigns** | `CrestApps.OrchardCore.Omnichannel.Managements` |
| `omnichannelCampaignGroup` | Campaign groups. Never deleted: the Campaign groups page has no delete. | **Manage campaign groups** | `CrestApps.OrchardCore.Omnichannel.Managements` |
| `omnichannelDisposition` | Dispositions: name (unique, set when created), description, outcome and whether they capture a date; with the CRM on, the lead status they set. | **Manage dispositions** | `CrestApps.OrchardCore.Omnichannel.Managements` |
| `omnichannelCadence` | Follow-up cadences with their ordered steps. Read-only: written with `saveOmnichannelCadence` and deleted with `deleteOmnichannelRecord`. | **Manage cadences** | `CrestApps.OrchardCore.Omnichannel.Managements` |
| `omnichannelAddress` | The business's own numbers and addresses: type, value, capabilities, provider and description; the settings other features store on an address are named only, never their values. Read-only: written with `saveOmnichannelAddress`. | **Manage channel endpoints** | `CrestApps.OrchardCore.Omnichannel.ChannelEndpoints` |
| `leadStatus` | Lead statuses: name (unique, set when created), order, kind (Open, Closed, Converted) and the default and contacted choices. Saving a default, contacted or converted status takes that flag off the other statuses, as the page does; the converted status is never deleted. | **Manage lead statuses** | `CrestApps.OrchardCore.Omnichannel.Crm` |
| `opportunityStage` | Opportunity stages: name (unique, set when created), order, probability (0 to 100), closed and won. | **Manage opportunity stages** | `CrestApps.OrchardCore.Omnichannel.Crm` |
| `messageTemplate` | Message templates: name, body, the kind of work the template is for (`scope`, such as `Tickets`; empty for the messaging workspace), and whether its Liquid placeholders are filled in (`fillsPlaceholders`). | **Manage the messaging workspace** | `CrestApps.OrchardCore.Omnichannel.Messaging` |
| `telephonyExtension` | Telephony extensions: number (unique), the user they ring (`UserId` takes the user's ID) and display name. | **Manage telephony extensions** | `CrestApps.OrchardCore.Telephony` |
| `team` | Teams of the hierarchy (Organization, Region, Site, Department, Team or Crew), with their postal address, listed in tree order. They are written through the team editor (its Address card, and the Workforce card when Workforce is on) and the team manager's rules. A team that still has sub-teams, or that another module depends on, is not deleted. | **Manage teams** | `CrestApps.OrchardCore.Teams` |
| `schedulingResource` | Scheduling resources (staff, rooms, equipment) with their calendar, time zone, working hours and date overrides in the editor's text form. A save gives a resource without a calendar its Appointments calendar; a delete keeps the calendar and appointments. | **Manage scheduling** | `CrestApps.OrchardCore.Scheduling` |
| `aiAlertRule` | AI alert rules: the metric, profile, condition, threshold, window, sample size, check interval and notified addresses, whether the rule is on and whether it is open now. The profile must exist and the addresses must be email addresses. `listAIAlerts` lists the open rules and their incidents. | **Manage AI tests, QA cohorts and alert rules** | `CrestApps.OrchardCore.AI.Quality` |
| `aiQACohort` | AI QA cohorts: the profiles sampled, the sample percent, the criteria, the passing score, the knowledge check and the judge deployment. Every profile must exist. A new cohort scores the conversations that close from then on; an enabled cohort makes a paid judge model call for every sampled conversation, which the kind's description says, and saving it does not ask for approval (it can be turned off at once). | **Manage AI tests, QA cohorts and alert rules** | `CrestApps.OrchardCore.AI.Quality` |
| `aiTestCase` | AI simulation test cases: the profile and version under test, scenario, criteria, variables and mocked tool results (as `name: value` lines), turn limit, passing score and deployments. The profile must exist. `createAITestCaseFromConversation` starts one from a real conversation. | **Manage AI tests, QA cohorts and alert rules** | `CrestApps.OrchardCore.AI.Quality` |
| `approvalPolicy` | Approval policies: the approval type they serve (or, for a general policy, the types it lists), whether they are enabled, their order, the amount range, currency and scope that choose them, their stages with who may approve each (the approver rule's kind, such as `Permission`, `Role`, `Users`, `Manager` or `Staff`, and its value) and how many must, the built-in rules (whether the requester or the person a request is about may decide, and whether one person may decide several stages) and the hours until a request is due. A policy saved before stages is shown as its one stage. Read-only: its editor posts stages with lists of approver rules, and a new policy takes its type from the page's address, neither of which the editor replay can post. Who may approve is changed on **Approvals › Policies**. | **Manage approval policies** | `CrestApps.OrchardCore.Approvals` |
| `lateFeePolicy` | Late-fee policies: name, description, whether enabled, flat or percent, the amount (in each invoice's own currency) or percent, grace days, how often a fee repeats and how many, the cap, the minimum balance, whether the fee is taxed and whether its debit note is e-mailed. Written through the **Commerce › Late fees** editor. A save that turns a policy on or makes it charge more, sooner or more often is refused: creating an enabled policy, enabling one, changing its kind, raising the amount, percent or cap, fewer grace days, a lower minimum balance, repeating or more frequent or more repeated fees, taxing it, or e-mailing its debit notes. An enabled policy charges every overdue customer on the next hourly run, and the record tools cannot ask anyone to confirm, so a person makes that change on the page. Turning a policy off, making it gentler, renaming it and creating a disabled policy are allowed. | **Manage invoicing settings** | `CrestApps.OrchardCore.Invoicing.Collections` |
| `estimate` | Estimates (quotes): number, revision, status, customer, totals, issue and expiry dates, deposit, what accepting invoices, lines with their ids, and the invoices made from it; the name the customer accepted with and why they declined are returned as the customer's text. The text filter matches the number, customer or reference, or a status name alone (`sent`, `accepted`...). Read-only: drafted with `draftEstimate`, sent with `sendEstimate`, invoiced with `convertEstimateToInvoice`. | **View invoices** (read), **Manage estimates** | `CrestApps.OrchardCore.Invoicing.Estimates` |
| `recurringInvoicePlan` | Recurring invoice plans: name, status, customer, total per run, schedule rule, start and end, mode (draft, issue, issue and send, issue and charge), next run date, run count, lines, usage charges and the last five runs. The text filter matches the name, customer or reference, or a status name alone. Read-only: paused and resumed with `pauseOrResumeRecurringPlan`, changed on their page. | **View invoices** (read), **Manage recurring invoices** | `CrestApps.OrchardCore.Invoicing.Recurring` |
| `usageMeter` | Usage meters: name, unique lowercase code, unit, how usage adds up (sum, maximum, last) and whether it takes usage. The text filter matches the name or the code. A deleted meter keeps its usage records, and the plans that bill it stop billing it. | **Create invoices and change drafts** | `CrestApps.OrchardCore.Invoicing.Billing` |
| `billableItem` | Charges kept to bill a customer later: customer, kind, date, description, quantity, unit, cost, markup, price, currency, tax category and the invoice that bills it. The text filter matches the description or the customer, or `unbilled` or `billed` alone. A charge an invoice bills is not changed (its editor refuses) or deleted, as on **Commerce › Unbilled charges**. | **Create invoices and change drafts** | `CrestApps.OrchardCore.Invoicing.Billing` |
| `ticketDesk` | Ticket desks: audience, default team, business hours, service level policy, ticket types, reopen and auto-close days, protected-information retention, assignment, notifications and the priority matrix, plus the desk's AI and time-tracking settings; the settings other features keep on a desk (the Contact Center queue) are named only. Written through the desk editor, its Contact Center queue card included. From the chat: the desk's AI settings (profile, off, apply priority) are never changed, and protected information is never set to be erased sooner, a new desk included (it can be kept longer or kept); a team, hours schedule, ticket service level policy or ticket type that is given must exist. A desk tickets or ticket types still use is not deleted. | **Manage ticket settings** | `CrestApps.OrchardCore.Tickets` |
| `ticketStatus` | Ticket statuses: name, category (New, Open, Pending, OnHold, Solved, Closed), customer label, whether it pauses service-level timers, color and order. A status tickets still have is not deleted. | **Manage ticket settings** | `CrestApps.OrchardCore.Tickets` |
| `ticketPriority` | Ticket priorities: name, rank and color. A priority is not deleted while any of these use it: tickets, a ticket type's default, a desk's priority matrix, a ticket category's priority, or a target of a tickets service level policy. | **Manage ticket settings** | `CrestApps.OrchardCore.Tickets` |
| `ticketView` | Saved ticket lists of the Tickets page: status categories, desks, assignee, due within hours and sort, shared with everyone or one team (`personal` says whether it belongs to one user). A team or desk that is given must exist. | **Manage ticket settings** | `CrestApps.OrchardCore.Tickets` |
| `serviceLevelPolicy` | Service level policies: scope, business hours schedule, warning percent and the minutes of each target by metric and priority (the scope's priority key, for tickets the priority id; empty for every other priority). Read-only: the targets grid is a list of objects, so a person changes policies on **Service levels**. | **Manage service level policies** | `CrestApps.OrchardCore.ServiceLevels` |
| `businessDocumentTemplate` | Business document templates (how invoices, receipts and other documents print): document kinds (none for every kind), default, logo, accent color, paper size and the first 500 characters of the footer. A template saved as the default takes the default from the other templates of its kinds, as on **Document templates**; a document kind no enabled feature registers is refused. | **Manage business document templates** | `CrestApps.OrchardCore.BusinessDocuments` |
| `agreement` | Agreements with accounts (support or maintenance contracts), every agreement whatever terms it carries. Each gives its number, name, status, account, period and time zone, the hours it covers and their rounding, the notice days, the first 500 characters of its notes, and the names (never the values) of the terms other features keep on it. The `listRecords` text matches the number, the name, an account's ID or a customer's user ID (the agreements of the account the customer belongs to), or is a status (Draft, Active, Paused, Expired, Canceled). Read-only: with Work Orders' service agreements on, `getServiceAgreement` gives an agreement's field-service terms, and `saveServiceAgreement` and `deleteServiceAgreement` change it; otherwise it is changed on **Agreements**. `getAgreementBalance` gives the hours used and left. | **View agreements with accounts and their hours** (read) | `CrestApps.OrchardCore.Agreements` |
| `paymentTerm` | Payment terms: name, description, kind (`DueOnReceipt`, `NetDays` with `Days`, `EndOfMonth` with `Days`, `DayOfNextMonth` with `DayOfMonth` 1 to 31), the early-payment discount (`DiscountPercent` within `DiscountDays`) and whether a term may be chosen for new documents. Written through the Payment terms page's editor and rules. A term is deleted whatever uses it, as on the page: documents already issued keep its name and due date. | **Manage payment terms** | `CrestApps.OrchardCore.Transactions.PaymentTerms` |
| `partyCollectionSettings` | The parties (`Kind:Id`) kept out of the scheduled payment reminders or the late fees: only those with one of them on are listed, and the text matches a party's id or kind. Read-only: changed with `setPartyCollectionSettings`, as the card on an account's or customer's page posts to a controller. | **Manage transactions** | `CrestApps.OrchardCore.Transactions` |

A tax category's or tax jurisdiction's name is set when it is created, as on the admin page; changing it later has no effect.

A read-only kind is listed and read with `listRecords` and `getRecord`; `saveRecord` and `deleteRecord` refuse it. Its records are changed on their admin pages (or with their recipe steps), because those pages bind their own view models or edit lists of objects, which the editor replay cannot post yet.

Some read-only kinds have a dedicated save tool, because their editors post lists of objects (a queue's overflow hops and skill requirements, a calendar's weekly schedule, an entry point's phone menu, a cadence's steps) or create a record for a type the record tools cannot pass (an address's type): `contactCenterQueue` is written with `saveContactCenterQueue`, `contactCenterEntryPoint` with `saveContactCenterEntryPoint`, `businessHoursCalendar` with `saveBusinessHoursCalendar`, `omnichannelCadence` with `saveOmnichannelCadence` and `omnichannelAddress` with `saveOmnichannelAddress`. Their fields carry those tools' argument names, so a record read with `getRecord` can be changed and given back to the tool. The save tools apply the editors' own rules (for a queue, `ActivityQueueEditorRules`: skill tags, proficiency, overflow hops and limits). Queues, entry points and calendars are deleted with `deleteContactCenterRecord`, cadences with `deleteOmnichannelRecord`. The kind registration names these tools (`ReadOnly(saveWith, deleteWith)`), so the `kind` argument's description lists them, `getRecord` says which tool changes the record, and `saveRecord` and `deleteRecord` name the tool when they refuse the kind instead of sending the model to the admin page.

#### Registering a record kind

A module exposes a record type by registering a kind in a startup that requires the AI feature. The contract lives in `CrestApps.OrchardCore.Abstractions` (namespace `CrestApps.OrchardCore.AI.Records`), so the module needs no reference to the AI modules:

```csharp
[Feature(TaxationConstants.Feature.Taxation)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIRecordKind<TaxType>("taxType", kind => kind
            .WithTitle(S["Tax type"])
            .WithDescription(S["a kind of tax (sales tax, VAT, excise...) that tax rules name"])
            .WithPermission(TaxationPermissions.ManageTaxation)
            .UseCatalogManager<INamedCatalogManager<TaxType>>()
            .WithProjection(type => new
            {
                id = type.ItemId,
                name = type.Name,
                description = type.Description,
            })
            .WithInUseCheck(GetTypeUseAsync)
            .UseEditorReplay());
    }
}
```

| Option | Required | What it does |
| --- | --- | --- |
| name | yes | The camelCase kind name the model passes. |
| `WithDescription` | yes | What a record is and what it is used for, read by the model to pick the kind. |
| `WithPermission` | yes | The permission the admin pages check; needed for writes, and for reads unless `WithReadPermission` sets another. |
| `WithProjection` | yes | The allow-listed fields returned by `listRecords` and `getRecord`. Never return the record itself, its `Properties` or a secret (return `hasX = true`). |
| `WithTitle` | no | A short title. |
| `WithReadPermission` | no | A permission for reading when it differs from the write permission. |
| `UseEditorReplay(groupId)` | no | Writes replay the admin editor (the default); give the editor group when the admin page uses one. |
| `ReadOnly(saveWith, deleteWith)` | no | The kind is only listed and read. When a dedicated AI tool saves or deletes its records, give the tool's name: the `kind` description, `getRecord`'s note and the refusals of `saveRecord` and `deleteRecord` then name that tool. |
| `WithSecretFields(...)` | no | Editor fields that hold a secret, besides those whose name says so. |
| `WithInUseCheck(...)` | no | Returns why a record cannot be deleted, or `null`. |
| `UseCatalogManager<TManager>()` | no | Use another catalog manager service than `ICatalogManager<T>`, such as `INamedCatalogManager<T>`, when the admin pages use that one. |
| `FindUsing`, `NewUsing`, `ListUsing`, `ValidateUsing`, `SaveUsing`, `DeleteUsing`, `CopyUsing` | no | Replace one operation of the default, which is the record's `ICatalogManager<T>`. |

The record type must be a `CatalogItem`. Writes need the record's editor to be built through `IDisplayManager<T>` and display drivers; a record whose admin page binds its own view model in the controller, or whose editor has a list of objects, cannot be saved from the chat and the result says so.

### Admin page search tool

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Admin`

| Function | Description |
| --- | --- |
| `findAdminPage` | Searches the admin menu the caller can see and returns the matching pages with their menu path and link. |

The tool needs **Access admin panel**. It walks the admin menu the same way the quick navigation palette (Ctrl+K) does, for the caller only, so it never returns a page the caller cannot open.

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
| `runBackgroundTask` | Asks the background service to run one or more tasks now, outside their schedule, and says for each whether the run was queued. Asks for approval. |

Both tools need **Manage background tasks**. Times are in the user's or the site's time zone. The execution state is kept in memory by each server, so on a web farm it shows the runs of the server that answered. `runBackgroundTask` returns as soon as the run is requested; call `listBackgroundTasks` afterwards to see how it went. When the background service is not registered, there is no state and nothing can be run on demand. Schedules and other task settings are changed on the **Background Tasks** page. `runBackgroundTask` asks for approval: a task can send mass messages, place calls or purge data.

### URL rewriting tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.UrlRewriting`

| Function | Description |
| --- | --- |
| `listUrlRewriteRules` | Lists the redirect and rewrite rules in the order they are applied, with their pattern, substitution and options. Search text, a type filter and paging. |
| `saveUrlRewriteRule` | Creates a `Redirect` or `Rewrite` rule, or changes one by ID; fields that are not sent keep their value. A new rule is applied after the existing ones. |
| `deleteUrlRewriteRule` | Deletes one or more rules by ID. Asks for approval. |

All three tools need **Manage URLs rewriting rules**. `saveUrlRewriteRule` validates the rule as the **URL Rewriting** page does and saves nothing when a pattern is not a valid regular expression. After a save or a delete the tenant reloads its rules at the end of the request. Pages under the admin prefix are never rewritten. Rules are reordered by dragging them on the **URL Rewriting** page.

### Dynamic translation tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.DataLocalization`

| Function | Description |
| --- | --- |
| `listDynamicTranslations` | Lists the translatable dynamic strings of one culture with their translation, and the translation progress of every supported culture. Filters by context, search text and untranslated only, with paging. Without a culture it returns only the progress. |
| `saveDynamicTranslations` | Adds or changes translations of one culture; strings that are not sent keep their translation. |

`listDynamicTranslations` needs **View dynamic translations and statistics**. `saveDynamicTranslations` needs **Manage all dynamic translations** or the culture's own **Manage `{culture}` translations** permission, as the **Dynamic Translations** page does. The culture must be one of the site's supported cultures, each string must be one that `listDynamicTranslations` returns, and a translation cannot be empty. The tool runs the `Translations` recipe step, which merges the translations into the culture's saved ones. Translations saved in one reply are stored when the reply ends, so `listDynamicTranslations` asks to be called again in the next reply instead of reading them early.

### Workflow tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + the matching workflow feature set

| Function | Required feature(s) | Description |
| --- | --- | --- |
| `getWorkflowType` | `OrchardCore.Workflows` | Gets information about a workflow type. |
| `listWorkflowTypes` | `OrchardCore.Workflows` | Lists information about workflow types. |
| `createOrUpdateWorkflow` | `OrchardCore.Workflows` + `OrchardCore.Recipes.Core` | Creates or updates a workflow. It needs **Manage workflows** and **Import data**, because the recipe it imports can hold any step. Asks for approval. |
| `listWorkflowActivities` | `OrchardCore.Workflows` + `OrchardCore.Recipes.Core` | Lists all available tasks and activities for workflows. |

### Workflow design tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `OrchardCore.Workflows`

| Function | Description |
| --- | --- |
| `getWorkflowActivity` | For up to 10 activity names, returns the JSON schema of the activity's `Properties`, its outcomes and whether they depend on its settings, its outputs and the values it provides to later activities. |
| `editWorkflowDraft` | Builds a new workflow from a list of operations (`addActivity`, `connect`, `addVariable`, `setSettings`) and saves it disabled. It generates the IDs, checks the result first and saves nothing when there are errors; `validateOnly` only checks. Changing an existing workflow is refused with the designer link. |
| `listWorkflowTypeVersions` | Lists a workflow's published versions, the most recent first, with who published each and when, and marks the version that runs now. |
| `cloneWorkflowType` | Copies a workflow's published activities, transitions, variables and settings under a new name. The copy is always disabled. |
| `setWorkflowTypeEnabled` | Turns a workflow on (`enabled: true`) or off (`enabled: false`) at once, without publishing. Turned off, its events no longer start instances; instances that already wait are kept. Asks for approval. |
| `deleteWorkflowType` | Deletes one or more workflows with their versions, draft, instances and journals. Asks for approval. |

All six tools need **Manage workflows**. `editWorkflowDraft` applies the operations in order and checks that each activity exists on the site (an unknown name returns the closest names), that only events start the workflow, that each activity's settings use only the names its schema declares (a misspelled name returns the closest ones) and match that schema, that transitions connect activities of the call with an outcome the activity produces (with the closest outcome names), and that variables have a known type. An execution setting it does not know, or an `instances` or `branchingMode` value outside its choices, is refused with the valid names or choices, never dropped or replaced by the default. An outcome it cannot check, because it depends on the activity's settings or no outcomes are registered, is reported as a warning, never as checked. The result's `checks` says what ran: the designer's own validation of each activity's settings and the validation of Liquid and JavaScript expressions live in the Workflows module and are reported as not run (`designerValidation: notRun`, `expressions: notChecked`). A new workflow is always saved disabled, so creating one does not ask for approval; going live asks once, in `setWorkflowTypeEnabled` (the flag is per tool, so turning a workflow off asks too). The older `createOrUpdateWorkflow` imports a `WorkflowType` recipe step and can bring in an enabled workflow, so it asks for approval too; it needs **Manage workflows** and **Import data**. Changing an existing workflow (`workflowTypeId`, with the draft's `revision`) is refused and returns the designer link: the designer keeps those changes in a draft that only the Workflows module's draft manager can write. The URL of an HTTP Request event is signed for its workflow, so `editWorkflowDraft` and `cloneWorkflowType` leave it empty and say to generate it in the designer. Enabling or disabling changes the live workflow and creates no version; a draft someone left open in the designer still holds the earlier state and would bring it back when published.

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

`listDataPipelineStepTypes` needs **View data pipelines**. To find the data sets a **Read a data source** step can read and their fields, use `listReportDataSets` and `getReportDataSetSchema` (see [Report Builder tools](#report-builder-tools)): they ask the same data sources with the user and work whether or not Data Pipelines is on; a step's `Source::DataSet` value is the `source` and the data set `name` they return, joined by `::`. The step settings schemas are not offered. The pipelines themselves (list, design, preview, publish, run and run history) are not reachable by tools yet: their documents and services live in the Data Pipelines module, which this module does not reference. Use the designer, or the `DataPipelines` recipe step through `importOrchardCoreRecipe`.

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
| `saveAIProfile` | Creates a profile or agent, or changes one by ID: title, description, type, instructions, welcome message, initial prompt, deployments, model parameters, persona and SystemPrompt templates. With `templateId` it creates the profile from a Profile template, as the **New AI profile** wizard does (a title, a technical name and optionally a chat deployment; everything else comes from the template, and it names the features to enable when the template's scenario needs them). It never changes the profile's tools, agents or connections, its instruction lock or whether it can be deleted, and refuses new instructions on a profile whose instructions are locked. |
| `setAIProfileCapabilities` | Sets the tools, tool instances and agents a profile can use and, for an agent, whether it is always available and whether it runs its tools. MCP and A2A connections are set on the profile's Capabilities tab. Asks for approval. |
| `deleteAIProfile` | Deletes one profile by ID; it cannot be restored. It refuses a profile marked not removable, the assistant that is running with its agents, a profile people or clients can still reach (a published chat widget, the admin chat widget, an enabled browser API key, the Anonymous role, or every profile as an always-available agent) and a profile another profile calls as an agent, and names those uses. Asks for approval. |
| `diagnoseAIProfile` | Checks one profile (by ID or technical name) for what would stop it working: whether its chat and utility deployments exist (or the site defaults when it names none), whether the chat deployment supports tool calling when the profile has tools or agents, whether every tool it names is still registered (renamed, removed or its feature off), whether its tool instances and agents exist, and whether its persona and data source exist. Returns each check with what to fix. Reads only. |
| `listAIProfileVersions` | Lists a profile's saved versions (number, date, author, note), and with `versionId` the settings that differ from the profile as it is now or from another version, as the **Compare** page shows them. |
| `restoreAIProfileVersion` | Restores a profile to a saved version, saved as a new version so it can be undone. Asks for approval. |
| `testAIProfile` | Sends one message to a profile with its tools turned off and returns the reply. It makes a paid model call. Asks for approval. |

The tools need **Manage AI profiles**; `testAIProfile` needs **Query any AI profile** on the profile, so a profile-specific grant counts. Every change is saved as a profile version whose note says the AI assistant made it, and for whom.

Until a site-wide guard for AI configuration exists, the tools that change profiles, templates, personas or tool instances apply three rules themselves:

- **An assistant cannot change itself.** From inside a chat, the profile that runs the tool, the chat session's profile, the admin chat widget's profile, the agents those can call (including every always-available agent), and the persona, SystemPrompt templates, speaking styles, guardrail topics and tool instances they use cannot be changed or deleted. The tool refuses and names the admin page where a person can make the change. This is a refusal, not an approval, because an approval can be clicked through and is not asked inside an agent.
- **No powerful tools on a profile visitors can reach.** A profile is reachable when the Anonymous role may query it, an enabled browser API key names it, a published content item with an AI profile part shows it, or it is an always-available agent. `setAIProfileCapabilities`, `restoreAIProfileVersion` and `saveAIProfile` (also when it creates a profile from a template) may then only add tools marked safe for anonymous callers, and no tool instance, MCP or A2A connection or agent that runs tools. Making an agent always available, or letting an always-available agent run its tools, checks all of its tools. Phone numbers and messaging channels are not checked yet.
- **Only tools the user may use.** Every tool and tool instance a change adds needs the user's own **Access AI tool** permission for it, as the profile editor asks.

### AI profile template tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `CrestApps.OrchardCore.AI`

| Function | Description |
| --- | --- |
| `listAIProfileTemplates` | Lists templates with their source (Profile or SystemPrompt), category, title, whether a module ships them (read-only) and, for a Profile template, whether its scenario can be used and which features it still needs. With `id` it returns one template instead: its instructions and, for a Profile template, the profile settings, tool and agent names it gives a profile created from it. |
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
| `deleteAIVoice` | Deletes a voice at the provider. It cannot be undone. Asks for approval. |

The tools need **Manage AI deployments**. A key a speech deployment holds is never returned, only whether one is stored, and never accepted: the result links to the deployment's admin page, where a person enters it. Deployments defined in configuration cannot be changed or deleted. From inside a chat, the deployments the running assistant uses (its profiles' chat and utility deployments, the site defaults they fall back to, and its persona's voice deployment) cannot be changed or deleted; their prices can.

### AI quality management tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `CrestApps.OrchardCore.AI.Quality`

| Function | Description |
| --- | --- |
| `listAITestRuns` | Lists the runs of a batch or a test case with status, score, pass or fail, the judge's findings, the pass rate and how many are still pending. Use it after `runAITests`. |
| `createAITestCaseFromConversation` | Turns a real conversation into a simulation test case, as **Create test from conversation** does: the customer's lines become the scenario and the conversation's profile and variables are kept. An optional name and criteria replace the defaults. |

Both tools need **Manage AI tests, QA cohorts and alert rules**. Test cases, alert rules and QA cohorts themselves are the record kinds `aiTestCase`, `aiAlertRule` and `aiQACohort` of the [generic record tools](#generic-record-tools): `listRecords`, `getRecord`, `saveRecord` and `deleteRecord` list, read, save and delete them through the editors of the **Tests**, **Alert rules** and **QA** pages, with the same permission. `listAIAlerts` still tells which alert rules are open and their incidents, and `listAITestCases` still lists the test cases. Times are in the user's or the site's time zone.

### AI API key tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `CrestApps.OrchardCore.AI.Chat.Api`

| Function | Description |
| --- | --- |
| `listAIApiKeys` | Lists API keys with name, kind, prefix, scopes, roles, allowed origins and profiles, expiry, enabled state and last use. The key value and its hash are never returned. |
| `saveAIApiKey` | Enables or disables an existing key, or sets or removes its expiry. It never makes a key and never changes a key's name, scopes, roles, web sites or allowed profiles: those widen what a key can do or reach, so a person changes them on the key's page, which the tool links to. Asks for approval. |
| `deleteAIApiKey` | Deletes (revokes) keys by ID. Clients using them stop working at once. Asks for approval. |

The tools need **Manage AI API keys** and apply the checks of the key editor. A key's value never passes through the assistant, a browser key's value included. Enabling a browser key again, or extending an expired one, is refused while one of its profiles can run tools not marked safe for visitors, because a browser key is public.

### AI conversation tools

**Available when:** `CrestApps.OrchardCore.AI.Agent` + `CrestApps.OrchardCore.AI.Chat`

| Function | Description |
| --- | --- |
| `deleteAIConversation` | Deletes one of the user's own conversations and its transcript by session ID. It cannot be undone. Asks for approval. |

Like a profile's chat **History** page, the tool deletes the user's own conversations through the chat session manager. `deleteAIConversation` needs **Delete chat session** and to be allowed to chat with the profile. The conversation the assistant is answering in is never deleted.

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

The prompt tools need **Manage MCP prompts** and the resource tools **Manage MCP resources**, and they apply the checks of the editors. An FTP or SFTP resource needs a host and credentials, so it is created on the admin page; its password, private key and passphrase are listed only as stored or not, and its connection is not changed by the tool. File resources, which read files of the server's own file providers, are not created or changed from the chat (as stdio connections are not); a person does that on the admin page.

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
| `runReport` | Runs a report for a period (and, for a designed report, filter values) and returns its headline numbers and tables, 200 rows per table by default, at most 1,000, and about 60,000 characters. It echoes the filters it applied (`appliedFilters`) and lists in `notApplied` any it could not apply, such as a period on a designed report with no date filter; a built-in report applies only the period. |
| `exportReport` | Returns the download link of a CSV, Excel or PDF export with its file name. The file is built when the link is opened. |

One set of tools serves every report a module registers and every designed report. A built-in report needs its own permission, and its export permission to export, as on the Reports page. A designed report must be one the user may view (its owner, the people and roles it is shared with, and **View all custom reports**), and runs with its owner's access, as on its page; exporting it needs the report to allow export or the right to change it. A built-in report takes only a period (`from`, `to` as days in the user's time zone, or `allTime` when the report offers it): its other filters, such as an AI profile or a queue, are set on its page, because they bind from the page's form. The download link is the report page's own export address, so whoever opens it is checked again and nothing is stored. Dates in results are in the user's or the site's time zone.

### Report Builder tools

**Available when:** `CrestApps.OrchardCore.Reports.Builder` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listReportDataSets` | Lists the data sources and the data sets the user may read, for one source or matching a text. |
| `getReportDataSetSchema` | Returns the fields of one data set and the formula functions for calculated fields. |
| `queryReportData` | Runs an ad hoc query (data sets, joins, calculated fields, filters, grouping, aggregates, sorts, limit) with the user's own access and returns rows, the query as it was normalized and the planner's warnings. When the rows are cut at the tool's row limit or the query reached a Report Builder limit (a data set is read up to 50,000 rows), `truncated` is true, `totalsAvailable` is false and the result says not to state totals from it. Nothing is saved. |
| `saveReportDesign` | Creates and publishes a designed report, or publishes a change to one by ID: title, description, group, query, visuals, admin menu and export options. Never changes who the report is shared with. |
| `shareReportDesign` | Sets the users and roles a designed report is shared with. Asks for approval. |
| `restoreReportDesignVersion` | Restores and publishes an earlier version of a designed report; who it is shared with does not change. |
| `deleteReportDesign` | Deletes a designed report with its draft, versions and share links, or a report that was never published. Asks for approval. |
| `setReportShareLink` | `create` makes a share link for named users of the site and emails it to them (the link is never returned); `revoke` stops one share link of a designed report from working. Asks for approval. |
| `listReportViews` | Lists the saved report views with owner, refresh interval and snapshot state, or one view with its query and the reports and views that read it. |
| `saveReportView` | Creates a report view from a query, or updates one by ID, with an optional refresh interval. |
| `deleteReportView` | Deletes a report view that no report or view reads. Asks for approval. |
| `listReportGroups` | Lists the report groups as a tree with the number of groups and reports in each. |
| `saveReportGroup` | Creates a report group under a parent, or renames, moves or reorders one by ID. |
| `deleteReportGroup` | Deletes an empty report group; a group that holds groups or reports is kept. Asks for approval. |

The data set, query and view tools and creating a report need **Build reports and manage own custom reports and views**; changing, sharing, restoring or deleting a report or view needs **Manage all custom reports and views** on it, which its owner holds; share links also need **Share custom reports publicly and through share links**, and so does sharing with the Anonymous role; the group tools need **Manage report groups**. A report that has unpublished changes in the Report Builder is not published over: the tool names who changed it and asks to publish or discard the draft first. Every user and role a report is shared with must exist. A group is given by its path, such as `Sales / Regional`, and must exist. A share link's address holds a secret token, so `setReportShareLink` emails it to its recipients and returns only its ID, recipients and expiry; the Report Builder shows the address once. A report, view or group changed earlier in the same chat message is changed again only in the next reply, because the change is saved when the reply ends.

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
| `listWebhookDeliveries` | Lists the deliveries of one webhook with event, status, attempts, last status code, last error and times, by status. Payloads are not returned. |
| `sendWebhookDelivery` | Sends to a webhook now and returns the receiver's status code or the error: with `ping` true, a test `webhook.ping` event; with a `deliveryId`, that logged delivery again. Asks for approval. |

The tools need **Manage webhooks** (`ManageCommerceWebhooks`), as every action of **Configuration → Webhooks** does. A webhook sends site data to another system, so every change and every send (a ping or a delivery again) asks for approval. Addresses are checked as the webhook editor checks them: an absolute `https` address the egress guard allows (plain `http` only to this machine in development), and all events or at least one event an enabled module declares; events no module declares are reported and left out. No tool takes or returns a signing secret: a new or rotated secret is copied from the webhook's admin page, whose address the results give. A ping and a delivery sent again go out during the call, as on the admin page.

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

### Learning tools

**Available when:** `CrestApps.OrchardCore.Learning.AI` on (`explainMyQuizResult` also needs `CrestApps.OrchardCore.Learning.Assessments`)

These tools act for the signed-in person using the AI and refuse anonymous callers. The learner tools need **Access learning** and read only the person's own enrollments and attempts; the staff tools list only learners in the person's scope (everyone with **View learner records**, the learners of the courses the person teaches, or the people the person manages). See [Learning AI](../modules/learning-ai.md#learning-ai-tools).

| Function | Description |
| --- | --- |
| `getMyLearningProgress` | Lists the learner's courses with their status, progress, due date, access end and course grade. |
| `getMyNextLesson` | Finds the next lesson the learner has not completed and may open now, with its link. |
| `explainMyQuizResult` | Gets the learner's quiz result: the score when the quiz shows it, and the questions, answers, right answers and feedback only when the quiz's review policy allows. |
| `listOverdueLearners` | Lists the learners whose course is past its due date, within the caller's scope. |
| `listLearnersExpiringSoon` | Lists the learners whose access to a course ends within the coming days, within the caller's scope. |

### Scheduling tools

**Available when:** `CrestApps.OrchardCore.Scheduling` with `CrestApps.OrchardCore.AI` on

These tools are meant for customer-facing assistants and also work for anonymous visitors. They offer only appointment types that are enabled and allow self-service, unless the caller has **Manage appointments**, and hold customers to the appointment type's booking policies. See [Scheduling](../modules/scheduling.md#ai-tools).

| Function | Description |
| --- | --- |
| `listAppointmentTypes` | Lists the kinds of appointments that can be booked. |
| `findAppointmentSlots` | Finds open times for an appointment. |
| `bookAppointment` | Books an appointment for the customer at a time `findAppointmentSlots` returned, and says which staff, rooms and equipment it is with. |
| `changeAppointment` | Cancels or moves a customer's appointment. The customer proves it is theirs with the manage code, or the email or phone number on the appointment. |

A caller with **Manage appointments** is recognized through the chat's signed-in user as well as the web request, so all four tools (the types offered, the slots, booking without the self-service rules, and changing an appointment without proof) treat staff as staff inside the chat hub too.

### Ticket tools

**Available when:** `CrestApps.OrchardCore.Tickets` with `CrestApps.OrchardCore.AI` on

These tools act for the signed-in person using the AI, within the tickets they may view and the actions their ticket permissions allow, and refuse anonymous callers. They are in the **Tickets** category and selectable on AI profiles. See [Tickets](../modules/tickets/ai.md#ai-tools).

| Function | Description |
| --- | --- |
| `listTicketOptions` | Lists the desks the user may open tickets in, every status with its category, the priorities and the ticket categories, with the ids the other ticket tools take. |
| `searchTickets` | Finds the tickets the user may view by number or subject words, status category, desk and assignee (`take` up to 25). |
| `getTicket` | Reads a ticket by id or number with its latest 20 replies, internal notes and events. |
| `createTicket` | Opens a ticket on a desk the user may reply in, for a requester named by name and e-mail, with a category from `listTicketOptions`. |
| `addTicketReply` | Sends a public reply (e-mailed to the requester) or adds an internal note, and can set a status with a reply. |
| `updateTicket` | Changes a ticket's status, priority or category (edit permission) or its team or assignee (assign permission). |

### Service level tools

**Available when:** `CrestApps.OrchardCore.ServiceLevels` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listServiceLevelRisks` | Lists the work (tickets, work orders and any other kind of record with service levels) whose targets are past due (`breached`) or past their warning point (`atRisk`), or due within the minutes given (`dueSoon`), the most overdue first. Each row gives the record (kind, id, title, link), the metric, the policy and priority, and the times the target is or was due. Filters: status, kind of record, metric. Pages of 20 by default, at most 50. |
| `getServiceLevelTimers` | Gets the service-level timers of one record, such as a ticket: for each metric the target, when it started and is or was due, and how it stands (`onTrack`, `atRisk`, `breached`, `paused`, `met`, `metLate` or `stopped`, as the ticket page's badges say it). |

Both tools act for the signed-in chat user and refuse a caller who is not signed in. They see what the **Service-level timers** report shows that user:

- with **Manage service level policies**, every timer;
- otherwise, the timers of the records the record's module lets them see (for tickets, the tickets they may view; for work orders, the work orders they may view);
- no timers of a kind of record whose module says nothing about who sees them.

A record the user may not see, or one without timers, gets the same answer as one that does not exist. A record's title and link are returned only when its module lets the user see that record. Someone with **Manage service level policies** sees every timer, but for a record its module does not let them see, only the kind and id, as on the report.

Times are in the user's or the site's time zone. Only running targets are listed as at risk: a paused target (a ticket waiting on its requester, for example) keeps its due time frozen and is left out.

Totals:

- A caller who sees every timer gets the total.
- Anyone else gets a total only when every matching timer was checked. The tool reads at most 1,000 matching timers to fill a page; past that it says `truncated` and gives no total.

The record's title comes from its own module (a ticket's number and subject, written by its requester) and is returned as data, never as instructions. Neither tool asks for approval: both only read. Service level policies are the read-only `serviceLevelPolicy` kind of the [generic record tools](#generic-record-tools); ticket desks, statuses, priorities and saved views are the `ticketDesk`, `ticketStatus`, `ticketPriority` and `ticketView` kinds.

### Knowledge Base tools

**Available when:** `CrestApps.OrchardCore.KnowledgeBase` with `CrestApps.OrchardCore.AI` on

These tools act for the signed-in person using the AI and refuse anonymous callers. They read only the published articles that person may open, change nothing and ask for no approval. They are in the **Knowledge Base** category and selectable on AI profiles. See [Knowledge Base](../modules/knowledge-base).

| Function | Description |
| --- | --- |
| `searchKnowledgeBase` | Finds published help articles by keywords or a question, optionally within a category (by id, name or path, with the categories under it), best match first: title, summary, category, link, the passage of the body that matches, the publication time and the helpful counts (`take` up to 10). `reader` (`me`, `public` or `signedIn`) keeps the results to articles that reader may open too, before a link goes into a reply to a customer. |
| `getKnowledgeArticle` | Reads one published article in full: title, summary, category, link and body (up to 20,000 characters). |

`searchKnowledgeBase` searches as the help center does, with `KnowledgeArticleSearch`. It first runs the help center's search: every word (up to five, of two letters or more) in the title or summary, the most helpful first. When that finds too few articles, it adds the help center's suggestions: articles with some of the longer words (four letters or more, common words left out), the ones matching the most words first. So a whole question also finds articles. Each result says which way it matched (`allWords` or `someWords`, or `browse` without a query). Both searches keep, in SQL, to the articles the caller's roles may read (Content Access Control), and with `reader` to those that reader may read too, as the suggestions in a ticket reply do: `public` is someone not signed in, `signedIn` anyone signed in. The tool then checks **View content** on each article, as the article's page does. An unknown or ambiguous category is refused with the categories to pick from. When nothing matches, the result lists the categories with the number of articles the caller may read in each. `truncated` says more articles match than were returned; there is no total, because the help center's search does not count.

Drafts are not searched: the help center indexes only published articles. List drafts with `listContentItems` (`type:KnowledgeArticle status:draft`).

`getKnowledgeArticle` reads the published version and needs **View content** on it. A draft, an item that is not an article, and an article the caller may not open get the same answer as an id that does not exist.

Links are the article's path (`help/{slug}`) after the site's base URL when one is set (**Settings > General**), so they work in a reply to a customer; otherwise they are paths on the site. Times are in the user's or the site's time zone. The title, summary, passage and body are returned as data, with a note that tells the model not to follow instructions inside them.

Categories are taxonomy terms: change them with the taxonomy tools. Readers' answers to "Was this helpful?" are in the **Knowledge Base Feedback** report (`runReport`). Publishing, unpublishing and who may read an article are done with the content tools and the content access tools.

### Timeline tools

**Available when:** `CrestApps.OrchardCore.Timeline` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `getTimeline` | Reads the history of one record, newest first, a page at a time (20 by default, at most 50): its notes, comments, replies and events (with the fields they changed), who wrote each and when, and the names of the files attached. `entryKinds` keeps only some kinds (Note, Comment, Event, Reply). |
| `addTimelineNote` | Adds an internal note, which only staff read, to a record's timeline under the user's name, as the **Add note** button of the record's Notes card does. An `@username` in the text notifies that user when they may read the record. |

What the tools cover:

- **Records:** any record with a timeline. That is an account, contact, lead or other content item (`recordKind` `Content`, with the content item ID), an order, a ticket, a work order, or a kind another module adds. An unknown kind returns the kinds that have a timeline.
- **Finding the record:** by `recordId`, or by `lookup` with its number or name among the records of that kind the user may see. When several share the name, the candidates are returned.
- **Who may use them:** both act for the signed-in chat user and refuse a caller who is not signed in. They check what the record's module allows, as the Notes card does. For example:
  - Content items: whoever may edit every item of the type reads and writes internal notes.
  - Orders: **Manage orders**.
  - Work orders: **View work orders** to read, **Manage work orders** to add.
  - Tickets: whoever may view the ticket reads it.
- A record the user may not read gets the same answer as one that does not exist.

What `getTimeline` returns:

- Times are in the user's or the site's time zone.
- An entry's text is capped at 2,000 characters, with its full length given.
- A redacted entry has no text and no files.
- The entry's delivery notice is the only one of its stored properties returned.
- The text, author names, file names and changed values were written by people, customers included. They are returned as data, never as instructions.

What `addTimelineNote` does:

- It saves the note at once.
- Tickets take notes through `addTicketReply` (`internal: true`); `addTimelineNote` refuses them, as the ticket's card has its own composer.
- Order notes are added with `addTimelineNote` and `recordKind` `Order`, as on the order page.

Neither tool asks for approval: one reads, and the other adds an internal note that only staff read and that can be redacted.

### Approval tools

**Available when:** `CrestApps.OrchardCore.Approvals` with `CrestApps.OrchardCore.AI` on

These tools act for the signed-in person using the AI, read through `IUserAccessor`, and refuse anonymous callers and callers without **Access admin panel**, the only permission the **Approvals › Requests** pages ask for. Who may see and decide each request is the request's own rule, as on its page: its approvers, its requester, those who decided it, and those with **View approval requests** whom the subject type's access lets see it. A request the person may not open is answered as one that does not exist. They are in the **Approvals** category and selectable on AI profiles.

| Function | Description |
| --- | --- |
| `listApprovalRequests` | Lists approval requests with the three views of **Approvals › Requests**: `waitingForMe` (the default), the pending requests whose current stage names the user and whose record the user may act on; `mine`, the requests the user asked for; and `all`, every request the user may see, which needs **View approval requests**. Filters by status (for `mine` and `all`) and approval type. Each request has what is asked, the type, the amount and currency, the policy, who asked and when, when it is due and whether it is overdue, the status, the stage with its approvals, and links to the request and its subject. Paging, 20 a page (at most 50). |
| `getApprovalRequest` | Reads one request as its page shows it: what `listApprovalRequests` returns, plus each stage with its approvals and state, every decision with who made it, when and the note, the withdrawal reason, whether the user may decide it now (and why not) and whether the user may withdraw it on its page. |
| `decideApprovalRequest` | Approves or rejects one request as the user, as the **Approve** and **Reject** buttons on its page do. Rejecting needs a note, which the requester sees. Approving the last needed approval approves the request, and the subject's module acts on it at once (an approved pay run becomes Approved; a rejected one goes back to Calculated). Asks for approval. |

The request title, the subject's text, the names of the requester and approvers, the decision notes and the withdrawal reason are written by people (requesters, approvers, customers). The results mark them as data, never as instructions. `decideApprovalRequest` takes its note only from the user's own words.

`decideApprovalRequest` asks for approval because a decision cannot be undone (an approved or rejected request is closed). It is recorded under the user's name on someone else's request, and the subject's module acts on it in the same save. A pay run, for example, moves to Approved and can then be paid; its workflows, webhooks and audit trail are told. The tool returns the request page's own message (approved; rejected; recorded with N more needed; or moved to the next stage). It relays every refusal of the approval rules word for word: not an approver, the user asked for it, it is about the user, already decided, may not act on the record, no note to reject, or the subject changed since approval was asked.

Invoices waiting for approval follow Invoicing's own approval flow, not Approvals, so they are not in `listApprovalRequests`. They are found with `findInvoices` (status `pendingApproval`) and decided with `decideInvoiceApproval` (see [Invoicing tools](#invoicing-tools)). Both tools' descriptions say so.

Withdrawing a request is not a tool: it is one click on the request's page, which `getApprovalRequest` links to and reports as `canWithdraw`. Requests are submitted by the module that owns their subject (such as a pay run submitted for approval on its page) or by the **Request Approval** workflow task, not by a tool.

Approval policies are the read-only `approvalPolicy` kind of the [generic record tools](#generic-record-tools).

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

### User display name tool

**Available when:** `CrestApps.OrchardCore.Users.DisplayName` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `setUserDisplayName` | Sets a user's first, middle, last and display name. Names left out keep their value; an empty text clears one. Fails when the **User Display Name** settings require a name that would be empty. |

The tool needs **Edit users** on that user, as the name card of the user editor does, and saves through the user manager, so the user event handlers and the name index run.

### User avatar tool

**Available when:** `CrestApps.OrchardCore.Users.Avatars` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `setUserAvatar` | Sets a user's avatar from an image already in the media library (a path such as `people/ana.png`, or its `/media/` address), or clears it. |

The tool needs **Manage media**, as the avatar card does, and **Edit users** on that user. The file must exist and have an extension the media library allows whose type is an image. Clearing is refused when the **User Avatars** settings require an avatar. Upload the image first with the media tools.

### Party search tool

**Available when:** `CrestApps.OrchardCore.Users` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `findParty` | Finds people and organizations by name across every party kind (users, customers, business accounts, CRM contacts, staff) in one search, and says which kind each one is. Filter with `kind`. |

The tool searches the party directory the owner pickers of other modules use; each kind is there only when its feature is on (Customers, Accounts, Assets for contacts, Staff). The directory checks no permission, so the tool searches only the kinds the caller may see: users need **Search users in user pickers**, customers **View customers**, staff **View staff**, and accounts and contacts, which are content items, **View content** on each item found. The result lists the kinds searched and the kinds left out because the caller may not see them. Each kind returns at most 25 matches (10 by default).

### Content access tools

**Available when:** `CrestApps.OrchardCore.ContentAccessControl` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `getContentItemAccessRoles` | Tells which roles may view a content item, whether its content type restricts access at all, and, per restricting Role Picker part, the roles of the published item and of a pending draft, the part's rules and the roles it offers. |
| `setContentItemAccessRoles` | Sets the roles that may view a content item in a Role Picker part with **Restrict content** on. An empty list makes the item visible to everyone who may view content. Asks for approval. |

Both tools need **Edit content** on the item. Roles must exist and not be excluded by the part, and the part's **Required** and **Allow select multiple** settings apply, as in the item's editor. A type with several restricting parts needs the part's name. A published item with no pending draft is published again with the new roles, which also needs **Publish content** on it; an item with unpublished changes is never published by the tool, because that would publish the other changes too: pass `saveAsDraft` to save the roles in the draft. Administrators can always view restricted items. Turning restriction on for a content type is a change to its definition.

### Structured data check tool

**Available when:** `CrestApps.OrchardCore.Seo` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `getContentItemStructuredData` | Returns the JSON-LD a published content item's page publishes, built by the same engine as the page, or says that its content type describes none or that pages publish none. |

The tool needs **View content** on the published item, so a restricted item is refused as it is on the site. Nodes that other shapes on the page add while it renders, such as a list's items, are not included.

### Child tenant tools

**Available when:** `CrestApps.OrchardCore.TenantHierarchy.Parent` with `CrestApps.OrchardCore.AI` on, in a parent tenant

| Function | Description |
| --- | --- |
| `listChildTenants` | Lists the child tenants with entry id, name, address, state, description and creation date, by text and state, and optionally the child limit, address pattern, grace period and setup recipes the platform allows. Offers the open link for the child tenants the caller may enter. |
| `saveChildTenant` | Creates a child tenant from a name, a slug, a description and one of the allowed setup recipes, and queues its setup, or changes the name, slug and description of one. Asks for approval. |
| `changeChildTenantState` | Suspends, resumes or reloads up to 50 child tenants. Asks for approval. |
| `removeChildTenant` | Removes a suspended child tenant and its database, at once or after the grace period. Needs its exact display name. Asks for approval. |
| `restoreChildTenant` | Cancels a pending removal; the child tenant stays suspended. Asks for approval. |
| `retryChildTenantSetup` | Removes a failed child tenant and creates it again with the same name, address and recipe, and queues its setup. Asks for approval. |
| `discardChildTenant` | Discards a failed setup, or dismisses the entry of a child tenant the platform removed or moved. Asks for approval. |
| `listChildTenantFeatures` | Lists the features a running child tenant may change, with whether each is on. |
| `setChildTenantFeatures` | Enables or disables features of a running child tenant. Features the Features screen does not offer, such as blocked ones, are ignored and listed. Asks for approval. |
| `listChildTenantAccessGrants` | Lists who may enter every child tenant or one child tenant, with the parent roles and child roles a grant can use. |
| `grantChildTenantAccess` | Lets a parent user or role enter one child tenant or every one, with child roles the Access screen offers. Replaces the roles of an existing grant and says so. Asks for approval. |
| `revokeChildTenantAccess` | Removes an access grant. Asks for approval. |

Each tool needs the permission of the child tenants page action it mirrors: **View child tenants** to list, **Create child tenants** to create, retry and discard a failed setup, **Edit, suspend, resume and reload child tenants** to change, change the state and dismiss, **Remove child tenants** to remove and restore, **Enable and disable features in child tenants** for features and **Manage who may enter child tenants** for grants. The address is only a slug, checked as the page checks it (format, reserved names, the parent's address pattern, addresses in use), and the recipe must be one the parent's policy allows; no tool takes a host, a URL prefix, a database or a connection string, and none is returned. The child tenants page queues a new tenant's setup until its request ends, which a chat does not have, so the tools queue it to run when the chat's reply ends; nothing is created when it cannot be queued. Follow the setup with `listChildTenants`. Entering a child tenant is a browser sign-in and has no tool. The activity of child tenants is in the audit trail.

### Tenant hierarchy platform tools

**Available when:** `CrestApps.OrchardCore.TenantHierarchy.Platform` with `CrestApps.OrchardCore.AI` on, in the Default tenant

| Function | Description |
| --- | --- |
| `getTenantHierarchyOverview` | Shows the parent tenants with their children, the orphaned children, the tenants that can become parents, the database pools and whether the host guard is installed; for one parent, its policy, children and the problems found. |
| `saveParentTenant` | Makes an ordinary tenant a parent (slug, display name, policy) or changes a parent's display name and policy. Fields left out keep their value. Asks for approval. |
| `changeParentTenantState` | Suspends a parent with its running children, or resumes it with the children suspended with it. Asks for approval. |
| `unmakeParentTenant` | Turns a parent without children back into an ordinary tenant. Asks for approval. |
| `removeParentTenant` | Removes a suspended parent and all its children with their data. Needs the parent's exact tenant name. Asks for approval. |
| `moveChildTenant` | Moves a child tenant to another parent, or gives an orphan a parent. Asks for approval. |
| `detachChildTenant` | Makes an orphaned child tenant an ordinary tenant. Asks for approval. |

The tools need **Manage the tenant hierarchy**. A policy change is applied on top of the stored policy, so a field not given, such as the blocked features or the allowed recipes, keeps its value; lists given replace the stored lists, and recipes must be setup recipes of the site. Session times are given in minutes and hours, as in the policy editor. A tenant's connection string, secret and database settings are never returned.

### Time zone map tool

**Available when:** `CrestApps.OrchardCore.TimeZones` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listTimeZoneMaps` | Lists the friendly time zone names shown in time zone pickers and the time zone each maps to, with an optional name filter. |

The tool needs **Manage time zone maps**. Maps are created, changed and deleted on the Time Zones page.

### Phone number verification tools

**Available when:** `CrestApps.OrchardCore.PhoneNumbers.Verifications` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listPhoneNumberVerifications` | Lists the phone numbers queued for or checked by verification, with status, provider, attempts, last error and next due date, and the count per status; filter by number, status and sort. |
| `retryPhoneNumberVerifications` | Re-queues phone verifications for the given content item ids, or for all failed ones, for the enabled lookup provider. Asks for approval. |

Listing needs **Run 'Phone Number Verifications' Report** and retrying **Verify phone numbers**, as the Phone Verifications Queue does. Retrying fails when no provider is enabled. The re-queued records are verified by the **Phone Number Revalidation** background task at its next run (every five minutes), with the configured delay between requests; each lookup is a billable provider call. Provider keys are never returned.

### Geo and territory tools

**Available when:** `CrestApps.OrchardCore.Geo` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listServiceTerritories` | Lists the service territories with parent, postal codes, time zone, travel speed, members and whether a shape is drawn; pass an id to also get its GeoJSON shape and the member kinds that can be assigned. |
| `saveServiceTerritory` | Creates or updates a service territory: name, parent, GeoJSON shape or postal codes, time zone, travel speed and members by kind. Members of kinds you do not pass are kept. |
| `deleteServiceTerritory` | Deletes a service territory. Its child territories move up to its parent. Asks for approval. |
| `findServiceTerritory` | Finds which service territory covers a street address, a latitude and longitude or a postal code. A street address is geocoded first with the configured maps provider and the result returns the point found. Asks for approval. |

The territory tools need **Manage service territories** and run the same checks as the territory editor. The parent must exist, the time zone must be one the time zone pickers offer, and each member must be one its kind lists. Finding the territory of a street address (`address` or `text`) geocodes it, which also needs **Manage maps and geocoding settings**: it is a billable call to the provider chosen under **Settings, Maps and Geocoding** and fails when none is chosen. A point or a postal code is never geocoded. Because a street address leaves the site and the call is billed, `findServiceTerritory` asks for approval on every call, as `geocodeAddress` did. Map provider keys are never accepted or returned, and anything that looks like a key in a provider's error is hidden.

### Account tools

**Available when:** `CrestApps.OrchardCore.Accounts` with `CrestApps.OrchardCore.AI` on; `setAccountMembers` with `CrestApps.OrchardCore.Accounts.Members`, the location tools with `CrestApps.OrchardCore.Accounts.Locations`, and `setAccountTeam` with `CrestApps.OrchardCore.Accounts.Team`

| Function | Description |
| --- | --- |
| `listAccounts` | Finds accounts by name across every account content type and returns id, name, type, status, member count, locations and owner. |
| `getAccount` | Returns one account by id: name, type, whether it is published or has unpublished changes, and when it was created and changed. When its type has them, it also returns its members with their role; its locations with their addresses, and which one is the billing location; its owner and account team with their names; and its e-mail domains (the domains whose people belong to the account). |
| `setAccountMembers` | Replaces the users who sign in for an account and their role (Member or Admin), by user name or email. Asks for approval. |
| `saveAccountLocation` | Adds a named location to an account or updates one by its location id, after checking the address against its country's rules. |
| `deleteAccountLocation` | Removes a location from an account by its location id. Asks for approval. |
| `setAccountTeam` | Sets the staff member who owns an account and the other staff on its account team, by staff member id. |

`listAccounts` needs **List content** and searches only the account types the caller may view. `getAccount` needs **Access admin panel** and **List content**, as the admin's content list does, and **View content** on the account, as the account's page does, and reads its latest version: it returns an explicit list of fields, each section as the page's Overview tab shows it (a member by email, or by user name when there is none, with their role and contact; a location with its postal address; the owner and the team by staff id and name), never the content item itself, which `getContentItemById` returns raw. The other tools need **Edit content** on the account, and `setAccountMembers` also needs **Manage the users who sign in for accounts**. Saving an account with `createOrUpdateContentItem` skips the rules of the account editor's members, locations and team parts; these tools apply them: each member must be an existing user who belongs to no other account (nothing changes when one does not qualify), a location's address is checked against its country's rules and addressed to the account, and team members must be staff. A published account with no pending draft is published again with the change, which needs **Publish content**; an account with unpublished changes is changed only with `saveAsDraft`. Orders and work orders keep their own copy of a removed location's address.

### Business buying and price list tools

**Available when:** `CrestApps.OrchardCore.Accounts.B2B` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `getB2BAccountSummary` | Returns an account's business-buying setup and standing: enabled, terms, credit limit and available credit, amount owed, open term invoices, monthly budget and spend, price list, buyers with their order limits, and whether punchout is on. |
| `saveB2BAccountSettings` | Turns business buying on or off for an account and sets tax id, billing email, payment terms, price list, monthly budget and per-buyer order limits. Only the settings given change. Asks for approval, since it sets credit limits and net terms. |
| `configureB2BPunchout` | Enables or disables punchout for an account and chooses the member the procurement system shops as. Asks for approval. |
| `listPriceLists` | Lists price lists with their percent off, currency and entry count, filtered by name; pass an id to get its prices by SKU. |
| `savePriceList` | Creates or updates a price list: name, percent off and unit prices by SKU, in the store's currency. Prices given replace the stored ones. |
| `deletePriceList` | Deletes a price list. The accounts on it pay the store's prices. Asks for approval. |

Every tool needs **Manage B2B accounts**. The account tools also need **View content** (`getB2BAccountSummary`) or **Edit content** (the other two) on the account, and save it as the account editor does: a published account with no pending draft is published again, which needs **Publish content**, and an account with unpublished changes is changed only with `saveAsDraft`. The rules are the Business buying card's: net days from 0 to 365, a deposit from 0 to 100 percent, no negative credit limit or budget, and a punchout buyer who is a member of the account and not a site administrator. Order limits are given by a member's user name or email; a user who is not a member, or a price list that does not exist, is refused. Amounts are in the store's currency.

A punchout key is a credential: no tool issues, returns or accepts one. The first key and a new key are issued in the Business buying card of the account's editor, which shows the key once, so `configureB2BPunchout` refuses to turn punchout on for an account that has no key and returns the editor's address. The summary says only whether a key exists and when it was made.

### Customer tools

**Available when:** `CrestApps.OrchardCore.Customers` with `CrestApps.OrchardCore.AI` on; `adjustCustomerStoreCredit` with `CrestApps.OrchardCore.Customers.StoreCredit`, and `adjustCustomerLoyaltyPoints` with `CrestApps.OrchardCore.Customers.Loyalty`

| Function | Description |
| --- | --- |
| `listCustomers` | Finds customers by the start of their name, company or email, with the filters of the customer list (Active, WithAccount, Guests, Businesses, Disabled, All) and paging. |
| `getCustomer` | Returns one customer's profile, addresses, recent orders, merge history, privacy log and, when those features are on, store credit balances and loyalty points. |
| `saveCustomer` | Creates a person as a customer or updates one: status (Active or Disabled), names, tax exemption, phone, marketing consent, and email for guests only. Only the fields given change. It does not create or change businesses: a business is an account. |
| `saveCustomerAddress` | Adds an address to a customer's address book or updates one by id, after checking it against the country's rules; can mark it as the default billing or shipping address. |
| `deleteCustomerAddress` | Deletes one address from a customer's address book. Asks for approval. |
| `exportCustomerData` | Returns the link that downloads everything the store holds on a customer, for a data access request. Asks for approval. |
| `eraseCustomerData` | Erases a customer's personal data for an erasure request, everywhere the site keeps it: every module that holds personal data about the customer erases its part too, and orders keep their totals without the personal details. It cannot be undone. Asks for approval. |
| `adjustCustomerStoreCredit` | Adds or removes store credit for a customer in a currency, with a reason. Asks for approval. |
| `adjustCustomerLoyaltyPoints` | Adds or removes loyalty points for a customer, with a reason. Asks for approval. |

Reading needs **View customers** and every change needs **Manage customers**, as on the Customers pages. The tools apply the customer form's rules: a customer needs a name or an email, an account holder's email comes from their user account, and an anonymized customer cannot be edited; an anonymized or merged customer's address book cannot change. `saveCustomer` keeps people only. Organizations belong to Accounts (the duplicate organization concept that `docs/engineering/building-blocks.md` flags on `Customer`), so it takes no kind, company name or tax id, creates every customer as a person, and refuses to change a customer stored as a business, whose page it links to; the stored company name and tax id are still returned by `getCustomer` and `listCustomers`.

`exportCustomerData` never puts the customer's data in the conversation: it returns the address of the customer page's Export action, which checks the permission again, builds the JSON file and records the export in the privacy log when it is opened. `eraseCustomerData` runs the same erasure as the customer page, through every module's personal-data contributor, and records it in the privacy log under the caller's name.

Store credit and points are money. The two adjustment tools pass the balance cards' checks (only a customer with an account, an amount that is not zero, a three-letter currency, a reason), change the balance under the checkout's lock, and keep the reason and the caller's user name with the activity. A balance never goes below zero; the result reports the change actually made.

### Team tools

**Available when:** `CrestApps.OrchardCore.Teams` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listTeamMembers` | Lists who is in a team on a day (today by default, in the user's time zone), with the members of its sub-teams unless `includeSubTeams` is false: each person's name, user id and staff member id, their role, the team or sub-team they are in, the first and last day of the membership and where it comes from. `allDates` lists every membership, past and future. Paging, 20 a page (at most 50). |

`listTeamMembers` needs **Manage teams**, as the Teams pages do. It asks `ITeamMembershipService`, the service every module asks, so it lists the members every member provider reports (Staff, today); names come from the staff directory when Staff is on. People are added to teams on their staff record, with `saveStaffMember`.

The teams themselves are the `team` kind of the [generic record tools](#generic-record-tools): `listRecords`, `getRecord`, `saveRecord` and `deleteRecord` with `kind: team` list the hierarchy in tree order (each team followed by its sub-teams, as **Content > Teams** shows it), read, create, change and delete teams through the team editor, with **Manage teams**. `saveRecord` replays every card of the team editor, the Workforce card included when Workforce is on, then the team manager's rules: a name is required, the parent must exist, a team cannot be placed under itself or one of its sub-teams, and the time zone must be a valid IANA id. The editor's `Kind` field takes `Organization`, `Region`, `Site`, `Department`, `Team` or `Crew`; an unknown kind is saved as `Team`, as the editor does. `deleteRecord` keeps a team that still has sub-teams or that another module still depends on, and returns the reasons. A team's `address` is the postal address of its editor's **Address** card, without coordinates. The Workforce time clock measures its geofence from it. `saveRecord` posts the card's current values back, so a change to another field keeps the address.

### Skill and certificate tools

**Available when:** `CrestApps.OrchardCore.Skills` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listSkills` | Lists the skill catalog with category, required certification and whether each skill is enabled, or with `kind: certificationType` the certification types people can hold (issuer, validity in months, reminder days, document required); filter by name. |
| `saveSkill` | Creates or updates a skill in the catalog: name, category, description, the certification it requires and enabled. Only the fields given change. |
| `deleteSkill` | Deletes a skill from the catalog; nobody holds it any more afterwards. Disable it instead when people still hold it. Asks for approval. |
| `saveCertificationType` | Creates or updates a certification type: name, issuer, validity in months, reminder days before expiry, document required and enabled. |
| `deleteCertificationType` | Deletes a certification type; the certificates of this type are kept. Asks for approval. |
| `getSubjectSkills` | Returns the skills a person holds (proficiency, effective dates and whether each counts today) and their certificates, for a user or a staff member. |
| `saveSkillAssignment` | Gives a user or staff member a skill, changes its proficiency (1 to 5) and effective dates, or with `remove` takes the skill away (a skill held through a linked user or staff record is removed there). A skill that requires a certification does not count until the person holds a valid certificate of it; the result says whether it counts today. |
| `findSkillHolders` | Finds the people who hold a skill at or above a proficiency on a date. |
| `listCertificates` | Lists the certificates people hold, filtered by person, certification type, status or expiring within a number of days, with paging. |
| `saveCertificate` | Records a certificate for a user or staff member, or updates one: certification type, number, issued and expiry dates, document path and notes. A new certificate is pending. |
| `setCertificateStatus` | Verifies a pending certificate or revokes one with a reason. |
| `deleteCertificate` | Deletes a certificate record. Use revoke instead to keep the history. Asks for approval. |

Reading needs **View skills and certifications**, the catalog changes need **Manage skills and certifications**, and changing what a person holds needs **Assign skills and record certifications**, as on the Skills pages and the Skills cards of the user and staff editors. A person is named by `subjectKind` (`User` or `Staff`) and `subjectId`: a user id, user name or email, or a staff member id from `getStaffMember`. Changing a staff member's skills or certificates also needs the person to be among the people the caller manages, as on the staff editor.

The tools write through the Skills services, so their rules apply: unique names, a validity of at least one month, reminders of one day or more, a proficiency from 1 to 5, an expiry worked out from the issue date and the type's validity, and no valid certificate without the copy its type requires. The copy of a certificate is a path to a file already in the media library, never the file itself.

### Staff tools

**Available when:** `CrestApps.OrchardCore.Staff` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `getStaffMember` | Returns one staff member's record with their id, found by id, employee number or name: employment details, manager, direct reports, team memberships, capabilities, linked user, employment history and, when you may see them, date of birth and pay (pay basis, currency, hourly rates and salaries with the dates they start, annual salary, hourly cost and commission rate). |
| `saveStaffMember` | Creates a staff member or updates one: job title, employment type, status (Active, OnLeave or Suspended), dates, manager, time zone, locations, contracted hours, capabilities and team memberships; can link a user account once. It refuses the Terminated status and a last working day (that is `terminateStaffMember`); it can cancel a planned last working day with an empty `terminationDate`. |
| `terminateStaffMember` | Ends a staff member's employment. It needs the staff member's id, or an exact e-mail address or employee number that matches one person; a name only lists who matches, with their ids, and changes nothing. Without a last working day, or with one already past, the person is terminated now and the other modules offboard them as when the staff editor sets the status to Terminated: published shifts after their last day come off the schedule, visits not started go back to the dispatch board, the Contact Center signs them out and their phone extension is freed. A last working day still to come is only recorded; the **Staff upkeep** background task terminates the person once it has passed. Asks for approval. |
| `deleteStaffMember` | Deletes a staff record. It is refused while people still report to them; to keep their history, end their employment with `terminateStaffMember` instead. Asks for approval. |
| `listStaffCapabilities` | Lists the kinds of work staff can be given, built-in and custom, with whether each is enabled. |
| `saveStaffCapability` | Creates a custom capability or changes the display name, description, position and enabled flag of any capability. |
| `deleteStaffCapability` | Deletes a custom capability, or resets a built-in one to its defaults. Asks for approval. |

`getStaffMember` answers the people the staff profile shows a record to: those with **View staff**, the people who manage the person, and the person themselves. The date of birth needs **View sensitive staff data** and pay needs **Manage staff pay rates**; the home base address and the badge are never returned. `saveStaffMember` and `terminateStaffMember` need the person to be in the caller's staff scope (**Manage staff**, or **Manage the staff you manage** for one's reports and teams), and a manager can only add people to the teams they manage. `deleteStaffMember` and the capability changes need **Manage staff**; `listStaffCapabilities` needs **View staff**.

`saveStaffMember` and `terminateStaffMember` write through the staff manager, so its checks apply (unique employee number and user, known teams and time zone, a manager who exists and is not in a loop, a last working day not before the hire date) and its handlers record the employment history and tell the other modules. "Today" for a last working day is the day in the person's own time zone, as the Staff upkeep task counts it. A person with a user account takes their name and e-mail from it, and the account can't be changed once linked. Pay, the badge, skills and the cards other modules add to the staff editor are not changed. A badge PIN is set or cleared on the staff editor. A capability added or changed in a message can be given to staff from the next reply.

### Store setup tools

**Available when:** `CrestApps.OrchardCore.Commerce` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `getStoreSetupGuide` | Lists the "Set up your store" steps in order with whether each is done, skipped or optional, what is set up now, the admin page that does it in full, and the fields its quick action takes. |
| `runStoreSetupStep` | Runs one setup step's quick action with the given field values (for example choose the store currency, save the store details, add a flat shipping rate, add the staff roles) and marks the step done. Asks for approval. |

The tools need **Set up the store**, as **Commerce › Set up your store** does. The steps come from the modules that are on, in the guide's order. `runStoreSetupStep` takes only the fields the step declares, needs the required ones, and takes a choice only from the step's list; a step without a quick action is done on the page its result links to. Some quick actions create a content type or staff roles, so running one asks for approval. A step run in a message is reported as run until the next reply, when its status shows what it set up.

### Product and currency tools

**Available when:** `CrestApps.OrchardCore.Products` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listProducts` | Lists products with their content item id, type, SKU, price and currency, sale price and dates, variant count, published state and, when stock is counted, units available. Filters by text, exact SKU of a product or variant, content type, on sale and published or draft, with paging. |
| `updateProductPricing` | Changes the price, compare-at price, sale price and sale dates of a product, or the prices of one of its variants, with the product editor's checks. Saves a draft unless asked to publish. |
| `saveProductVariants` | Sets a product's option values (for example Size and Color) and its variants' SKU, prices, image and enabled flag. Variants are made from the options and keep their ids. Saves a draft unless asked to publish. |

`listProducts` needs **List content** and shows only the product types the caller may view. The two product tools need **Edit content** on the product, and **Publish content** to publish; a product with unpublished changes is changed only in its draft, so publishing never publishes someone else's edits. They run the product editor's rules: a price when the product can be bought or shows its price, no negative amount, a sale price below the price, and a sale that ends after it starts. Variants come from the options the content type defines and keep their ids, so stock and past orders still point at them, and no SKU is used by two variants. Every amount must fit its currency's decimal places (whole yen, three decimals for a dinar). Sale dates are in the store's time zone; a variant uses the product's sale dates. Removed variants are listed in the result.

Currencies are the `currency` record kind of the [generic record tools](#generic-record-tools): `listRecords`, `getRecord`, `saveRecord` and `deleteRecord` with `kind: "currency"`, with **Manage currencies**. Writes replay the **Commerce › Currencies** editor.

### Product feed tools

**Available when:** `CrestApps.OrchardCore.Products.Feeds` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `getProductFeedStatus` | Returns the XML and CSV feed addresses, whether an access key and the site's base URL are set, how many items the feed holds, and what Merchant Center would refuse. |
| `regenerateProductFeedKey` | Makes a new access key for the feed addresses, or removes it so the feed is public. Asks for approval. |

The tools need **Manage product feeds**. The access key is a secret: it is never shown, and the addresses are returned without it. The full addresses, key included, are on **Settings › Commerce › Product feeds**, whose link the results give. A new key stops every address already given to a shopping channel until it is replaced there.

### Inventory tools

**Available when:** `CrestApps.OrchardCore.Inventory` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listStockItems` | Lists the products and variants the store counts, with units on hand, held and available, the low-stock threshold and backorders, filtered by text and by all, low, out of stock or backordered. |
| `getStockItem` | Returns one stock item, by id, SKU or product and variant, with its level at each location, the units held for orders, the shoppers waiting for a back-in-stock email, and a page of its history. |
| `adjustStock` | Receives units, corrects up or down, or sets a count at one location, with a reason. |
| `transferStock` | Moves available units of a stock item between locations without changing the total. |
| `importStockCounts` | Sets the units on hand of many items at once from rows of SKU (or product and variant) and count, all or nothing, at most 10,000 rows. |
| `syncInventory` | Starts counting products whose type gained the Inventory part and refreshes every stock item's title, SKU and threshold. |
| `listStockLocations` | Lists the stock locations in the order orders take units from them. |
| `saveStockLocation` | Adds a stock location or changes its name, address or place in the order. |
| `deleteStockLocation` | Removes a stock location; the default location can't be removed. Asks for approval. |

Reading needs **View inventory** and every change needs **Manage inventory**, as on **Commerce › Inventory**. Every stock change is a movement in the item's history with the caller as its author and the reason given (a transfer's reason defaults to "From A to B"), and is saved at once, as the page does. When a checkout changed the same stock at the same moment, the tool says so and nothing is changed, and the stock tools refuse further changes until the next reply. A location must be one the store lists; units can still be moved out of a removed location. `importStockCounts` checks every row with the import's own rules before anything changes. The location tools edit the list of **Settings › Commerce › Inventory**: every location needs a name, at most 50 are listed, a location keeps its id, and locations owned by another record, such as a van, are managed with their owner.

### Purchase order tools

**Available when:** `CrestApps.OrchardCore.Inventory.PurchaseOrders` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listPurchaseOrders` | Lists purchase orders with number, supplier, status, location, expected date, units ordered and received, and total; filters by text and status, with paging. |
| `getPurchaseOrder` | Returns one purchase order, by id or number, with its details and lines (stock item, SKU, ordered, received, outstanding, unit cost). |
| `savePurchaseOrder` | Creates a draft purchase order (supplier, location, expected date, note, lines, or started from the items that are low on stock) or changes one that is not yet received or canceled. It sends nothing to the supplier. |
| `markPurchaseOrderOrdered` | Marks a draft purchase order with at least one line as ordered, so its items can be received. It records the status only. |
| `receivePurchaseOrder` | Records items of an ordered purchase order as arrived, by line and quantity or everything still outstanding, adding them to stock at the order's location. |
| `cancelPurchaseOrder` | Cancels a purchase order that is not fully received; what already arrived stays in stock. Asks for approval. |
| `deletePurchaseOrder` | Deletes a draft purchase order; an ordered one can only be canceled. Asks for approval. |

Reading needs **View inventory** and every change needs **Manage inventory**, as on **Commerce › Purchase orders**. A new purchase order gets its number from the same locked sequence as the page (such as `PO-261019-K3QZ7`), so two purchase orders never share one. A draft's lines can be added, removed and their quantities changed; once it is ordered, only the unit costs can change. The tools refuse what the page would quietly ignore: an unknown location, currency or stock item, and a quantity change on an ordered purchase order. Receiving adds the units at the purchase order's location as movements with the caller as their author, never more than is still to come, and is saved at once; when the stock changed at the same moment, nothing is recorded and the tool says so. No tool emails a supplier.

### Coupon tools

**Available when:** `CrestApps.OrchardCore.Checkout` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listCoupons` | Lists coupons and automatic discounts with code, what they take off, dates, use limit and uses so far, and whether each can be used now; filters by text and by active, not available or disabled, and flags enabled coupons with neither a use limit nor an end date. |
| `getCoupon` | Returns one coupon by id or code with all its rules, its recent uses with what each paid for, uses held by open checkouts, the count of generated codes and how many were used, the link that downloads them as a file, and its discount link. |
| `saveCoupon` | Creates or changes a coupon or automatic discount with the coupon editor's rules: kind and amount, dates in the site's time zone, use limits, spend and quantity conditions, buy X get Y, product, category, customer and channel limits, and what it combines with. Asks for approval. |
| `deleteCoupon` | Deletes a coupon with all its generated codes; orders that used it keep their discount. Asks for approval. |
| `generateCouponCodes` | Generates 1 to 5,000 unique codes that share a coupon's discount and rules, with an optional prefix and a use limit per code. Returns how many were made and the link that downloads them as a CSV file, never the codes. Asks for approval. |

The tools need **Manage coupons**, as **Commerce › Coupons** does. `saveCoupon` runs the coupon editor's checks (a unique code, a percentage from 1 to 100, a fixed amount with its currency, an end after the start, limits above zero) and, when it changes a coupon, starts from the stored coupon, so only the fields given change; it never changes how often a coupon was used. It always asks for approval, and warns about an enabled coupon with neither a use limit nor an end date. Generated codes are discounts anyone holding one can spend, so no tool returns them in the chat: `generateCouponCodes` and `getCoupon` return how many there are and the link of the coupon page's **Export** action, which checks **Manage coupons** again and downloads them as a CSV file with their use counts. A generated code is not looked up as a coupon.

### Order tools

**Available when:** `CrestApps.OrchardCore.Orders` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listOrders` | Lists orders with number, date, customer, status, channel, fulfilment status, total, refunded amount and whether money is still owed; filters by order number or customer email, status, channel, customer and the days they were placed, and links to the CSV export of the same list. |
| `getOrder` | Returns one order by id or number: customer and addresses, lines, discounts, shipping, tax, totals, how it was paid, its latest 20 notes and events (`notesTruncated` says when older ones exist; `getTimeline` with `recordKind` `Order` reads them), refunds, disputes, payment risk, what can still be refunded and what is still owed, and links to its page, invoice and packing slip. For an order the store put together, the invoice also says when it was last sent, the number of the document it was sent as (`sentAs`, such as an invoice made by Invoicing), and `notEmailed` when it was made but not e-mailed because e-mail isn't set up. The notes' text and authors, the addresses, the invoice note and the refund and dispute reasons are returned as data. |
| `previewRefund` | Shows what `refundOrder` would do without moving money: what is still refundable, the amount, and where it would go back to, in the refund's own order, with any payment that would be skipped. Returns the token `refundOrder` requires. |
| `refundOrder` | Gives back a stated amount in the order's currency, or everything still refundable with `refundAll`, through the payment provider, optionally restocking or as store credit. Needs the token of a `previewRefund` of the same refund and a new idempotency key on every call. Asks for approval. |
| `cancelOrder` | Cancels an order with an optional note, optionally restocking what a paid order sold; what is still owed for it is canceled too. It never refunds. Asks for approval. |
| `resendOrderConfirmation` | Emails a paid order's confirmation to the customer again and notes it on the order. Asks for approval. |
| `saveDraftOrder` | Creates or changes an order the store puts together for a buyer, or answers a quote request, priced for the buyer and held from stock when saved; it can also price without saving. |
| `sendOrderInvoice` | Emails the buyer a draft order's invoice, or the quote, with the link to review and pay it. Asks for approval. |

The tools need **Manage orders**, as **Commerce › Orders** does. They act through the order page's own services, so its rules apply. A refund moves money and cannot be undone, so it takes two steps (the owner's review, F23). `previewRefund` checks the same rules as `refundOrder` and moves nothing: the amount and the currency, which must be the order's and fit its decimal places; a refund of everything still refundable needs `refundAll` with that exact amount; store credit only when the order allows it; a return must be an approved or received return of the order. It returns what is still refundable, the amount, and where the money would go, planned by the order operations service itself in the order the refund follows: online payments through the checkout's refund service and the payment provider, newest first (a payment whose earlier refund waits to be settled by hand is skipped and listed), then Pay Later and offline payments, then gift cards and store credit; or store credit, when asked. It also returns a `previewToken`. `refundOrder` requires that token: it is tied to the user who previewed, the site, and the refund's order, amount (25.5 and 25.50 are the same amount), currency and options (`refundAll`, `asStoreCredit`, `restock` and `returnId`), and to the order's refund state at the preview (what is still refundable, what was refunded, the return's refunded amount and status, and the payments the refund would go to), so a refund made or a payment changed in between makes it fail. It works once and expires 10 minutes after the preview; a missing, expired, used, mismatched or another user's token is refused and nothing moves. Every refund also needs a new idempotency key, so a second call with the same key is refused instead of refunding twice; a retry with the same key gets the first answer back, and a call refused for its token leaves its key free. A payment method that can't refund automatically is recorded for settling by hand, and the result relays what happened. `cancelOrder` never moves money: what was paid stays refundable, and the result says how much. `getOrder` returns only the listed fields: no phone number, no tender reference, and only whether the order has a pay link (the link itself lets anyone pay or see the order, and is on the order's page). `saveDraftOrder` keeps the draft editor's limits (100 lines, 10,000 of a line, a quoted price below 100,000,000) and saves at once, so stock someone else changed at the same moment is reported. An internal note is added to an order with `addTimelineNote` and `recordKind` `Order` (see [Timeline tools](#timeline-tools)).

`CrestApps.OrchardCore.AI.Core.AIActionPreviewTokens` is the shared store for the tokens a preview tool issues and the tool that acts requires. The token is kept in the distributed cache for 10 minutes and taken once under a distributed lock. Its key holds a purpose, so one tool family's tokens never work for another, and a scope such as the tenant and the caller. The Orders refund tools use purpose `orders-refund-preview`. The Invoicing money tools use `invoicing-money-preview`, with the action family as the first part of the fingerprint.

### Return tools

**Available when:** `CrestApps.OrchardCore.Orders.Returns` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listOrderReturns` | Lists buyers' return requests with number, order, customer, status, whether a refund or an exchange is wanted, and quantity. Filters by return or order number and status (open by default, or requested, approved, received, completed, declined, withdrawn, all), with paging. |
| `getOrderReturn` | Returns one return request by id or number with its lines and reasons, the buyer's comment, its order, what the order can still refund, the suggested refund for the returned items, the return instructions and the stock locations the items can go back to. |
| `updateOrderReturnStatus` | Approves a requested return, declines a requested or approved one (a message is required), or closes an approved or received exchange. Each emails the buyer. Asks for approval. |
| `receiveOrderReturn` | Records that a return's items came back and puts the chosen lines (or all) back into stock, at a named location or where they were sold from. It does not refund. |

All four need **Manage orders**, as Commerce › Returns. A return is refunded with `refundOrder` and its `returnId` (see below), not with a tool of its own.

`refundOrder` (batch 23) takes an optional `returnId`: the id of an approved or received return of the same order. The refund then runs the Returns page's own step (`OrderReturnService.RefundAsync`), which refunds the order, closes the return and emails the buyer, with the same money rules (amount, currency, `refundAll`, idempotency key, and the token of a `previewRefund` given the same `returnId`). `restock` and `asStoreCredit` are refused with a return. The answer gives what the return's refunded amount grew by and says to check the order or the Refunds page for whether the payment method paid it back or it waits to be settled by hand: the service closes the return in both cases.

### Gift card tools

**Available when:** `CrestApps.OrchardCore.Orders.GiftCards` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listGiftCards` | Lists gift cards with the last characters of the code, balance and initial value, currency, status (active, used up, expired, disabled), expiry and recipient. Searches by the last characters of a code or the recipient's email; filters by status, with paging. |
| `getGiftCard` | Returns one gift card with its balance, recipient, note, the order that bought it and its activity (up to 50 entries). |
| `issueGiftCard` | Creates a gift card worth an amount in a currency, with an optional expiry, recipient and note, and optionally emails it. Needs `amount`, `currency` and `idempotencyKey`. Asks for approval. |
| `updateGiftCard` | Changes a gift card's enabled flag, expiry, recipient name and email, and note; only the fields given change. Not the balance. |
| `adjustGiftCardBalance` | Adds to a gift card's balance or takes from it (never more than the balance), in the card's currency, with a required reason. Needs `idempotencyKey`. Asks for approval. |
| `emailGiftCard` | Emails a gift card with its code to its saved recipient again. Asks for approval. |

All six need **Manage gift cards**. A card's code is a bearer credential worth money: no tool returns it (only its last four characters, `codeEndsWith`) and none takes it; `issueGiftCard` always lets the site make up the code. Expiry dates are taken and shown in the site's time zone.

### Order diagnosis tool

**Available when:** `CrestApps.OrchardCore.Orders` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `diagnoseOrder` | Explains why an order is stuck, unpaid or not shipped: what is owed and refundable, its fulfillment status, open disputes and payment risk, and a list of findings. With **Manage payments and refunds** as well, it adds the payment attempts of the order's checkout, its refunds waiting to be settled by hand and the open payment issues about it. It changes nothing. |

Needs **Manage orders**.

### Shipping tools

**Available when:** `CrestApps.OrchardCore.Shipping` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `getOrderFulfillment` | Returns, for one order, the lines and quantities still to ship and its shipments with number, status, lines, carrier, tracking, tracking events, label cost and dates. |
| `createShipment` | Creates a pending shipment for a paid order from some lines (or everything still to ship), with an optional note. |
| `shipShipment` | Marks a pending shipment as shipped with its carrier and tracking, or creates and ships everything still to ship on an order. The buyer is emailed when shipped emails are on. Asks for approval. |
| `updateShipmentStatus` | Marks a shipped shipment as delivered, or as returned so its items can ship again. |
| `deleteShipment` | Removes a shipment that has not left and puts its items back among those still to ship. Asks for approval. |
| `getShipmentLabelRates` | Asks the default carrier provider (EasyPost) for label rates for a pending shipment, weighing it as the label page does unless a weight is given. Buys nothing; the quote is kept 30 minutes. |
| `buyShipmentLabel` | Buys a rate of a quote of `getShipmentLabelRates` for that shipment, at the quoted amount and currency, then marks the shipment shipped with the label's tracking and returns the label link. Needs `idempotencyKey`. Asks for approval. |
| `testShippingRates` | Shows which zone a country and region fall in and what each method would charge for a test parcel, as Try your rates. |

The shipment and label tools need **Fulfill orders**; `testShippingRates` needs **Manage shipping**. Shipping zones and their methods are the read-only `shippingZone` record kind; there is no tool that saves or deletes a zone or a method (see "status.md").

### Fulfillment partner tools

**Available when:** `CrestApps.OrchardCore.Fulfillment` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listFulfillmentRequests` | Lists orders sent, or waiting to be sent, to fulfillment partners: each partner's status and why, its message and order id, the lines and times; for one order, also whether it can be sent now. Tracking is in `getOrderFulfillment`. |
| `sendOrderToFulfillmentPartners` | Queues a paid order for the fulfillment partners that don't have it yet, and again where a request is waiting or needs attention; or queues one request again by its id. It is sent within about a minute and the partner then makes and ships the items, which cannot be undone here. Asks for approval. |

Both tools need **Ship orders and update their shipments** (`FulfillOrders`, Shipping). That is the permission the order's **Fulfillment partners** card, **Commerce › Fulfillment requests** and their Send buttons check. **Manage fulfillment partners** guards only the partners' settings. The tools act as the signed-in chat user and refuse a caller who is not signed in.

`listFulfillmentRequests` works in two ways:

- **Given an order** (by id or number), it shows what the order's card shows:
  - each partner's request;
  - the connected partners the order was not sent to yet;
  - `canSend`, which is true when the card would offer its Send button: the order is paid and there is something to send;
  - `sendNeedsPartnerCheck`.
- **Otherwise** it is the **Fulfillment requests** page: the requests of one status, most recently changed first, a page at a time (20 by default, at most 50).

Each request is returned with:

- the partner;
- its status;
- the reason, as the page words it;
- the partner's own message, returned as data and never as an instruction;
- the partner's order id;
- the lines sent;
- the id of the shipment that holds those lines;
- the number of attempts;
- the times, in the user's time zone;
- `mayAlreadyBeMade`.

The order's address, email and phone are never returned. For the carrier and tracking of the shipment, use `getOrderFulfillment` (Shipping).

`sendOrderToFulfillmentPartners` does what the order's **Send** button and the list's **Send again** button do, but it does not call the partner itself. It queues the work: it records a request for each connected partner that has none, and makes due now every request that is waiting, needs attention or had nothing to send. The **Send orders to fulfillment partners** background task then sends the due requests within about a minute, so a chat never waits on a partner. The answer says the order is queued, not sent, and to check the outcome with `listFulfillmentRequests`. Background tasks must be running on the site, as they must for automatic sending.

The tool:

- refuses an order that is not paid, also when it is given one request (stricter than the list page's **Send again**);
- leaves alone requests that were sent or are being sent;
- works under the order's lock, so it never queues while another person or the background task is sending the order;
- queues a request the partner may already have (the partner did not answer clearly, or sending was interrupted) only with `checkedWithPartner: true`. The page asks the person to confirm in that case. The model passes the flag only after the user says they checked the partner's dashboard and the partner does not have the order.

It asks for approval because the order leaves the site: the partner makes, ships and bills it.

Partners are connected, and automatic sending is turned on or off, on **Settings › Commerce › Fulfillment partners**. That is the `fulfillment-partners` site settings group, which `getSiteSettings` and `updateSiteSettings` read and change; they never show or accept a partner's API key.

### Stripe tools

**Available when:** `CrestApps.OrchardCore.Stripe` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `getStripeConnectionStatus` | Reports live or test mode, whether an account is connected for each environment and its account id, and which keys are set and whether each comes from the site or the application configuration. No key or secret is returned. |
| `disconnectStripe` | Disconnects the live or test Stripe account: removes the webhook at Stripe and clears that environment's stored keys. Keys from the application configuration stay in use. Asks for approval. |

Both need **Manage Stripe settings**. There is no connect tool: connecting needs the secret key, which is entered only on Settings › Payments › Stripe.

### Transaction tools

**Available when:** `CrestApps.OrchardCore.Transactions` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listTransactions` | Lists transactions with title, source, status, total, paid, adjusted, outstanding, refunded, due date and invoice number. Filters by text, status and source, with paging. |
| `getTransaction` | Returns one transaction with its amounts, tax lines and exchange rate; the owner or guest contact; the payer (the party that owes it: kind, id, name and e-mail) and, when the payer can hold it, their customer credit; what it paid for and whether part payments are accepted online, with any minimum; the due date, the reminders sent, and whether reminders are paused; a scheduled charge of a saved card (the card, when it is next charged, and the declines so far); its timeline, up to 50 events (see below); and which actions it allows now. A `dataNote` names the fields people or payment providers wrote. |
| `recordTransactionPayment` | Records money received outside the site: an amount in the transaction's currency, how it was paid (`paymentMethod`), when it was received (`receivedAt`, in the site's time zone, not in the future), a reference and a note. Everything still owed needs `entireBalance`. More than is owed is refused, unless `creditExcess` (with `entireBalance`) keeps the rest as the customer's credit. Needs `idempotencyKey`. Asks for approval. |
| `previewTransactionAction` | Shows what `reverseTransactionEntry`, `applyTransactionCredit` or `adjustTransaction` would do, without changing anything: the entry or the credit involved, what is owed before and after, the status after and the customer credit that moves. Returns the `previewToken` the action requires. It works once, only for the same user and the same transaction, action and arguments, and expires after 10 minutes. |
| `reverseTransactionEntry` | Takes back a payment recorded by hand, or an adjustment (a returned check, a payment on the wrong transaction): what it paid or took off is owed again, and customer credit the payment moved moves back. Both entries stay on the timeline and the reversal can't be undone. A payment taken online is refunded instead. Needs the entry's amount, a reason and the `previewToken`. Asks for approval. |
| `applyTransactionCredit` | Pays toward an outstanding transaction with its payer's customer credit (store credit, which business accounts hold too), recorded like any other payment. The amount is no more than what is owed and no more than the credit; everything owed needs `entireBalance`. Needs the `previewToken` and `idempotencyKey`. Asks for approval. Offered with the Customers **Store Credit** feature. |
| `adjustTransaction` | Lowers what is owed as a discount, waiver, credit or write-off, with a reason; everything still owed needs `entireBalance`. Needs the `previewToken` and `idempotencyKey`. Asks for approval. |
| `cancelTransaction` | Cancels a transaction so it is no longer collected or reminded. It refunds nothing. Asks for approval. |
| `cancelTransactionAutoCharge` | Cancels the charge of a saved card scheduled on a transaction (a subscription renewal, an installment, a booking balance), so the card is not charged and the owner pays the balance themselves. Asks for approval: no admin page can schedule the charge again. |
| `sendTransactionNotice` | Sends the owner the invoice with its pay link, or a payment reminder (needs the Transaction Reminders feature). Asks for approval. |
| `addTransactionNote` | Adds an internal note to a transaction's timeline under the caller's name. |
| `setPartyCollectionSettings` | Sets whether a business account or a customer (`Account:...` or `Customer:...`) gets the scheduled payment reminders and is charged late fees, for every debt it owes or pays, as the collection card on its page does. A setting that is left out keeps its value. Reminders sent by hand still go. |

All need **Manage transactions**.

**Money rules.** Taking back an entry, paying with credit and adjusting follow the owner's money rules (F23), as `previewRefund` and `refundOrder` do. `previewTransactionAction` runs the same checks as the tool that acts, so a token is issued only for an action that tool would accept. The token is bound to the transaction's state at the preview: what was owed, its status and, for a payment with credit, the customer's credit. A payment or an entry that lands in between makes the token fail, so the user sees a new preview. Amounts are always given with the transaction's currency. A reversal takes the entry back whole, so its amount must be the entry's. A payment with credit and an adjustment can't be more than what is owed, and a payment with credit can't be more than the credit; the transaction service would otherwise cap them quietly.

**Card and bank account numbers.** Notes, references and reasons never hold a card or bank account number. These are refused:

- a run of 13 to 19 digits that passes the Luhn check card numbers carry;
- an IBAN in capitals that passes its mod-97 check;
- bank wording (a routing, ABA, transit, sort code or BSB number, an account number) followed right away by a run of six or more digits, because domestic bank details carry no check digits. Bank wording followed by other words, such as "bank account transfer for invoice INV-100234", is accepted.

Ordinary references (dates, fiscal years, check numbers, `account ending 4321`) are accepted. The check is `AIMoneyArguments` in `CrestApps.OrchardCore.AI.Core`, which the Invoicing money tools use too.

**Idempotency keys.** `recordTransactionPayment`, `applyTransactionCredit` and `adjustTransaction` look the key up before they check what is owed. A retry with the same key after the first call went through gets the first answer back, never a refusal about the balance that call lowered. A refused call keeps its key free. `reverseTransactionEntry` needs no key: the transaction service refuses to take an entry back twice, and the token works once.

**What stays on the page.** Pausing the reminders of one transaction is one click on its page; `getTransaction` reports `remindersPaused`, and `setPartyCollectionSettings` pauses them for every debt of an account or customer. Charging a saved card, or scheduling a charge, is done only by the module that owns the debt, never from a chat. A business account's credit is given or taken by hand nowhere in the admin; it moves through payments with credit, overpayments kept as credit, reversals and credit notes.

**Collection settings** are the read-only `partyCollectionSettings` kind of the [generic record tools](#generic-record-tools), written with `setPartyCollectionSettings`. Only accounts and customers are accepted, as only their pages have the card, and the party must exist. **Payment terms** are the `paymentTerm` kind.

Each `getTransaction` timeline event has:

- its `id`;
- for a payment recorded by hand, the payment method, as its key and name, and when the money was received;
- the credit kept from an overpayment;
- the document behind it, such as a credit note number;
- the event it takes back (`reversesEventId`), or `takenBack` when a later event took it back;
- the receipt number.

`recordTransactionPayment` posts what the **Record payment** form on a transaction's page posts.

- `paymentMethod` is one of `cash`, `check`, `bank-transfer`, `card-terminal` or `other`, or any short text up to 100 characters, as the form's field takes it. A common method's label is stored as its key.
- `receivedAt` is read as a wall-clock time in the site's time zone and stored in UTC with `ILocalClock.ToUtcLenientAsync`, as the form does. A time in the future is refused.
- `creditExcess` is the form's **Keep anything above what is owed as customer credit** choice. It is the only way the tool records more than is owed. It needs `entireBalance` too, and the Customers Store Credit feature with a payer who can hold credit; the transaction service refuses otherwise. The answer says how much was added to the credit.

The payment method, the received time and `creditExcess` are part of the idempotency fingerprint, so a retry with the same key and another method is refused.

`getTransaction` returns the payer's contact by name and e-mail only, as the page's **Payer** row does. It never returns the phone number or address.

### Payment, refund and payment issue tools

**Available when:** `CrestApps.OrchardCore.Transactions` and `CrestApps.OrchardCore.Checkout` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listPayments` | Lists payment attempts with method, state, amount, checkout and what they paid for, whether they belong to an order, and for refundable ones what was refunded and what is still refundable. Filters by state, method and checkout. |
| `refundPayment` | Refunds part or all of one settled payment that does not belong to an order, through its payment provider; everything still refundable needs `refundAll`. A payment of an order is refused (use `refundOrder`). Needs `amount`, `currency` and `idempotencyKey`. Asks for approval. |
| `listRefunds` | Lists refunds with method, status (including those waiting to be settled by hand), amount, reason and provider reference. Filters by status, method and checkout. |
| `resolveRefund` | Marks a refund waiting for manual review as settled, with the reference of the money returned by hand (required). Asks for approval. |
| `listPaymentIssues` | Lists failed money operations that need a person; open ones by default. |
| `resolvePaymentIssue` | Marks a payment issue resolved with a note. It performs no payment operation. |

All six need **Manage payments and refunds**.

### Invoicing tools

**Available when:** `CrestApps.OrchardCore.Invoicing` with `CrestApps.OrchardCore.AI` on (`sendInvoiceStatement`: also `CrestApps.OrchardCore.Invoicing.Collections`)

These tools act for the signed-in person using the AI. The newer ones read the person through `IUserAccessor` and refuse anonymous callers. Each checks the permission of the admin page or action it stands for. They are in the **Invoicing** category and selectable on AI profiles.

| Function | Description |
| --- | --- |
| `findInvoices` | Finds invoices by state (draft, open, overdue, paid, void, uncollectible, pendingApproval) or by part of the number, customer name or reference, newest first. Each comes with its total, the amount still due, its dates, and whether it is overdue. **View invoices**. |
| `summarizeReceivables` | Sums what customers still owe on open invoices, per currency. Gives the total, the amount overdue, aging by days past due, and the five customers who owe most. **View invoices**. |
| `draftInvoice` | Drafts an invoice for a customer from lines. Staff check it and issue it. **Manage invoices**. |
| `sendInvoiceReminder` | E-mails an open invoice that still owes something to its customer again, with its PDF and pay link. **Manage invoices**. |
| `getInvoice` | Gets one invoice as its page shows it, first brought in step with its ledger. Returns its lines, totals, what is paid, credited and due, the last 20 history entries, approval details, revision links, late-fee debit notes, credit notes, the bill-to party, and `canDo` (what the user may do with it now, as the page's buttons). Text people wrote is returned under `writtenText`: references, notes, the internal note, the void reason. **View invoices**. |
| `issueInvoice` | Issues a draft: numbers it and posts what it totals to the ledger as owed. With `send` it also e-mails it to the customer's billing address and copy addresses. When the draft's total needs approval, it says so, and with `submitForApproval` it submits the draft. **Issue invoices**. Sending also needs **Manage invoices**. Submitting needs **Manage invoices** or **Issue invoices**. Asks for approval. |
| `decideInvoiceApproval` | For invoices waiting for approval only: approves one (which issues it) or rejects it with a reason (which makes it a draft again). Find them with `findInvoices` and status `pendingApproval`. The approver is always the user, so whoever submitted an invoice can't approve it. Other approval requests, such as pay runs, use `decideApprovalRequest`. **Approve invoices**. Asks for approval. |
| `previewInvoiceAction` | Shows what voiding, writing off or recording a payment would do, without changing anything: what the ledger still has owed, the amount, and the invoice after. For a payment, `receivedAt` (in the site's time zone, not in the future) says when the money arrived. Returns the `previewToken` the action requires. It works once, only for the same user and the same invoice, action and arguments, and expires after 10 minutes. Needs the action's permission: **Void and write off invoices**, or **Record invoice payments**. |
| `voidInvoice` | Voids an open invoice nothing was paid or credited on. What is owed is canceled on the ledger, and the invoice keeps its number. Given a late fee's debit note, it waives that fee. Needs a reason and the `previewToken`. **Void and write off invoices**. Asks for approval. |
| `writeOffInvoice` | Writes off everything still owed on an open invoice as uncollectible. Needs a reason and the `previewToken`. **Void and write off invoices**. Asks for approval. |
| `recordInvoicePayment` | Records money received outside the site against an open invoice: cash, check, bank transfer, card terminal or other, and when it was received (`receivedAt`, in the site's time zone, as the transaction's **Record payment** form takes it; left out, now). Takes an amount in the invoice's currency up to what the ledger still has owed, or all of it with `entireBalance`. Needs the `previewToken` (previewed with the same `receivedAt`) and a new `idempotencyKey`. **Record invoice payments**. Asks for approval. |
| `sendInvoiceStatement` | E-mails a customer their statement, with its PDF, to their billing address on file. It is either open items (what they still owe and how late) or activity over a period. Returns the balances the statement shows. **Manage invoices**. Asks for approval. |

**Money rules.** Voids, write-offs and payments follow the owner's money rules (F23), as `previewRefund` and `refundOrder` do. `previewInvoiceAction` runs the same checks as the tool that acts, so a token is issued only for an action that tool would accept. The token is bound to the invoice's state at the preview: what the ledger had owed, its status, and what was paid and credited. A payment or credit that lands in between makes the token fail, so the user sees a new preview.

The checks:

- **Amount and currency.** Both are always given. The currency must be the invoice's, and the amount must fit its decimal places.
- **Voids and write-offs** take exactly what the ledger still has owed.
- **Payments** are capped by what the ledger still has owed, so nothing is left unapplied. Paying all of it needs `entireBalance`.
- **Idempotency.** A repeated `idempotencyKey` returns the first answer instead of recording twice.
- **Free text.** A reference, note or reason that holds a card or bank account number is refused, with the check the transaction tools use (see [Transaction tools](#transaction-tools)): 13 to 19 digits that pass the Luhn check, an IBAN in capitals that passes its mod-97 check, or bank wording followed right away by six or more digits.

**Refused from a chat, done on the invoice's page:**

- issuing or approving a draft that charges the customer's saved card on issue;
- issuing or approving a revision, because issuing it voids the invoice it revises with no preview;
- payments on an invoice split into a payment schedule. Record those on each part's transaction, on the page or with `recordTransactionPayment`.

**Recipients.** `issueInvoice`, `sendInvoiceReminder` and `sendInvoiceStatement` take no e-mail address. They send only to the customer's own addresses.

**One chat message.** All tool calls in a message share one database session. An invoice command that is refused discards the session's unsaved work. So the tools save the earlier work of the message before every command. After a refusal, they refuse further changes in that message and ask for a new message.

Late-fee policies are the `lateFeePolicy` kind of the [generic record tools](#generic-record-tools).

### Invoicing credit note, estimate and recurring plan tools

**Available when:** `CrestApps.OrchardCore.AI` + `CrestApps.OrchardCore.Invoicing` (credit notes), `CrestApps.OrchardCore.Invoicing.Estimates` (estimates) or `CrestApps.OrchardCore.Invoicing.Recurring` (recurring plans)

| Function | Required feature | Description |
| --- | --- | --- |
| `previewCreditNote` | `CrestApps.OrchardCore.Invoicing` | With only an invoice, lists what is still creditable on each line and the credit notes already issued. With the lines to credit (a quantity or an amount of each) or `creditAll`, the currency and the disposition (`reduceBalance`, `refund` or `customerCredit`), saves the credit note as a draft and returns its lines, tax, total and effect with the token `issueCreditNote` requires. |
| `issueCreditNote` | `CrestApps.OrchardCore.Invoicing` | Issues the draft `previewCreditNote` prepared: numbers it and lowers what the invoice owes, refunds the payment through the payment provider, or adds the amount to the customer's credit. Needs the preview's token and the draft's total, currency and disposition. Asks for approval. |
| `draftEstimate` | `CrestApps.OrchardCore.Invoicing.Estimates` | Drafts an estimate for a customer (a party, or a name and email) from lines, with an optional expiry date, deposit and what accepting it invoices. It stays a draft. |
| `sendEstimate` | `CrestApps.OrchardCore.Invoicing.Estimates` | Numbers an estimate and emails it to the customer with its PDF and the link to accept or decline it; when the site's email is not set up, only numbers it. Also emails an estimate waiting for an answer again. Asks for approval. |
| `convertEstimateToInvoice` | `CrestApps.OrchardCore.Invoicing.Estimates` | Turns a sent or accepted estimate into a draft invoice: all of it, its deposit as a prepayment invoice, or a percent of its lines (progress invoicing). Asking again returns the invoice made the first time. |
| `pauseOrResumeRecurringPlan` | `CrestApps.OrchardCore.Invoicing.Recurring` | Pauses a recurring invoice plan, skipping the runs due while it is paused, or resumes a paused one from its next date on or after today. Asks for approval. |

The tools act as the signed-in user, read from the chat's user accessor, with the permission the admin action checks: **Issue credit notes** for the credit note tools, **Manage estimates** for the estimate tools (and **Create invoices and change drafts** too for `convertEstimateToInvoice`, as on the estimate's page), **Manage recurring invoices** for `pauseOrResumeRecurringPlan`. An argument a tool does not declare is refused.

A credit note moves money or credit and cannot be undone, so it takes two steps. `previewCreditNote` starts the draft through the invoice's **Credit** action, keeps the lines asked for as the credit note editor would (a quantity at the line's own price, only on a line nothing was credited on yet; or an amount of the line's total, tax included, at the line's own taxes), works it out, and checks it with the rules the issue applies: no more than is left to credit on each line and on the invoice, an amount that adds up to everything still creditable only with `creditAll`, the currency the invoice's, `reduceBalance` no more than the ledger still has owed, `refund` no more than was paid and not refunded, and `customerCredit` only when the customer can hold store credit. A draft that breaks a rule is deleted again and the errors are returned. A draft someone already started on the invoice is never touched: the tool names it, and changes its lines, reason and disposition only when `replaceDraft` names it and the new credit passes every check; a refused replacement leaves the draft as it was. The reason, printed on the credit note, may not hold a card or bank account number. The token is tied to the user, the site, the stored draft and the ledger's limit for its disposition; it works once and expires after 10 minutes. `issueCreditNote` refuses a draft changed on its page after the preview, a total, currency or disposition other than the draft's, and a missing, used, expired or another user's token. The issue is idempotent per credit note: a second call reports the credit note as issued already and moves nothing, so the tool takes no idempotency key. An issued credit note has no void action, as on its page.

`sendEstimate` does what the estimate's **Send** button does and never returns the estimate's signed link, which lets anyone holding it accept the offer; staff copy it from the estimate's page. `convertEstimateToInvoice` makes a draft invoice only: nothing is numbered, posted, sent or charged until staff issue it. `pauseOrResumeRecurringPlan` changes nothing else on the plan; a resumed plan does not make up the runs it skipped. It asks for approval: once a plan is resumed, the recurring task (every 30 minutes) makes its next invoice as soon as it is due, today included, and issues it, emails it or charges the customer's saved payment method as the plan's mode says, with nobody asked again. Recording a customer's acceptance or decline, revising, canceling and deleting estimates, and starting, canceling and running recurring plans stay on their pages.

Estimates, recurring plans, usage meters and unbilled charges are the `estimate`, `recurringInvoicePlan`, `usageMeter` and `billableItem` kinds of the [generic record tools](#generic-record-tools).

### Point of sale tools

**Available when:** `CrestApps.OrchardCore.PointOfSale` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listRegisterShifts` | Lists register shifts, optionally for one register, or returns one shift in full with its cash movements, takings by tender and department, expected against counted cash, and the card and cash tips the cashier declared, with whether payroll took them (after that they can no longer change). |
| `declareShiftTips` | Declares or corrects the card and cash tips of a closed register shift, as the shift page's **Tips** card does, until payroll took them. Amounts are in the shift's currency, zero or more, with at most two decimals; a tip that is left out keeps its value, and zero clears it. Asks for approval: with Payroll on, card tips are paid to the cashier on the next pay run and cash tips are reported for tax. |

Both need **Manage registers**. Registers and departments are the read-only `posRegister` and `posDepartment` record kinds.

### Subscription tools

**Available when:** `CrestApps.OrchardCore.Subscriptions` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listSubscriptions` | Lists subscription agreements with optional status, title search, owner and paging. Use it to find an agreement's id before reading, canceling or pausing it. |
| `getSubscription` | Returns one agreement: owner, plan, amount, billing period, next billing date, lines, entitlements and its history of events. |
| `cancelSubscription` | Cancels an agreement now or at the end of the paid period and tells the payment provider to stop billing. Cannot be undone, and never refunds. Asks for approval. |
| `setSubscriptionPaused` | Pauses billing on an agreement (with a reason) or resumes a paused or past-due one. The change is sent to the payment provider, so resuming means the customer is billed again. Asks for approval. |

The tools need **Manage subscriptions**, as **Subscriptions › Agreements** does, and act through the agreement's lifecycle service with the caller recorded as the source. The page reports success even when a cancel, pause or resume changes nothing; the tools check the agreement first and say so. Canceling never gives money back: the answer says nothing was refunded and points to `refundOrder`. When the payment provider bills the agreement, the change is sent to it after the agreement is saved; a refusal there is recorded as a payment issue, and the answer says to check it. The provider's customer id, the checkout session and a guest's email are never returned.

### Site provisioning tools

**Available when:** `CrestApps.OrchardCore.Subscriptions.Tenants` with `CrestApps.OrchardCore.AI` on (Default tenant only, as the feature)

| Function | Description |
| --- | --- |
| `listSiteProvisioningJobs` | Lists the jobs that create sold sites, newest first, with status, attempts, next attempt, last error, tenant name and site URL, and whether each can be retried or marked created. |
| `resolveSiteProvisioningJob` | Retries a job (`action: retry`), or closes an abandoned job whose site was created by hand, with its URL (`action: markCreated`). |

The tools need **Manage subscriptions**, as **Subscriptions › Site provisioning** does. A retry resets the job and queues it: the Tenant Provisioning background task, which runs every minute, creates the site, and `listSiteProvisioningJobs` shows the outcome. A retry is refused once the job no longer holds the administrator password; create the site with the tenant tools, then mark the job created with the site's address, which the customer then sees. The administrator password, user name and email kept by the job and the customer's contact email are never returned.

### Installment plan tools

**Available when:** `CrestApps.OrchardCore.Subscriptions.Installments` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listInstallmentPlans` | Lists installment plans with optional status, search, customer and paging, showing customer, total, paid, outstanding and next payment. |
| `getInstallmentPlan` | Returns one plan with its schedule (each payment's number, due date, amount, status, attempts and last failure), its history, the saved card by brand and last four digits, and, while it waits for its down payment, the admin link where staff take that payment by card. |
| `createInstallmentPlan` | Creates a draft plan for an existing customer and returns the link where staff take the first payment by card. Nothing is charged. |
| `chargeInstallmentPayment` | Charges the saved card for one scheduled payment now. The amount and currency must equal the payment's, and every call needs a new idempotency key. Asks for approval. |
| `cancelInstallmentPlan` | Cancels a plan so payments not yet received are no longer collected; payments already received are not refunded, and a customer whose plan had started is emailed. Asks for approval. |

The tools need **Manage installment plans**, as **Subscriptions › Installment Plans** does, and act through the plan service the page uses, with its validation and messages. Due dates are dates, shown as entered. `createInstallmentPlan` takes only an existing customer (`customerUserId`, from `searchForUsers`); a plan for a new customer, which creates an account, is made on the page. `chargeInstallmentPayment` keeps each idempotency key a day per site and caller, so a repeated call is refused instead of charging twice; a charge the provider has not confirmed yet is reported as pending, never as received. The saved card's provider references and the customer's email are never returned.

### Membership tools

**Available when:** `CrestApps.OrchardCore.Memberships` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listMemberships` | Lists memberships, newest first, with member, product, roles, start and end dates, status and orders; searches by the start of the member's user name and filters by active, expired or ended early. |
| `endMembership` | Ends an active membership now and takes the member out of the roles it added that nothing else still gives them. Refunds nothing. Asks for approval. |

The tools need **Manage memberships**, as **Commerce › Memberships** does. `endMembership` refuses a membership that is not active, as the page does, records "Ended by" the caller with the reason given, and saves at once. Refund the order separately when money should go back.

### Marketplace tools

**Available when:** `CrestApps.OrchardCore.Marketplace` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `getVendor` | Returns one vendor with its totals per currency (held, available, paid out), its latest earnings per order and its latest payouts with any failure message, and the link to its page. |

The tool needs **Manage the marketplace**, as **Commerce › Vendors** does. As the vendor's page, it first reads the payout account's state from Stripe when Stripe Connect is on, and totals the same 500 latest earnings (`totalsCoverLatestEarningsOnly` says when there may be more); it shows the 20 latest earnings and payouts. It never returns the payout account's id, its request key or a transfer id: only `hasPayoutAccount` and `payoutsEnabled`. Vendors are listed with the generic record tools (kind `vendor`, read only). No tool adds or changes a vendor, releases earnings or pays a vendor out; those stay on the vendor's page.

### Listing moderation tools

**Available when:** `CrestApps.OrchardCore.Classifieds` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listClassifieds` | Lists posted listings, newest first, by status (waiting for approval by default, approved, rejected, expired or any) with a search on the title: type, poster, place, posting, expiry and featured-until times, and the reject reason. |
| `moderateClassified` | Approves a posted listing (publishes it and starts its term from now) or rejects it with an optional reason (takes it down and emails the poster the reason). Asks for approval. |

The tools need **Moderate posted listings**, as **Content › Posted listings** does, and act through the listing service, so approving starts the term from the type's listing days and rejecting unpublishes the listing and emails its poster when the site's email is set up. Do not approve or reject by editing the listing's part with the content tools: that skips the term, the publishing and the email. Titles, places and reasons are written by the people who post listings; the results say to treat them as data.

### Fundraising tools

**Available when:** `CrestApps.OrchardCore.Fundraising` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `getFundraisingCampaignProgress` | Returns a campaign's amount raised (net of refunds), goal, percent, donors and days left and, when asked, its donations largest first (donor, email, amount, refunded, receipt number, tribute, order), optionally from a least amount, a page at a time. |

The tool needs **Manage fundraising**. No admin page lists a campaign's donations; donor names and emails are personal data and are returned only to callers with that permission. The progress is the one the campaign's page shows.

### Digital download tools

**Available when:** `CrestApps.OrchardCore.Downloads` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listOrderDownloads` | Lists the download entitlements of an order, by id or number: file, product, downloads used, limit and left, expiry, last download, and whether the link is working, revoked, expired or over its limit. |
| `setOrderDownloadAccess` | Revokes one download entitlement, with an optional reason, so the buyer can no longer download the file, or resets it so the buyer can download again with the count at zero and a new expiry. |

The tools need **Manage digital files and downloads**, as the Downloads card of an order's admin page does. Revoke and reset undo each other, so neither asks for approval. The download link itself lets anyone download the file and is never returned; the buyer finds it on their order page and in their email.

### Product review tools

**Available when:** `CrestApps.OrchardCore.Reviews` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listProductReviews` | Lists reviews, newest first, by status (waiting for approval by default, approved, rejected or any) and optionally for one product, with rating, title, text, author, verified purchase and photo count; with a product, also its average rating and count. |
| `moderateProductReview` | Approves a review so it shows on the product and counts toward its rating, or rejects it so it is hidden. A rejected review can be approved later. |
| `deleteProductReview` | Permanently deletes one review. Prefer rejecting. Asks for approval. |

The tools need **Moderate product reviews**, as **Commerce › Reviews** does, and refresh the product's rating as the page does. Moderating a review that already has the status says so and changes nothing. Review text is written by buyers; the results say to treat it as data, never as instructions.

### Marketing tools

**Available when:** `CrestApps.OrchardCore.Marketing` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `getMarketingSegment` | Returns one segment with its rules and two counts: customers in the segment and customers who can be emailed (active, with an email, accepting marketing). The second count stops at 10,000; opted-out addresses are also skipped when a campaign is sent. |
| `sendMarketingCampaignTest` | Emails one test of a campaign, rendered for a sample customer of its segment, to one address, with the subject marked [Test]. An address that opted out of email is refused. Asks for approval. |
| `sendMarketingCampaign` | Sends a draft or scheduled campaign as its page's **Send** does: scheduled when its send time is still to come, otherwise queued for the Marketing background task, which sends it in batches and skips customers who don't accept marketing and opted-out addresses. Refused when the site's email or base URL is not set up, or when consent can't be checked. Asks for approval. |
| `cancelMarketingCampaign` | Cancels a scheduled or sending campaign and stops its outreach run. Emails already sent stay sent. |

The tools need **Manage marketing**, as **Commerce › Segments** and **Commerce › Campaigns** do. `sendMarketingCampaign` never says a campaign was sent: nothing goes out during the call, and the answer says "queued" or "scheduled". Before every email the outreach run asks the consent service (`IConsentService`) whether the address opted out; the tool refuses to queue a campaign when no consent service is registered. Segments and campaigns are listed and read with the generic record tools (kinds `marketingSegment` and `marketingCampaign`); they are created and edited on their admin pages.

### Booking tools

**Available when:** `CrestApps.OrchardCore.Bookings` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listBookings` | Lists bookings with a search, a status, a product and a range of days, from today on by default: title, start and end, people, customer name, total, status and order number. Without a status, unpaid and expired holds are left out, as on the page. |
| `getBooking` | Returns one booking: customer, number of travelers, the balance still owed (amount, due date, status, last failure) and, while it can be canceled, the cancellation quote (days before, fee percent, paid, the policy's refund). Travelers' details stay on the booking's page. |
| `completeBooking` | Marks a confirmed booking as completed or as a no-show, and notes it on the order. |
| `cancelBooking` | Cancels a booking, refunds the stated amount through its order (0 for no refund, everything paid only with `refundAll`), lets go of any balance owed, frees the dates and emails the buyer. Needs the booking's currency and a new idempotency key on every call. Asks for approval. |
| `chargeBookingBalance` | Charges the balance still owed to the card the deposit was paid with, also after a failed attempt. The amount and currency must equal the balance owed, and every call needs a new idempotency key. Asks for approval. |
| `listBookingDates` | Lists the dates or time slots of one bookable product (from yesterday on), or of every product in a range of days (this month by default): capacity, held, sold, remaining, the date's own price, closed. |
| `saveBookingDate` | Adds a date or time slot to a bookable product with a capacity (the product's by default) and an optional price, or changes a date's capacity, price or closed flag, keeping what is not given. The capacity never goes below what is held or sold; a date is closed, never deleted. |

The tools need **Manage bookings**, as every **Commerce › Bookings** page does, and act through the pages' own services. Days, times and slot keys are in the store's booking time zone, which every result names. The money tools are stricter than the page: the page refunds the policy's amount when no amount is typed, while `cancelBooking` always refunds exactly the amount given, in the booking's currency, never more than was paid toward the booking, and everything paid only with `refundAll`; `chargeBookingBalance` charges only when the amount and currency given equal the balance owed. Both need a new idempotency key on every call, kept a day per site and caller, so a repeated call is refused instead of refunding or charging twice; a call that ran keeps its key even when it failed. A charge the provider accepted may still be settling, and the answer says so.

### Agentic commerce admin tools

**Available when:** `CrestApps.OrchardCore.AgenticCommerce` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listAgentCheckouts` | Lists the checkouts AI agent platforms opened in the store, newest first, filtered by platform, open or ended, and whether they became an order; or explains one checkout by its id, or by the id or number of the order it became: platform, protocol, status in plain words, the order, times, item count, and why it stopped (the payment message the agent got). |

The tool needs **Manage AI agent platforms**, as **Commerce › AI agents** does. It never returns the buyer, the billing or shipping address, the consents, the platform's protocol data, the card authentication challenge, the intent trace or the escalation link itself (only whether there is one). A failure message comes from the payment provider or describes what the agent sent; the result marks it as data, never as instructions. These admin tools are separate from the five storefront tools above: they are never offered to anonymous visitors and do not need the product listing or feeds features.

The AI agent platforms themselves are the `agentPlatform` kind of the [generic record tools](#generic-record-tools): `listRecords`, `getRecord`, `saveRecord` and `deleteRecord` with `kind: agentPlatform` list, read, create, change and delete them through the platform editor of **Commerce › AI agents**, with its validation (a name, at least one protocol, https addresses, origins, the ACP signature algorithm and public key). The API key and the webhook signing secret are not editor fields: they are issued once by the page's **New API key** and **New webhook secret** buttons and are never returned; `getRecord` says only whether each is set, with the key's hint. To stop a platform for a while, save it with `Enabled` off instead of deleting it.

### Agent product feed tools

**Available when:** `CrestApps.OrchardCore.AgenticCommerce.Feeds` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `getAgentFeedStatus` | Returns the state of the AI agent product feed: how many products are in it, which are left out and why (up to 20), the SFTP push and feed API settings without their credentials, and the last SFTP push and the last push to each ACP feed API target with its result message. |
| `pushAgentFeeds` | Pushes the product feed now, over SFTP and to every enabled ACP feed API, with the saved settings, and returns each result. Asks for approval. |

The tools need **Manage agent product feeds**, as **Settings › Commerce › Agent feeds** does. `pushAgentFeeds` runs the same push as the page's **Push now** button and sends the store's product data to outside servers, so it asks for approval; a push that failed is reported as failed with the message the pusher recorded, and when neither the SFTP push nor a feed API target is on, nothing is pushed and the result says so. No SFTP password, private key, passphrase or feed API bearer token is ever returned, only whether each is set; the SFTP host and the feed API's host are shown. Product titles and push messages are marked as data.

### Omnichannel activity tools

**Available when:** `CrestApps.OrchardCore.Omnichannel.Managements` with `CrestApps.OrchardCore.AI` on

Activities are customer work items, so these tools check the permission of the admin page they mirror and return only the fields listed here: never the contact's phone numbers or addresses, the instructions, the subject's form or the AI session. No admin page limits activities by queue or campaign, so neither do the tools. Times are in the user's time zone. `getOmnichannelActivity` marks the notes and hand-off summary as data, never instructions.

| Function | Description |
| --- | --- |
| `listOmnichannelActivities` | Lists activities with paging. Scope `mine` returns the caller's scheduled manual activities (**List activities**), `contact` one contact's scheduled or completed activities (**List contact activities** on that contact), and `all` the Manage Activities list with its filters: status, interaction type, campaign, dialer profile, assignees, scheduled and created dates, urgency and phone number (**Manage activities**). |
| `getOmnichannelActivity` | Returns one activity: contact, subject type, channel, schedule, assignment, status, attempts, disposition, notes, hand-off summary and whether an AI session ran it. Only for an activity the caller could see on an admin page: their own, one on a contact whose activities they may list, or any with **Manage activities**. |
| `completeOmnichannelActivity` | Completes an activity with a disposition and notes, as the Complete page does (**Complete activity**, or **Complete own activity** for the caller's own). The disposition must be one the activity's subject offers; its subject actions then run. The subject's own form fields are not filled in, and follow-up dates use the subject actions' defaults. |
| `purgeOmnichannelActivities` | Purges up to 50 activities by ID so they are never worked (**Purge activity**, checked on each activity). Activities on a live call, and activities already completed or purged, are skipped and reported. Asks for approval. |

Creating, editing and logging activities, and the other Manage Activities bulk actions (assign, reschedule, change subject, source, campaign or dialer profile), are done on the admin pages.

### Omnichannel activity load tools

**Available when:** `CrestApps.OrchardCore.Omnichannel.Managements` with `CrestApps.OrchardCore.AI` on

The tools need **Manage activity batches**.

| Function | Description |
| --- | --- |
| `listActivityBatches` | Lists activity loads with their source, campaign, channel, status (New, Started, Loading, Loaded), totals loaded, matched and skipped, and any load error, or the full definition of one load. |
| `saveActivityBatch` | Copies a load as a new, unloaded load, optionally with a new title. A load is created from scratch or changed on the Load Activities page. |
| `previewActivityBatch` | Counts how many records a saved load would match and how many it would skip and why, without creating anything. |
| `loadActivityBatch` | Starts loading a saved load in the background after the reply, creating one activity per match. For an automated or dialer load this leads to real calls or text messages. A load runs once. Asks for approval. |
| `deleteActivityBatch` | Deletes a load record; the activities it created are kept. Refused while it is loading; a loaded batch also needs **Delete loaded activity batches**. Asks for approval. |

### Omnichannel cadence tools

**Available when:** `CrestApps.OrchardCore.Omnichannel.Managements` with `CrestApps.OrchardCore.AI` on

Campaigns, campaign groups and dispositions are the `omnichannelCampaign`, `omnichannelCampaignGroup` and `omnichannelDisposition` record kinds, and cadences are read as the `omnichannelCadence` kind, with the [generic record tools](#generic-record-tools).

| Function | Description |
| --- | --- |
| `saveOmnichannelCadence` | Creates or updates a cadence and its ordered steps, each a delay in minutes and a fixed or AI-written message (**Manage cadences**). Read the cadence first with `getRecord` (kind `omnichannelCadence`), which returns these field names. |
| `deleteOmnichannelRecord` | Deletes a cadence or a subject flow action, with the permission of that kind (**Manage cadences** or **Manage subject flows**). Dispositions, lead statuses and opportunity stages are deleted with `deleteRecord`; campaigns, campaign groups and addresses cannot be deleted. Asks for approval. |

### Omnichannel subject flow, not-in-service and AI call tools

**Available when:** `CrestApps.OrchardCore.Omnichannel.Managements` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listSubjectFlows` | Lists the subject content types with their flow settings, or one subject's actions per disposition: the action type, what it schedules and the do-not-call, do-not-text and do-not-email flags it sets (**Manage subject flows**). The AI prompts of a flow are reported as flags only. |
| `saveSubjectAction` | Creates or updates one action of a subject flow: the disposition that triggers it, the action type (Finish, TryAgain, NewActivity, and ConvertLead with the CRM) and its settings (**Manage subject flows**). The type and subject cannot change after creation; the flow settings themselves are content type settings. |
| `listNotInServiceNumbers` | Lists or searches the numbers marked as not in service, with the source, reason, who marked them, and when they were first and last detected (**Manage activities**). |
| `setNumberNotInService` | Marks up to 50 numbers as not in service by hand, as the caller, or allows dialing them again (**Manage activities**). Numbers need their country code. |
| `startAIActivity` | Queues an automated AI call or text to one E.164 number with an AI profile, optionally from an address, for a contact, campaign and subject, now or at a time in the user's time zone (**Edit activity**). Before queuing it asks the consent service (the contact's flags, the opt-outs recorded against the number and the site's do-not-call lists) and refuses an opted-out number, and refuses a number marked not in service. The result says `queued: true`, never "started": the tool does not place the call or send the text, and the automated activities processor screens the activity again when it is placed (opt-out, do-not-call registries and calling window, numbers not in service). The activity's source is `AIAssistant`. Asks for approval. |
| `recordContactOptOut` | Records that a person asked not to be contacted by phone, text message or e-mail at a number or address, through the consent service (**Edit activity**; with a `contactId`, also **Edit content** on that contact). With a contact, the contact's do-not-call, do-not-text or do-not-e-mail flag is set; without one, the opt-out is kept against the address. Opted-out numbers and addresses are skipped by every send, dial and campaign from then on. It records opt-outs only and never opts anyone back in. |
| `checkContactConsent` | Tells whether a person opted out of phone calls, text messages or e-mail at an E.164 number or e-mail address, optionally for a CRM contact, and what recorded the opt-out (the contact's communication preferences, an opt-out recorded against the address, or one of the site's do-not-call lists), by asking the consent service every send, dial and campaign asks; it never reads a do-not-call list or a contact flag itself (**List activities** or **Edit activity**; with a `contactId`, also **View content** on that contact). Consent records only opt-outs, so the answer is "opted out" or "has not opted out", never "consented". Quiet hours, calling windows and numbers not in service are checked when a call or message is placed, not here. |
| `cancelAIActivity` | Cancels an automated call or text that has not started (**Edit activity**); one already in progress or finished is reported as a conflict. |

### Omnichannel address tools

**Available when:** `CrestApps.OrchardCore.Omnichannel.ChannelEndpoints` with `CrestApps.OrchardCore.AI` on

Addresses are read as the `omnichannelAddress` record kind with the [generic record tools](#generic-record-tools).

| Function | Description |
| --- | --- |
| `saveOmnichannelAddress` | Creates or updates an address's name, type, value, capabilities, provider and description (**Manage channel endpoints**). The settings other features add to the address editor are set on the admin page, and no credential is accepted. Changing an address re-routes its inbound traffic at once. |

### Omnichannel CRM tools

**Available when:** `CrestApps.OrchardCore.Omnichannel.Crm` with `CrestApps.OrchardCore.AI` on

Lead statuses and opportunity stages are the `leadStatus` and `opportunityStage` record kinds of the [generic record tools](#generic-record-tools).

| Function | Description |
| --- | --- |
| `convertLead` | Converts a lead into a new or existing contact, optionally with a new or existing account and an opportunity, and moves or cancels its open activities (**Convert lead**, checked on the lead). With `preview` it returns the matching contacts and accounts instead. A converted lead is not converted again. |

### Messaging workspace tools

**Available when:** `CrestApps.OrchardCore.Omnichannel.Messaging` with `CrestApps.OrchardCore.AI` on

The conversation tools need **Use the messaging workspace** and check the conversation itself as the workspace does: a supervisor may work any conversation, an agent their own and their queues' unclaimed ones. `listMessagingConversations` and `getMessagingConversation` send customer messages to the model provider; the AI session, provider message identifiers and attachments are never returned. Times are in the user's time zone.

| Function | Description |
| --- | --- |
| `listMessagingConversations` | Lists the conversations the caller may see (scope mine, unassigned or all), filtered by channel, unread, open, holder (supervisors only), queue and campaign, with the last message preview and first-response state. Rows name the contact by ID only, without the customer's address. |
| `getMessagingConversation` | Returns one conversation by ID or by the customer's address: its newest messages (20 by default, at most 50), contacts, holder, queue, summary, transfer history and what the caller may do. Reading does not mark it read. |
| `sendMessagingMessage` | Replies in a conversation, or starts one to a single recipient from a chosen address, through the workspace's conversation service, which refuses a contact who opted out. The recipient is checked as the composer checks it, and a recipient whose conversation the caller may not send on is refused. Outside the queue's business hours it is refused unless the caller holds **Send messages outside business hours** (the workspace only warns). Asks for approval. |
| `updateMessagingConversation` | Claims a conversation, sets it open, snoozed or closed, marks it read, or stars or unstars the customer for the caller. |
| `transferMessagingConversation` | Hands a conversation to an agent or a queue with a note, or lists the people and queues it can go to. |
| `setMessagingAvailability` | Sets whether the caller is assigned new messaging conversations. |
| `listMessagingBroadcasts` | Lists broadcasts newest first with status and recipient, sent, failed and skipped counts, never the recipients (**Send group messages**). |
| `sendMessagingBroadcast` | Queues one message to up to 500 existing CRM contacts, given by content item ID, from an address on a channel that supports broadcasts, with the checks of the Broadcasts page (**Send group messages**). Each contact is messaged at their own address on the channel (for text messages, their mobile number first); numbers or addresses typed into the chat are not accepted, and a contact with no address on the channel is refused by ID. The Outreach engine sends it in the background; opted-out numbers are skipped and text messages wait until quiet hours end. Asks for approval. |

Message templates are the `messageTemplate` record kind of the [generic record tools](#generic-record-tools) (**Manage the messaging workspace**). `listMessagingConversations` and `getMessagingConversation` mark the message previews, messages, summary and history notes as data, never instructions.

### Contact Center queue tools

**Available when:** `CrestApps.OrchardCore.ContactCenter.Queues` with `CrestApps.OrchardCore.AI` on

Queue groups are the `contactCenterQueueGroup` record kind, and queues are read as the `contactCenterQueue` kind, with the [generic record tools](#generic-record-tools). The save tool goes through the queue manager, so a queue is validated as its admin page validates it; only the fields given change, and a list given replaces the whole list. IDs that name nothing (a group, calendar or overflow queue) are refused.

| Function | Description |
| --- | --- |
| `saveContactCenterQueue` | Creates or updates a queue, applying the queue editor's rules (skill tags, proficiency, overflow hops, limits and treatment) before validation (**Manage Contact Center queues**). Changes apply to live routing at once. Read the queue first with `getRecord` (kind `contactCenterQueue`), which returns these field names. |
| `deleteContactCenterRecord` | Deletes a queue, inbound entry point, business-hours calendar or voice media clip, with the permission of that record's page. A queue or calendar that queues, entry points or dialer profiles still use is kept, with the list of what uses it, unless `deleteEvenIfUsed` is set. Queue groups, reason codes and dialer profiles are deleted with `deleteRecord`. Asks for approval. |

### Contact Center entry point tools

**Available when:** `CrestApps.OrchardCore.ContactCenter.EntryPoints` with `CrestApps.OrchardCore.AI` on

Entry points are read as the `contactCenterEntryPoint` record kind with the [generic record tools](#generic-record-tools); to answer "where does this number go", find the number with the `omnichannelAddress` kind and the entry point whose `addressIds` hold it.

| Function | Description |
| --- | --- |
| `saveContactCenterEntryPoint` | Creates or updates an entry point that routes to a queue or an agent, with its numbers and, for a phone entry point with Inbound Voice on, its call settings and whole phone menu (**Manage Contact Center queues**). The menu is validated as the editor validates it, and its external transfers must be approved destinations. Routing to an AI agent and the settings other features add are set on the entry point's page. Read the entry point first with `getRecord` (kind `contactCenterEntryPoint`), which returns these field names. |

### Contact Center business hours tools

**Available when:** `CrestApps.OrchardCore.ContactCenter.BusinessHours` with `CrestApps.OrchardCore.AI` on

Calendars are read as the `businessHoursCalendar` record kind with the [generic record tools](#generic-record-tools).

| Function | Description |
| --- | --- |
| `saveBusinessHoursCalendar` | Creates or updates a calendar, a Business Hours schedule, as its editor does (**Manage business hours schedules**). It changes the time zone; the opening hours of the days given (the entries for a day replace that day's hours, and a day given twice has two windows, two at most, as the weekly hours card offers); holidays (dates closed all day) to set, add or remove; and special days to set, add or remove: a date open only for its own hours (`open` and `close`) or closed all day (`closed`), each with an optional `name`, as the **Holidays and special days** card takes them. Queues, entry points and dialer profiles follow the change at once. |
| `checkBusinessHours` | Tells whether a calendar is open now or at a time given in the user's time zone, through the business hours service routing uses, and when it next opens or closes (**Manage business hours schedules**). |

`specialDays` replaces every holiday and special day, as the editor's text box does. `addSpecialDays` and `removeSpecialDays` change single dates. The holiday arguments still change only the closed dates: a holiday that stays keeps its name. Special hours on a date stay unless that date is made a holiday.

A call is refused when:

- a date is given in two lists, such as both a holiday and a special day;
- a special day has neither its hours nor `closed`;
- a day has more than two windows;
- a day is given both closed and with hours.

### Contact Center agent tools

**Available when:** `CrestApps.OrchardCore.ContactCenter.Agents` with `CrestApps.OrchardCore.AI` on; `saveContactCenterAgent` also needs `CrestApps.OrchardCore.ContactCenter.AgentEntitlements` and `CrestApps.OrchardCore.ContactCenter.Queues`

| Function | Description |
| --- | --- |
| `listContactCenterAgents` | Lists agent profiles with presence, signed-in queues and campaigns, allowed queues and campaigns, skills with proficiency, queue memberships and concurrency limit, filtered by user, queue or skill (**Manage Contact Center agents**). Says when the allow-lists are not enforced because Agent Entitlements is off. |
| `saveContactCenterAgent` | Creates a user's agent profile, or changes the queues and campaigns the agent may sign in to, their skills, queue memberships and concurrency limit, as the Agent entitlements page does; an agent is signed out of what they are no longer allowed in (**Manage Contact Center agents**). Asks for approval. |

Agent state reason codes are the `agentStateReasonCode` record kind of the [generic record tools](#generic-record-tools). Agent profiles keep their own tools: the Agent entitlements page binds its own form, which the editor replay cannot post.

### Contact Center callback tools

**Available when:** `CrestApps.OrchardCore.ContactCenter.Dialer` with `CrestApps.OrchardCore.AI` on

Dialer profiles are the `dialerProfile` record kind of the [generic record tools](#generic-record-tools); a save that relaxes a compliance rule is refused and left to the Dialer profiles page. A profile is copied by reading it and saving a new one; set `ConnectWaitMilliseconds` to 0 on the copy, as the Clone action does, because the copy has measured no connected calls.

| Function | Description |
| --- | --- |
| `listCallbackRequests` | Finds callback requests by number (or its last digits), contact, state, agent or due date in the user's time zone, newest first, and tells what became of each: waiting for its time, due but not promoted yet (the Callback Dispatch background task promotes due callbacks every minute), or promoted to a call activity. A promoted callback stays Scheduled and its call activity carries the outcome, so the result gives that activity's state (waiting for an agent, with an agent, completed, canceled, failed or purged), assignee, attempts and disposition, and says when it sits in no queue with no assignee. Someone with **Monitor the Contact Center in real time** or **View Contact Center reports** sees every callback, as the callbacks report and calendars show them; an agent with **Sign in to Contact Center queues and campaigns** sees only the callbacks assigned to them. Numbers appear by their last four digits, and the callback notes are marked as data. |

### Contact Center voice media tools

**Available when:** `CrestApps.OrchardCore.ContactCenter` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `listVoiceMedia` | Lists voice media clips with format, whether a master copy exists and which providers hold a copy, filtered by name or by clips no provider holds yet (**Manage the Contact Center voice media library**). Storage and provider references are never returned. |
| `saveVoiceMedia` | Creates, renames or describes a clip, and gives it audio from a file already in the media library (mp3, wav or ogg, at most 20 MB) through the editor's own upload rules (**Manage the Contact Center voice media library**, and **Manage Media** with the right to manage the file's folder). The audio is copied to the telephony provider. |
| `resyncVoiceMedia` | Re-sends a clip's master copy to every enabled telephony provider that hosts media and reports, per provider, whether it was verified, uploaded or refused (**Manage the Contact Center voice media library**). Asks for approval. |

### Contact Center call recording tools

**Available when:** `CrestApps.OrchardCore.ContactCenter.Recording` with `CrestApps.OrchardCore.AI` on

The tools follow the Call recordings page: someone with **Listen to own call recordings** only reaches the calls they took, whatever the arguments say, and a recording they may not hear is reported as not found.

| Function | Description |
| --- | --- |
| `listCallRecordings` | Searches recordings by date range in the user's time zone, customer number, direction, source and agent, newest first, with duration and agent (**Listen to own call recordings** or **Listen to anyone's call recordings**). Customer numbers appear by their last four digits. |
| `getCallRecording` | Returns one recording with its transcript, contact, interaction and legal-hold state, and records the read on the recording-access audit as opening the page does. No audio. The transcript holds what the customer said, is sent to the AI model provider, and is marked in the result as data, never instructions. |
| `shareCallRecording` | Makes the 24-hour signed playback link the page's Share button makes, and audits it (**Listen to anyone's call recordings**). Asks for approval. |
| `eraseCallRecording` | Erases a recording and its media with a reason, through recording governance, which refuses a recording under legal hold (**Delete call recordings**). Asks for approval. |

### Contact Center shared voicemail tools

**Available when:** `CrestApps.OrchardCore.ContactCenter.InboundVoice` with `CrestApps.OrchardCore.AI` on

Every action goes through the shared voicemail service, which checks the caller's queue entitlements as the Shared voicemail page does.

| Function | Description |
| --- | --- |
| `listSharedVoicemails` | Lists the voicemails of the queues the caller may see, by queue and status, with caller, time, length, who claimed them and what the caller may do with each (**Access shared queue voicemail for entitled queues**). Caller numbers appear by their last four digits. |
| `updateSharedVoicemail` | Claims a voicemail, releases it to the queue or marks it done with a note; taking over another person's claim needs **Manage shared queue voicemail: delete messages and take over other users' claims**. |
| `callBackSharedVoicemail` | Asks the caller's own soft phone to dial the person who left the voicemail; the voicemail stays open until it is marked done. Asks for approval. |
| `deleteSharedVoicemail` | Deletes a voicemail and erases its recording; refused under legal hold (**Manage shared queue voicemail: delete messages and take over other users' claims**). Asks for approval. |

### Contact Center supervision tools

**Available when:** `CrestApps.OrchardCore.ContactCenter.Supervision` with `CrestApps.OrchardCore.AI` on

The tools act only on what the live dashboard lets the caller act on: an agent's call in a queue they oversee, an agent's own phone call when they oversee a queue the agent works, and an agent of a queue they oversee. The supervision services check the permission and the scope again and audit each action. A call is named by `callType` and `callId`, or by `agentId` for the call an agent is on.

AI calls are limited to the campaigns whose queue the caller oversees, so an AI call with no campaign is not listed or reachable. The live dashboard's AI call panel checks the permission only; whether queue and campaign scoping is meant to apply to AI calls is still open, and the tools apply the stricter rule until it is decided.

| Function | Description |
| --- | --- |
| `listLiveAIVoiceCalls` | Lists the AI voice calls in progress with contact, AI profile, campaign, direction and duration (**Monitor the Contact Center in real time**). Numbers appear by their last four digits; `customerNumber` finds a call by number. |
| `getLiveAIVoiceCallTranscript` | Returns the transcript so far of a live AI call, or only the turns after a given index (**Monitor the Contact Center in real time**). An ended call is refused. The transcript is sent to the AI model provider, and the result marks it as data, never instructions. |
| `sendNoteToLiveAIVoiceCall` | Sends the AI on a live call a private instruction the customer does not hear (**Take over, end, transfer and record live Contact Center calls, and set agents' state**). The note must be the user's own words as typed: never written by the model, and never taken from a transcript or a customer message, so a caller cannot steer the live AI through the supervisor's assistant. The note must be, word for word (case, spacing, surrounding quotes and end punctuation aside), one of the user's three latest messages in full, or a part of one they put in double quotes; otherwise nothing is sent. Outside a chat session (an MCP client, an API call) nothing is sent either: the check fails closed, and the approval, which shows the exact text, comes on top of it. The result repeats what was sent. Asks for approval. |
| `freeContactCenterAgent` | Releases an agent from work that has already ended (**Take over, end, transfer and record live Contact Center calls, and set agents' state**). Asks for approval. |
| `sendMessageToContactCenterAgent` | Shows a short message from the caller on an agent's desktop (**Monitor the Contact Center in real time**). |

### Contact Center agent workspace tools

**Available when:** `CrestApps.OrchardCore.ContactCenter.Agents` with `CrestApps.OrchardCore.AI` on

`getMyAgentWorkState` acts only on the signed-in caller. `setContactCenterAgentState` acts on the caller when no `agentId` is given; with an `agentId` it changes another agent's state as the live dashboard does, which needs the Contact Center Supervision feature.

| Function | Description |
| --- | --- |
| `getMyAgentWorkState` | Returns the caller's own presence and reason, signed-in queues and campaigns, current interaction, pending wrap-up and whether their workspace or soft phone is open (**Sign in to Contact Center queues and campaigns**). |
| `setContactCenterAgentState` | Without `agentId`: signs the caller in to queues and campaigns, leaves some of them, signs out, or sets their presence (Available, Break, Away, Do not disturb, Meeting, Training, After-hours unavailable) with a reason (**Sign in to Contact Center queues and campaigns**). Joining while signed in adds the queues and keeps the presence; a change asked for on a call applies when the call ends. Queues the caller's entitlements do not allow are left out and named. With `agentId` (a supervisor): sets an agent of a queue the caller oversees to Available, Away or Break with a reason, or signs the agent out, through the supervision services, which check the scope again and audit it; an agent on a call gets the state when the work ends (**Monitor the Contact Center in real time** and **Take over, end, transfer and record live Contact Center calls, and set agents' state**). It does not ask for approval: a state change is undone by setting the state again. |

### Contact Center interval statistics tools

**Available when:** `CrestApps.OrchardCore.ContactCenter.IntervalStatistics` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `getIntervalStatisticsStatus` | Returns the days of the last 30 computed and final, today's totals so far, the last run and whether a rebuild is running or stalled (**View Contact Center reports**). |
| `rebuildIntervalStatistics` | Queues a range of days to be computed again by the background task, a few days every five minutes; the result says it was queued, not run (**Manage the Contact Center**). |

### Contact Center health tool

**Available when:** `CrestApps.OrchardCore.ContactCenter` and Orchard Core's `OrchardCore.HealthChecks` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `getContactCenterHealth` | Runs the Contact Center dependency health checks, as the dependency probe does, and returns each one's status and description, never exception details (**Monitor the Contact Center in real time**). |

### Telephony soft phone tool

**Available when:** `CrestApps.OrchardCore.Telephony.SoftPhone.Core` with `CrestApps.OrchardCore.AI` on

Telephony extensions are the `telephonyExtension` record kind of the [generic record tools](#generic-record-tools) (`CrestApps.OrchardCore.Telephony`, **Manage telephony extensions**).

| Function | Description |
| --- | --- |
| `dialFromSoftPhone` | Asks the caller's own soft phone to dial a number, as the call button beside a phone field does. Nothing happens when no soft phone of theirs is open, and the phone applies outbound screening (**Use the telephony soft phone**). Asks for approval. |

### Telnyx tools

**Available when:** `CrestApps.OrchardCore.Telnyx` with `CrestApps.OrchardCore.AI` on

Every tool needs **Manage telephony settings**. No tool returns or accepts the API key, the webhook public key or any other credential; a person enters them on the Telnyx settings page, where the account is also connected and disconnected.

| Function | Description |
| --- | --- |
| `getTelnyxConnectionStatus` | Says whether Telnyx is enabled and connected and returns the default caller id; keys are reported only as present or not. |
| `listTelnyxNumbers` | Lists the numbers the account owns, with status and carrier spam rejections over the last 30 days. Telnyx does not return the caller name in this list. |
| `searchTelnyxAvailableNumbers` | Searches numbers for sale by country, area code, city, region, digits and whether texts are needed; up to 30, with their cost. |
| `setTelnyxNumberCallerName` | Sets the caller name of a number the account owns. Asks for approval. |

### Local do-not-call list tools

**Available when:** `CrestApps.OrchardCore.DncRegistry.Local` with `CrestApps.OrchardCore.AI` on

Every tool needs **Manage DNC registry settings**. The lists decide who may be contacted, so importing, resuming or pausing an import and deleting a list ask for approval. A list screens its numbers only once its import has completed.

| Function | Description |
| --- | --- |
| `listLocalDncLists` | Lists the local lists by country with status, counts and any import error; rejected rows are counted, not returned. |
| `importLocalDncList` | Creates a list for a country from up to 1,000 numbers given inline or from a CSV file in the media library (which also needs **Manage Media** and the right to manage its folder), and queues its import. Asks for approval. |
| `setLocalDncListImportState` | Starts or resumes the import of a pending, paused or failed list now, or pauses a running one. Asks for approval. |
| `deleteLocalDncList` | Deletes a list and its numbers in the background; its numbers stop being screened at once. Asks for approval. |

### Calendar management tools

**Available when:** `CrestApps.OrchardCore.Calendars` with `CrestApps.OrchardCore.AI` on

Like the calendar tools above, these act for the signed-in person with **Use calendars** (`AccessCalendars`) and that person's access to each calendar, as the Manage Calendars, event editor and Categories pages do. Calendar types come from the site's features, and Calendars has no business hours or holidays of its own (a holiday is an event), so there are no tools for those. Sharing, deleting, changing an event (which emails its attendees when Calendar Invitations is on) and answering an invitation ask for approval.

| Function | Description |
| --- | --- |
| `getCalendar` | Gets a calendar the user can read: type, owner, time zone, visibility, overlap limit, room or office details and event types. Its sharing, whether it has a feed address and its subscription state and last error only for someone who manages it; the feed address and the subscription address are never returned. |
| `saveCalendar` | Creates a calendar of a type or changes one the user manages: name, description, color, time zone, visibility, enabled, owner (a role for a Team calendar; a Personal calendar's user only for a calendar administrator), overlap limit, and room, equipment or office details. |
| `shareCalendar` | Replaces who a calendar is shared with (existing users and roles, Anonymous excluded) at free/busy, read, write or manage level. Asks for approval. |
| `deleteCalendar` | Deletes a calendar the user manages and all its events; refused for a calendar a module manages. Asks for approval. |
| `getCalendarEvent` | Gets an event in full, with each attendee's answer, when the user may see its details. |
| `updateCalendarEvent` | Changes an event or occurrences of a repeating event (this one, this and following, all): title, type, times, time zone, location, description, status, busy/free, visibility, color, category, reminder, repeat rule and attendees. Confirming a tentative event needs the right to manage its calendar. Asks for approval. |
| `deleteCalendarEvent` | Permanently deletes an event, the whole series for a repeating event. Asks for approval. |
| `respondToCalendarEvent` | Records the user's own answer to an invitation. Asks for approval. |
| `listCalendarCategories` | Lists the user's own and the shared event categories. |
| `saveCalendarCategory` | Creates or changes a category; only a calendar administrator may share one with everyone or change a shared one. |
| `deleteCalendarCategory` | Deletes a category the user may change. Asks for approval. |

### Team availability tool

**Available when:** `CrestApps.OrchardCore.Calendars.Teams` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `getTeamAvailability` | For a Team calendar the user can read and a day, lists each member's busy times and the first times everyone is free for a meeting, from 07:00 to 19:00 in the calendar's time zone, as the Team Availability page does. |

### Calendar feed tools

**Available when:** `CrestApps.OrchardCore.Calendars.Feeds` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `resetCalendarFeedAddress` | Makes a new private subscription address (the old one stops working) or removes it, for a calendar the user manages. Asks for approval. |

### Scheduling administration tools

**Available when:** `CrestApps.OrchardCore.Scheduling` with `CrestApps.OrchardCore.AI` on

Unlike the customer-facing scheduling tools above, these are for staff: each works only for a signed-in person with the permission of the matching admin page (**View appointments**, **Manage appointments** or **Manage scheduling**), and never for an anonymous visitor, whatever the Anonymous role holds. They call the services and editor rules of the Appointments, Appointment Types and Waitlist pages. Appointment times are shown in the appointment's time zone, or the site's when it has none. No tool returns an appointment's manage code or link. Booking, moving and cancelling stay with `bookAppointment` and `changeAppointment`; confirming and marking an appointment completed or no-show is `setAppointmentStatus`, which asks for approval.

The scheduling resources (staff, rooms and equipment that take appointments) are the `schedulingResource` kind of the [generic record tools](#generic-record-tools), with **Manage scheduling**. `saveRecord` replays the resource editor, so its checks apply (a name, a known time zone, readable hours), then saves as the Scheduling Resources page does: a new resource starts with weekday hours from 09:00 to 17:00, and a resource without a calendar, or whose calendar no longer exists, gets its own Appointments calendar. `WorkingHours` and `DateOverrides` are the editor's text: one line per day (`Monday 09:00-12:00, 13:00-17:00`; a day left out is closed) and one line per date (`2026-12-24 closed`, `2026-12-31 08:00-12:00`), wall-clock times in the resource's own time zone; `getRecord` returns them in the same form. `deleteRecord` keeps the resource's calendar and appointments, as the page does. Appointment types keep `getAppointmentType`, `saveAppointmentType` and `deleteAppointmentType`: their editor's resource groups are a list of objects the record tools cannot post.

| Function | Description |
| --- | --- |
| `listAppointments` | Lists appointments in start time order, filtered by status, a search on the customer's name, email or phone, and a first day, with paging. Needs **View appointments**. |
| `getAppointment` | Gets one appointment: type, time, status, resources, customer, notes, cancel reason and the calendar event it sits on. Needs **View appointments**. |
| `setAppointmentStatus` | Confirms a held or requested appointment, or marks a booked one completed or no-show. Confirming updates the customer's calendar invitation and every change can start workflows that message the customer, so it asks for approval. Needs **Manage appointments**. |
| `getAppointmentType` | Gets an appointment type in full: timing, limits, notice rules, capacity, resource groups with their assignment rules, self-service and its public booking address. Needs **Manage scheduling**. |
| `saveAppointmentType` | Creates or updates an appointment type with the editor's rules; only the fields given change. Resources must exist, and a resource can be in only one group. Needs **Manage scheduling**. |
| `deleteAppointmentType` | Deletes an appointment type; appointments already booked keep pointing at it. Asks for approval. Needs **Manage scheduling**. |
| `listAppointmentWaitlist` | Lists the waitlist, newest first, with the appointment type, the days (in the site's time zone) and the status. Needs **Manage appointments**. |
| `addToAppointmentWaitlist` | Adds a customer to the waitlist of an appointment type for whole days in the site's time zone. Needs **Manage appointments**. |
| `removeFromAppointmentWaitlist` | Takes a customer off the waitlist; the entry is kept with status Removed. Needs **Manage appointments**. |

### Work order tools

**Available when:** `CrestApps.OrchardCore.WorkOrders` with `CrestApps.OrchardCore.AI` on

These work without Dispatch, beside the three work order tools Dispatch registers (`workOrderCreate`, `workOrderStatus`, `workOrderSummarize`), which are unchanged. Each works only for a signed-in person with the permission of the matching admin page (**View work orders**, **Manage work orders** or **Manage work order types, checklists and service levels**), and calls the services and editor rules of the Work Orders and Work Order Settings pages. A work order is named by its number (WO-000123) or ID. Times are shown in the user's or the site's time zone. When a line, checklist, question, sub-status or stock location given does not match exactly one, the tool returns the ones to pick from.

| Function | Description |
| --- | --- |
| `listWorkOrders` | Lists work orders, newest first, filtered by status (or every open one), type, priority, asset and a search on number or summary, with paging. Needs **View work orders**. |
| `updateWorkOrder` | Changes a work order's summary, description, priority, customer, contact, service address, access notes, assets, time window, estimated length and lines with the editor's rules, and adds a note to its history; only the fields given change, and a note can be given alone. It does not change the status. Needs **Manage work orders**. |
| `setWorkOrderStatus` | Moves an open work order to Ready, Scheduled, In progress or On hold, with an optional sub-status (by name or ID) and note, along the status rules. Needs **Manage work orders**. |
| `closeWorkOrder` | Cancels an open work order (with a reason) or closes a completed one; both are final. With Dispatch on, cancelling cancels its open visits, which notifies the customer when customer notices are on. Asks for approval. Needs **Manage work orders**. |
| `recordWorkOrderPartUsed` | Records what was used on a line, taken off a stock location when Inventory is on. Needs **Manage work orders**. |
| `answerWorkOrderChecklist` | Saves answers to one of a work order's checklists; answers that do not fit their question are reported, the others saved. Needs **Manage work orders**. |
| `completeWorkOrder` | Completes a work order in progress once its required answers, signature and photos are in, and bills it when the type is billable and a billing feature is on; otherwise returns what is missing. Asks for approval. Needs **Manage work orders**. |
| `listWorkOrderSettings` | Lists the work order types, checklists, service levels or sub-statuses, with a name search. Needs **Manage work order types, checklists and service levels**, and for service levels also **Manage service level policies**. |
| `getWorkOrderSetting` | Gets one type, checklist, service level or sub-status in full with the JSON schema of its kind's fields; without an ID, the fields a new one starts with. Needs **Manage work order types, checklists and service levels**, and for service levels also **Manage service level policies**. |
| `saveWorkOrderSetting` | Creates or updates a type, checklist, service level or sub-status with its editor's rules; only the fields given change, and the records a type names must exist. A service level is a shared Service Levels policy of the Work orders scope: its name, Business Hours schedule, warning point, pause statuses and response and resolution targets by priority. Needs **Manage work order types, checklists and service levels**, and for service levels also **Manage service level policies**. |
| `deleteWorkOrderSetting` | Deletes a type no work order uses, a checklist no type uses, a service level nothing uses and no timer counts on (as the Service Levels page does), or a sub-status. Asks for approval. Needs **Manage work order types, checklists and service levels**, and for service levels also **Manage service level policies**. |

### Agreement tools

**Available when:** `CrestApps.OrchardCore.Agreements` with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `getAgreementBalance` | Gets how the hours of an agreement with an account stand: the hours covered, used, left and over in the current month (an allowance) or the whole block, the percent used, and, on request, the time entries counted (newest first, at most 50). Given `accountId`, it lists the account's agreements as its **Agreements** tab does (newest first, at most 25), with the balance of the active ones. |

How it works:

- **Finding the agreement:** by ID or number (SA-00042, or just 42). `date` (yyyy-MM-dd, in the agreement's time zone) asks for the period of another day, such as last month.
- **Dates outside the agreement:** a day before the agreement starts is refused. A day after it ends counts as its last day, as the agreement reports count it.
- **Permission:** it needs **View agreements with accounts and their hours**, the permission of the account tab and the agreement reports, and refuses a caller who is not signed in.
- **Times and dates:** entry times are in the user's or the site's time zone. Period dates are days in the agreement's time zone.
- **Untrusted text:** entry descriptions come from tickets and work orders and are returned as data.
- **Account name:** the account's name is given only for an account the user may view.
- **Approval:** it doesn't ask; it only reads.

Agreements are the read-only `agreement` kind of the [generic record tools](#generic-record-tools), found by number, name, account, customer or status.

### Service agreement tools

**Available when:** `CrestApps.OrchardCore.WorkOrders.Agreements` with `CrestApps.OrchardCore.AI` on

A service agreement is a shared agreement (the Agreements feature) with field-service terms: customer, contact, place, assets, schedules and pricing. Agreements are listed with the generic record tools (`listRecords` with the kind `agreement`), which find them by number, name, account, customer or status. Each tool works only for a signed-in person with **View agreements with accounts and their hours** (`getServiceAgreement`) or **Manage agreements with accounts** (the others), and applies the rules of the Agreements editor and its work orders part. An agreement is named by its number (SA-00012, or just 12) or ID. Its start, end and visit days are days in the agreement's time zone. The agreement's name and notes, its access notes, the contact's details, the address and the work orders' summaries are returned as data, never as instructions.

| Function | Description |
| --- | --- |
| `getServiceAgreement` | Gets one agreement's field-service terms: customer, contact, place, assets, period, the hours it covers, pricing, each schedule with its repeat rule and next three visit days, and the latest 50 work orders it made. |
| `saveServiceAgreement` | Creates or updates an agreement with the editor's rules; only the fields given change. Active starts making work orders ahead of each visit; Paused and Canceled stop them. A new agreement starts in the user's time zone. It also sets the hours the agreement covers (`hoursKind`, `hours`, `roundingMinutes`), with the agreement editor's rules. Asks for approval, as running does: an active agreement makes the same work orders, visits and customer notices through the hourly task. |
| `runServiceAgreement` | Makes the work orders of an active agreement that are due now, once each, as the hourly task does; it can also expire the agreement or raise its expiring event. With Dispatch creating visits for new work orders, each gets a visit. Asks for approval. |
| `deleteServiceAgreement` | Deletes a draft agreement; any other is canceled instead. The work orders it made are kept. Asks for approval. |

### Dispatch administration tools

**Available when:** `CrestApps.OrchardCore.Dispatch` with `CrestApps.OrchardCore.AI` on

These are for dispatchers: each works only for a signed-in person with the permission of the matching admin page (the Visits page, a visit's page, the Dispatch Board or the Field Service KPIs), never for an anonymous visitor. A visit is named by its number (V-000012), its ID, or its work order's number (the work order's open visit). Times are in the dispatch time zone (the Dispatch settings' zone, or the site's), as on the Visits page and the board, and every result names it. The customer tracking link (a bearer link) and technicians' live positions are never returned. The existing field service tools (`dispatchFindTechnician`, `dispatchOptimizeAndExplain`, `dispatchRescheduleVisit`, `dispatchWhereIsMyTechnician` and the three work order tools) are unchanged; to give a visit a technician and a time, use `dispatchRescheduleVisit`. See [Dispatch](../modules/dispatch.md).

| Function | Description |
| --- | --- |
| `listVisits` | Lists visits filtered by status (open by default, all, or one status), a day, a technician (name or ID) and a search on the visit number, work order number or summary, 20 a page (at most 50), most urgent first; also says how many open work orders have no visit. Needs **View visits and the dispatch board**. |
| `getVisit` | Gets one visit: its work order, type, priority, status and the statuses it can move to next, technicians and lead, start and end, arrival window, ETA, due time, travel, equipment, pinned and at-risk state, the incomplete reason and return visit, the work order's other visits, the customer notices sent and the status history. Needs **View visits and the dispatch board**. |
| `createVisits` | Makes one more visit for a work order, or a visit for every open work order that has none, as the work order page's "Add a visit" and the Visits page's "Make missing visits" do. New visits start unscheduled. Needs **Dispatch visits**. |
| `updateVisit` | Pins a visit so the optimizer leaves its technician and time alone, unpins it, or takes a scheduled visit that hasn't started off its technicians and time, back to the unscheduled visits (a pinned visit is unpinned first when `pinned` is false). No customer notice is sent. Needs **Dispatch visits**. |
| `setVisitStatus` | Moves a visit to Dispatched, En route, On site or Paused along the visit's allowed statuses, with an optional note kept in its history. En route texts or emails the customer when customer notices are on, so it asks for approval. Needs **Dispatch visits**. |
| `closeVisit` | Ends a visit as Completed, Incomplete or Canceled, with a note (required for Incomplete and Canceled). Incomplete makes a return visit while the work order is open. These are final, and Completed and Canceled notify the customer when notices are on, so it asks for approval. Needs **Dispatch visits**. |
| `getDispatchBoard` | Gives a day of the dispatch board (today by default): each technician of the board's capability (or a chosen one), optionally of one team and its sub-teams, with their team, shift, open time, time off and visits in order; visits given to people the board doesn't show; and up to 50 of the visits waiting for a time, with their count. Needs **View visits and the dispatch board**. |
| `getFieldServiceKpis` | Gives the field service KPIs for a range of days (the last 30 by default, at most a year, as on the KPIs page): finished visits, first-time fix, on-time arrival, travel and on-site minutes, jobs per technician day, return-visit and repeat-work rates, work orders, response and resolution compliance, mean hours to repair, revenue per work order and parts used by location. Rates are fractions from 0 to 1. Needs **View visits and the dispatch board**. |

### Time Tracker tools

**Available when:** `CrestApps.OrchardCore.TimeTracker` with `CrestApps.OrchardCore.AI` on

`getMyTimeStatus` and `clockMyTime` are self-service: they work only on the signed-in person's own time (the person comes from their sign-in, never from an argument), with **Track one's own time**, as the My Time page. When the Workforce time clock keeps the person's time, `clockMyTime` punches there (In, Out, MealStart, MealEnd, BreakStart, BreakEnd) with that clock's checks, as the My Work punch buttons do: the person needs **Use the My Work portal** and **Clock in and out** and must be a worker, and the time clock's policy applies (punch order, location); such a worker needs no Time Tracker permission for `getMyTimeStatus` and `clockMyTime`, as in My Work. A clock that takes no punches from the assistant is named with its link. The other tools are for managers and need the Time Entries page's permission. Times are shown and read in the Time Tracker's time zone (the site's), as the Time Tracker pages count days; each result names it. See [Time Tracker](../modules/time-tracker.md).

| Function | Description |
| --- | --- |
| `getMyTimeStatus` | Says whether the user is clocked in, in which stage and since when, with today's entries, the week's hours by category and the stages they can switch to; when another clock keeps their time, names it with the punches they can make next. Needs **Track one's own time**. |
| `clockMyTime` | Clocks the user in or switches them to another stage they may choose themselves (such as Work, Break or Lunch), or clocks them out, now. When the Workforce time clock keeps their time, makes the punch there instead. Needs **Track one's own time**. |
| `listTimeEntries` | Lists anyone's time entries, newest first, filtered by person (ID or text in the name), stage and a range of days, with the stage, start, end, note, the clock that keeps an entry and the latest changes (who, when, why). Needs **View anyone's time and the time report**. |
| `getTimeTotals` | Totals tracked time per person, optionally per day, by category (with each category's factor) and by stage, for up to 366 days; time still running counts up to now. Needs **View anyone's time and the time report**. |
| `saveTimeEntry` | Adds a time entry for a user, or changes an entry's stage, start, end or note. A reason is required and is kept in the entry's history. Time kept by another clock is corrected there. Needs **Edit anyone's time**. |
| `deleteTimeEntry` | Deletes a time entry, with a reason. It cannot be undone, so it asks for approval. Needs **Edit anyone's time**. |

### Asset tools

**Available when:** `CrestApps.OrchardCore.Assets` with `CrestApps.OrchardCore.AI` on

For day-to-day field service ("is the Hendersons' furnace still under warranty?"). Each tool works only for a signed-in person with the Assets pages' permission. Assets are created and changed on the Assets pages. See [Assets](../modules/assets.md).

| Function | Description |
| --- | --- |
| `listAssets` | Lists assets filtered by type, status, owner (kind and ID) and a search on name, number or serial number, by name, with paging; each with its owner, the asset it sits in and whether its warranty covers today. Needs **View assets**. |
| `getAsset` | Gets one asset by ID or number: type and attributes, owner, location, status, warranty and whether it covers today (with the days left), the assets it sits in and those inside it, the latest reading of each meter and, with Fleet, its vehicle details and calendar. Needs **View assets**. |
| `recordAssetMeterReading` | Records a reading of one of an asset's meters (such as hours or kilometres), now or at a past time. A cumulative meter cannot go below its last reading. Needs **Manage assets**. |

### Workforce time-off administration tools

**Available when:** `CrestApps.OrchardCore.Workforce.TimeOff` with `CrestApps.OrchardCore.AI` on

These are for managers: each works only for a signed-in person with the permission of the matching admin page, never for an anonymous visitor, and keeps to the teams and workers that person manages, as the Time-Off Requests page and a worker's time-off page do. Workers can be named by ID or by name. The existing `workforceTimeOffBalance` and `workforceRequestTimeOff` stay the tools for a person's own time off; asking for time off on a worker's behalf is done on the Time-Off Requests page. See [Workforce](../modules/workforce.md#time-off).

| Function | Description |
| --- | --- |
| `listTimeOffRequests` | Lists time-off requests of the teams the user manages, filtered by team (with its sub-teams), worker, status and dates, with the days asked for and, for a waiting request, the balance left. Waiting (pending and waitlisted) by default. Needs **Approve time off**. |
| `decideTimeOffRequest` | Approves, declines or cancels up to 20 requests, with a note to the worker. Approving books the days, puts them on the team calendar and handles the worker's shifts by the team's rule; the worker is notified. Asks for approval. Needs **Approve time off**. |
| `getWorkerTimeOff` | Gets a worker's balances per policy (earned, booked, pending, expiring, available), the policies that apply, the ones assigned to the worker alone, and the 20 latest ledger entries. Needs **Approve time off** or **Adjust time-off balances**. |
| `adjustTimeOffBalance` | Writes a correction, opening balance, carry-over, grant with a last day, or payout to a worker's ledger. A ledger entry cannot be edited, only offset, so it asks for approval; a retried call with the same arguments writes nothing more. Needs **Adjust time-off balances**. |
| `assignTimeOffPolicy` | Assigns a policy to a worker alone from and to a day, or takes the worker's own assignments of it away. Policies a worker gets from a team are changed on the policy. Needs **Manage time-off policies**. |

### Workforce time clock administration tools

**Available when:** `CrestApps.OrchardCore.Workforce.TimeClock` with `CrestApps.OrchardCore.AI` on

These are for managers: each works only for a signed-in person with the permission of the matching admin page, never for an anonymous visitor, and keeps to the teams and workers that person manages, as the Timesheets, Attendance and worker time clock pages do. Punch days and times are in the worker's time zone, as on the timesheet page. No tool reads, returns or sets a kiosk PIN or a kiosk address, and none locks a pay period, exports payroll or changes pay rates: those stay on the admin pages. See [Workforce](../modules/workforce.md#time-clock).

| Function | Description |
| --- | --- |
| `listTimesheets` | Lists timesheets filtered by team (with its sub-teams), worker, status, pay period calendar and period start, with total and worked hours and open exceptions. Needs **Approve timesheets**. |
| `getTimesheet` | Gets one timesheet, by ID or by a worker and a day in the period: each day's punches, lines and exceptions, the totals by pay code, the pending punch corrections and the pay codes a line can use. Needs **Approve timesheets**. |
| `prepareTimesheets` | Creates the timesheets of the pay period a day falls in for the workers of the teams the user manages (or one team and its sub-teams) who have none yet, up to 500, as the page's Prepare button does. Needs **Approve timesheets**. |
| `decideTimesheet` | Approves up to 20 timesheets, sends them back to the worker with a reason, or reopens approved ones (a locked timesheet can't be reopened). The worker is notified, so it asks for approval. Needs **Approve timesheets**. |
| `saveTimesheetPunch` | Adds a missing punch to a worker's timesheet or corrects one, with a reason that is kept. Needs **Approve timesheets** and **Edit punches**. |
| `removeTimesheetPunch` | Removes a punch, with a reason that is kept; the original stays in the punch history. Needs **Approve timesheets** and **Edit punches**. |
| `addTimesheetLine` | Adds a manual line (pay code, hours on a day, note) to an open, sent back or reopened timesheet. Needs **Approve timesheets**. |
| `removeTimesheetLine` | Removes a manual line from an open, sent back or reopened timesheet. Needs **Approve timesheets**. |
| `decidePunchCorrection` | Approves or declines a worker's waiting request to add or correct a punch; approving adds the punch. Needs **Approve timesheets**. |
| `listAttendanceExceptions` | Lists attendance exceptions filtered by team, worker, kind, status (open by default) and dates, with each worker's current attendance points. Needs **Approve timesheets**. |
| `decideAttendanceException` | Confirms up to 20 exceptions, which adds their points, or excuses them with a note. Needs **Approve timesheets**. |
| `getWorkerTimeClock` | Gets a worker's time clock policy, whether they have a kiosk PIN and whether it is locked after wrong tries (never the PIN), whether they are clocked in, and their attendance points. Needs **Manage workers**. |

Changing punches and lines changes what the worker is paid, but each can be undone with the other tool of its pair while the timesheet is open, so they don't ask for approval; `decideTimesheet` does.

### Workforce shift schedule tools

**Available when:** `CrestApps.OrchardCore.Workforce.ShiftPlanning` with `CrestApps.OrchardCore.AI` on

These are for managers: each works only for a signed-in person with the permission of the Shift Schedules pages, never for an anonymous visitor, and keeps to the schedules of the teams that person manages, as the pages do. A shift's day and times are the team's wall-clock times, as on the planner board. Breaks are written as on the shift editor, one per line, such as `13:00-13:30 Meal`. The existing `workforceFindCover` and `workforceExplainViolations` are unchanged. See [Workforce](../modules/workforce.md#shift-planning).

| Function | Description |
| --- | --- |
| `listShiftSchedules` | Lists the schedule periods of the teams the user manages (or one team and its sub-teams), newest first, with team, dates, name, status and when they were published. Needs **Plan team schedules**. |
| `getShiftSchedule` | Gets a schedule: its draft shifts (worker or open, day, start, end, template, breaks, notes; up to 300) for the whole period or a range of days, the coverage per day against the team's requirements (half-hours short of the minimum or under target, and the worst of each), the labor rule problems, and with `includeChanges` the latest 50 changes made after publishing. Needs **Plan team schedules**. |
| `createShiftSchedule` | Starts a draft schedule for a team from a day for 1 to 12 weeks (2 by default), with an optional name. Needs **Plan team schedules**. |
| `saveDraftShift` | Adds a draft shift, or changes one (only the values given change): the worker or nobody, day, start and end, template (optionally with its times and breaks), breaks and notes. Returns that worker's labor rule problems for the day. Needs **Plan team schedules**. |
| `deleteDraftShift` | Removes a shift from a draft schedule. It does not ask for approval: the shift can be added again. Needs **Plan team schedules**. |
| `fillShiftSchedule` | Fills a draft schedule from the team's rotations, by copying another schedule of the team, or with the auto-scheduler against the coverage requirements (20 seconds by default, at most 30). Returns the shifts added, the scheduler's scores and the coverage per day. Needs **Plan team schedules**. |
| `publishShiftSchedule` | Publishes a schedule: shifts go on the team calendar, workers are notified, and shifts nobody has are offered as open shifts. Nothing is published while a rule blocks or a rule needing an override has no `overrideReason`; the problems are returned. Asks for approval. Needs **Publish team schedules**, and **Override labor rules** with an override reason. |
| `changePublishedShift` | Changes a shift on the published schedule now: new times, another worker, or removal, with a reason and whether the worker asked for it. The worker is notified and a late change by the employer can record predictability pay, so it asks for approval. Needs **Plan team schedules**. |
| `deleteShiftSchedule` | Deletes a schedule that was never published, with its draft shifts. Asks for approval. Needs **Plan team schedules**. |

### Workforce labor rule tools

**Available when:** `CrestApps.OrchardCore.Workforce.LaborRules` with `CrestApps.OrchardCore.AI` on

For managers, in the teams they manage, as the Rule Overrides and Predictability Pay pages. Rule sets themselves are read through the generic record tools (kind `laborRuleSet`) and changed on the Labor Rules page.

| Function | Description |
| --- | --- |
| `listLaborRuleOverrides` | Lists the labor rule overrides recorded, newest first: the rule and its rule set, the worker and team, the problem, who overrode it, when and why. Needs **Manage labor rules**. |
| `listPredictabilityPay` | Lists predictability pay entries filtered by team, worker and status (to decide by default), with the change that caused each, the notice given, the hours owed and the shift's start in the team's time. Needs **Approve predictability pay**. |
| `decidePredictabilityPay` | Approves up to 20 entries, so they are paid with the worker's timesheet, or waives them with a reason. A decided entry can't be decided again, so it asks for approval. Needs **Approve predictability pay**. |

### Workforce open shift and shift trade tools

**Available when:** `CrestApps.OrchardCore.Workforce.ShiftPlanning` with `CrestApps.OrchardCore.AI` on

These are for managers: each works only for a signed-in person with the permission of the matching admin page, never for an anonymous visitor, and keeps to the teams that person manages, as the Open Shifts and Shift Trades pages do. Workers can be named by ID or by name. An open shift's day and times are the team's own wall-clock times; a trade's shifts are shown in the team's time zone. See [Workforce](../modules/workforce.md#shift-planning).

| Function | Description |
| --- | --- |
| `listOpenShifts` | Lists open shifts filtered by team (with its sub-teams), status (open and claimed by default) and first day, soonest first, each with its claims and, on request (10 per page at most), the workers eligible to take it. Needs **Manage open shifts**. |
| `postOpenShift` | Posts a shift nobody has for a team, on a day and times or from a shift template (its times, breaks and required skills); breaks can be given as `HH:mm-HH:mm Activity`. The team's settings decide how it is claimed and the claim deadline. Eligible workers are notified, so it asks for approval. Needs **Manage open shifts**. |
| `decideOpenShiftClaim` | Gives an open shift to a worker (one who claimed it or any eligible worker), or declines a worker's claim with a note. The worker is notified, so it asks for approval. Needs **Manage open shifts**. |
| `cancelOpenShift` | Cancels an open or claimed shift so it can no longer be claimed. Needs **Manage open shifts**. |
| `listShiftTrades` | Lists shift trades and giveaways filtered by team, worker (offered by or to) and status, with both shifts and, for a trade waiting for approval, the rule problems of the worker taking the shift. Needs **Approve shift trades**. |
| `createShiftTrade` | Offers a worker's published shift to a colleague, to anyone on the team, or in a swap for one of the colleague's shifts, on the worker's behalf. Without a shift it returns the worker's shifts of the next 60 days to choose from. The colleague is asked to accept, so it asks for approval. Needs **Approve shift trades**. |
| `decideShiftTrade` | Approves, declines (with a note) or cancels a trade, or accepts or rejects it for the other worker. Approving swaps the shifts on the published schedule and notifies both workers, so it asks for approval. Needs **Approve shift trades**. |

### Workforce: my open shifts

**Available when:** `CrestApps.OrchardCore.Workforce.ShiftPlanning` and `CrestApps.OrchardCore.Workforce.Portal` with `CrestApps.OrchardCore.AI` on

Self-service, as on the **My Work > Shifts** page: the tools act only for the worker linked to the signed-in person's account (no argument names another worker), need **Use the My Work portal**, and refuse a person whose account isn't linked to a worker.

| Function | Description |
| --- | --- |
| `listMyOpenShifts` | Lists the open shifts of the user's own teams from today, soonest first, with whether the user has claimed each and, when not, why they couldn't take it. |
| `claimOpenShift` | Claims an open shift for the user, or withdraws the user's claim. With first come, first served the shift is the user's at once and goes on their schedule; otherwise a manager decides. |

### Workforce adherence tools

**Available when:** `CrestApps.OrchardCore.Workforce.Adherence` with `CrestApps.OrchardCore.AI` on

These are for supervisors: each works only for a signed-in person with the permission of the matching adherence page, never for an anonymous visitor, and keeps to the workers of the teams that person manages, as the worker's adherence page and the Adherence Exceptions page do. Times are in the worker's own time zone, as those pages show them. The existing `workforceOutOfAdherence` stays the tool for "who is out of adherence right now". See [Workforce for Contact Center](../modules/workforce-contact-center.md).

| Function | Description |
| --- | --- |
| `getAdherenceDay` | Gets an agent's adherence for a day (today by default): adherence and conformance percent, minutes scheduled, in adherence, excepted and out, each stretch out of adherence (up to 100) with the scheduled activity and the actual state and reason, and the exceptions recorded. Uses the saved day, or computes it now when it isn't saved yet, as the page does. Needs **View adherence**. |
| `listAdherenceExceptions` | Lists the adherence exceptions of the teams the user manages (or one team and its sub-teams), newest first, or one worker's exceptions over up to 92 days, with the time, reason, note and who recorded each. Needs **View adherence**. |
| `addAdherenceException` | Records an exception for an agent (day, start and end, reason, note) so that time doesn't count against them. With `recomputeDay` it also computes and saves the agent's day again, as the page's Save button does; otherwise it counts once the day is saved. Needs **Manage adherence rules and exceptions**. |
| `deleteAdherenceException` | Deletes an exception, so the time counts against the agent again once the day is saved. It can be recorded again, so it doesn't ask for approval. Needs **Manage adherence rules and exceptions**. |

### Workforce intraday tools

**Available when:** `CrestApps.OrchardCore.Workforce.Intraday` with `CrestApps.OrchardCore.AI` on

Each works only for a signed-in person with **Manage forecasts**, the Intraday page's permission. Times are in the planning group's team time zone, as the Intraday page shows them. A planning group is named by its name or ID, and can be left out when there is only one.

| Function | Description |
| --- | --- |
| `getIntradayStatus` | For a planning group and day (today by default), compares forecast with actual contacts and handle time, and agents needed and to schedule with scheduled and available, per 15 minutes (or only the intervals still to come that are short); gives the reforecast factor for the rest of the day and lists the day's overtime and time-off offers with the places taken. |
| `reforecastIntraday` | Rescales the rest of the day's active forecast from how the day has gone so far and staffs it again (the team's coverage follows), as the page's Reforecast button does, and returns the factor used. Nothing changes when the day is close to the forecast. |
| `postIntradayOffer` | Offers voluntary overtime or voluntary time off to the planning group's team for a time range and a number of places. Workers take places in My Work, which changes their shifts and their pay, so it asks for approval. |
| `closeIntradayOffer` | Closes an offer so no more workers can take it. Places already taken stay. |

### Workforce forecasting tools

**Available when:** `CrestApps.OrchardCore.Workforce.Forecasting` with `CrestApps.OrchardCore.AI` on

Each works only for a signed-in person with **Manage forecasts**. These are reads; building, adjusting, activating and deleting forecasts stay on the Forecasts page. The existing `workforceForecastSummary` stays the tool for a day's summary of the active forecast.

| Function | Description |
| --- | --- |
| `listForecasts` | Lists the forecasts of a planning group or of all, newest first, with their dates, model, status (draft, active or superseded), number of adjustments and accuracy against history (MAPE and WAPE per model). |
| `getForecast` | Gets a forecast one day at a time (its first day by default): its adjustments and accuracy, and for the day the contacts, average handle time, agent hours, busiest interval and each 15-minute interval with contacts (only the busy ones by default). Times are in the forecast's time zone. |
| `getCapacityPlanProjection` | Gives a capacity plan's week-by-week projection: the workload, the full-time agents needed, available and in training, the gap, and the first and worst weeks the plan runs short. |

### Workforce for Contact Center tools

**Available when:** `CrestApps.OrchardCore.Workforce.ContactCenter` (and, for the stage tools, `CrestApps.OrchardCore.Workforce.ContactCenter.TimeTracker`) with `CrestApps.OrchardCore.AI` on

| Function | Description |
| --- | --- |
| `createWorkersForAgents` | Creates a worker for every contact center agent with a user who has none, optionally in a team (by name or ID), as the Contact Center Agents page's Create workers button does, and returns how many were created. Needs **Manage workers**. |
| `getContactCenterTimeStages` | Gets which Time Tracker stage each agent state (except Offline) and each enabled not-ready reason is recorded in, and the stages that can be chosen. Needs **Manage time stages and categories**. |
| `saveContactCenterTimeStages` | Sets the stage of the agent states and reasons given (stage by ID or name; empty stops tracking a state, or makes a reason use its state's stage). Unlike the page, which drops an unknown stage silently, an unknown state, reason or stage saves nothing and is reported. Needs **Manage time stages and categories**. |

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
