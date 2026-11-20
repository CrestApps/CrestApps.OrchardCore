# Orchard Core: content management

Part of the [AI tool coverage plan](README.md). Nothing in this file is built yet. The design rules, the permission and approval model and the test plan that apply to every tool below are in [01-foundation.md](01-foundation.md) and [03-testing.md](03-testing.md); the final agent roster is in [02-agents.md](02-agents.md) and wins over the agent notes in this file.

Scope: 27 modules under `OrchardCore/src/OrchardCore.Modules/`. Everything below was read from code; where something could not be confirmed it is marked "unverified".

Findings about the EXISTING tools that affect the whole group (read in `src/Modules/CrestApps.OrchardCore.AI.Agent/`):

- No tool in `CrestApps.OrchardCore.AI.Agent` calls `IAuthorizationService` (0 occurrences of `AuthorizeAsync`). The only gate is the per-tool `AccessAITool` permission (`OrchardCoreAIToolAccessEvaluator`). The content tools therefore do not apply `EditContent` / `PublishContent` / `DeleteContent` / own-content rules or per-type (Securable) permissions that the admin UI applies on every action. Every new tool below names the Orchard permission to check; the existing content and content-definition tools need the same checks added.
- `searchForContentItems` builds `new QueryFilterResult<ContentItem>(new Dictionary<...>())` (an empty filter) and only sets `SearchText`. `DefaultContentsAdminListQueryService.QueryAsync` only executes `model.FilterResult`, so the search term is not applied and no `status`/`sort`/`type` term runs (the result is very likely every version of every item, unordered; exact YesSql behaviour unverified because YesSql is a package). It must parse the text with `IContentsAdminListFilterParser` as the admin list does.
- `getContentItemById`, `cloneContentItem`, `deleteContentItem`, `unpublishContentItem` and `getLinkForContentItem` call `IContentManager.GetAsync(id)` with no version, which is `VersionOptions.Published` (`DefaultContentManager.GetAsync`). A draft-only item is reported as "not found", so a draft cannot be read, cloned or deleted. The admin UI uses `VersionOptions.Latest` for all of these.
- `createOrUpdateContentItem` merges the JSON payload straight into the item (`ContentItemPreparationService`, `MergeArrayHandling.Replace`) and never runs display drivers. Content handlers and `ValidateAsync` run, but everything that only exists in a driver or a controller does not: owner editor, created-date editor, Publish Later / Archive Later local-time conversion, the `SetHomepage` permission check, `LayerMetadata` validation, and the taxonomy and menu tree editors. Arrays are replaced whole, so any nested list (Flow widgets, Bag items, menu items, taxonomy terms, widget zones) must be resent complete and is easy to break. This is the reason for the dedicated nested/tree tools below.

---

## Contents — `OrchardCore.Contents`, `OrchardCore.Contents.QuickNavigation`, `OrchardCore.Contents.VersionPruning`, `OrchardCore.Contents.FileContentDefinition`

**What an admin can do in the UI:**

- Content → Content Items (`AdminController.List`): search by text with the filter syntax (`status:`, `sort:`, `type:`, `stereotype:`, plus `culture:` from Content Localization), filter by content type / status (Latest, Owned by me, Published, Unpublished, All versions), sort (created, modified, published, title), page.
- Bulk actions on checked rows (`ListPOST`): Publish Now, Unpublish, Delete.
- New → pick a creatable type → Create; Save Draft, Publish, Save and continue (`Create*POST`, `Edit*POST`).
- Per item: Display (admin detail), Edit, Publish, Unpublish, Clone (saved as a draft), Discard Draft (`DiscardDraft`, only when the latest version is an unpublished draft), Delete (`Remove`).
- Owner and created date: when the type's `CommonPart` has `CommonPartSettings.DisplayOwnerEditor` / `DisplayDateEditor`, the editor shows an Owner (username) field (`OwnerEditorDriver`, needs `EditContentOwner`) and a Created date field (`DateEditorDriver`).
- With `OrchardCore.AuditTrail` enabled (`AuditTrail/Startup.cs`): each row gets a history action that opens the audit trail for the item; an event can be opened read-only as that version (`AuditTrailContentController.Display`) and restored (`Restore` → `IContentManager.RestoreAsync`, creates a new draft from the snapshot). Events recorded: Created, Saved, Published, Unpublished, Removed, Cloned, Restored. `AuditTrailPart` adds a version comment input.
- Front end: `ItemController.Display` and `Preview`.
- Settings → Content Version Pruning (feature `OrchardCore.Contents.VersionPruning`): retention days, versions to keep, content types, disabled. The background task `ContentVersionPruningBackgroundTask` (daily, `Enable = false` by default) calls `IContentVersionPruningService.PruneVersionsAsync`. There is no "prune now" button in this module.
- Content type settings contributed here: `CommonPartSettings` (per type-part), `FullTextAspectSettings` (per type: `IncludeFullTextTemplate`, `FullTextTemplate`, `IncludeBodyAspect`, `IncludeDisplayText`), `AuditTrailPartSettings.ShowCommentInput`.
- API: `api/content` GET / POST / DELETE (`Endpoints/Api/*`, permission `AccessContentApi`).
- Workflow activities: Content Created/Deleted/Draft Saved/Published/Unpublished/Updated/Versioned events, Create/Delete/Publish/Retrieve/Unpublish/Update Content tasks.
- `OrchardCore.Contents.FileContentDefinition`: stores definitions in a file, no UI.

**Already covered:** `searchForContentItems` (broken, see above), `getContentItemById`, `getSampleContentItemForContentType`, `getContentItemSchema`, `createOrUpdateContentItem`, `cloneContentItem`, `publishContentItem`, `unpublishContentItem`, `deleteContentItem`, `getLinkForContentItem`. Recipe step `Content` (`ContentStep`, `IContentManager.ImportAsync`) through `importOrchardCoreRecipe`. Workflow activities are reachable through `listWorkflowActivities` / `createOrUpdateWorkflow`. "View or download as JSON" is the same data `getContentItemById` returns. Not covered: a working filtered list, drafts, versions and restore, discard draft, bulk actions, change owner.

**Settings groups:**

- `ContentAuditTrailSettings` / group `AuditTrail` (`AuditTrailSettingsGroup.Id`) / permission `ManageAuditTrailSettings` / secrets: none. Field: `AllowedContentTypes`.
- `ContentVersionPruningSettings` / group `ContentVersionPruningSettings` / permission `ManageContentVersionPruningSettings` / secrets: none. Fields: `RetentionDays`, `VersionsToKeep`, `ContentTypes`, `Disabled`.
- `ContentQuickNavigationSettings` / group `admin` / permission `ManageAdminSettings` / secrets: none. Field: `MaxItems`.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listContentItems` | Read | no | `ListContent` (and `ViewContent` / own-content per type, as the list's `type` filter does) | Lists content items with the same filters as the admin Content Items page: text, content types, stereotype, status (latest, published, draft, owned by me, all versions), sort, culture, parent list, taxonomy term, scheduled, and paging. Use it to find items and their IDs; it returns summaries, not full bodies. Replaces `searchForContentItems`. | ContentAgent |
| `listContentItemVersions` | Read | no | `ViewContent` on the item | Lists the stored versions of one content item (version ID, created/modified/published dates, author, latest and published flags) and, when the audit trail is on, the matching audit events with version number and comment. Use it before restoring a version. | ContentAgent |
| `restoreContentItemVersion` | Write | no | `PublishContent` on the item | Restores an earlier version of a content item as a new draft, keeping the published version live. Takes a content item version ID or an audit trail event ID; the current draft, if any, stops being the latest. Publish the item afterwards to make it live. | ContentAgent |
| `discardContentItemDraft` | Destructive | yes | `DeleteContent` on the item | Discards the unpublished draft of a content item so the published version becomes the latest again. Fails when the latest version is published; when the item was never published the item disappears from the lists. | ContentAgent |
| `changeContentItemOwner` | Security | yes | `EditContentOwner` on the item | Changes the owner of a content item to another user, given by user name, user ID or email. Ownership decides who can edit, publish or delete the item under the "own content" permissions. | ContentAgent |

**Implementation notes:**

- `listContentItems`: `IContentsAdminListFilterParser.Parse(text)` → `ContentOptionsViewModel.FilterResult`, then `IContentsAdminListQueryService.QueryAsync`, `Skip/Take`, `ListAsync(contentManager)`. Build the filter text from structured arguments (`text`, `contentTypes[]`, `stereotype`, `status`, `sort`, `culture`) so the model does not need the syntax. The list container filter (`ListPartContentsAdminListFilter`, `ContainedPartIndex.ListContentItemId`) and the taxonomy filter (`TaxonomyContentsAdminListFilter`, `TaxonomyIndex`) read from the `IUpdateModel` form values, so they must be applied directly on the query in the tool. `scheduled: publish|archive` joins `PublishLaterPartIndex` / `ArchiveLaterPartIndex` when those features are on. Return ID, version ID, type, display text, published/latest, owner, author, modified/published dates converted with `ILocalClock`. Honour `PagerOptions.MaxPagedCount` like the controller.
- `listContentItemVersions`: `IContentManager.GetAllVersionsAsync(contentItemId)`. `RemoveAsync` only clears the `Latest`/`Published` flags, so versions of a deleted item are still listed (until version pruning runs) and can be restored. Audit events: `AuditTrailEvent` documents in collection `AuditTrailEvent.Collection`, `AuditTrailContentEvent` (`ContentItem`, `VersionNumber`, `Comment`); only when `OrchardCore.AuditTrail` is enabled and the type is in `ContentAuditTrailSettings.AllowedContentTypes`.
- `restoreContentItemVersion`: `IContentManager.GetVersionAsync(versionId)` or the snapshot in the audit event, then `LoadAsync` and `RestoreAsync` (returns `ContentValidateResult`; report the errors). The UI only offers restore from an audit trail event; restoring by version ID is the same service call and works without the Audit Trail feature.
- `discardContentItemDraft`: `GetAsync(id, VersionOptions.Latest)`, refuse when `Published`, then `DiscardDraftAsync`.
- `changeContentItemOwner`: `UserManager<IUser>.FindByNameAsync/FindByIdAsync/FindByEmailAsync`, set `ContentItem.Owner` on the latest version (and on the published version when different) and save. The logic only exists in `OwnerEditorDriver.UpdateAsync` and is tied to `CommonPartSettings.DisplayOwnerEditor`; the tool should follow the permission, not the display setting. Decide whether `Author` is changed too (the driver does not change it).
- Improve existing tools instead of adding new ones: (1) `getContentItemById`: add `version` (`published` | `latest` | a version ID) and default to latest; (2) `publishContentItem`, `unpublishContentItem`, `deleteContentItem`: accept `contentItemIds[]` to cover the list's bulk actions (same arguments and risk as the single form), load with `VersionOptions.Latest`, check the permission per item and report per-item results; (3) `cloneContentItem`, `deleteContentItem`, `unpublishContentItem`, `getLinkForContentItem`: load `VersionOptions.Latest`; (4) `createOrUpdateContentItem`: add optional `createdUtc`/local created date (only honoured with `DisplayDateEditor`), an `auditTrailComment` (sets `AuditTrailPart.Comment`), and keep `isDraft`; (5) `getLinkForContentItem`: add a `preview` link type (`ItemController.Preview`, permission `PreviewContent`); (6) add permission checks to all of them (`ViewContent`, `EditContent`, `PublishContent`, `DeleteContent`, `CloneContent`).
- Version pruning has no manual action here; "run now" belongs to the Background Tasks tools (task `ContentVersionPruningBackgroundTask`). The task is disabled by default, so the settings alone do nothing until the task is enabled.

**Test prompts:**

- "Show me the 20 most recently modified draft blog posts."
- "Roll the About page back to the version from last Tuesday."
- "Throw away my unsaved draft changes on the Pricing page."
- "Make jane the owner of all three press releases we published this week."

---

## Contents deployment actions — `OrchardCore.Contents.Deployment.AddToDeploymentPlan`, `OrchardCore.Contents.Deployment.ExportContentToDeploymentTarget`, `OrchardCore.Contents.Deployment.Download`

**What an admin can do in the UI:**

- Content Items → row action / bulk action "Add to deployment plan" → pick a plan (`AddToDeploymentPlanController.AddContentItem`, `AddContentItems`): appends a `ContentItemDeploymentStep` per item to the plan. The bulk form only takes published items.
- Content Items → row action / bulk action "Export to deployment target" → pick a target (`ExportContentToDeploymentTargetContentDriver`, modal): runs the configured plan with the item(s) against the chosen target (for example a remote instance).
- Settings → Export Content To Deployment Target: choose the plan used for the export.
- Content Items → row action "View as JSON" / "Download as JSON" (`DownloadController.Display`, `Download`; permissions `Export` and `EditContent`).
- Deployment steps added to plans: `ContentDeploymentStep`, `AllContentDeploymentStep`, `ContentItemDeploymentStep`, `ExportContentToDeploymentTargetDeploymentStep`.

**Already covered:** View/Download as JSON → `getContentItemById`. Nothing else.

**Settings groups:** `ExportContentToDeploymentTargetSettings` / group `ExportContentToDeploymentTarget` / permission `ManageDeploymentPlan` / secrets: none. Field: `ExportContentToDeploymentTargetPlanId`.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `addContentItemsToDeploymentPlan` | Write | no | `ManageDeploymentPlan` and `Export`, plus `EditContent` on each item | Adds one or more content items to an existing deployment plan as content item steps, so they are included the next time the plan is exported. Needs the plan ID and the content item IDs. | DeploymentAgent |
| `exportContentItemsToDeploymentTarget` | External | yes | `Export`, plus `EditContent` on each item | Sends one or more content items to a deployment target, such as a remote Orchard Core instance, using the site's "export content to deployment target" plan. Use it to push content to another environment; it overwrites those items there. | DeploymentAgent |

**Implementation notes:**

- `addContentItemsToDeploymentPlan`: load `DeploymentPlan` by ID from `ISession`, create a `ContentItemDeploymentStep` through the `IDeploymentStepFactory` named `ContentItemDeploymentStep`, set `ContentItemId`, add to `DeploymentSteps`, save. The logic lives only in `AddToDeploymentPlanController`.
- `exportContentItemsToDeploymentTarget`: `ExportContentToDeploymentTargetDeploymentSource` reads the item IDs from the current HTTP request (`TryUpdateModelAsync(model, "ExportContentToDeploymentTarget", ...)`), so it cannot be driven from a tool as is. The item selection has to be extracted into a service before this tool can exist; the actual send is done by the Deployment / Deployment.Remote module (other group). Both tools belong with that group's agent.

**Test prompts:**

- "Add the three new landing pages to the 'Marketing release' deployment plan."
- "Push the updated Terms of Service page to the staging site."

---

## Content Types — `OrchardCore.ContentTypes`

**What an admin can do in the UI:**

- Design → Content Definition → Content Types (`AdminController.List`, `ViewContentTypes`): list types; Create (`CreatePOST`: display name + technical name → `IContentDefinitionService.AddTypeAsync`); Edit (`EditPost`: display name, type settings, drag-order of parts and of the type's own fields → `AlterTypePartsOrderAsync`, `AlterPartFieldsOrderAsync`); Delete (`Delete` → `RemoveTypeAsync(id, true)`).
- Type settings editor (`ContentTypeSettings`): Creatable, Listable, Draftable, Versionable, Securable, Stereotype, Description, Category, ThumbnailPath; plus settings other modules add (full-text, GraphQL, etc.).
- On a type: Add Parts (`AddPartsToPOST` → `AddPartToTypeAsync`), Add Named Part (`AddReusablePartToPOST` → `AddReusablePartToTypeAsync(name, displayName, description, part, type)`), Edit a type-part (`EditTypePartPOST` → `AlterTypePartAsync`: display name, description, editor, display mode, plus the part's own settings drivers), Remove part (`RemovePart` → `RemovePartFromTypeAsync`).
- Design → Content Definition → Content Parts (`ListParts`): Create part (`CreatePartPOST` → `AddPartAsync`), Edit part (`EditPartPOST`: part settings, field order), Delete part (`DeletePart` → `RemovePartAsync`).
- On a part or a type: Add Field (`AddFieldToPOST` → `AddFieldToPartAsync(name, displayName, fieldType, part)`), Edit Field (`EditFieldPOST` → `AlterFieldAsync`: display name, editor, display mode, plus the field's settings drivers), Remove Field (`RemoveFieldFromPOST` → `RemoveFieldFromPartAsync`).
- Deployment steps: Content Definition, Replace Content Definition, Delete Content Definition.

**Already covered:** `listContentTypesDefinitions`, `getContentTypeDefinition`, `listContentPartsDefinitions`, `getContentPartDefinition`, `listContentFieldDefinitions` (returns field type names only), `applyContentTypeDefinitionFromRecipe` (create or merge a type or part: display name, type settings, attach parts, add fields, merge settings), `removeContentTypeDefinition`, `removeContentPartDefinition`. Recipe steps `ContentDefinition` (merge only: never removes a part from a type or a field from a part, and only merges type settings when `DisplayName` is supplied), `ReplaceContentDefinition` (deletes then recreates the whole type/part) and `DeleteContentDefinition` through `importOrchardCoreRecipe`. Not covered without rewriting a whole definition: removing one part or field, reordering, named parts with their own settings in one safe call, and finding which editors, display modes and settings exist.

**Settings groups:** none (appsettings only: `OrchardCore:ContentTypes:DefaultCategory`, `DefaultThumbnailPath`).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `getContentDefinitionOptions` | Read | no | `ViewContentTypes` | Returns what can be configured on a content part or field type: the available editors and display modes, the settings objects it accepts with their properties and defaults, and the known stereotypes. Call it before `saveContentTypePart` or `saveContentPartField` so setting names and values are valid. | ContentTypeAgent |
| `saveContentTypePart` | Write | no | `EditContentTypes` | Attaches a part to a content type or updates how it is attached: display name and description (named parts), position, editor, display mode and the part's settings for this type. Use it to add or reconfigure a single part without resending the whole type definition. | ContentTypeAgent |
| `removeContentTypePart` | Destructive | yes | `EditContentTypes` | Removes a part from a content type. Existing items keep the stored data but it is no longer edited or displayed; a part that owns the type's fields cannot be re-created with the same data automatically. | ContentTypeAgent |
| `saveContentPartField` | Write | no | `EditContentTypes` | Adds a field to a content part (or to a content type's own part) or updates an existing field: display name, field type on creation, position, editor, display mode and the field's settings. The field type cannot be changed after creation. | ContentTypeAgent |
| `removeContentPartField` | Destructive | yes | `EditContentTypes` | Removes a field from a content part or content type. Existing items keep the stored values but they are no longer edited or displayed. | ContentTypeAgent |
| `reorderContentTypeElements` | Write | no | `EditContentTypes` | Sets the order of the parts on a content type, or of the fields on a part, from an ordered list of names. Use it instead of editing each position. | ContentTypeAgent |

**Implementation notes:**

- Services: `IContentDefinitionService` (`OrchardCore.ContentTypes.Abstractions`) for add/remove/alter/order, and `IContentDefinitionManager.AlterTypeDefinitionAsync` / `AlterPartDefinitionAsync` with `MergeSettings(JsonObject)` for the settings. Settings are JSON objects keyed by the settings class name, the same shape the recipe uses (for example `{"AutoroutePartSettings": {"Pattern": "...", "AllowCustomPath": true}}`). The UI applies settings through display drivers (`IContentTypePartDefinitionDisplayDriver`, `IContentPartFieldDefinitionDisplayDriver`) which also validate (for example Liquid patterns); a tool that merges JSON skips that validation, so validate known settings or run the driver update with a model updater built from the arguments.
- `saveContentTypePart`: new attachment → `AddPartToTypeAsync` or `AddReusablePartToTypeAsync`; the controller's checks must be reproduced (part exists and is attachable, named part name unique and a valid identifier, display name unique on the type). Then `AlterTypePartAsync(AlterTypePartContext)` and a settings merge.
- `saveContentPartField`: `AddFieldToPartAsync(fieldName, displayName, fieldTypeName, partName)` then `AlterFieldAsync(AlterFieldContext)` and a settings merge; reproduce `AddFieldToPOST` validation (name is a valid identifier, unique in the part, display name unique). `GenerateFieldNameFromDisplayNameAsync` gives the technical name.
- `getContentDefinitionOptions`: editors and display modes are discovered inside Razor views (`Views/Admin/EditField.cshtml`, `EditTypePart.cshtml`) by scanning the theme shape table for bindings named `{Field or Part}_Option__{Editor}` and `{Field or Part}_DisplayOption__{Mode}`. That logic must be extracted into a service. The list of settings classes per part/field is not exposed by any API; build it from the known settings types (table in the open questions) or reflect over a registry the plan adds. Stereotypes: `IStereotypeService.GetStereotypesAsync()` (interface in `OrchardCore.ContentManagement.Abstractions`, implemented in this module's `Services/StereotypeService.cs`).
- Improve existing tools: `removeContentTypeDefinition` and `removeContentPartDefinition` run the `DeleteContentDefinition` recipe step, which calls `IContentDefinitionManager.DeleteTypeDefinitionAsync` / `DeletePartDefinitionAsync` directly. The UI calls `IContentDefinitionService.RemoveTypeAsync` / `RemovePartAsync`, which refuse system-defined definitions (`ContentSettings.IsSystemDefined`), detach parts, delete the type's own part and its fields, and raise the `ContentTypeRemoved` / `ContentPartRemoved` events. Switch the tools to the service. `listContentFieldDefinitions` should return more than names (fold `getContentDefinitionOptions` data in, or point to it). `applyContentTypeDefinitionFromRecipe` should say in its description that it only adds and merges.
- Risk: `EditContentTypes` is marked security critical in Orchard Core. Definition changes are tenant-wide and apply immediately to every item of the type; turning on `Securable` creates per-type permissions. `removeContentTypePart` on a part with data hides that data from all items.

**Test prompts:**

- "Add an optional 'Subtitle' text field to the Article type, right under the title."
- "Remove the Markdown body from the Landing Page type and add a Flow part instead."
- "Which editors can I use for a Text field, and what settings does it have?"
- "Make blog posts use a custom URL pattern of blog/{year}/{slug} and allow editors to override it."

---

## Content Localization — `OrchardCore.ContentLocalization`, `OrchardCore.ContentLocalization.ContentCulturePicker`, `OrchardCore.ContentLocalization.Sitemaps`

**What an admin can do in the UI:**

- On the editor of an item whose type has `LocalizationPart`: see the cultures of its localization set and create the missing ones ("Localize" → `AdminController.Localize(contentItemId, targetCulture)` → `IContentLocalizationManager.LocalizeAsync`, which creates a draft copy in the target culture in the same `LocalizationSet`; refused when a localization for that culture already exists).
- Content Items list: `culture:` filter and culture column (`LocalizationPartContentsAdminListFilterProvider`, `LocalizationContentsAdminListDisplayDriver`).
- Settings → Localization → Content Culture: `ContentRequestCultureProviderSettings.SetCookie`. Settings → Localization → Content Culture Picker: `ContentCulturePickerSettings` (`RedirectToHomepage`, `SetCookie`). Both drivers are registered by the `ContentCulturePicker` feature.
- Front end: culture picker redirect (`ContentCulturePickerController.RedirectToLocalizedContent`). `OrchardCore.ContentLocalization.Sitemaps` adds hreflang metadata and a localized sitemap query, no UI.

**Already covered:** content tools can read `LocalizationPart` (`LocalizationSet`, `Culture`) on an item. Creating a translation with `createOrUpdateContentItem` would require copying the item and hand-writing `LocalizationSet` and `Culture`, bypassing the localization handlers; not a good fit.

**Settings groups:**

- `ContentCulturePickerSettings` / group `ContentCulturePicker` / permission `ManageContentCulturePicker` / secrets: none.
- `ContentRequestCultureProviderSettings` / group `ContentRequestCultureProvider` / permission `ManageContentCulturePicker` / secrets: none.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listContentItemLocalizations` | Read | no | `ViewContent` on the item | Lists the translations of a content item: for each site culture, the content item ID, title and published state of its version in that culture, or that it is missing. Use it to find the item for a language or to see what still needs translating. | ContentAgent |
| `localizeContentItem` | Write | no | `LocalizeContent` on the item and `EditContent` for its type | Creates the version of a content item in another culture as a draft copy linked to the same localization set, and returns the new content item ID. Fails if that culture already exists or the type has no localization part; translate the copy with `createOrUpdateContentItem` afterwards. | ContentAgent |

**Implementation notes:** `IContentLocalizationManager.GetItemsForSetAsync(localizationSet)`, `GetContentItemAsync(set, culture)`, `LocalizeAsync(item, culture)` (throws `InvalidOperationException` on failure). Supported cultures come from `ILocalizationService.GetSupportedCulturesAsync()` (Localization module, other group). Load the source item with `VersionOptions.Latest` as the controller does. The copy contains the source language text; the model then rewrites the fields, so this pairs with an LLM translation step and may create many items quickly: consider a per-call limit.

**Test prompts:**

- "Which languages is the Pricing page available in?"
- "Create a French version of the About page and translate it."
- "Find blog posts that have no Spanish translation yet."

---

## Autoroute, Alias and Home Route — `OrchardCore.Autoroute`, `OrchardCore.Alias`, `OrchardCore.HomeRoute`

**What an admin can do in the UI:**

- On an item with `AutoroutePart`: edit the permalink (`Path`), tick "Set as homepage" (`SetHomepage`, shown with `AutoroutePartSettings.ShowHomepageOption`, saved only when the user has permission `SetHomepage`), "Disabled", "Absolute", "Route contained items" (`AutoroutePartDisplayDriver`). On publish, `AutoroutePartHandler.PublishedAsync` writes the item into `ISite.HomeRoute` and clears the flag.
- On an item with `AliasPart`: edit the alias (`AliasPartSettings`: `Pattern`, `Options` Editable / GeneratedDisabled).
- Type-part settings: `AutoroutePartSettings` (`AllowCustomPath`, `Pattern`, `ShowHomepageOption`, `AllowUpdatePath`, `AllowDisabled`, `AllowRouteContainedItems`, `ManageContainedItemRoutes`, `AllowAbsolutePath`), `AliasPartSettings`.
- `OrchardCore.HomeRoute` has no UI (routing only). The home route is also a site setting (`HomeRoute`) settable with the `Settings` recipe step.

**Already covered:** content tools set `AutoroutePart.Path` and `AliasPart.Alias`; the path uniqueness check runs in the handler's validation. Part settings through `applyContentTypeDefinitionFromRecipe` / `saveContentTypePart`. Setting the home page works today only by writing `AutoroutePart.SetHomepage: true` in the payload and publishing, which skips the `SetHomepage` permission; `Settings` recipe step with `HomeRoute` through `importOrchardCoreRecipe` needs the raw route values. Not covered: finding the item behind a URL or alias, and a safe "set home page".

**Settings groups:** none.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `findContentItemByHandle` | Read | no | `ViewContent` on the item found | Finds the content item behind a URL path, an alias or a handle (`slug:about-us`, `alias:main-menu`, `contentitemid:...`), and returns its ID, type, title and state. A path of `/` returns the current home page. Use it when the user names a page by its address. | ContentAgent, SiteStructureAgent |
| `setHomepage` | Write | no | `SetHomepage` (and `PublishContent` on the item) | Makes a published content item the site's home page. The item's type must have an Autoroute part with a path and the route must not be disabled. | SiteStructureAgent |

**Implementation notes:**

- `findContentItemByHandle`: `IContentHandleManager.GetContentItemIdAsync(handle)`; providers: `AutorouteHandleProvider` (`slug:`), `AliasPartContentHandleProvider` (`alias:`), `ContentItemIdHandleProvider` (`contentitemid:`). Normalize a plain URL or path to `slug:`. Home page: read `ISite.HomeRoute` and take the content item ID route value (key `AutorouteOptions.ContentItemIdKey`); a home route that points to a non-content controller should be reported as such.
- `setHomepage`: the only code path is the part flag + publish. Either set `AutoroutePart.SetHomepage = true` on the latest version and call `PublishAsync`, or extract the private `AutoroutePartHandler.SetHomeRouteAsync` logic (copy `AutorouteOptions.GlobalRouteValues`, set the content item ID, remove the JSON path key, `ISiteService.UpdateSiteSettingsAsync`). Site-wide and visible to every visitor at once; the previous home page is not remembered, so return the previous value in the result.

**Test prompts:**

- "Which content item is shown at /about-us?"
- "What is the site's home page right now?"
- "Make the new Spring Campaign landing page the home page."

---

## Lists — `OrchardCore.Lists`

**What an admin can do in the UI:**

- Open a list item (a type with `ListPart`, for example Blog): its admin detail shows the contained items with a text and status filter, paging, and a Create button per contained type (`ListPartDisplayDriver`, `IContainerService.QueryContainedItemsAsync`). A new item created from there gets `ContainedPart` (`ListContentItemId`, `ListContentType`, `Order`) through `ContainedPartDisplayDriver`.
- Drag to reorder items when `ListPartSettings.EnableOrdering` is on (`OrderController.UpdateContentItemOrders` → `IContainerService.UpdateContentItemOrdersAsync`; checks `PublishContent` on every item of the page).
- Content Items list: filter "show list types" / items of one list (`ListPartContentsAdminListFilter`).
- Type-part settings `ListPartSettings`: `PageSize`, `ContainedContentTypes`, `EnableOrdering`, `ShowHeader`, `ShowFullPager`.
- RSS feed options on the list item editor (`ListFeedEditViewModel`: `DisableRssFeed`, `FeedProxyUrl`, `FeedItemsCount`). Admin menu node "Lists" (`ListsAdminNode`) and MetaWeblog remote publishing (`RemotePublishingController`).

**Already covered:** list items and contained items are content items → content tools. Listing the items of one list → `listContentItems` with `listContentItemId` (proposed above). Creating an item inside a list with `createOrUpdateContentItem` works only if the payload carries a correct `ContainedPart`; `ContainedPartHandler` rejects a contained type without `ListContentItemId` unless the type is creatable. Not covered: reordering.

**Settings groups:** none.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `moveListItem` | Write | no | `PublishContent` on the items whose order changes | Moves a content item to a new position inside its parent list, for example a post within a blog or a slide within a slideshow. Only works when ordering is enabled on the list's content type. | ContentAgent |

**Implementation notes:** `IContainerService.QueryContainedItemsAsync(listId, true, pager, new ContainedItemOptions { Status = Latest })` then `UpdateContentItemOrdersAsync(items, orderOfFirstItem)`. The controller works on one page of items and page-relative indexes; a tool should take `contentItemId` plus `position` (or `beforeContentItemId` / `afterContentItemId`), load the affected range and renumber it. Check `ListPartSettings.EnableOrdering` on the list type first. Improve `createOrUpdateContentItem`: add an optional `listContentItemId` argument that fills `ContainedPart.ListContentItemId`, `ListContentType` and `Order` (`IContainerService.GetNextOrderNumberAsync`) and validates the type against `ListPartSettings.ContainedContentTypes`.

**Test prompts:**

- "Move the 'Summer sale' slide to the first position in the home page slideshow."
- "Add a new post called 'Release notes 3.0' to the company blog as a draft."
- "List the posts in the News blog, newest first."

---

## Flows and Widgets — `OrchardCore.Flows`, `OrchardCore.Widgets`

**What an admin can do in the UI:**

- On an item with `FlowPart`: add, edit, reorder and delete widgets inline; each widget has `FlowMetadata` (`Alignment`, `Size`) (`FlowPartDisplayDriver`, `Flows/AdminController.BuildEditor`, permission `EditContent`).
- On an item with `BagPart` (also named bag parts): add, edit, reorder and delete contained items of the allowed types or stereotypes (`BagPartDisplayDriver`).
- On an item with `WidgetsListPart`: add, edit, reorder and delete widgets per zone (`WidgetsListPartDisplayDriver`, `Widgets/AdminController.BuildEditor`); each widget has `WidgetMetadata` (`RenderTitle`, `Position`).
- Type-part settings: `FlowPartSettings` (`ContainedContentTypes`, `CollapseContainedItems`, `DefaultAlignment`), `BagPartSettings` (`ContainedContentTypes`, `ContainedStereotypes`, `DisplayType`, `CollapseContainedItems`), `WidgetsListPartSettings.Zones`.
- No menu entries and no site settings in either module. Site-wide widgets are the Layers module.

**Already covered:** the nested items are stored inside the parent item (`FlowPart.Widgets`, `BagPart.ContentItems`, `WidgetsListPart.Widgets[zone]`), so `getContentItemById` returns them and `createOrUpdateContentItem` can write them, but only by resending the complete array (arrays are replaced, not merged). Editing one widget on a page with many is error-prone and token-heavy, and nested items in nested bags multiply the risk.

**Settings groups:** none.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listNestedContentItems` | Read | no | `ViewContent` on the parent item | Returns the outline of the items nested in a content item's Flow, Bag and widget-zone parts: for each, its part, zone, position, nested item ID, content type and title, with children indented. Use it to locate a widget or contained item before changing it. | ContentAgent |
| `saveNestedContentItem` | Write | no | `EditContent` on the parent item (`PublishContent` when publishing) | Adds, updates or moves a single widget or contained item inside a content item's Flow, Bag or widget-zone part without resending the rest of the page. Takes the parent item, the target part (and zone or parent nested item), an optional position, and the nested item's content. | ContentAgent |
| `removeNestedContentItem` | Destructive | yes | `EditContent` on the parent item | Deletes one widget or contained item, and everything nested inside it, from a content item's Flow, Bag or widget-zone part. | ContentAgent |

**Implementation notes:** No service exists; the UI rebuilds the lists from posted form prefixes in the part drivers. The tool loads the parent with `VersionOptions.DraftRequired`, edits the part's list (`FlowPart.Widgets`, `BagPart.ContentItems` including named bag parts, `WidgetsListPart.Widgets[zone]`) at any depth (find by nested `ContentItemId`), then `UpdateAsync` + `ValidateAsync` + `SaveDraftAsync` or `PublishAsync` (`isDraft`, like `createOrUpdateContentItem`). New nested items must be created with `IContentManager.NewAsync(type)` so they get an ID and handlers run, and validated against the part settings (`ContainedContentTypes`, `ContainedStereotypes`, zone in `WidgetsListPartSettings.Zones`). Reuse `ContentItemPreparationService` / `getContentItemSchema` for the nested payload. Moving = same tool with only a new position or parent. Menus and taxonomies use different containers and permissions and have their own tools.

**Test prompts:**

- "Add a quote block after the second paragraph widget on the About page."
- "Change the heading of the hero banner on the home page to 'Welcome back'."
- "Remove the newsletter signup widget from the Contact page."
- "What widgets does the Services page contain, in order?"

---

## Layers — `OrchardCore.Layers`

**What an admin can do in the UI:**

- Design → Widgets (`AdminController.Index`, permission `ManageLayers`): see the layers and, per zone, the widgets with their layer; drag a widget to another position or zone (`UpdatePosition(contentItemId, position, zone)`, also updates the published version); Add Widget to a zone (creates a widget content item with a welded `LayerMetadata`: `Zone`, `Position`, `Layer`, `RenderTitle`, plus the title, `LayerMetadataWelder`); edit, publish, unpublish, delete a widget (Contents actions).
- Layers: Create (`CreatePost`: name, description, empty rule), Edit (`EditPost`: description; the layer is looked up by the posted name, so the name does not change), Delete (`Delete`: refused while any latest widget uses the layer).
- Layer rule editor (`LayerRuleController`): add (`Create`), edit (`Edit`), delete (`Delete`) and reorder (`UpdateOrder`) conditions in the layer's rule, including nested groups. Condition types come from `OrchardCore.Rules`: `AllConditionGroup`, `AnyConditionGroup`, `BooleanCondition`, `HomepageCondition`, `UrlCondition`, `CultureCondition`, `RoleCondition`, `JavascriptCondition`, `IsAuthenticatedCondition`, `IsAnonymousCondition`, `ContentTypeCondition`.
- Settings → Zones: the zone names offered for widgets (`LayerSettings.Zones`).
- Deployment step All Layers; recipe step `Layers`.

**Already covered:** recipe step `Layers` through `importOrchardCoreRecipe` creates or updates layers by name and replaces a layer's whole condition list (cannot delete a layer, cannot place widgets). Widget content items themselves → content tools, but `LayerMetadata` is a welded part that is not in the widget type's definition, so the schema-driven `createOrUpdateContentItem` is a poor fit for placing a widget (whether an undeclared `LayerMetadata` object in the payload survives is unverified), and nothing validates that the layer and zone exist.

**Settings groups:** `LayerSettings` / group `zones` / permission `ManageLayers` / secrets: none. Field: `Zones`.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listLayers` | Read | no | `ManageLayers` | Lists the site's layers with their description and rule conditions, the configured zones, and the widgets placed in each zone with their layer, position, title, content type and published state. Use it to see what shows where before changing widgets or layers. | SiteStructureAgent |
| `saveLayer` | Write | no | `ManageLayers` | Creates a layer or updates its description and rule. The rule is a list of conditions (home page, URL, culture, role, signed in, anonymous, content type, all/any groups, always/never); the given list replaces the existing one. Layer names are unique and cannot be changed afterwards. | SiteStructureAgent |
| `deleteLayer` | Destructive | yes | `ManageLayers` | Deletes a layer. Refused while any widget is assigned to it; move or delete those widgets first. | SiteStructureAgent |
| `setLayerWidgetPlacement` | Write | no | `ManageLayers` (and `EditContent` on the widget) | Places a widget content item in a zone and layer, or moves it: sets its zone, layer, position and whether its title is rendered. Create the widget with `createOrUpdateContentItem` first; the widget shows on the site once it is published. | SiteStructureAgent |

**Implementation notes:**

- `ILayerService`: `GetLayersAsync` / `LoadLayersAsync` / `UpdateAsync(LayersDocument)`, `GetLayerWidgetsMetadataAsync(x => x.Latest)`; zones from `ISiteService.GetSettingsAsync<LayerSettings>()`.
- `saveLayer`: build conditions with the `IConditionFactory` registered for each condition name and `IConditionIdGenerator.GenerateUniqueId`, exactly as `Recipes/LayerStep.cs` does; reuse its JSON shape (`LayerRule.Conditions[]` with a `Name` discriminator) so the recipe schema documents it. Validation (name required and unique) is the private `ValidateViewModel` in the controller. The UI has no rename (`EditPost` finds the layer by the posted name) and widgets reference the layer by name in `LayerMetadata.Layer`, so the tool should not offer one.
- `JavascriptCondition` runs a script through `OrchardCore.Scripting` on every request. Either reject it in `saveLayer` or require approval for that condition type.
- `setLayerWidgetPlacement`: weld/apply `LayerMetadata` on the latest version, and on the published version when it differs, as `UpdatePosition` does (`ISession.SaveAsync`), then invalidate the layer cache by updating `LayerState` (`IVolatileDocumentManager<LayerState>`, done by `LayerMetadataHandler` on publish/unpublish/remove only). Validate that the layer exists and the zone is in `LayerSettings.Zones`; require the content type stereotype `Widget`.
- Removing a widget from the site = `unpublishContentItem` or `deleteContentItem`.

**Test prompts:**

- "Which widgets are shown in the footer, and on which pages?"
- "Create a layer that is only active on blog pages for signed-in users."
- "Add a 'Holiday opening hours' HTML widget to the top of the sidebar on every page."
- "Move the cookie banner widget from the Footer zone to the Header zone."

---

## Taxonomies — `OrchardCore.Taxonomies`, `OrchardCore.Taxonomies.ContentsAdminList`

**What an admin can do in the UI:**

- Taxonomies are content items of type `Taxonomy` with `TaxonomyPart` (`TermContentType`, `Terms`: a tree of term content items; children are in each term's `Terms` array). Create/edit/delete the taxonomy through Contents.
- On the taxonomy editor: Add Term at the root or under a term (`AdminController.CreatePost(id, taxonomyContentItemId, taxonomyItemId)`), Edit Term (`EditPost`), Delete Term with its children (`Delete`), drag to reorder and re-parent terms (posted as `Hierarchy`, applied in `TaxonomyPartDisplayDriver.UpdateAsync`). All term actions check `ManageTaxonomies` (permission name `ManageTaxonomy`).
- On a content item with a `TaxonomyField`: pick terms (`TermContentItemIds`); with the Tags editor, create a new term on the fly (`TagController.CreatePost(taxonomyContentItemId, displayText)`).
- Field settings: `TaxonomyFieldSettings` (`TaxonomyContentItemId`, `Unique`, `LeavesOnly`, `Open`, `Placeholder`), `TaxonomyFieldTagsEditorSettings.Open`.
- Settings → Taxonomy Filters (feature `ContentsAdminList`): which taxonomies appear as filters on the Content Items list.

**Already covered:** the taxonomy item itself (create, title, alias, term type) → content tools; assigning terms to content → `createOrUpdateContentItem` writing `TermContentItemIds`. Editing terms through `createOrUpdateContentItem` means resending the entire nested tree and hand-building term items (with `TermPart.TaxonomyContentItemId` welded); a single mistake drops terms that content already references. Reading term IDs requires pulling the whole taxonomy document.

**Settings groups:** `TaxonomyContentsAdminListSettings` / group `taxonomyContentsAdminList` / permission `ManageTaxonomy` / secrets: none. Field: `TaxonomyContentItemIds`.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `getTaxonomyTerms` | Read | no | `ViewContent` on the taxonomy | Returns the terms of a taxonomy as a flat, ordered tree: term ID, title, parent ID, depth and full path. Use it to find term IDs before tagging content or changing the tree; the taxonomy is given by content item ID, alias or title. | SiteStructureAgent, ContentAgent |
| `saveTaxonomyTerm` | Write | no | `ManageTaxonomy` | Adds a term to a taxonomy, updates an existing term, or moves it under another parent or to another position, without touching the other terms. New terms use the taxonomy's term content type. | SiteStructureAgent |
| `deleteTaxonomyTerm` | Destructive | yes | `ManageTaxonomy` | Deletes a term and all its child terms from a taxonomy. Content items that reference the term keep the stale ID and are not updated. | SiteStructureAgent |

**Implementation notes:** No service; the find/add/remove logic is private in `Taxonomies/Controllers/AdminController` (`FindTaxonomyItem`, `RemoveTaxonomyItem`) and the reorder logic is private in `TaxonomyPartDisplayDriver` (`ProcessItem`, index paths). Extract a taxonomy tree service first. Follow the controller: load the taxonomy with `VersionOptions.Latest` when the `Taxonomy` type is not draftable, else `DraftRequired` (so with a draftable Taxonomy type the change sits in a draft until published: return that state and offer to publish); new term = `IContentManager.NewAsync(part.TermContentType)`, `Weld<TermPart>()`, `TaxonomyContentItemId`, then validate (`TagController` shows the handler-only path). `deleteTaxonomyTerm`: report how many content items reference the term (`TaxonomyIndex.TermContentItemId`) before asking for approval. `getTaxonomyTerms` is shared with ContentAgent because tagging content needs term IDs.

**Test prompts:**

- "Show me the Categories taxonomy as a tree."
- "Add a 'Sustainability' category under 'Company news'."
- "Move 'Webinars' so it sits under 'Events' instead of 'Resources'."
- "Tag the three new case studies with the 'Healthcare' category."

---

## Menu — `OrchardCore.Menu`

**What an admin can do in the UI:**

- Content → Menus (list of `Menu` content items; menu entry requires list permission on the `Menu` type). A menu is a content item with `TitlePart`, `AliasPart`, `MenuPart` and `MenuItemsListPart.MenuItems` (a tree; children are in each item's own `MenuItemsListPart.MenuItems`).
- On the menu editor: Add Menu Item at the root or under an item (`AdminController.CreatePost(id, menuContentItemId, menuItemId)`; `id` is a content type with stereotype `MenuItem`: `LinkMenuItem`, `ContentMenuItem`, `HtmlMenuItem`), Edit Menu Item (`EditPost`), Delete Menu Item with its children (`Delete`), drag to reorder and re-parent (posted as `Hierarchy`, applied in `MenuPartDisplayDriver.UpdateAsync`). All check `ManageMenu`.
- Menu item parts: `LinkMenuItemPart` (`Url`, `Target`), `ContentMenuItemPart` (content picker + `CheckContentPermissions`), `HtmlMenuItemPart` (`Url`, `Target`, `Html`; type-part setting `HtmlMenuItemPartSettings.SanitizeHtml`), `MenuItemPermissionPart.PermissionNames` (show the item only to users with these permissions).
- Recipe file `menu.add-permissions.recipe.json` (a one-off definition update, not a step).

**Already covered:** creating, renaming, publishing and deleting a menu, and reading it whole → content tools. Changing menu items through `createOrUpdateContentItem` means resending the complete nested `MenuItems` tree; one mistake silently drops branches of the site navigation.

**Settings groups:** none.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `getMenuTree` | Read | no | `ViewContent` on the menu | Returns a menu's items as a flat, ordered tree: item ID, type, title, URL or linked content item, parent ID, depth and required permissions. The menu is given by content item ID, alias or title; without one it lists the site's menus. | SiteStructureAgent |
| `saveMenuItem` | Write | no | `ManageMenu` | Adds an item to a menu, updates an existing item, or moves it under another parent or to another position, leaving the other items untouched. Supports link, content and HTML menu items; the change is saved as a draft of the menu unless told to publish. | SiteStructureAgent |
| `deleteMenuItem` | Destructive | yes | `ManageMenu` | Deletes a menu item and all its child items from a menu. | SiteStructureAgent |

**Implementation notes:** No service; `FindMenuItem` (internal static) and the remove logic are in `Menu/Controllers/AdminController`, the reorder logic is private in `MenuPartDisplayDriver`. Extract a menu tree service. The controller loads the menu with `VersionOptions.Latest` when the `Menu` type is not draftable, else `DraftRequired`, and ends with `SaveDraftAsync(menu)`: the live menu does not change until the menu is published, so the tool needs a `publish` flag (permission `PublishContent` on the menu) and must report the state. New item = `IContentManager.NewAsync(menuItemType)` filled from arguments; validate that the type has stereotype `MenuItem`. `MenuItemPermissionPart.PermissionNames` controls visibility by permission: treat a change to it as part of the same Write (it hides links, it does not protect pages).

**Test prompts:**

- "Show me the main menu structure."
- "Add a 'Careers' link to the main menu after 'About', pointing to /careers."
- "Move 'Blog' under 'Resources' in the main menu and publish the menu."
- "Remove the 'Black Friday' item from the footer menu."

---

## Publish Later and Archive Later — `OrchardCore.PublishLater`, `OrchardCore.ArchiveLater`

**What an admin can do in the UI:**

- On the editor of a type with `PublishLaterPart`: set a date and time and press "Publish Later", or "Cancel Publish Later" (`PublishLaterPartDisplayDriver`; requires `PublishContent` on the item; converts local time to `ScheduledPublishUtc`). The list shows the scheduled time.
- On the editor of a type with `ArchiveLaterPart`: the same with "Archive Later" / cancel (`ArchiveLaterPartDisplayDriver`, `ScheduledArchiveUtc`).
- Background tasks, every minute: `ScheduledPublishingBackgroundTask` publishes latest unpublished items whose time has passed; `ScheduledArchivingBackgroundTask` unpublishes published items whose time has passed. Both clear the date afterwards.
- No menu, settings or permissions of their own.

**Already covered:** `createOrUpdateContentItem` can write `PublishLaterPart.ScheduledPublishUtc` / `ArchiveLaterPart.ScheduledArchiveUtc` directly, but as a raw UTC value with no time-zone conversion, no `PublishContent` check, and no check that the part is attached or that the item is in the right state (a scheduled publish only fires for a draft, a scheduled archive only for a published item). Listing what is scheduled → `listContentItems` with `scheduled` (proposed above).

**Settings groups:** none.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `scheduleContentItem` | Write | no | `PublishContent` on the item | Schedules a content item to be published or archived (unpublished) at a date and time given in the site's time zone, or cancels the schedule. The content type must have the Publish Later or Archive Later part; publishing is scheduled on a draft, archiving on a published item. | ContentAgent |

**Implementation notes:** Arguments: `contentItemId`, `action` (`publish` | `archive`), `when` (local date-time) or `cancel`. Convert with `ILocalClock.ConvertToUtcAsync` as the drivers do; reject past times; check the part is on the type (`IContentDefinitionManager`). Save the part on the latest version (draft for publish; for archive the published version, since `ArchiveLaterPartIndex` matches `Latest && Published`). Return the scheduled time in local time. The tasks run every minute and are part of the Background Tasks module; if that feature or task is disabled nothing happens, so the tool should warn (unverified how to read task state from here; other group).

**Test prompts:**

- "Publish the Easter promo page next Friday at 8 am."
- "Take the summer sale banner offline at midnight on August 31."
- "Cancel the scheduled publish for the pricing update."
- "What is scheduled to go live this week?"

---

## SEO — `OrchardCore.Seo`

**What an admin can do in the UI:**

- On an item with `SeoMetaPart`: page title, render flag, meta description, keywords, canonical URL, meta robots, custom meta tags, default social image, Open Graph (image, type, title, description), Twitter (image, title, description, card, creator, site), Google schema JSON (`SeoMetaPartDisplayDriver`). Type-part settings `SeoMetaPartSettings`: `DisplayKeywords`, `DisplayCustomMetaTags`, `DisplayOpenGraph`, `DisplayTwitter`, `DisplayGoogleSchema`.
- Settings → Search → Search Engine Optimization → Robots: `RobotsSettings` (`AllowAllAgents`, `DisallowAdmin`, `AdditionalRules`). The page warns when a physical `robots.txt` file exists in the site's static files, because the file then wins (`RobotsMiddleware`). With Sitemaps enabled the same page has "include sitemaps".
- Front end: `/robots.txt` is built from every `IRobotsProvider` (`SiteSettingsRobotsProvider`, `SitemapsRobotsProvider`).

**Already covered:** `SeoMetaPart` values → content tools; part settings → content-definition tools. The CrestApps Seo module already registers `getSchemaOrgOptions`, `configureContentTypeSchemaOrg` and `configureSiteStructuredData`. Robots settings → generic site settings tools (group below). Not covered: reading what `/robots.txt` actually serves.

**Settings groups:** `RobotsSettings` / group `robotsSettings` (`SeoConstants.RobotsSettingsGroupId`) / permission `ManageSeoSettings` / secrets: none.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `getRobotsTxt` | Read | no | `ManageSeoSettings` | Returns the robots.txt content the site currently serves, and says whether it comes from the robots settings or from a physical robots.txt file that overrides them. Use it to check the effect of the robots settings before or after changing them. | SeoAgent |

**Implementation notes:** `IStaticFileProvider.GetFileInfo("robots.txt")` first (as `RobotsMiddleware` does), otherwise concatenate `IRobotsProvider.GetContentAsync()` from all providers. Changing the rules = `updateSiteSettings("robotsSettings", ...)`. `AdditionalRules` is free text served verbatim to crawlers; a wrong `Disallow: /` de-indexes the site, so the SeoAgent instructions should require reading back with `getRobotsTxt` after any change.

**Test prompts:**

- "What does our robots.txt look like right now?"
- "Stop search engines from crawling /internal and /drafts."
- "Write a meta description and social sharing title for the Pricing page."

---

## Sitemaps — `OrchardCore.Sitemaps`, `OrchardCore.Sitemaps.RazorPages`, `OrchardCore.Sitemaps.Cleanup`

**What an admin can do in the UI:**

- Tools → Search Engine Optimization → Sitemaps (`AdminController`, permission `ManageSitemaps`): list with search, Create (`Name`, `Path` generated from the name when empty and validated by `ISitemapHelperService.ValidatePathAsync`, `Enabled`), Edit, Delete, Enable/Disable (`Toggle`), bulk Delete, Display (sources of a sitemap).
- Sources of a sitemap (`SourceController`): add, edit, delete. Types: `ContentTypesSitemapSource` (registered by Contents when Sitemaps is on: `IndexAll`, `LimitItems`, `ChangeFrequency`, `Priority`, `ContentTypes[]` each with `ContentTypeName`/`ChangeFrequency`/`Priority`, `LimitedContentType` with `Skip`/`Take`) and `CustomPathSitemapSource` (`Path`, `LastUpdate`, `ChangeFrequency`, `Priority`).
- Tools → Search Engine Optimization → Sitemap Indexes (`SitemapIndexController`): list, Create/Edit (name, path, enabled, contained sitemaps → one `SitemapIndexSource.ContainedSitemapIds`), Delete, Toggle, bulk Delete.
- Tools → Search Engine Optimization → Sitemaps Cache (`SitemapCacheController`): list cached files, Purge one (`Purge(cacheFileName)`), Purge All.
- On an item with `SitemapPart`: `OverrideSitemapConfig`, `ChangeFrequency`, `Priority`, `Exclude`.
- Settings → Robots (with `OrchardCore.Seo`): `SitemapsRobotsSettings.IncludeSitemaps`.
- Background task `SitemapCacheBackgroundTask` ("Sitemap Cache Cleaner", every 5 minutes) in feature `OrchardCore.Sitemaps.Cleanup`. Deployment step All Sitemaps; recipe step `Sitemaps`. `OrchardCore.Sitemaps.RazorPages` is for developers.

**Already covered:** recipe step `Sitemaps` through `importOrchardCoreRecipe` (upserts whole `SitemapType` documents; needs the polymorphic type discriminators, IDs and source lists written by hand, cannot delete or purge). `SitemapPart` values → content tools. `SitemapsRobotsSettings` → generic site settings tools.

**Settings groups:** `SitemapsRobotsSettings` / group `robotsSettings` / permission `ManageSeoSettings` / secrets: none. Only registered when `OrchardCore.Seo` is enabled.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listSitemaps` | Read | no | `ManageSitemaps` | Lists the sitemaps and sitemap indexes with their path, enabled state and sources (content types with frequency and priority, custom paths, contained sitemaps), and the cached sitemap files. Use it to see what search engines are given. | SeoAgent |
| `saveSitemap` | Write | no | `ManageSitemaps` | Creates or updates a sitemap or a sitemap index: name, path, enabled, and its sources (content type sources, custom path sources, or the sitemaps an index contains). The given source list replaces the existing one; the path must be unique and end in .xml. | SeoAgent |
| `deleteSitemap` | Destructive | yes | `ManageSitemaps` | Deletes a sitemap or a sitemap index. Indexes that contain the sitemap are not updated. | SeoAgent |
| `purgeSitemapCache` | Write | no | `ManageSitemaps` | Clears the cached sitemap files, one file or all of them, so sitemaps are rebuilt on the next request. Use it after changing sitemaps or publishing many items. | SeoAgent |

**Implementation notes:** `ISitemapManager` (`GetSitemapsAsync`, `LoadSitemapAsync`, `UpdateSitemapAsync`, `DeleteSitemapAsync`), `ISitemapIdGenerator.GenerateUniqueId()` for new sitemap and source IDs, `ISitemapHelperService.GetSitemapSlug` / `ValidatePathAsync` (takes an `IUpdateModel`: pass one and read its model state), `ISitemapSourceFactory` per source type, `ISitemapCacheProvider` (`ListAsync`, `PurgeAsync(fileName)`, `PurgeAllAsync`; both return whether there were errors). Types: `Sitemap`, `SitemapIndex` (in the module), sources `ContentTypesSitemapSource`, `CustomPathSitemapSource`, `SitemapIndexSource`. Purging only deletes cache files that are regenerated on demand, so it is not treated as destructive. `saveSitemap` with `enabled` covers the Toggle action.

**Test prompts:**

- "Which sitemaps do we have and what is in them?"
- "Create a sitemap for blog posts and articles, updated daily with priority 7."
- "Add /pricing and /contact to the main sitemap as custom paths."
- "Clear the sitemap cache."

---

## No tools needed

| Module | Reason | Settings group (if any) |
| --- | --- | --- |
| `OrchardCore.ContentFields` (also `OrchardCore.ContentFields.Indexing.SQL`, `OrchardCore.ContentFields.Indexing.SQL.UserPicker`) | Only field types (Boolean, Text, Html, Link, MultiText, Numeric, Date, DateTime, Time, Youtube, ContentPicker, LocalizationSetContentPicker, UserPicker), their settings drivers and index providers. Values are set with the content tools, settings with the content-definition tools (`saveContentPartField`, `getContentDefinitionOptions`). The three picker search endpoints (`ContentFields/SearchContentItems`, `SearchLocalizationSets`, `SearchUsers`) are covered by `listContentItems` and `searchForUsers`. | none |
| `OrchardCore.ContentPreview` | Live preview while editing (`PreviewController.Draft` / `Display`, permission `PreviewContent`); rendering only. `PreviewPartSettings.Pattern` is a type-part setting. | none |
| `OrchardCore.Title` | `TitlePart` and `TitlePartSettings` (`Options`, `Pattern`, `RenderTitle`, `Placeholder`); content and definition tools cover it. | none |
| `OrchardCore.Alias` | `AliasPart` and `AliasPartSettings`; content and definition tools cover it. Lookup by alias is `findContentItemByHandle` (Autoroute section). | none |
| `OrchardCore.HomeRoute` | Routing only, no UI. The home page is set with `setHomepage` (Autoroute section). | none (site property `HomeRoute`) |
| `OrchardCore.Html` | `HtmlBodyPart`, `HtmlBodyPartSettings.SanitizeHtml` and editor settings; content and definition tools cover it. | none |
| `OrchardCore.Markdown` | `MarkdownBodyPart`, `MarkdownField`, `SanitizeHtml` settings; pipeline configured in appsettings (`OrchardCore:Markdown:Extensions`). Content and definition tools cover it. | none |
| `OrchardCore.Widgets` | Only `WidgetsListPart`, `WidgetMetadata` and `WidgetsListPartSettings.Zones`; no menu or settings. Covered by the nested-item tools (Flows section) and the Layers tools. | none |
| `OrchardCore.Feeds` | One public endpoint (`FeedController.Index`) that renders RSS; feed options live on the list content item (`DisableRssFeed`, `FeedProxyUrl`, `FeedItemsCount`) and are set with the content tools. | none |
| `OrchardCore.Forms` | Form widgets (`Form`, `Label`, `Input`, `TextArea`, `Select`, `Button`, `Validation`, `ValidationSummary`) are content items placed in a Flow: content tools plus the nested-item tools. Its workflow activities (validate form, bind model state, add model error, redirect to form location) are reached through the workflow tools. The module stores no submissions. | none |
| `OrchardCore.Spatial` | `GeoPointField` (`Latitude`, `Longitude`) only; content tools cover it. | none |
| `OrchardCore.XmlRpc` (also `OrchardCore.RemotePublishing`) | Machine endpoint for MetaWeblog clients such as Open Live Writer; no admin UI. | none |
| `OrchardCore.Demo` (also `OrchardCore.Demo.Foo`) | Developer sample: placeholder menu items, sample controllers and pages. | none |
| `OrchardCore.Mvc.HelloWorld` | Developer sample with one controller. | none |
| `OrchardCore.Contents.VersionPruning` | Settings page plus a background task; no manual action in the module. "Run now" belongs to the Background Tasks tools. | `ContentVersionPruningSettings` / `ManageContentVersionPruningSettings` |
| `OrchardCore.Contents.QuickNavigation` | Adds recent items to the admin quick navigation palette. | `admin` (`ContentQuickNavigationSettings.MaxItems`) / `ManageAdminSettings` |
| `OrchardCore.Contents.FileContentDefinition` | Storage choice for definitions, enabled as a feature; no UI. | none |
| `OrchardCore.ContentLocalization.ContentCulturePicker`, `OrchardCore.ContentLocalization.Sitemaps` | Settings pages only (picker) and sitemap metadata (no UI). | `ContentCulturePicker`, `ContentRequestCultureProvider` / `ManageContentCulturePicker` |
| `OrchardCore.Taxonomies.ContentsAdminList` | Settings page only. | `taxonomyContentsAdminList` / `ManageTaxonomy` |
| `OrchardCore.Sitemaps.RazorPages`, `OrchardCore.Sitemaps.Cleanup` | Developer support and a background task. | none |
| Admin menu nodes `ContentTypesAdminNode` (Contents) and `ListsAdminNode` (Lists) | Node types for the Admin Menu module; they belong with that module's tools (other group). | none |

## Proposed agents for this group

| Agent | Purpose (one sentence) | Tools | Could merge with |
| --- | --- | --- | --- |
| ContentAgent (existing, re-scoped) | Finds, reads, creates, edits, translates, schedules, versions and deletes content items, including the widgets and items nested inside them. | Existing: `getContentItemById`, `getSampleContentItemForContentType`, `createOrUpdateContentItem` (+ its dependency `getContentItemSchema`), `cloneContentItem`, `publishContentItem`, `unpublishContentItem`, `deleteContentItem`, `getLinkForContentItem`, and read-only `listContentTypesDefinitions`, `getContentTypeDefinition`. New: `listContentItems` (replaces `searchForContentItems`), `listContentItemVersions`, `restoreContentItemVersion`, `discardContentItemDraft`, `changeContentItemOwner`, `listContentItemLocalizations`, `localizeContentItem`, `moveListItem`, `listNestedContentItems`, `saveNestedContentItem`, `removeNestedContentItem`, `scheduleContentItem`, `findContentItemByHandle`, `getTaxonomyTerms`. Total 25 with the schema tool: at the limit. If it must shrink, move the three nested-item tools and `moveListItem` to SiteStructureAgent. | Media (picking images for content), Content Transfer / import-export, Queries |
| ContentTypeAgent (new, split out of ContentAgent) | Designs content types: creates and changes types, parts and fields with their settings, order, editors and display modes, and removes them. | Existing: `listContentTypesDefinitions`, `getContentTypeDefinition`, `listContentPartsDefinitions`, `getContentPartDefinition`, `listContentFieldDefinitions`, `applyContentTypeDefinitionFromRecipe`, `removeContentTypeDefinition`, `removeContentPartDefinition`. New: `getContentDefinitionOptions`, `saveContentTypePart`, `removeContentTypePart`, `saveContentPartField`, `removeContentPartField`, `reorderContentTypeElements`. Total 14. | Placement / Templates / Shapes, GraphQL and indexing settings per type, Custom Settings, CrestApps content fields |
| SiteStructureAgent (new) | Manages what the site is built from around the content: menus, taxonomies, site-wide widgets and layers, zones and the home page. | New: `getMenuTree`, `saveMenuItem`, `deleteMenuItem`, `getTaxonomyTerms`, `saveTaxonomyTerm`, `deleteTaxonomyTerm`, `listLayers`, `saveLayer`, `deleteLayer`, `setLayerWidgetPlacement`, `setHomepage`, `findContentItemByHandle`. Shared content tools it needs because delegation is one level deep: `listContentItems`, `getContentItemById`, `createOrUpdateContentItem` (+ `getContentItemSchema`), `publishContentItem`, `deleteContentItem`. Total 18. | Admin Menu, Navigation, Themes, URL Rewriting, Rules |
| SeoAgent (new) | Manages search-engine facing configuration: sitemaps, sitemap cache, robots.txt and structured data. | New: `listSitemaps`, `saveSitemap`, `deleteSitemap`, `purgeSitemapCache`, `getRobotsTxt`. Existing CrestApps Seo tools: `getSchemaOrgOptions`, `configureContentTypeSchemaOrg`, `configureSiteStructuredData`. Generic: `getSiteSettings`, `updateSiteSettings` (group `robotsSettings`). Total 10. | CrestApps Seo / advanced SEO, URL Rewriting, Search and indexing |
| DeploymentAgent | Receives the two content deployment actions from this group. | `addContentItemsToDeploymentPlan`, `exportContentItemsToDeploymentTarget`. | Deployment, Deployment.Remote, Recipes |

New tools proposed in this group: 37 (Contents 5, Contents deployment 2, Content Types 6, Content Localization 2, Autoroute/Home Route 2, Lists 1, Flows/Widgets 3, Layers 4, Taxonomies 3, Menu 3, Publish/Archive Later 1, SEO 1, Sitemaps 4).

## Open questions

- Authorization: should the existing content and content-definition tools get Orchard permission checks in the same change as the new tools? Today an assistant with `AccessAITool` for `deleteContentItem` can delete any published item regardless of the user's content permissions. New tools that check permissions next to old tools that do not would be inconsistent.
- `searchForContentItems`: confirm by test that the term is ignored (the code passes an empty filter). Replace it by `listContentItems`, or keep the name and fix it? The ContentAgent template's `ToolNames` must change either way.
- Drafts: confirm that the existing get/clone/delete/unpublish/link tools should default to the latest version (the admin UI behaviour) instead of the published one.
- Bulk actions: extend `publishContentItem` / `unpublishContentItem` / `deleteContentItem` with `contentItemIds[]` (recommended, no new tools) or add separate bulk tools? A bulk delete should show the titles in the approval prompt and have a per-call limit.
- `changeContentItemOwner` is classed Security (it changes who may edit an item under the own-content permissions). If the roster treats it as a plain Write, drop the approval.
- `restoreContentItemVersion` replaces the current draft as the latest version (the old draft stays in the version list). Is that acceptable without approval?
- `removeNestedContentItem`, `deleteMenuItem` and `deleteTaxonomyTerm` are Destructive by the rules but act on a draft for draftable/versionable types, where the previous version can be restored. Keep approval for all, or only when the parent type is not versionable?
- `saveLayer`: allow `JavascriptCondition` (server-side script evaluated on every request) or reject it in the tool?
- `setHomepage` changes the site for all visitors immediately. Write without approval by the rules; should it be approval-gated anyway?
- Menu and taxonomy tree logic and the Flow/Bag/WidgetsList edit logic exist only inside controllers and display drivers. The plan needs small services extracted first (in `CrestApps.OrchardCore.AI.Agent`, or contributed upstream). The same applies to editor/display-mode discovery (in Razor views), layer validation (private in the controller) and export-to-deployment-target item selection (bound to the HTTP request).
- `getContentDefinitionOptions` needs a source for "which settings classes does this part/field accept". Known from code in this group: type `ContentTypeSettings`, `FullTextAspectSettings`; part `ContentPartSettings`; type-part `ContentTypePartSettings`, `CommonPartSettings`, `AuditTrailPartSettings`, `TitlePartSettings`, `AliasPartSettings`, `AutoroutePartSettings`, `HtmlBodyPartSettings`, `MarkdownBodyPartSettings`, `ListPartSettings`, `FlowPartSettings`, `BagPartSettings`, `WidgetsListPartSettings`, `PreviewPartSettings`, `SeoMetaPartSettings`, `HtmlMenuItemPartSettings`; field `ContentPartFieldSettings`, per-field settings in `OrchardCore.ContentFields/Settings`, `MarkdownFieldSettings`, `TaxonomyFieldSettings`, `TaxonomyFieldTagsEditorSettings`, `GeoPointFieldSettings`. Other modules (media, CrestApps fields) add more; a registry or a convention is needed.
- `createOrUpdateContentItem` and welded parts (`LayerMetadata`, `ContainedPart`, `TermPart`): it is unverified whether an object for a part that is not in the type definition survives the payload validation and merge. If it does, the dedicated tools are still safer; if it does not, they are required.
- ContentAgent reaches 25 tools. Decide whether the nested-item tools and `moveListItem` stay there or move to SiteStructureAgent, and whether `getTaxonomyTerms` and `findContentItemByHandle` may be listed on two agents.
- Version pruning and the Publish Later / Archive Later tasks depend on the Background Tasks module (other group). `scheduleContentItem` should warn when the scheduling task is disabled; which tool exposes task state?
