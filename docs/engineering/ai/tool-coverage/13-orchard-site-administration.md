# Orchard Core: site administration and infrastructure

Part of the [AI tool coverage plan](README.md). Nothing in this file is built yet. The design rules, the permission and approval model and the test plan that apply to every tool below are in [01-foundation.md](01-foundation.md) and [03-testing.md](03-testing.md); the final agent roster is in [02-agents.md](02-agents.md) and wins over the agent notes in this file.

Scope: 26 modules under `OrchardCore/src/OrchardCore.Modules/`. Everything below was read from code in this checkout. Where a module's `Manifest.cs` declares no explicit feature, the feature id is the module id.

Three findings apply to the whole group and are repeated where they matter:

1. **The existing tools in `CrestApps.OrchardCore.AI.Agent` do not check any Orchard permission.** A grep for `AuthorizeAsync` / `IAuthorizationService` in that module returns nothing, and `OrchardCorePermissions.cs` (mirrored `ManageTenants`, `ManageFeatures`, `ManageRecipes`…) is never used. Access is gated only by the per-tool permission `AccessAITool_{toolName}` (`OrchardCoreAIToolAccessEvaluator`). The tenant tools add an `IsDefaultShell()` check, nothing else. Other CrestApps modules do it properly (e.g. `CrestApps.OrchardCore.Seo/AI/ConfigureSiteStructuredDataTool.cs` reads `IHttpContextAccessor.HttpContext.User` and calls `IAuthorizationService.AuthorizeAsync`). Every tool in this file names the permission it must check; the existing ones need the same fix.
2. **`AI.Agent` references no Orchard Core module package**, only abstractions/core packages (`OrchardCore.Abstractions`, `OrchardCore.DisplayManagement`, `OrchardCore.Deployment.Core`, `OrchardCore.Setup.Core`…). Several services needed below live inside module assemblies (`IAdminMenuService`, `BackgroundTaskManager`, `FeatureProfilesManager`, `RemoteInstanceService`, `RemoteClientService`, `ITranslationsManager`, `CustomSettingsService`). Each section says whether an abstractions-only path exists.
3. **`applySiteSettings` is an unrestricted recipe import without approval.** `ApplySystemSettingsTool` extends `ImportRecipeBaseTool` and passes the whole recipe to `ProcessRecipeAsync`, which validates against the root recipe schema (all steps) and runs it. It is registered without `.RequireApproval()`, while `importOrchardCoreRecipe` (same code) requires approval.

---

## Settings — `OrchardCore.Settings` (always enabled) — design input for the generic settings tools

**What an admin can do in the UI:**

- Settings → General (`Admin/Settings/general`, tabs Site, Resources, Cache): site name, page title format, base URL, time zone, page size, allow page size selection, page size options, use CDN, CDN base URL, resource debug mode, append version, cache mode. Saving always reloads the tenant (`IShellReleaseManager.RequestRelease()`).
- Settings → Debugging (`Admin/Settings/debugging`): write shape debug information.
- Every other settings page of every module is the same controller: `OrchardCore.Settings/Controllers/AdminController.cs`, route `Admin/Settings/{groupId}`. GET builds the editor with `IDisplayManager<ISite>.BuildEditorAsync(site, updater, false, groupId)`; POST calls `UpdateEditorAsync(site, updater, false, groupId, "")` and, when `ModelState.IsValid`, `ISiteService.UpdateSiteSettingsAsync(site)`. A group with no editor shapes returns 404.

**Already covered:** `applySiteSettings` (runs any recipe, see finding 3), recipe step `Settings` (usable through `importOrchardCoreRecipe`; CrestApps describes it in `Schemas/Steps/SettingsRecipeStep.cs` plus 75 `ISiteSettingsSchemaDefinition` fragments keyed by settings class name), `listTimeZones`. Deployment steps `SiteSettingsDeploymentStep` and `SiteSettingsPropertyDeploymentStep<T>` export settings.

**Settings groups:** `ISite` core properties / group `general` / permission `ManageGeneralSettings` / secrets: none editable in the UI (`SiteSalt` and `SuperUser` exist on `ISite` and are settable by the recipe step; never expose them). `DebugSettings` / group `debugging` / permission `ManageDebuggingSettings` / secrets: none. Full list for all modules below.

### How authorization works for a settings page (code facts)

- The controller checks `SettingsPermissions.ManageGroupSettings` (permission name `ManageResourceSettings`, implied by `ManageSettings`) with the group id as resource. `SiteSettingsAuthorizationHandler` grants it when the user has a permission registered for that group in `SiteSettingsPermissionOptions.GroupPermissions` (`services.AddSiteSettingsPermission(groupId, permission)`).
- Only three groups are registered there: `general`, `debugging`, `email`. For every other group the user needs `ManageSettings` to reach the page, and then each display driver checks its own permission (against `IHttpContextAccessor.HttpContext.User`) and silently returns `null` when it fails.
- So the effective permission of a group is: (`ManageSettings` or a registered group permission) AND the driver's permission. A tool should check the driver's permission from the table below; `ManageSettings` implies none of them.

### How a generic tool can enumerate groups (four sources, none complete on its own)

| Source | What it gives | Limit |
| --- | --- | --- |
| `SiteSettingsPermissionOptions.GroupPermissions` | group id → permissions | Only `general`, `debugging`, `email` are registered. |
| `SiteDisplayDriver<TSettings>.SettingsGroupId` | group id per driver, settings CLR type | `protected abstract`, so reflection is needed; `DefaultSiteSettingsDisplayDriver`, `ButtonsSettingsDisplayDriver` and `CustomSettingsDisplayDriver` are plain `DisplayDriver<ISite>`; permission is not exposed. |
| Admin menu: `INavigationManager.BuildMenuAsync(NavigationConstants.AdminId, actionContext)` | every settings page is a menu item whose `RouteValues` are `area=OrchardCore.Settings`, `groupId=X`, with `Permissions` and a localized title; the menu is already filtered for the current user; includes CrestApps groups and custom settings types | Needs an `ActionContext` (HTTP request). Pattern verified in `AdminMenu.cs` of Settings, Admin, Localization, CustomSettings; other modules unverified one by one. Gives no field list. |
| CrestApps `ISiteSettingsSchemaDefinition` (75 definitions) | JSON schema with descriptions per settings class | Keyed by class name, not group; no group id, no permission, secrets only described in text (`ProtectedCredential` / `SecretName` helpers). Missing for `ContentQuickNavigationSettings`, `ContentVersionPruningSettings`, `DebugSettings`, `MediaApiSettings`, `OpenApiSettings`, `UserSecuritySettings`. |

Recommendation for `listSiteSettingsGroups`: enumerate from the admin menu (zero maintenance, permission-filtered, covers third-party groups), and enrich with a small CrestApps descriptor registry (group id → settings class names, permission, secret fields, reload flag) for the groups in the table below.

### Which mechanism is safest

| Mechanism | Read | Update | Verdict |
| --- | --- | --- | --- |
| Settings recipe step / `applySiteSettings` | No read. | `SettingsStep` sets known `ISite` properties and, for any other key, does `site.Properties[key] = value.Clone()`: it replaces the whole settings object (fields left out fall back to defaults), runs no validation, no driver permission check, no tenant reload, none of the driver side effects, and a secret must already be data-protection ciphertext or a secret name. It also accepts `SuperUser`, `SiteSalt` and `HomeRoute`. Through `ImportRecipeBaseTool` it is deferred (`DeferredWork.Add`) and the tool answers "successfully imported" before it runs, so errors never reach the model. | Least safe. Keep for bulk provisioning only; do not build `updateSiteSettings` on it. |
| `ISiteService` + JSON binding (`GetSiteSettingsAsync`, `site.As<T>()`, `site.Properties[name]`, merge, `UpdateSiteSettingsAsync`) | Safe and cheap: no side effects, cached immutable document. Secrets must be masked from a descriptor. | Works, but bypasses everything the drivers do: validation (base URL, page sizes, culture list, sender address, OpenID authority…), secret protection with the right protector purpose (e.g. `SmtpOptionsConfiguration.ProtectorName`), cross-settings effects (enabling SMTP sets `EmailSettings.DefaultProviderName`), options refresh (`IOptionsUpdateNotifier` in `DebugSettingsDisplayDriver`) and `IShellReleaseManager.RequestRelease()` (16 drivers call it). Each of these would have to be re-implemented per group and would drift. | Safest for reads. Acceptable for writes only for plain groups with no secret, no validation and no reload. |
| Display drivers (replay of `AdminController.IndexPost`) | `BuildEditorAsync` returns the edit view models already filled, permission-filtered, with secrets reduced to `SecretInputViewModel { Source, SecretName, HasValue }` (no value). | Same code path as the UI: driver permission, validation errors in `ModelState` (can be returned to the model), secret protection through `SecretInputViewModel.UpdateAsync`, side effects, tenant reload; nothing is saved when `ModelState` is invalid. Needs a JSON-backed `IUpdateModel` (the interface has only `TryUpdateModelAsync`, `TryValidateModel`, `ModelState`); the driver prefix is `ISite.{SettingsClassName}`, so tool input can be keyed by settings class name. | Safest for updates. |

Verdict: read with `ISiteService`, update through the display drivers, never through the recipe step. The catch that decides the final design: drivers bind view models, not the stored settings, and the two differ (`LocalizationSettingsViewModel.SupportedCultures` is a JSON-encoded string and `FallBackToParentCultures` maps to `FallBackToParentCulture`; `SiteSettingsViewModel.TimeZone` maps to `TimeZoneId` and `PageSizeOptions` is a comma-separated string; secrets are `SecretInputViewModel`). The values and schema returned by `getSiteSettings` must therefore have the same shape `updateSiteSettings` accepts. Two workable designs:

- Design A (parity, no per-group code): both tools go through the drivers. `getSiteSettings` serializes the editor view models captured from `BuildEditorAsync` and derives the schema from the view model CLR types (skip `[BindNever]` members); `updateSiteSettings` merges the patch over those values inside the JSON `IUpdateModel` (drivers assign every field, so a partial patch must be merged first). Needs a spike: confirm the view models are initialized when the editor is built, not when it is rendered (`ShapeResult` initializer), and that drivers reading `HttpContext.User` see the chat user.
- Design B (settings-shaped, curated): both tools use `ISiteService` with the CrestApps schemas, plus a per-group hook that re-applies validation, secret protection and reload. More code and drift, but better field descriptions for the model.

Either way: treat a group with no editor shapes as "not found or not allowed" (the controller does), return `ModelState` errors verbatim, report `reloadRequested`, mask secrets on read and accept either a new plain value or a secret name on write, and exclude `SuperUser`, `SiteSalt`, `HomeRoute`.

### All settings groups in this checkout (Orchard Core `src`, 39 static groups + dynamic custom settings)

Settings class = property name in `ISite.Properties`. Permission = name string from code. "Reload" = the driver calls `IShellReleaseManager.RequestRelease()`.

| Group id | Settings class(es) | Permission | Secret fields | Reload | Module |
| --- | --- | --- | --- | --- | --- |
| `general` | `ISite` typed properties (SiteName, PageTitleFormat, BaseUrl, TimeZoneId, PageSize, AllowPageSizeSelection, PageSizeOptions, UseCdn, CdnBaseUrl, ResourceDebugMode, AppendVersion, CacheMode) | `ManageGeneralSettings` | none in UI | yes | Settings |
| `debugging` | `DebugSettings` | `ManageDebuggingSettings` | none | no | Settings |
| `admin` | `AdminSettings`, `ContentQuickNavigationSettings` | `ManageAdminSettings` | none | no | Admin, Contents |
| `AuditTrail` | `AuditTrailSettings`, `AuditTrailTrimmingSettings`, `ContentAuditTrailSettings` | `ManageAuditTrailSettings` | none | no | AuditTrail, Contents |
| `azureAISearch` | `AzureAISearchDefaultSettings` | `ManageAzureAISearchSettings` | `ApiKey` / `ApiKeySecretName` | no | AzureAI |
| `ContentCulturePicker` | `ContentCulturePickerSettings` | `ManageContentCulturePicker` | none | no | ContentLocalization |
| `ContentRequestCultureProvider` | `ContentRequestCultureProviderSettings` | `ManageContentCulturePicker` | none | no | ContentLocalization |
| `ContentVersionPruningSettings` | `ContentVersionPruningSettings` | `ManageContentVersionPruningSettings` | none | no | Contents |
| `ExportContentToDeploymentTarget` | `ExportContentToDeploymentTargetSettings` | `ManageDeploymentPlan` | none | no | Contents |
| `email` | `EmailSettings`, `SmtpSettings`, `AzureEmailSettings` | `ManageEmailSettings` | `SmtpSettings.Password` / `PasswordSecretName`; `AzureEmailSettings.ConnectionString` / `ConnectionStringSecretName` | no | Email, Email.Smtp, Email.Azure |
| `sms` | `SmsSettings`, `TwilioSettings`, `AzureSmsSettings` | `ManageSmsSettings` | `TwilioSettings.AuthToken` / `AuthTokenSecretName`; `AzureSmsSettings.ConnectionString` / `ConnectionStringSecretName` | no | Sms, Sms.Azure |
| `OrchardCore.Facebook` | `FacebookSettings` | `ManageFacebookApp` | `AppSecret` / `AppSecretSecretName` | yes | Facebook |
| `OrchardCore.Facebook.Login` | `FacebookLoginSettings` | `ManageFacebookApp` | none | yes | Facebook |
| `facebook-pixel` | `FacebookPixelSettings` | `ManageFacebookApp` | `ConversionsApiAccessToken` (data-protected) | no | Facebook |
| `OrchardCore.GitHub.Authentication` | `GitHubAuthenticationSettings` | `ManageGitHubAuthentication` | `ClientSecret` / `ClientSecretSecretName` | yes | GitHub |
| `OrchardCore.Google.GoogleAuthentication` | `GoogleAuthenticationSettings` | `ManageGoogleAuthentication` | `ClientSecret` / `ClientSecretSecretName` | yes | Google |
| `OrchardCore.Google.Analytics` | `GoogleAnalyticsSettings` | `ManageGoogleAnalytics` | none | no | Google |
| `OrchardCore.Google.TagManager` | `GoogleTagManagerSettings` | `ManageGoogleTagManager` | none | no | Google |
| `OrchardCore.Microsoft.Authentication.AzureAD` | `AzureADSettings` | `ManageMicrosoftAuthentication` | none | yes | Microsoft.Authentication |
| `OrchardCore.Microsoft.Authentication.MicrosoftAccount` | `MicrosoftAccountSettings` | `ManageMicrosoftAuthentication` | `AppSecret` / `AppSecretSecretName` | yes | Microsoft.Authentication |
| `OrchardCore.Twitter` | `TwitterSettings` | `ManageTwitter` to save (the same driver checks `ManageTwitterSignin` to show the editor) | `ConsumerSecret` / `ConsumerSecretSecretName`; `AccessTokenSecret` / `AccessTokenSecretSecretName` | yes | Twitter |
| `OrchardCore.Twitter.Signin` | `TwitterSigninSettings` | `ManageTwitterSignin` | none | yes | Twitter |
| `OrchardCore.OpenId.Client` | `OpenIdClientSettings` | `ManageClientSettings` | `ClientSecret` / `ClientSecretSecretName` | yes | OpenId |
| `Https` | `HttpsSettings` | `ManageHttps` | none | yes | Https |
| `ReverseProxy` | `ReverseProxySettings` | `ManageReverseProxySettings` | none | yes | ReverseProxy |
| `SecurityHeaders` | `SecuritySettings` | `ManageSecurityHeadersSettings` | none | yes | Security |
| `recaptcha` | `ReCaptchaSettings` | `ManageReCaptchaSettings` | `SecretKey` (stored as a plain string in the model; mask it) | no | ReCaptcha |
| `localization` | `LocalizationSettings` | `ManageCultures` | none | yes | Localization |
| `mediaApi` | `MediaApiSettings` | `ManageMediaApiSettings` | none | yes | Media |
| `openapi` | `OpenApiSettings` | `ManageOpenApi` | none | yes | OpenApi |
| `search` | `SearchSettings` | `ManageSearchSettings` | none | no | Search |
| `robotsSettings` | `RobotsSettings`, `SitemapsRobotsSettings` | `ManageSeoSettings` | none | no | Seo, Sitemaps |
| `zones` | `LayerSettings` | `ManageLayers` | none | no | Layers |
| `taxonomyContentsAdminList` | `TaxonomyContentsAdminListSettings` | `ManageTaxonomy` (constant `Permissions.ManageTaxonomies`) | none | no | Taxonomies |
| `userLogin` | `LoginSettings`, `ExternalLoginSettings`, `TwoFactorLoginSettings`, `RoleLoginSettings`, `AuthenticatorAppLoginSettings`, `EmailAuthenticatorLoginSettings`, `SmsAuthenticatorLoginSettings`, `UserSecuritySettings` | `ManageUsers` (security critical) | none | no | Users |
| `userRegistration` | `RegistrationSettings`, `ExternalRegistrationSettings` | `ManageUsers` | none | no | Users |
| `userChangeEmail` | `ChangeEmailSettings` | `ManageUsers` | none | no | Users |
| `userResetPassword` | `ResetPasswordSettings` | `ManageUsers` | none | no | Users |
| `WorkflowTrimmingSettings` | `WorkflowTrimmingSettings` | `ManageWorkflowSettings` | none | no | Workflows |
| `{ContentTypeName}` (dynamic) | content item JSON of a content type with stereotype `CustomSettings` | `ManageCustomSettings_{ContentTypeName}` | depends on the type | no | CustomSettings |

Not site-settings groups (own controllers and documents, out of the generic trio): OpenID server and validation settings (`OpenIdSecretSettingsDisplayDriver : DisplayDriver<OpenIdServerSettings>`, permissions `ManageServerSettings`, `ManageValidationSettings`; recipe steps `OpenIdServerSettings`, `OpenIdValidationSettings`). The CrestApps repository adds about 28 more group ids of its own (count from a grep of `GroupId` constants, not enumerated here).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listSiteSettingsGroups` (planned generic) | Read | no | none beyond admin access; returns only groups the caller may manage | Lists the site settings pages the current user can manage, with group id, title, menu path and the settings each contains. Call it first to find the group id for `getSiteSettings`. | SiteConfigurationAgent |
| `getSiteSettings` (planned generic) | Read | no | the group's permission (table above) | Returns the current values of one settings group and a JSON schema of its editable fields. Secrets are never returned, only whether a value is set and the secret name. | SiteConfigurationAgent |
| `updateSiteSettings` (planned generic) | Security | yes | the group's permission (table above) | Changes fields of one settings group; send only the fields to change, in the shape returned by `getSiteSettings`. Returns validation errors, or the saved values and whether the site is reloading. | SiteConfigurationAgent |

**Implementation notes:** `ISiteService`, `IDisplayManager<ISite>`, `IUpdateModel`, `IShellReleaseManager` are all in packages `AI.Agent` already references. `updateSiteSettings` is marked Security because many groups control authentication, HTTPS, security headers, reverse proxy trust and credentials; one tool has one approval flag, so approval per group is an open question. Drivers authorize `IHttpContextAccessor.HttpContext?.User`, so the tool only works inside the user's request. After a save that requests a release the tenant restarts at the end of the request; the chat connection may reconnect (unverified). Retire `applySiteSettings` once the trio exists, or at least add approval and restrict it to the `settings` and `custom-settings` steps.

**Test prompts:**

- "Change the site name to Contoso Portal and set the time zone to Central European Time."
- "Which settings pages can I manage on this site?"
- "Turn on the page size selector in admin lists and offer 20, 50 and 100."
- "What is our SMTP host and is a password configured?"

---

## Custom Settings — `OrchardCore.CustomSettings`

**What an admin can do in the UI:**

- Settings → one entry per content type with stereotype `CustomSettings` (`Admin/Settings/{ContentTypeName}`): edit the content item editor of that type and save. The item is not stored as a content item; it is saved as JSON in `ISite.Properties[ContentTypeName]`.

**Already covered:** recipe step `custom-settings` (replaces `site.Properties[key]` with the given JSON; CrestApps schema `CustomSettingsRecipeStep`). The content tools do not cover it: the item never reaches the content store. Creating the settings type itself is covered by the content definition tools (stereotype `CustomSettings`).

**Settings groups:** content item JSON / group `{ContentTypeName}` / permission `ManageCustomSettings_{ContentTypeName}` (template in `OrchardCore.CustomSettings/Permissions.cs`, created with `new Permission("ManageResourceSettings")` as its implying permission) / secrets: depends on the type.

No dedicated tool. The generic trio must support these groups.

**Implementation notes:** `CustomSettingsService` is in the module assembly, but the same result is reachable with `IContentDefinitionManager` (types whose stereotype is `CustomSettings`), `ISiteService` and the content item schema service CrestApps already has (`getContentItemSchema` / `ContentItemPreparationService`). The UI saves through `IContentItemDisplayManager.UpdateEditorAsync` then `site.Properties[type] = JObject.FromObject(contentItem)`; a JSON view model replay of nested part and field editors is fragile, so for these groups use content-item-shaped JSON plus content validation instead of driver replay.

**Test prompts:**

- "Show me the values of the Store Policies custom settings."
- "In the Footer Settings, change the copyright text to '© 2026 Contoso'."

---

## Admin — `OrchardCore.Admin`

**What an admin can do in the UI:**

- Settings → Admin (`Admin/Settings/admin`): theme toggler, menu filter, quick navigation palette, "New" menu, titles in the top bar, breadcrumb, menu behavior (Focused or Persistent), list layout (List or Grid), actions layout (Buttons or Menu), allow users to pick a list layout.
- Quick navigation palette (Ctrl+K): `QuickNavigationController.Index` (`Admin/QuickNavigation/Index`) returns the admin pages the user can open (title, menu path, href) from every `QuickNavigationSource`; `AdminMenuItemNavigationSource` walks the authorized admin menu.
- Navbar: visit site link.

**Already covered:** settings through `applySiteSettings` (CrestApps `AdminSettingsSchema`). Nothing for finding an admin page.

**Settings groups:** `AdminSettings` / group `admin` / permission `ManageAdminSettings` / secrets: none. (`ContentQuickNavigationSettings` from OrchardCore.Contents shares the group.)

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `findAdminPage` | Read | no | `AccessAdminPanel` | Searches the admin menu the current user can see and returns matching pages with their menu path and link. Use it to tell the user where something is configured or to give a link to an admin page. | direct |

**Implementation notes:** `INavigationManager.BuildMenuAsync(NavigationConstants.AdminId, actionContext)` (package `OrchardCore.Navigation.Core`), same walk as `AdminMenuItemNavigationSource`; the result is already permission-filtered and must never be cached across users (comment in that class). Needs an active HTTP request. The same menu walk is the enumeration source proposed for `listSiteSettingsGroups`. Optionally include the other `QuickNavigationSource`s (content types) through the public `QuickNavigationSource` API.

**Test prompts:**

- "Where do I configure reCAPTCHA?"
- "Give me the link to the background tasks page."
- "Switch the admin lists to the grid layout." (should use `updateSiteSettings` with group `admin`)

---

## Admin Dashboard — `OrchardCore.AdminDashboard`

**What an admin can do in the UI:**

- Dashboard (admin home): see the widgets (`AccessAdminDashboard`, per widget `ViewContent`).
- Manage Dashboard (`Admin/dashboard/manage`): add a widget of any content type with stereotype `DashboardWidget`, edit or delete a widget, drag to reorder and resize; the page posts `Update` with `ContentItemId`, `Position`, `Width`, `Height` per widget, saved in place on the latest and published versions (no new version).

**Already covered:** content tools cover creating, editing, publishing and deleting a widget (a content item; `DashboardPart { Position, Width, Height }` is attached to types with stereotype `DashboardWidget` by `DashboardPartContentTypeDefinitionHandler`). Not covered: finding widgets (`searchForContentItems` only takes `term` and `pageNumber`, no content type or stereotype filter) and arranging them in one step.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listAdminDashboardWidgets` | Read | no | `AccessAdminDashboard` | Lists the widgets on the admin dashboard with their content type, position, width and height, and the content types that can be added as widgets. Use it before adding or rearranging widgets. | SiteStructureAgent |
| `arrangeAdminDashboardWidgets` | Write | no | `ManageAdminDashboard` | Sets the position, width and height of dashboard widgets by content item id. Only moves and resizes existing widgets; create or delete them with the content tools. | SiteStructureAgent |

**Implementation notes:** `IAdminDashboardService` is in the module assembly; the same query is `ISession.Query<ContentItem, ContentItemIndex>` for types with stereotype `DashboardWidget` (latest or published), ordered by `DashboardPart.Position`. The arrange logic exists only inside `DashboardController.Update` and must be reproduced (write `Content["DashboardPart"]` on latest and published versions, `ISession.SaveAsync`). `Width` and `Height` are doubles defaulting to 1.0; the accepted values were not read from the manage view (unverified), so take them from there before writing the tool schema.

**Test prompts:**

- "What widgets are on my admin dashboard?"
- "Move the Recent Orders widget to the top and make it full width."
- "Add a dashboard widget that shows our support phone number." (content tools, then arrange)

---

## Admin Menu — `OrchardCore.AdminMenu`

**What an admin can do in the UI:**

- Tools → Admin Menus (`Admin/AdminMenu/List`): list and search menus, create (name), edit (name), delete, bulk remove, toggle enabled.
- Edit nodes (`Admin/AdminMenu/Node/List`): tree of nodes; add a node by type (this module: Link, Placeholder; other modules can add node types, and CrestApps already has schemas for ContentTypes and Lists nodes), edit, delete, toggle enabled, drag to move (`MoveNode`). Link node: `LinkText`, `LinkUrl`, `IconClass`, `PermissionNames`. Placeholder node: `LinkText`, `IconClass`, `PermissionNames`.
- Each menu also creates a dynamic permission `ViewAdminMenu_{Name}`.

**Already covered:** recipe step `AdminMenu` creates or replaces a whole menu by id (`IAdminMenuService.SaveAsync`); CrestApps has its schema (`IAdminMenuSchemaService`, node schemas for Link, Placeholder, ContentTypes, Lists). No read and no delete.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listAdminMenus` | Read | no | `ManageAdminMenu` | Lists the custom admin menus, optionally with their full node tree and the node types available on this site. Call it before changing a menu so existing nodes are kept. | SiteConfigurationAgent |
| `saveAdminMenu` | Write | no | `ManageAdminMenu` | Creates a custom admin menu or replaces an existing one, including its name, enabled state and whole node tree. Send the complete tree: nodes left out are removed. | SiteConfigurationAgent |
| `deleteAdminMenu` | Destructive | yes | `ManageAdminMenu` | Permanently deletes a custom admin menu and all its nodes. | SiteConfigurationAgent |

**Implementation notes:** `IAdminMenuService` and `AdminMenuList` are in the module assembly (`AdminMenu`, `AdminNode` models are in `OrchardCore.AdminMenu.Abstractions`). Without a module reference, save can go through the `AdminMenu` recipe step and read through the `AdminMenuDeploymentStep`, but delete needs `IAdminMenuService.DeleteAsync`. Node JSON is polymorphic (`DocumentJsonSerializerOptions`); reuse `IAdminMenuSchemaService` for the schema and validate `[Required]` fields. Toggle and move are just fields of the tree, so they need no tool of their own.

**Test prompts:**

- "Add a 'Reports' section to the admin menu with links to /Admin/Reports/Sales and /Admin/Reports/Refunds."
- "Which custom admin menus exist and what is in them?"
- "Disable the Shortcuts admin menu without deleting it."

---

## Features — `OrchardCore.Features` (always enabled)

**What an admin can do in the UI:**

- Tools → Features (`Admin/Features`): list by category with dependencies, enable or disable one feature, bulk enable or disable (with force).
- From the Default tenant only: manage the features of another running tenant (`Admin/Features/{tenant}`), reached from the "Features" button of a tenant row.
- The page hides the action when it is not allowed: no disable for always-enabled features, category `Core`, or the application module; no enable for features enabled by dependency only, with missing dependencies, or `OrchardCore.Setup` / `OrchardCore.AutoSetup`.

**Already covered:** `listSiteFeature`, `searchSiteFeature`, `getSiteFeature`, `enableSiteFeature`, `disableSiteFeature` (all on `IShellFeaturesManager`, current tenant, themes and dependency-only features filtered out). Recipe step `Feature`.

No new tools.

**Implementation notes (gaps in the existing tools compared with the UI):**

- No permission check (`FeaturesPermissions.ManageFeatures`, name `ManageFeatures`).
- No way to target another tenant. The UI does it with `IShellHost.GetScopeAsync(settings)` and a `FeatureService` built on that scope's `IShellFeaturesManager`; add an optional `tenant` argument, Default tenant only, tenant must be running.
- The UI guards listed above are not applied; enable and disable call the manager with `force: true` and report only the ids that were asked for, not the dependencies that were enabled or the dependents that were disabled with them (`EnableFeaturesAsync` / `DisableFeaturesAsync` return the affected features).
- `getSiteFeature` has the description "Enables feature site features" (copied from the enable tool), `disableSiteFeature` "Disable features site features"; these hurt tool selection.
- `listSiteFeature` returns every feature in one payload; fold `searchSiteFeature` and `getSiteFeature` into one `listSiteFeatures` with `search`, `category`, `enabled` filters and paging, and return description and dependents (`FeatureService.GetModuleFeaturesAsync` in `OrchardCore.Features.Core` has `EnabledDependentFeatures` and `FeatureDependencies`).
- `enableSiteFeature` has no approval and `disableSiteFeature` has it. That matches the rules (enable is Write, disable can break dependents), but enabling a feature runs its migrations and can open new endpoints, so the agent instructions should make the model name the feature and its dependencies before enabling.

**Test prompts:**

- "Is the Audit Trail feature enabled?"
- "Enable URL Rewriting and tell me what else got enabled with it."
- "Turn off the Mini Profiler on the tenant named blog."

---

## Tenants — `OrchardCore.Tenants`, `OrchardCore.Tenants.FeatureProfiles`, `OrchardCore.Tenants.Distributed`, `OrchardCore.Tenants.FileProvider`

**What an admin can do in the UI (Default tenant only):**

- Multi-Tenancy → Tenants (`Admin/Tenants`): list with search (name, host, prefix), category and status filters, sort; bulk Enable or Disable.
- Create: name, description, category, URL prefix, host names, feature profiles, database provider, connection string, table prefix, schema, setup recipe; "Create and setup" redirects to the tenant's setup page with a token.
- Edit: description, category, URL prefix, host, feature profiles; database and recipe presets only while the tenant is uninitialized (the connection string is shown redacted).
- Per tenant: Setup (link with a 24 hour token built from the tenant's `Secret`), Enable, Disable, Reload (any tenant, including Default), Remove (only when `TenantsOptions.TenantRemovalAllowed` and the tenant is disabled or uninitialized), Features (see Features).
- Multi-Tenancy → Feature Profiles (`Admin/TenantFeatureProfiles`): list and search, create or edit (name, JSON list of rules `{ Rule: Include or Exclude, Expression }`), delete, bulk delete.
- Workflow tasks: Create, Enable, Disable, Setup tenant. HTTP API `api/tenants/{create,edit,disable,enable,remove,setup}`.
- `Tenants.Distributed` and `Tenants.FileProvider` have no UI.

**Already covered:** `createTenant`, `getTenant`, `listTenant`, `enableTenant`, `disableTenant`, `removeTenant`, `reloadTenant`, `setupTenant`, `listStartupRecipes`. Recipe step `FeatureProfiles` (create or update a profile).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `updateTenant` | Write | no | `ManageTenants` | Changes an existing tenant's description, category, URL prefix, host names and feature profiles; database settings and setup recipe can only be changed while the tenant is not set up. Only works from the Default tenant. | TenantAgent |
| `getTenantSetupLink` | Security | yes | `ManageTenants` | Returns the setup page link of a tenant that is not set up yet, with a token valid for 24 hours. Use it when the user wants to complete the setup themselves instead of giving a password in chat; anyone with the link can set up the tenant. | TenantAgent |
| `listTenantFeatureProfiles` | Read | no | `ManageTenantFeatureProfiles` | Lists the tenant feature profiles with their include and exclude rules and the tenants that use each one. | TenantAgent |
| `saveTenantFeatureProfile` | Write | no | `ManageTenantFeatureProfiles` | Creates or updates a tenant feature profile: a name and an ordered list of rules, each with a rule name (Include or Exclude) and an expression. It limits which features the tenants using the profile can enable. | TenantAgent |
| `deleteTenantFeatureProfile` | Destructive | yes | `ManageTenantFeatureProfiles` | Deletes a tenant feature profile. Check with `listTenantFeatureProfiles` which tenants still use it first. | TenantAgent |

**Implementation notes:**

- Services: `IShellHost`, `IShellSettingsManager`, `IShellRemovalManager`, `IFeatureProfilesService` (read) are in `OrchardCore.Abstractions`. `ITenantValidator`, `FeatureProfilesManager`, `TenantsOptions`, `TenantConnectionStringRedactor` are in the module assembly. Without a module reference, saving a profile works through the `FeatureProfiles` recipe step; deleting needs `FeatureProfilesManager.RemoveFeatureProfileAsync`. The setup link logic is in `HttpContextExtensions.GetEncodedUrl` plus `IDataProtectionProvider.CreateProtector("Tokens").ToTimeLimitedDataProtector()`.
- Gaps in the existing tools compared with the UI:
  - No `ManageTenants` check.
  - `createTenant` does not follow every step of the Create page, so `saveTenant` must create tenants the way `AdminController.Create` does.
  - `createTenant` skips `ITenantValidator` (name characters, one-segment prefix, duplicate host and prefix, required table prefix, table prefix and schema patterns, database connection check) and cannot set feature profiles. Its JSON schema has `additionalProperties: false` and omits `category`, `description` and `schema`, although the code reads them.
  - `removeTenant` ignores `TenantsOptions.TenantRemovalAllowed`, which the UI and the API both enforce.
  - `reloadTenant` refuses the Default tenant; the UI allows it. `reloadTenant` and `removeTenant` answer "You cannot enable the default tenant." in that case.
  - `listTenant` has no filters; `getTenant` and `listTenant` do not return the feature profiles.
  - `setupTenant` takes the admin password as an argument, so it ends up in the transcript; `getTenantSetupLink` is the alternative.
  - Names: `listTenant` should be `listTenants`. `createTenant` and `updateTenant` could become one `saveTenant`.
- All tenant tools must refuse outside the Default tenant (they do) and should not even be offered there.
- `updateTenant` changes routing of a live tenant (host, prefix) and triggers `IShellHost.UpdateShellSettingsAsync`; call it out in the agent instructions.

**Test prompts:**

- "Create a tenant called acme at the prefix /acme with the SaaS recipe and give me a link to finish the setup."
- "Change the description of the blog tenant and put it in the Marketing category."
- "Create a feature profile that hides all the search features, and apply it to the acme tenant."
- "Which tenants are disabled?"

---

## Recipes — `OrchardCore.Recipes` (always enabled), `OrchardCore.Recipes.Core`

**What an admin can do in the UI:**

- Tools → Recipes (`Admin/Recipes`): list the recipes that are not setup recipes and not tagged `hidden`, grouped by feature, and run one (`Execute(basePath, fileName)`), which then releases the tenant and shows step errors.

**Already covered:** `listNonStartupRecipes`, `executeNonStartupRecipe`, `listStartupRecipes`, `getOrchardCoreRecipeJsonSchema`, `listOrchardCoreRecipeStepsAndSchemas`, `importOrchardCoreRecipe`, `applySiteSettings`. Recipe steps of this module: `Recipes` (run named recipes), `Command` (run Orchard commands), `ReloadTenant`.

No new tools.

**Implementation notes (gaps in the existing tools compared with the UI):**

- No permission check: the UI checks `ManageRecipes` (security critical) to list and run recipes, and `Import` for JSON import.
- `importOrchardCoreRecipe` and `applySiteSettings` run through `RecipeExecutionService.ExecuteRecipeAsync` (writes `Recipe.json` to a temp folder and calls every `IDeploymentTargetHandler.ImportFromFileAsync`). Inside a request the run is deferred (`DeferredWork.Add("ai_recipe_import", …)`) and the tool has already answered "Recipe was successfully imported"; `ExecuteRecipeAsync` swallows exceptions into `false`. The model never sees step errors, while the UI's JSON Import shows `RecipeExecutionException.StepResult.Errors`. This is the main reason dedicated tools are worth having for anything the model has to verify.
- `applySiteSettings` accepts any recipe and has no approval (finding 3). A recipe can contain `command`, `Roles`, `Users`, `Secrets` and `settings` (including `SuperUser`), so both import tools are Security.
- `executeNonStartupRecipe` finds a recipe by `Name` only; the UI uses base path and file name, which is unique. `listNonStartupRecipes` does not return the owning feature or the file name.

**Test prompts:**

- "Which recipes can I run on this site?"
- "Run the Blog recipe."
- "Import this recipe JSON and tell me if any step failed." (today the tool cannot answer the second half)

---

## Deployment — `OrchardCore.Deployment`

**What an admin can do in the UI:**

- Tools → Deployments → Plans (`Admin/DeploymentPlan/Index`): list and search plans, create (name), rename, delete, bulk delete.
- Plan page (`Display/{id}`): add a step from the step types registered by enabled features (`IDeploymentStepFactory`), edit a step, delete a step, drag to reorder (`UpdateOrder`), execute the plan to a target: File Download (zip built by `ExportFileController.Execute`) or a remote instance.
- Tools → Deployments → Package Import: upload a zip or json package. JSON Import: paste a recipe; step errors are shown.
- Step types of this module: Deployment Plan (exports plans), Custom File, JSON Recipe, Recipe File (recipe metadata for the exported file).

**Already covered:** JSON Import equals `importOrchardCoreRecipe`. Recipe step `deployment` creates or updates plans by name (`IDeploymentPlanService.CreateOrUpdateDeploymentPlansAsync`); CrestApps has its schema with one definition per step type (`IDeploymentSchemaService`). Not covered: listing, deleting, executing a plan, package import.

**Settings groups:** none in this module (`ExportContentToDeploymentTargetSettings` / group `ExportContentToDeploymentTarget` / `ManageDeploymentPlan` belongs to OrchardCore.Contents).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listDeploymentPlans` | Read | no | `ManageDeploymentPlan` | Lists deployment plans, optionally filtered by name and with their ordered steps, plus the step types available on this site. Use it to find a plan before exporting or changing it. | DeploymentAgent |
| `saveDeploymentPlan` | Write | no | `ManageDeploymentPlan` | Creates a deployment plan or replaces an existing one: its name and the ordered list of steps. Send every step to keep; step types and their fields come from `listDeploymentPlans`. | DeploymentAgent |
| `deleteDeploymentPlan` | Destructive | yes | `ManageDeploymentPlan` | Permanently deletes a deployment plan. It does not touch data already exported. | DeploymentAgent |
| `exportDeploymentPlan` | Security | yes | `Export` | Runs a deployment plan and returns the generated recipe JSON, the same content as the File Download target. The output can contain any site data the plan selects, so use it only when the user asks for an export. | DeploymentAgent |

**Implementation notes:** `IDeploymentPlanService`, `IDeploymentManager`, `IDeploymentStepFactory`, `DeploymentPlan`, `DeploymentPlanResult` are in `OrchardCore.Deployment.Abstractions`; `DeploymentPermissions` is in `OrchardCore.Deployment.Core` (already referenced). Plans are YesSql documents with a numeric id (`ISession.GetAsync<DeploymentPlan>(id)`); the UI's plan list requires both `ManageDeploymentPlan` and `Export`. The export logic (temp folder, `RecipeFileDeploymentStep` metadata, zip) lives only in `ExportFileController.Execute` and must be extracted. A plan can produce extra files (media, custom files); returning only `Recipe.json` inline loses them, so the tool should say so and point to the plan page for the zip. `exportDeploymentPlan` is marked Security because a plan can dump the whole site into the conversation. Package Import is not proposed: the chat has no file channel for a zip, and JSON import is already covered.

**Test prompts:**

- "Create a deployment plan called 'Content types and settings' that exports all content definitions and the general site settings."
- "What deployment plans do we have and what does each one export?"
- "Export the Nightly Backup plan and show me the recipe."

---

## Remote Deployment — `OrchardCore.Deployment.Remote`

**What an admin can do in the UI:**

- Tools → Deployments → Remote Instances: list and search, create or edit (name, URL, client name, API key), delete, bulk delete. These are the sites this site can push to.
- Tools → Deployments → Remote Clients: list and search, create or edit (client name, API key), delete, bulk delete. These are the credentials other sites use to push to this site (`ImportRemoteInstanceController.Import`).
- Plan page: execute a plan to a remote instance (`ExportRemoteInstanceController.Execute` zips the plan output and posts it with client name and API key to the instance URL).

**Already covered:** nothing. No recipe or deployment step for remote instances or clients.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listRemoteInstancesAndClients` | Read | no | `ManageRemoteInstances` for instances, `ManageRemoteClients` for clients | Lists the remote instances this site can deploy to and the remote clients allowed to deploy to this site. API keys are never returned. | DeploymentAgent |
| `saveRemoteInstance` | Security | yes | `ManageRemoteInstances` | Creates or updates a remote instance: name, import URL, client name and API key of the target site. It defines where site data can be sent. | DeploymentAgent |
| `deleteRemoteInstance` | Destructive | yes | `ManageRemoteInstances` | Deletes a remote instance, so plans can no longer be sent to it. | DeploymentAgent |
| `saveRemoteClient` | Security | yes | `ManageRemoteClients` | Creates or updates a remote client: a client name and API key another site uses to import deployment packages into this site. Whoever holds the key can change this site. | DeploymentAgent |
| `deleteRemoteClient` | Destructive | yes | `ManageRemoteClients` | Deletes a remote client, so the other site can no longer deploy to this one. | DeploymentAgent |
| `exportDeploymentPlanToRemote` | External | yes | `Export` | Runs a deployment plan and sends the package to a remote instance, where it is imported immediately. Confirm the plan and the target with the user first; it cannot be undone from this site. | DeploymentAgent |

**Implementation notes:** `RemoteInstanceService` and `RemoteClientService` are in the module assembly and there is no recipe step, so these tools need a module reference (or a new CrestApps module). `RemoteInstance.ApiKey` is stored as a plain string; `RemoteClient.ProtectedApiKey` is data-protected. Mask both on read; on save accept a key from the user and consider generating the client key in the tool and returning it once. The push logic lives only in `ExportRemoteInstanceController.Execute` and must be extracted. That action checks `Export`; `ExportRemoteInstances` is declared in `DeploymentPermissions` but checked nowhere in this checkout. If fewer tools are wanted, the two deletes can fold into one tool with a `kind` argument.

**Test prompts:**

- "Add a remote instance called Staging at https://staging.contoso.com/OrchardCore.Deployment.Remote/ImportRemoteInstance/Import with client name prod-site."
- "Which sites are allowed to deploy to this one?"
- "Send the Content types and settings plan to Staging."

---

## Data Localization — `OrchardCore.DataLocalization`

**What an admin can do in the UI:**

- Settings → Localization → Dynamic Translations (`Admin/DataLocalization/Index?culture=`): pick a culture the user may view or edit, see every translatable string from all `ILocalizationDataProvider`s grouped by provider and context (content type names, field names, permission names, admin menu texts…), type translations, save (`Save` posts the full set for that culture).
- Statistics (`Admin/DataLocalization/Statistics`): progress per culture and per category.

**Already covered:** recipe step `Translations` (merges `{ culture, context, key, value }` entries into the culture's set) and the legacy step `DynamicDataTranslations`. No read.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listDynamicTranslations` | Read | no | `ViewDynamicTranslations` | Lists the translatable dynamic strings for one culture with their current translation, filterable by context, search text and "untranslated only", and returns translation progress per culture. Use it to find what is missing before translating. | SiteConfigurationAgent |
| `saveDynamicTranslations` | Write | no | `ManageTranslations` or `ManageTranslations_{culture}` | Adds or changes translations of dynamic strings for one culture; strings not sent keep their current translation. The culture must be one of the site's supported cultures. | SiteConfigurationAgent |

**Implementation notes:** `ILocalizationDataProvider` and `DataLocalizationPermissions` are in `OrchardCore.Abstractions` / `OrchardCore.Localization.Core`; `ITranslationsManager` and `TranslationsDocument` are in the module assembly. The grouping and statistics logic are private methods of `AdminController` (`GetTranslatableStringsAsync`, `GetStatisticsAsync`) and must be extracted. `ITranslationsManager.UpdateTranslationAsync(culture, translations)` replaces the whole culture, and the controller drops empty values; the tool must load, merge and save (the `Translations` recipe step already merges and is a module-free write path). Check the culture permission the way `CanEditCultureAsync` does. Page the list: a site has hundreds of strings.

**Test prompts:**

- "Translate all missing admin strings into French."
- "How complete are our Spanish translations?"
- "Change the German translation of the 'Blog Post' content type name to 'Blogbeitrag'."

---

## Background Tasks — `OrchardCore.BackgroundTasks`

**What an admin can do in the UI:**

- Tools → Background Tasks (`Admin/BackgroundTasks`): list with search and status filter; bulk Enable, Disable, Run.
- Details (`Details/{name}`): settings, current state, next start, run and failure counts, recent runs (kept up to `BackgroundServiceOptions.MaxRecentRuns`, default 10).
- Edit: schedule (cron, validated with `CrontabSchedule.TryParse`), description, lock timeout, lock expiration, use pipeline. Enable, Disable, Run now.

**Already covered:** nothing. No recipe step.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listBackgroundTasks` | Read | no | `ManageBackgroundTasks` | Lists the site's background tasks with schedule, enabled state, current status, next start, and run and failure counts; pass a task name to also get its recent runs and errors. Use it to answer why a task did not run or failed. | SiteConfigurationAgent |
| `saveBackgroundTaskSettings` | Write | no | `ManageBackgroundTasks` | Changes one background task's settings: enabled, cron schedule, description, lock timeout, lock expiration and use pipeline. Fields left out keep their value. | SiteConfigurationAgent |
| `runBackgroundTask` | Write | no | `ManageBackgroundTasks` | Queues one or more background tasks to run now, outside their schedule. It returns once the run is requested; use `listBackgroundTasks` afterwards to see the result. | SiteConfigurationAgent |

**Implementation notes:** `IBackgroundTask`, `BackgroundTaskSettings`, `IBackgroundTaskSettingsProvider`, `IBackgroundTaskMonitor` (`GetStatesAsync`, `GetStateAsync`, `RequestRunAsync`) and `BackgroundTaskState` are in `OrchardCore.Abstractions`, so list and run need no module reference. Saving needs `BackgroundTaskManager.UpdateAsync` and `BackgroundTaskDocument` from the module assembly, and there is no recipe step. `IBackgroundTaskMonitor` can be null (the controller handles "not monitored"). The cron validation and "empty schedule" handling live in the controller's `Edit` action. Run times are UTC in `BackgroundTaskState`; convert with `ILocalClock` before showing them.

**Test prompts:**

- "Which background tasks failed recently and why?"
- "Run the index update task now."
- "Make the audit trail trimming task run every night at 2 am."

---

## URL Rewriting — `OrchardCore.UrlRewriting`

**What an admin can do in the UI:**

- Tools → URL Rewriting (`Admin/UrlRewriting/Index`): list and search rules, create a rule from a source (`Redirect`: pattern, substitution, case insensitive, query string policy, redirect type; `Rewrite`: pattern, substitution, case insensitive, query string policy, skip further rules), edit, delete, bulk remove, drag to reorder (`POST url-rewriting/resort`).

**Already covered:** recipe step `UrlRewriting` (creates a rule or updates it by `Id`, validates with `IRewriteRulesManager.ValidateAsync`); CrestApps has the schema (`IRewriteRuleSchemaService`). No read, delete or reorder.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listUrlRewriteRules` | Read | no | `ManageUrlRewritingRules` | Lists the URL redirect and rewrite rules in the order they are applied, with their patterns and options. Call it before adding a rule so it does not conflict with an existing one. | SeoAgent |
| `saveUrlRewriteRule` | Write | no | `ManageUrlRewritingRules` | Creates or updates one redirect or rewrite rule (pattern, substitution and options) and can set its position in the order. Rules apply to live requests at once; a wrong pattern can make pages, including the admin, unreachable. | SeoAgent |
| `deleteUrlRewriteRule` | Destructive | yes | `ManageUrlRewritingRules` | Deletes one or more URL rewriting rules by id. | SeoAgent |

**Implementation notes:** `IRewriteRulesManager` (`NewAsync(source, data)`, `UpdateAsync`, `ValidateAsync`, `SaveAsync`, `DeleteAsync`, `ResortOrderAsync`, `GetAllAsync`) is in `OrchardCore.UrlRewriting.Abstractions`; permission in `OrchardCore.UrlRewriting.Core`. This is the cleanest area in the group: no controller-only logic, and the rule sources are `Redirect` (`UrlRedirectRuleSource.SourceName`) and `Rewrite` (`UrlRewriteRuleSource.SourceName`). Return `ValidateAsync` errors verbatim. Consider refusing a rule that would capture the admin prefix; how patterns are matched was not read (unverified), so take the pattern syntax for the tool description from the module docs.

**Test prompts:**

- "Redirect /old-blog/anything to /blog/anything permanently."
- "What redirects do we have?"
- "Remove the redirect for /summer-sale."

---

## Health Checks — `OrchardCore.HealthChecks`

**What an admin can do in the UI:**

- Nothing in the admin. The module maps an endpoint (`HealthChecksOptions.Url`, default `/health/live`, details when `ShowDetails`), configured in `appsettings` section `HealthChecks`.

**Already covered:** nothing.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `getSiteHealth` | Read | no | none in code (the endpoint is public); suggest `AccessAdminPanel` | Runs the site's registered health checks and returns the overall status and each check's status and description. Use it when the user asks whether the site or one of its dependencies is working. | SiteConfigurationAgent |

**Implementation notes:** `HealthCheckService.CheckHealthAsync()` from `Microsoft.Extensions.Diagnostics.HealthChecks` (registered by `services.AddHealthChecks()`); checks come from other features (e.g. the Redis health check in `OrchardCore.Redis/HealthChecks`). Optional tool: it is not an admin UI action, keep it only if an operations agent is wanted.

**Test prompts:**

- "Is the site healthy?"
- "Is Redis reachable?"

---

## No tools needed

| Module | Reason | Settings group (if any) |
| --- | --- | --- |
| Navigation — `OrchardCore.Navigation` | Always enabled; only shapes (navigation, breadcrumb, pager). Front-end menus are content items (Menus module, content tools). | none |
| Localization — `OrchardCore.Localization`, `OrchardCore.Localization.ContentLanguageHeader`, `OrchardCore.Localization.AdminCulturePicker` | Only a settings page (Settings → Localization → Cultures) and a navbar culture picker. PO files are on disk. | `LocalizationSettings` / group `localization` / permission `ManageCultures` / secrets: none. Saving reloads the tenant. The edit view model differs from the stored settings (see the Settings section). |
| Setup — `OrchardCore.Setup` | Anonymous setup wizard for an uninitialized tenant; covered by `setupTenant` and the proposed `getTenantSetupLink`. | none |
| Auto Setup — `OrchardCore.AutoSetup` | Driven only by configuration (`AutoSetupOptions`) and a middleware. | none |
| Diagnostics — `OrchardCore.Diagnostics` | Error pages only (`DiagnosticsController.Error`). | none |
| Dynamic Cache — `OrchardCore.DynamicCache` | Tag helpers and cache services; options from `appsettings` section `DynamicCache`. No admin UI and no "clear cache" action. The admin's levers are Cache mode in General settings and reloading the tenant. | none |
| Redis — `OrchardCore.Redis`, `OrchardCore.Redis.Cache`, `OrchardCore.Redis.Bus`, `OrchardCore.Redis.Lock`, `OrchardCore.Redis.DataProtection` | Connection from configuration only; features are switched with the feature tools. | none |
| Response Compression — `OrchardCore.ResponseCompression` | Middleware only; on or off through the feature tools. | none |
| Mini Profiler — `OrchardCore.MiniProfiler` | Developer widget; only permissions `ViewMiniProfilerOnFrontEnd`, `ViewMiniProfilerOnBackEnd` (role tools) and the feature switch. | none |
| SignalR — `OrchardCore.SignalR` | Registers SignalR and client resources. | none |
| SignalR Azure Backplane — `OrchardCore.SignalR.Azure` | Provider configured in `appsettings` (`AzureSignalROptions`). | none |
| SignalR Redis Backplane — `OrchardCore.SignalR.Redis` | Provider on top of the Redis configuration. | none |
| Tenants sub-features — `OrchardCore.Tenants.Distributed`, `OrchardCore.Tenants.FileProvider` | No UI; switched with the feature tools. | none |

## Proposed agents for this group

| Agent | Purpose (one sentence) | Tools | Could merge with |
| --- | --- | --- | --- |
| SiteConfigurationAgent | Reads and changes how the site is configured: settings pages, features, custom admin menus and dynamic translations. | `listSiteSettingsGroups`, `getSiteSettings`, `updateSiteSettings`, `listTimeZones`, `listSiteFeature`, `searchSiteFeature`, `getSiteFeature`, `enableSiteFeature`, `disableSiteFeature`, `listAdminMenus`, `saveAdminMenu`, `deleteAdminMenu`, `listDynamicTranslations`, `saveDynamicTranslations` (14) | SiteOperationsAgent (21 together); a localization agent from the content localization group could take the two translation tools; themes and security settings tools from other groups fit here. |
| SiteOperationsAgent | Looks after the running site: background tasks, URL redirects and rewrites, and health. | `listBackgroundTasks`, `saveBackgroundTaskSettings`, `runBackgroundTask`, `listUrlRewriteRules`, `saveUrlRewriteRule`, `deleteUrlRewriteRule`, `getSiteHealth` (7) | SiteConfigurationAgent; or operations tools from other groups (audit trail, search index rebuilds, media cache). The URL rules could also go to an SEO agent. |
| TenantAgent | Manages tenants from the Default tenant: create, set up, change, enable, disable, reload, remove, and feature profiles. | `listTenant`, `getTenant`, `createTenant`, `updateTenant`, `setupTenant`, `getTenantSetupLink`, `enableTenant`, `disableTenant`, `reloadTenant`, `removeTenant`, `listStartupRecipes`, `listTenantFeatureProfiles`, `saveTenantFeatureProfile`, `deleteTenantFeatureProfile` (14, plus the feature tools once they accept a `tenant` argument) | Keep separate: Default tenant only, high impact. Could absorb CrestApps.OrchardCore.TenantHierarchy tools if that module gets any. |
| DeploymentAgent | Moves configuration and content in and out of the site with recipes, deployment plans and remote instances. | `listNonStartupRecipes`, `executeNonStartupRecipe`, `getOrchardCoreRecipeJsonSchema`, `listOrchardCoreRecipeStepsAndSchemas`, `importOrchardCoreRecipe`, `listDeploymentPlans`, `saveDeploymentPlan`, `deleteDeploymentPlan`, `exportDeploymentPlan`, `listRemoteInstancesAndClients`, `saveRemoteInstance`, `deleteRemoteInstance`, `saveRemoteClient`, `deleteRemoteClient`, `exportDeploymentPlanToRemote` (15) | Content transfer (import and export) tools from the CrestApps group. |
| ContentAgent (existing) | Gains the two dashboard tools, because dashboard widgets are content items. | `listAdminDashboardWidgets`, `arrangeAdminDashboardWidgets` | — |
| direct | Broadly useful, read only. | `findAdminPage` (and optionally `listSiteSettingsGroups`, `getSiteSettings`) | — |

New tools proposed in this file: 30 (`findAdminPage`; 2 dashboard; 3 admin menu; 5 tenants; 4 deployment; 6 remote deployment; 2 data localization; 3 background tasks; 3 URL rewriting; `getSiteHealth`). The three generic settings tools are designed here but not counted.

## Open questions

- Permission checks: should every tool check the Orchard permission itself (as `ConfigureSiteStructuredDataTool` does), and should the existing `AI.Agent` tools be fixed in the same pass? Today only `AccessAITool_{name}` protects tenant, feature and recipe tools.
- Module references: may `AI.Agent` reference Orchard Core module packages (`OrchardCore.AdminMenu`, `OrchardCore.BackgroundTasks`, `OrchardCore.Tenants`, `OrchardCore.Deployment.Remote`, `OrchardCore.DataLocalization`)? Without them, `deleteAdminMenu`, `saveBackgroundTaskSettings`, `deleteTenantFeatureProfile`, the remote instance and client tools and a clean translation read are not possible; the writes that have a recipe step can fall back to it.
- Settings trio: Design A (driver replay, view-model-shaped) or Design B (`ISiteService`, settings-shaped with per-group hooks)? A needs a spike on view model initialization timing and on `HttpContext.User` inside a tool call.
- `updateSiteSettings` approval: always (simple, proposed) or per group, so that harmless groups such as `admin` do not prompt? Does the tool framework support a dynamic approval decision?
- `applySiteSettings`: retire it, or restrict it to the `settings` and `custom-settings` steps and add approval?
- `importOrchardCoreRecipe`: can it run synchronously and return step errors? Deferred execution means the model reports success for recipes that fail.
- Feature tools: add a `tenant` argument (Default tenant only) to the existing tools, or separate tenant-scoped tools in TenantAgent?
- `createTenant`: fix in place (the Create page's steps, `ITenantValidator`), and merge with `updateTenant` into `saveTenant`?
- `exportDeploymentPlan`: is returning `Recipe.json` inline enough, or should the zip be saved somewhere (media library, temporary download link)?
- Default-tenant-only tools: register them only when `ShellSettings.IsDefaultShell()`, or keep the runtime refusal?
- HTTP context: `findAdminPage`, the menu-based group enumeration and the settings drivers need an `HttpContext` with the user. Do agent calls and scheduled or background AI runs have one?
- `getSiteHealth`: wanted at all, and which permission (`AccessAdminPanel`)?
- Naming of existing tools: rename `listTenant` → `listTenants` and `listSiteFeature` → `listSiteFeatures`, and fix the copied descriptions?
