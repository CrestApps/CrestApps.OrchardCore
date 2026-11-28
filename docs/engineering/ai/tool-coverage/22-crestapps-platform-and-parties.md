# CrestApps: platform building blocks and parties

Part of the [AI tool coverage plan](README.md). Nothing in this file is built yet. The design rules, the permission and approval model and the test plan that apply to every tool below are in [01-foundation.md](01-foundation.md) and [03-testing.md](03-testing.md); the final agent roster is in [02-agents.md](02-agents.md) and wins over the agent notes in this file.

Scope: 28 module folders under `CrestApps.OrchardCore/src/Modules`. Three of them (`CrestApps.OrchardCore.Companies`, `CrestApps.OrchardCore.SignalR.Azure`, `CrestApps.OrchardCore.SignalR.Redis`) contain only stale `bin/` and `obj/` folders: no source, no project file. Everything below was read from code (manifests, startups, controllers, drivers, permission providers, recipe steps); doc pages were used only to confirm.

Conventions used in the tables:

- "content tools" means the existing `searchForContentItems`, `getContentItemById`, `getContentItemSchema`, `createOrUpdateContentItem`, `publishContentItem`, `unpublishContentItem`, `deleteContentItem`. `createOrUpdateContentItem` saves through `IContentManager` (`ValidateAsync`, `CreateAsync`, `UpdateAsync` in `AI.Agent/Contents/CreateOrUpdateContentTool.cs`), so content **handlers** run but content part **display drivers** do not. Any rule that only lives in a display driver is skipped by the content tools. That is the main reason for the few dedicated content-part tools proposed here.
- "settings trio" means the planned generic `listSiteSettingsGroups` / `getSiteSettings(group)` / `updateSiteSettings(group, values)`.
- Permission names are the string names from code (the `new Permission("...")` argument), with the constant in brackets where it differs.

## User Display Name, User Avatar, email-code sign-in — `CrestApps.OrchardCore.Users` (dependency only), `CrestApps.OrchardCore.Users.DisplayName`, `CrestApps.OrchardCore.Users.Avatars`, `CrestApps.OrchardCore.Users.EmailCodeSignIn`

**What an admin can do in the UI:**

- Settings → User Display Name: choose how names are shown (`Username`, `DisplayName`, `FirstThenLast`, `LastThenFirst`, `Other` with a Liquid template) and whether display, first, middle and last name are unused, optional or required.
- Settings → User Avatars: avatar required, use default style.
- Users → edit a user: First, Middle, Last and Display name card (`UserFullNamePartDisplayDriver`, shown and saved only with `EditUsers` on that user); Avatar media picker card (`UserAvatarPartDisplayDriver`, shown and saved only with `ManageMedia`).
- Users list: the search term matches display names (`DisplayNameUsersAdminListFilterProvider`).
- Content type → Common part editor option `PermissionDefinedEditor` (owner editor shown only with `EditContent`); user picker field editor showing display names. Both are content definition settings.
- Email-code sign-in adds a login form card and the public `EmailSignInController`; it has no admin page and no settings.
- `GET Admin/api/crestapps/users/search` (permission `SearchUsers`) feeds every user picker.

**Already covered:** `getUserInfo`, `searchForUsers` (existing, Orchard users). Recipe step `IndexUsers` (`UpdateUserRecipeStepHandler`: re-saves users in batches so `UserFullNameIndex` is rebuilt; arguments `IncludeDisabledUsers`, `BatchSize`) through `importOrchardCoreRecipe`. The editor options are covered by `applyContentTypeDefinitionFromRecipe`.

**Settings groups:**

- `DisplayNameSettings` (Users.Core) / group `userDisplayName` / permission `ManageDisplaySettings` / secrets: none.
- `UserAvatarOptions` / group `avatarOptions` / permission `ManageAvatarSettings` / secrets: none.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `setUserDisplayName` | Write | no | `EditUsers` (on the user) | Sets a user's first, middle, last and display name. Use it after `searchForUsers`; it applies the required-field rules from the User Display Name settings and fails when a required part is missing. | UserAccessAgent |
| `setUserAvatar` | Write | no | `ManageMedia` and `EditUsers` (on the user) | Sets or clears a user's avatar from a media library path. The file must already be in the media library and have an allowed image extension; clearing fails when avatars are required. | UserAccessAgent |

**Implementation notes:**

- The name parts live on the user as `UserFullNamePart` (`User.Put`/`As`), the avatar as `UserAvatarPart.Avatar` (a `MediaField`). Required-field validation and the allowed-extension check exist only inside the two display drivers; extract them into a small service both the driver and the tool call. Save with `UserManager<IUser>.UpdateAsync` so `IUserEventHandler` and the `UserFullNameIndex` provider run.
- If the Orchard group proposes a single `saveUser` tool, these two can become optional arguments of it (registered from this module only when the feature is on); keep them separate otherwise, because each is gated by its own feature.
- Settings trio caveat: both settings drivers have side effects in `UpdateAsync` that a naive "write the settings object" would skip. `DisplayNameSettingsDisplayDriver` validates the Liquid template and removes the cache tag `user-display-name` when the type or template changes; `UserAvatarOptionsDisplayDriver` calls `IOptionsUpdateNotifier.RequestUpdate<UserAvatarOptions>()` and never lets `UseDefaultStyle` go from true to false. The trio must run the site display drivers (or these two need a hook).
- Improvement to an existing tool: `searchForUsers` could call `IUserSearchService` (Users.Abstractions; display-name aware, role and audience filters, behind `SearchUsers`) so it finds people by the name the UI shows.

**Test prompts:**

- "Change Maria Lopez's display name to 'Maria L.' and set her last name to Lopez-Garcia."
- "Use /media/people/ana.png as Ana's avatar."
- "Show user names as 'Last, First' everywhere." (settings trio, group `userDisplayName`)
- "User names in the list look stale after the import, rebuild the user name index." (`importOrchardCoreRecipe` with `IndexUsers`)

## Content Access Control (with Enhanced Roles) — `CrestApps.OrchardCore.ContentAccessControl`, `CrestApps.OrchardCore.Roles`

**What an admin can do in the UI:**

- Content → Content Types → a type → add the reusable `RolePickerPart` ("Role Picker") and edit its settings: Hint, Required, Allow select multiple, Excluded roles (`RolePickerPartSettings`, Roles module); "Restrict content" checkbox (`RolePickerPartContentAccessControlSettings.IsContentRestricted`, ContentAccessControl module).
- Content item editor: pick the roles on the item (`RolePickerPart.RoleNames`).
- Effect: `RoleBasedContentItemAuthorizationHandler` refuses `ViewContent` on a restricted item to every non-administrator who is in none of its roles, even when another rule allowed it.

**Already covered:** the part and both settings through `applyContentTypeDefinitionFromRecipe` (schemas `RolePickerPartSchemaDefinition`, `RolePickerPartContentAccessControlSchemaDefinition`); the roles on an item through `createOrUpdateContentItem` (plain field edit). `getRoleInfo` lists roles.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `getContentItemAccessRoles` | Read | no | `EditContent` (on the item) | Tells which roles may view a content item and whether its content type restricts access at all (role picker parts with "Restrict content" on, including named parts). Use it before changing who can see an item. | UserAccessAgent |
| `setContentItemAccessRoles` | Security | yes | `EditContent` (on the item) | Sets the roles that may view a content item on a content type that restricts access by role. An empty list makes the item visible to everyone who can view content; it fails when the type has no restricting role picker part. | UserAccessAgent |

**Implementation notes:**

- Both tools reuse the logic in `RoleBasedContentItemAuthorizationHandler` (walk `definition.Parts`, read `IsContentRestricted`, handle `IsNamedPart()`); extract it to a service. The write tool must honour `RolePickerPartSettings` (`Required`, `AllowSelectMultiple`, `ExcludedRoles`), which today is enforced only in `RolePickerPartDisplayDriver`, and must save a new draft or published version the same way the content tools do.
- Why a dedicated write tool although it is a field edit: it changes who can access content, and `createOrUpdateContentItem` needs no approval. If the plan prefers no extra tools, the alternative is to document in the ContentAgent instructions that `RolePickerPart.RoleNames` is access control.
- Turning restriction on for a whole content type stays with `applyContentTypeDefinitionFromRecipe`.

**Test prompts:**

- "Who can see the 'Board minutes March' page?"
- "Only Editors and Board Members should be able to view the 'Board minutes March' page."
- "Make blog posts restrictable by role." (content definition tool)

## Advanced SEO — `CrestApps.OrchardCore.Seo`

**What an admin can do in the UI:**

- Settings → Search → Search Engine Optimization → Advanced SEO: render JSON-LD on pages, publisher type (Organization, Person, none), publisher name, logo URL, profile URLs (`sameAs`), include the WebSite node, include the search action.
- Content Types → a type → Schema.org part settings: schema.org type and property mappings (`SchemaOrgPartSettings.SchemaType`, `Properties` with source `Content`, `Fixed`, `Editor`, `Liquid`), with "add suggested properties".
- Content item editor: Schema.org part with "disable for this item" and the values of `Editor`-sourced properties (`SchemaOrgPart.Disabled`, `Values`).
- There is nothing else in this module: no redirects, no meta tags, no robots, no sitemap and no audit page. Those belong to Orchard Core (`OrchardCore.Seo` with `SeoMetaPart` and `RobotsSettings`, `OrchardCore.Sitemaps`, `OrchardCore.UrlRewriting`) and must come from the Orchard group. When `OrchardCore.Seo` is on, this module only merges its JSON-LD (`SeoMetaStructuredDataProvider`, `EditorJsonLdStructuredDataProvider`).

**Already covered (existing tools, registered in `Startup.cs` class `AIStartup`, category "Search Engine Optimization"):**

- `getSchemaOrgOptions` — Read. Without a content type: content types with their schema.org type, and the known schema.org types. With one: its settings, the available value sources, the properties of a type and suggested mappings. No permission check today.
- `configureContentTypeSchemaOrg` — Write. Adds the Schema.org part if needed, sets the type, merges or replaces mappings, adds suggested ones, returns the required properties still missing. Checks `EditContentTypes` (matched by name).
- `configureSiteStructuredData` — Read and Write in one. With no arguments returns `StructuredDataSettings`; otherwise changes only the given fields. Checks `ManageAdvancedSeoSettings`.
- Per-item values: content tools (schema from `SchemaOrgPartSchemaDefinition`). Deployment: site settings step for `StructuredDataSettings`.

**Settings groups:** `StructuredDataSettings` / group `advanced-seo` / permission `ManageAdvancedSeoSettings` / secrets: none.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `getContentItemStructuredData` | Read | no | `ViewContent` (on the item) | Returns the JSON-LD a content item's page publishes, built by the same engine as the page, or says that its content type publishes none. Use it to check the result after configuring schema.org or to answer "what do search engines see for this item". | SeoAgent |

**Implementation notes:**

- New tool: `IStructuredDataManager.BuildPageDocumentAsync(contentItem)` (whole page graph) or `BuildEntityAsync(contentItem, StructuredDataPurpose.Page)` (the item only). Take a content item id or a path; resolve with `IContentManager`.
- Proposed improvements instead of more tools: (1) give `getSchemaOrgOptions` a permission check (`EditContentTypes`; today anyone who can call tools lists every content type); (2) in its list mode add `missingRequired` per content type, which turns it into the structured-data audit (the data is already computed by `SchemaOrgConfigurator`); (3) once the settings trio exists, `configureSiteStructuredData` duplicates `getSiteSettings`/`updateSiteSettings` for group `advanced-seo`. Keep it only if its validation (publisher type, absolute `sameAs` URLs) is not reproduced by the trio.
- Names are fine as they are; if harmonizing verbs, `configureContentTypeSchemaOrg` → `saveContentTypeSchemaOrg` and `configureSiteStructuredData` → retire.

**Test prompts:**

- "Describe blog posts to search engines as BlogPosting and map the headline, image and author."
- "Show me the structured data Google gets for the 'Spring sale' page."
- "Which content types are missing required schema.org properties?"
- "Set our publisher to the organization 'Acme Inc.' with our LinkedIn profile."

## Tenant Hierarchy, parent side — `CrestApps.OrchardCore.TenantHierarchy.Parent`

Only active on a tenant the platform made a parent (`ShellSettings.IsParentTenant()`; every controller action returns Forbid otherwise).

**What an admin can do in the UI** (menu label comes from the policy's labels, default "Child tenants"):

- All child tenants (`ChildTenantsController.Index`, `ViewChildTenants`): search (name, host, description), state filter (Running, Suspended, SettingUp, Failed, PendingRemoval, ChangedByPlatform), sort (name, newest, state); bulk Suspend, Resume, Reload (`ManageChildTenants`) and Remove (`RemoveChildTenants`).
- Create (`CreateChildTenants`): display name, slug (live address check `CheckAddress`), description, setup recipe from the policy's allowed recipes; setup runs after the request. Status polling (`Status`).
- Edit (`ManageChildTenants`): display name, slug, description.
- Row actions: Suspend, Resume, Reload (`ManageChildTenants`); Remove with typed-name confirmation, only when suspended, immediate or after the policy's grace days (`RemoveChildTenants`); Restore a pending removal (`RemoveChildTenants`); Retry or Discard a failed setup (`CreateChildTenants`); Dismiss an entry the platform removed or moved (`ManageChildTenants`).
- Features of a child (`ManageChildFeatures`): list by category, enable or disable several.
- Access (`AccessController`, `ManageChildAccess`): grants for every child or one child; add a grant (principal type User or Role, user name/email or parent role, child roles), remove a grant.
- Open a child tenant (`Switch`, `DelegatedAccessController.Open`, `EnterChildTenants`): tenant picker with favorites and recents, then a browser sign-in redirect; "sign out everywhere".
- Activity: the Audit Trail filtered to `category:` tenant hierarchy events (`ViewAuditTrail`).

**Already covered:** recipe step `TenantHierarchyAccessGrants` (`Grants` array: `PrincipalType`, `Principal`, `ChildEntryId` or `ChildSlug`, `ChildRoles`) through `importOrchardCoreRecipe`; deployment step `AccessGrantsDeploymentStep`. The existing tenant tools (`createTenant`, `listTenant`, …) work on the Default tenant's shell list and must not be used for child tenants (the building-blocks catalog forbids creating or setting up tenants outside `ITenantHierarchyBroker`).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listChildTenants` | Read | no | `ViewChildTenants` | Lists this parent tenant's child tenants with entry id, name, address, state, description and creation date; filter by text and state, and optionally include the limits and setup recipes the platform allows. Call it first: every other child tenant tool needs the entry id. | TenantHierarchyAgent |
| `saveChildTenant` | Security | yes | `CreateChildTenants` (new) / `ManageChildTenants` (existing) | Creates a child tenant (name, slug, description, setup recipe) and starts its setup, or changes the name, slug and description of an existing one. Creating provisions a database and fails when the policy's child limit is reached or the address is taken. | TenantHierarchyAgent |
| `changeChildTenantState` | Security | yes | `ManageChildTenants` | Suspends, resumes or reloads one or more child tenants. Suspending takes the site offline for its users; a tenant pending removal must be restored before it can be resumed. | TenantHierarchyAgent |
| `removeChildTenant` | Destructive | yes | `RemoveChildTenants` | Removes a suspended child tenant and its database, at once or after the policy's grace period. Requires the tenant's exact display name as confirmation. | TenantHierarchyAgent |
| `restoreChildTenant` | Security | yes | `RemoveChildTenants` | Cancels the pending removal of a child tenant during its grace period. The tenant stays suspended until resumed. | TenantHierarchyAgent |
| `retryChildTenantSetup` | Security | yes | `CreateChildTenants` | Creates a child tenant whose setup failed again, with the same name, address and recipe, and runs the setup. Use it only on entries in the Failed state. | TenantHierarchyAgent |
| `discardChildTenant` | Destructive | yes | `CreateChildTenants` (discard) / `ManageChildTenants` (dismiss) | Deletes the leftovers of a child tenant that cannot be used: discards a failed setup with its registry entry, or dismisses the entry of a tenant the platform removed or moved. | TenantHierarchyAgent |
| `listChildTenantFeatures` | Read | no | `ManageChildFeatures` | Lists the features a running child tenant may enable, by category, with whether each is on. | TenantHierarchyAgent |
| `setChildTenantFeatures` | Security | yes | `ManageChildFeatures` | Enables or disables features in a child tenant. Blocked and always-on features are ignored; the tenant must be running. | TenantHierarchyAgent |
| `listChildTenantAccessGrants` | Read | no | `ManageChildAccess` | Lists who may enter child tenants: the grants for all children or for one child (with the inherited ones), plus the parent roles and child roles a grant can use. | TenantHierarchyAgent |
| `grantChildTenantAccess` | Security | yes | `ManageChildAccess` | Lets a parent user (by user name or email) or a parent role enter one child tenant or all of them, with the given child roles. | TenantHierarchyAgent |
| `revokeChildTenantAccess` | Security | yes | `ManageChildAccess` | Removes an access grant by its id. Open sessions end at their next validation. | TenantHierarchyAgent |

**Implementation notes:**

- Services (public, in the module): `ITenantHierarchyBroker` (`ListChildrenAsync`, `GetChildAsync`, `GetChildFeaturesAsync`, `GetChildRolesAsync`), `ChildTenantManager` (`ValidateCreateAsync`, `CreateAsync`, `UpdateAsync`, `SuspendAsync`, `ResumeAsync`, `ReloadAsync`, `RemoveAsync`, `RestoreAsync`, `DiscardAsync`, `RetryAsync`, `DismissAsync`, `SetFeaturesAsync`, `GetPolicy`, `GetAllowedRecipesAsync`, `GetHostPattern`), `AccessGrantManager` (`ListAsync`, `AddAsync`, `RemoveAsync`, `GetParentRolesAsync`).
- All write permissions are declared `isSecurityCritical: true` in `TenantHierarchyPermissions`, which is why every write tool is Security or Destructive.
- Gaps to extract from `ChildTenantsController`: the list filter and sort (private static `Filter`), the `IsParent()` guard that every action repeats, the typed-name check before removal, and the entry-state guard (`ChildTenantRuntimeState.ChangedByPlatform` → not found).
- Risk: after `CreateAsync` and `RetryAsync` the controller calls `ChildTenantManager.ScheduleSetup`, which uses `HttpBackgroundJob.ExecuteAfterEndOfRequestAsync`; `SetupChildAsync` "needs an HTTP context" (broker doc comment). A tool runs inside a chat hub or streaming request, so verify the job fires there, or call `SetupAsync` directly. Setup is asynchronous: the tool should return "being set up" and the agent should poll with `listChildTenants`.
- Entering a child tenant is an interactive browser sign-in (PKCE redirect in `DelegatedAccessController`/`DelegatedEntryController`); the AI cannot do it. `listChildTenants` can return the open link (`delegated-access/open/{entryId}`) for tenants the user may enter (`DelegatedAccessIssuer.GetEnterableChildrenAsync`, `EnterChildTenants`). Favorites and "sign out everywhere" are personal UI and get no tool.
- Activity is the Audit Trail: use the Orchard group's audit trail tool with `category:` of `HierarchyAuditEventNames.Category`.
- Background task "Tenant Hierarchy Maintenance" (`*/5 * * * *`) removes tenants whose grace period ended; no tool.

**Test prompts:**

- "Create a new store for Bella's Bakery at the address bellas-bakery using the retail recipe."
- "Which of our child tenants are suspended or failed setup?"
- "Give everyone in the Support role the Editor role in all child tenants."
- "Suspend the Downtown Gym tenant and remove it."

## Tenant Hierarchy, platform side — `CrestApps.OrchardCore.TenantHierarchy.Platform` (Default tenant only)

**What an admin can do in the UI** (Multi-Tenancy → Tenant Hierarchy, `PlatformController`, all actions `ManageTenantHierarchy`):

- Overview: parents with their children, orphaned children, tenants that can become parents, whether the host guard is installed (`AddTenantHierarchy()` in `Program.cs`) and the platform domain; search.
- Make a tenant a parent: tenant, slug, display name and the policy (`ParentTenantPolicy`: `MaxChildren`, `ChildHostPattern`, `Recipes`, `DatabaseStrategy`, `DatabasePool`, `BlockedFeatures`, `DeniedLocalPermissions`, `RequireMfa`, `SessionValidationInterval`, `SessionIdleTimeout`, `SessionLifetime`, `SwitcherMode`, `Labels`, `RemovalGraceDays`).
- Parent page: details and children; edit the policy and display name; Unmake (only without children); Suspend (parent and every running child) and Resume (exactly those children); Remove (suspended parent and all its children, typed tenant name); Move a child to another parent.
- Orphans: Detach (make an orphaned child an ordinary tenant) or Adopt (move it to a parent).

**Already covered:** nothing. The existing tenant tools cover ordinary tenants only.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `getTenantHierarchyOverview` | Read | no | `ManageTenantHierarchy` | Shows the tenant hierarchy from the Default tenant: parent tenants with their children, orphaned children, tenants that can become parents, the database pools and whether the host guard is installed. Pass a parent to get its policy and children. | TenantHierarchyAgent |
| `saveParentTenant` | Security | yes | `ManageTenantHierarchy` | Makes an ordinary tenant a parent tenant (slug, display name, policy) or changes the display name and policy of an existing parent. Making a parent changes the tenant's host and forces the Parent feature on; it fails until `AddTenantHierarchy()` is in the host. | TenantHierarchyAgent |
| `changeParentTenantState` | Security | yes | `ManageTenantHierarchy` | Suspends a parent tenant with all its running children, or resumes it with exactly the children suspended with it. | TenantHierarchyAgent |
| `unmakeParentTenant` | Security | yes | `ManageTenantHierarchy` | Turns a parent tenant back into an ordinary tenant. Only works when it has no children left. | TenantHierarchyAgent |
| `removeParentTenant` | Destructive | yes | `ManageTenantHierarchy` | Removes a suspended parent tenant and all its child tenants with their data. Requires the parent's exact tenant name as confirmation. | TenantHierarchyAgent |
| `moveChildTenant` | Security | yes | `ManageTenantHierarchy` | Moves a child tenant to another parent, or gives an orphaned child a parent. Its address follows the new parent's pattern and sessions from the old parent end. | TenantHierarchyAgent |
| `detachChildTenant` | Security | yes | `ManageTenantHierarchy` | Makes an orphaned child tenant an ordinary tenant that no parent manages. | TenantHierarchyAgent |

**Implementation notes:**

- Service: `TenantHierarchyPlatformService` (`GetOverview`, `GetParentDetailAsync`, `MakeParentAsync`, `UpdateParentAsync`, `UnmakeParentAsync`, `SuspendParentAsync`, `ResumeParentAsync`, `RemoveParentAsync`, `MoveChildAsync`, `DetachChildAsync`, `GetDatabasePoolNames`, `CanProvision`, `IsHostGuardInstalled`, `GetPlatformDomain`). Every method calls `EnsureDefault()`. Validation is `ParentPolicyValidator.Validate`.
- The policy view model stores session times as minutes and hours (`ParentPolicyEditViewModel.ToPolicy()`); let the tool take minutes/hours too and convert.
- These tools change shell settings and reload or remove tenants: tenant-wide effect, and the chat's own tenant (Default) is not touched. `removeParentTenant` deletes databases through the provisioners.
- Runtime tool counts: the Platform feature is `DefaultTenantOnly`, and the parent-side tools only exist on parent tenants, so a tenant sees at most 12 (parent) or 7 (Default) of these 19 tools.

**Test prompts:**

- "Make the tenant 'franchise-west' a parent that can have 50 children, SQLite per child, and the 'store' recipe only."
- "Show me the whole tenant hierarchy and any orphaned tenants."
- "Move the 'riverside' tenant from franchise-west to franchise-east."
- "Raise franchise-west's child limit to 200 and require MFA to enter children."

## Time Zones — `CrestApps.OrchardCore.TimeZones`

**What an admin can do in the UI:** Tools → Time Zones (`AdminController`, permission `ManageTimeZoneMaps`): list with search and paging; create, edit and delete a time zone map (`TimeZoneMap`: a unique friendly `Name` and a `TimeZoneId`). The module replaces `ITimeZoneSelectListProvider` with `MappedTimeZoneSelectListProvider`, so every time zone picker shows the mapped names.

**Already covered:** `listTimeZones` (existing; returns `IClock.GetTimeZones()`, not the maps). Recipe step `TimeZoneMaps` (`Maps` array, matched by name) through `importOrchardCoreRecipe`; deployment step `TimeZoneMapDeploymentStep`. A default set is seeded from `Migrations/default-timezones.recipe.json`.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listTimeZoneMaps` | Read | no | `ManageTimeZoneMaps` | Lists the friendly time zone names shown in time zone pickers and the time zone each maps to, with an optional name filter. | PlacesAgent |
| `saveTimeZoneMap` | Write | no | `ManageTimeZoneMaps` | Creates or updates a friendly time zone name for a time zone id. The name must be unique; use `listTimeZones` to find a valid time zone id. | PlacesAgent |
| `deleteTimeZoneMap` | Destructive | yes | `ManageTimeZoneMaps` | Deletes a friendly time zone name, so pickers no longer offer it. Records that already store the time zone id keep working. | PlacesAgent |

**Implementation notes:** `INamedCatalogManager<TimeZoneMap>` (`PageAsync` with `QueryContext.Name`, `NewAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`) and `INamedCatalog<TimeZoneMap>.FindByNameAsync` for the uniqueness check, which today is only in the controller. The recipe step already covers "save", but it needs the recipe import permission instead of `ManageTimeZoneMaps`, hence the dedicated tool. Improvement: make `listTimeZones` return `ITimeZoneSelectListProvider` options when this feature is on, so the model uses the names the UI shows.

**Test prompts:**

- "Add 'Mountain Time (Denver)' to the time zone list."
- "Which friendly time zone names do we offer?"
- "Remove the 'Hawaii' entry from the time zone pickers."

## Addresses — `CrestApps.OrchardCore.Addresses`, `CrestApps.OrchardCore.Addresses.Countries`, `CrestApps.OrchardCore.Addresses.GooglePlaces`

**What an admin can do in the UI:**

- Countries feature: `Country` content items (title plus `CountryPart.Code`), seeded from the ISO list; rename, add, remove through the content list. `CountryContentHandler` validates and normalizes the code.
- Google Places feature: Settings → Address suggestions: the Places API key.
- Everywhere else the module is the shared address editor (`IAddressEditorService`), formatter and validator, plus public endpoints `GET /addresses/formats/{countryCode}`, `/addresses/suggest`, `/addresses/resolve`.

**Already covered:** countries through content tools (validation is in a content handler, so it runs). Site settings schema `GooglePlacesSettingsSchema` for recipes.

**Settings groups:** `GooglePlacesSettings` / group `address-suggestions` / permission `ManageAddressSuggestions` / secrets: `ApiKey` (data-protected, purpose `CrestApps.OrchardCore.Addresses.GooglePlaces.ApiKey`; the driver uses `ApplySecret` with a "remove key" flag).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `validateAddress` | Read | no | none (signed-in user; the format endpoint is public) | Checks a postal address against its country's rules and returns the problems per field, the normalized address, the lines as printed, and which fields that country uses and requires. Call it before saving an address on a customer, account location, staff member or content item. | direct (also PlacesAgent, CustomerAgent, AccountAgent) |

**Implementation notes:** `IAddressValidator.ValidateAsync(Address)` (problems: `Missing`, `Invalid`, `Unknown`, `NotInRegion` per `AddressComponent`), `IAddressFormatter.GetLinesAsync`, `IAddressEditorService.GetLayoutAsync(countryCode)`, `ICountryService.GetCountriesAsync()` (Countries feature) to tell the model which countries the site serves. Address autocomplete (`IAddressAutocompleteProvider`, Google Places) is a paid lookup with a session token model built for typing; no tool proposed.

**Test prompts:**

- "Is '10 Downing St, London SW1A 2AA, UK' a complete address?"
- "What does a Japanese address need?"
- "Rename the country 'Türkiye' to 'Turkey' in our country list." (content tools)

## Phone Numbers Services — `CrestApps.OrchardCore.PhoneNumbers` (dependency only)

**What an admin can do in the UI:** nothing directly; it registers `IPhoneNumberService` (parse, validate, E.164, display format, region, time zones of a number).

**Already covered:** nothing.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `normalizePhoneNumber` | Read | no | none (signed-in user) | Parses a phone number for a region and returns whether it is valid, its E.164 form, display form, region and time zones. Call it before storing a phone number so it can match CRM, caller-ID and do-not-call records. | direct (also PlacesAgent, CustomerAgent) |

**Implementation notes:** `IPhoneNumberService.TryFormatToE164`, `IsValidNumber`, `FormatForDisplay`, `GetRegionCode`, `GetTimeZones`. Pure computation, no I/O. It is not a UI action; it exists so the model writes phone values the way the catalog requires (`PhoneNumberNormalization.ToE164OrTrimmed`).

**Test prompts:**

- "Is (555) 010-9999 a valid US number? Give me the E.164 form."
- "What time zone is +44 20 7946 0958 in?"

## Phone Number Verifications — `CrestApps.OrchardCore.PhoneNumbers.Verifications` (dependency only), `.Verifications.AbstractApi`, `.Verifications.Veriphone`, `.Verifications.Twilio`

**What an admin can do in the UI:**

- Tools → Phone Verifications Queue (`RecordsController.Index`, permission `RunPhoneNumberVerificationsReport`): content items that carry `PhoneNumberVerificationPart`, with search by number, status filter (All, Verified, Invalid, Pending, Failed, NeedsAttention = failed and out of attempts), counts per status, four sort orders, paging.
- Retry one record, retry the selected records, retry all failed (up to 10,000) (`VerifyPhoneNumbers`): re-queues the items and verifies them in a deferred task with the configured request delay. Refused when no provider is enabled.
- Settings → Phone Number Verifications: general settings and one card per provider.
- The part's card on contact items is read-only (status, provider, carrier, line type, attempts, last error).
- Report "Phone Number Verifications" (Reports feature) and its report data set.

**Already covered:** the report through the Reports tools of the Reports group. Site settings schemas exist for all four settings classes.

**Settings groups** (all on group `phoneNumberVerifications`, permission `ManagePhoneNumberVerificationSettings`):

- `PhoneNumberVerificationsSettings` (`RevalidationIntervalDays`, `MaxVerificationAttempts`, `RequestDelayMilliseconds`, `SelectedProvider`) / secrets: none.
- `AbstractApiPhoneNumberVerificationSettings` / secrets: `ProtectedApiKey`, `ProtectedPassword`.
- `VeriphonePhoneNumberVerificationSettings` / secrets: `ProtectedApiKey`.
- `TwilioPhoneNumberVerificationSettings` / secrets: `ProtectedApiKeySecret`, `ProtectedAuthToken`.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listPhoneNumberVerifications` | Read | no | `RunPhoneNumberVerificationsReport` | Lists the phone numbers queued for or checked by verification, with status, provider, attempts, last error and next due date, and the count per status; filter by number, status and sort. Use it to find failed or stuck verifications. | PlacesAgent |
| `retryPhoneNumberVerifications` | External | yes | `VerifyPhoneNumbers` | Re-queues phone verifications for the given content item ids, or for all failed ones, and runs them against the enabled lookup provider. Each lookup is a billable provider call; fails when no provider is enabled. | PlacesAgent |

**Implementation notes:**

- Everything is inside `RecordsController` today (query building on `PhoneNumberVerificationPartIndex`, status filter, counts, `RequeueContentItemsAsync`, the deferred `ProcessRequeuedRecordsAsync`). Extract a service first. Provider check: `IPhoneNumberVerificationManager.GetEnabledProvidersAsync()`.
- Background task `PhoneNumberRevalidationBackgroundTask` re-verifies due numbers; no tool.
- An ad hoc "verify this number now" (`IPhoneNumberVerificationManager.VerifyAsync`) is not a UI action; see Open questions.

**Test prompts:**

- "How many phone verifications failed, and why?"
- "Retry all the failed phone number checks."
- "Switch phone verification to Twilio and re-check numbers every 180 days." (settings trio)

## Geo, Maps and Territories — `CrestApps.OrchardCore.Geo` (providers `CrestApps.OrchardCore.Geo.AzureMaps`, `.Geo.GoogleMaps`, `.Geo.Osrm` are settings only)

**What an admin can do in the UI:**

- Field Service → Territories (`TerritoriesController`, permission `ManageGeoTerritories`, constant `GeoPermissions.ManageTerritories`): list with a map of all shapes; a test form that finds the territory for a latitude/longitude or postal code (`TerritoryMatcher.Match`); create and edit a territory (name, parent, shape drawn as GeoJSON, postal codes, time zone, travel speed km/h, members per kind from `ITerritoryMemberSource`); delete (children are re-parented to the deleted territory's parent).
- Settings → Maps and Geocoding (`ManageGeoSettings`): geocoding, travel-time and route provider, average speed, road factor, matrix cache minutes, map tile URL and attribution, default map centre and zoom; the geocode queue counters (waiting, done, failed) and last error. One extra card per provider feature.

**Already covered:** recipe step `Geo` (`Territories` array) through `importOrchardCoreRecipe`; deployment steps for territories and for `GeoSettings`.

**Settings groups** (all on group `geo`, permission `ManageGeoSettings`):

- `GeoSettings` / secrets: none. The driver validates ranges and the `{z}`/`{x}`/`{y}` tile URL, and calls `IGeocodeQueue.WakeAsync()` when the geocoding provider changes.
- `AzureMapsSettings` / secrets: `Key`.
- `GoogleMapsSettings` / secrets: `Key`.
- `OsrmSettings` (`BaseUrl`, `Profile`) / secrets: none.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listServiceTerritories` | Read | no | `ManageGeoTerritories` | Lists the service territories with parent, postal codes, time zone, travel speed, members and whether a shape is drawn; pass an id to also get its GeoJSON shape and the member kinds that can be assigned. | PlacesAgent |
| `saveServiceTerritory` | Write | no | `ManageGeoTerritories` | Creates or updates a service territory: name, parent, GeoJSON shape or postal codes, time zone, travel speed and members by kind. Members of kinds you do not pass are kept. | PlacesAgent |
| `deleteServiceTerritory` | Destructive | yes | `ManageGeoTerritories` | Deletes a service territory. Its child territories move up to its parent. | PlacesAgent |
| `findServiceTerritory` | Read | no | `ManageGeoTerritories` | Finds which service territory covers a latitude/longitude or a postal code. Use `geocodeAddress` first when you only have a street address. | PlacesAgent |
| `geocodeAddress` | External | yes | `ManageGeoSettings` | Looks up the coordinates of an address, or the address of coordinates, with the configured maps provider. It is a billable provider call and fails when no geocoding provider is set. | PlacesAgent |
| `getGeocodingStatus` | Read | no | `ManageGeoSettings` | Reports the background geocoding queue: how many addresses are waiting, done and failed, the last error, and which providers are selected. Use it when addresses have no coordinates. | PlacesAgent |

**Implementation notes:**

- `ServiceTerritoryManager` (Geo.Core, `CatalogManager<ServiceTerritory>`; `GetAllAsync`, `ValidateAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`), `ITerritoryResolver.ResolveAsync(point, postalCode)`, `ITerritoryMemberSource` (kinds and options), `IGeoService.GeocodeAsync`/`ReverseGeocodeAsync`, `IGeoSettingsService.GetAsync`.
- Controller-only logic to extract: merging members per kind on save, re-parenting children on delete, and the queue counters, which the settings driver reads straight from `GeocodeRequestIndex`.
- `geocodeAddress` is not a UI action (geocoding runs in the "Geocode addresses" background task every minute); it exists so the model can answer territory questions from a street address. Drop it if strict UI parity is wanted.
- Travel times and routes (`IGeoService.GetMatrixAsync`, `GetRouteAsync`) are used by Dispatch; no tool here.

**Test prompts:**

- "Create a 'North Austin' territory under 'Austin' for zip codes 78758, 78759 and 78727."
- "Which territory covers postal code 78704?"
- "Why don't the new work order addresses have coordinates?"
- "Use Azure Maps for geocoding." (settings trio; the key is a secret the user must enter)

## Accounts — `CrestApps.OrchardCore.Accounts`, `CrestApps.OrchardCore.Accounts.Members`, `CrestApps.OrchardCore.Accounts.Team`, `CrestApps.OrchardCore.Accounts.Locations`

**What an admin can do in the UI:**

- Accounts menu (permission `ListContent`; hidden when `AccountsAdminMenuOptions.ShowAccountsMenu` is false or no account type exists): Orchard's content list of every content type that carries `AccountPart` (the seeded `Account` type has `TitlePart`, `AccountPart`, `ListPart`). Create, edit, publish and delete accounts as content items.
- Account page (`DetailAdmin` with page sections Overview, Members, Locations, Contacts, Opportunities, Leads, Activities, Orders, Business buying, Invoices, Service, All records; other modules fill them). Child records are contained items of the account's `ListPart`.
- Members tab (Members feature, permission `ManageAccountMembers`): rows of user (user name or email) and role (`Member`, `Admin`). A user can belong to one account only.
- Locations tab (Locations feature): named places with an address checked by country rules (`AccountLocation { LocationId, Name, Address }`).
- Team tab (Team feature): owner staff member and account team staff members (`AccountTeamPart.OwnerStaffId`, `TeamStaffIds`).
- `GET Admin/api/crestapps/accounts/search` (`ListContent`, then `ViewContent` per account type) feeds the account picker.
- Content type settings: `AccountPartSettings.OfferedContentTypes` (which child types an account offers), kept in sync by `AccountListSynchronizer`.

**Already covered:** creating, renaming, publishing and deleting an account, and adding child records, through the content tools. Recipe step `Accounts` (`Accounts` and `Records` arrays of content items) and deployment step `AccountsDeploymentStep`. Bulk import through ContentTransfer (`AccountContentImportHandler`).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listAccounts` | Read | no | `ListContent`, then `ViewContent` per account type | Finds accounts (businesses and households) by name across every account content type and returns id, name, type, status, member count, locations and owner. Use it to get the account id; use `getContentItemById` for the full record. | AccountAgent |
| `setAccountMembers` | Security | yes | `ManageAccountMembers` and `EditContent` (on the account) | Replaces the users who sign in for an account and their role (Member or Admin), by user name or email. Each user must exist and can belong to one account only; the tool fails without changing anything when one does not qualify. | AccountAgent |
| `saveAccountLocation` | Write | no | `EditContent` (on the account) | Adds a named location to an account or updates one by its location id, after checking the address against its country's rules. | AccountAgent |
| `deleteAccountLocation` | Destructive | yes | `EditContent` (on the account) | Removes a location from an account by its location id. Orders and work orders that already used it keep their copy of the address. | AccountAgent |
| `setAccountTeam` | Write | no | `EditContent` (on the account) | Sets the staff member who owns an account and the other staff on its account team, by staff member id. Find the ids with the staff search tool first. | AccountAgent |

**Implementation notes:**

- Why not the content tools: member rows are resolved from user name or email to `UserId`, `UserName`, `Email`, and the one-account rule (`IAccountMemberService.FindOtherAccountIdAsync`) is enforced only in `AccountMembersPartDisplayDriver.UpdateAsync`; a raw `createOrUpdateContentItem` could store unknown users or put one user in two accounts. Location ids and address validation live in `AccountLocationsPartDisplayDriver`. Extract each into a service used by the driver and the tool. `AccountMembersPartHandler` (links each member to a contact by email) is a handler and runs either way.
- `listAccounts`: `IAccountContentTypeProvider.GetAccountTypesAsync()` plus the query from `AccountSearchEndpoints` (which caps at 50 and filters by viewable type).
- `setAccountMembers` is Security because members are who may act and buy for the account (B2B buyers are these members).
- `deleteAccountLocation` claim "orders keep their copy" is unverified; check `AccountLocationsShippingAddressSource` in Accounts.B2B before writing the description.
- The account's child records (contacts, opportunities, assets) are content items contained in the account's `ListPart`; the content tools create them, but setting the container (`ContainedPart.ListContentItemId`) is fiddly. If the Orchard group proposes a "create contained item" tool, it covers this.

**Test prompts:**

- "Find the account for Northwind Traders."
- "Let jane@northwind.example and tom@northwind.example sign in for Northwind; Jane is the admin."
- "Add a warehouse location for Northwind at 500 Harbor Blvd, Oakland CA 94607."
- "Make Sam Carter the owner of the Northwind account."

## Accounts B2B — `CrestApps.OrchardCore.Accounts.B2B`

**What an admin can do in the UI:**

- Commerce → B2B Accounts (`ManageB2BAccounts`): Orchard's content list of account types that carry `B2BAccountPart`, filtered with the `b2b:true` term.
- Account editor, "Business buying" card (`B2BAccountPartDisplayDriver`, `ManageB2BAccounts`): enabled, tax id, billing email, per-buyer order limits (buyers are the account's members), payment terms (`NetDays` 0–365 or blank, `DepositPercent` 0–100, `CreditLimit`, `RequirePurchaseOrder`), price list, monthly budget; punchout (enabled, the member the procurement system shops as, who must not be a site administrator, and "new key": the key is shown once in a notification and stored hashed).
- Account page: Business buying tab with what the account owes; Invoices tab with its term invoices (`AccountTermsService`).
- Commerce → Price lists (`PriceListsAdminController`, `ManageB2BAccounts`): list with search; create, edit, delete a price list (`Name`, `PercentOff`, `Currency`, `Entries` of `Sku` + `UnitPrice`).
- Storefront only (not site admin): the business account page, order approvals by account admins (`BusinessAccountController.Decide`), punchout (`PunchoutController`), and the commerce API endpoints.

**Already covered:** recipe step `PriceLists` (creates or updates by id or name) and deployment step; report data sets (B2B accounts, buyers, price lists, price list entries) through the Reports/data pipeline tools.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `getB2BAccountSummary` | Read | no | `ManageB2BAccounts` | Returns an account's business-buying setup and standing: enabled, terms, credit limit and available credit, amount owed, open term invoices, monthly budget and spend, price list, buyers with their order limits, and whether punchout is on. | AccountAgent |
| `saveB2BAccountSettings` | Write | no | `ManageB2BAccounts` | Turns business buying on or off for an account and sets tax id, billing email, payment terms (net days, deposit percent, credit limit, purchase order required), price list, monthly budget and per-buyer order limits. Buyers must already be members of the account. | AccountAgent |
| `configureB2BPunchout` | Security | yes | `ManageB2BAccounts` | Enables or disables punchout for an account, chooses the member the procurement system shops as, and can issue a new punchout key. A new key is returned once and replaces the old one immediately; the buyer cannot be a site administrator. | AccountAgent |
| `listPriceLists` | Read | no | `ManageB2BAccounts` | Lists price lists with their percent off, currency and entry count, filtered by name; pass an id to get its SKU prices. | AccountAgent |
| `savePriceList` | Write | no | `ManageB2BAccounts` | Creates or updates a price list: name, percent off, currency and unit prices by SKU. On update the entries you send replace the stored ones. | AccountAgent |
| `deletePriceList` | Destructive | yes | `ManageB2BAccounts` | Deletes a price list. Accounts that used it fall back to normal prices. | AccountAgent |

**Implementation notes:**

- All validation of the B2B part is in the private `B2BAccountPartDisplayDriver.ApplyAsync` (ranges, limits only for members, punchout user rules, key generation with `PunchoutService.NewKey()`); move it to a service. The part is saved on the account content item, so the tool must load, alter and update the item like the content tools do (draft/published handling).
- `getB2BAccountSummary`: `new B2BAccount(contentItem)`, `AccountTermsService.GetOwedAsync`, `ListInvoicesAsync`, `GetAvailableCreditAsync`, `B2BApprovalService.GetSpentThisMonthAsync`. `AccountTermsService` and `B2BApprovalService` are module classes, not interfaces.
- Price lists: `IPriceListStore` (`PageAsync(search, skip, take)`, `ListAllAsync`, plus `ICatalog<PriceList>` create/update/delete). Name uniqueness and SKU clean-up are in the controller. `B2BPriceProvider` gives no account price when the account's `PriceListId` no longer resolves, so accounts fall back to normal prices after a delete; the stale id stays on the account.
- The punchout key is a credential: never log it, return it once in the tool result, and say so in the answer.

**Test prompts:**

- "Give Northwind net-30 terms with a $20,000 credit limit and require a PO number."
- "How much does Northwind owe us, and how much credit do they have left?"
- "Create a 'Wholesale 15' price list with 15% off everything and assign it to Northwind."
- "Rotate Northwind's punchout key."

## Customers — `CrestApps.OrchardCore.Customers`, `.Customers.StoreCredit`, `.Customers.Loyalty`, `.Customers.Referrals`, `.Customers.CheckoutDetails`, `.Customers.Api`

**What an admin can do in the UI:**

- Commerce → Customers (`CustomersAdminController`; list and view need `ViewCustomers`, changes need `ManageCustomers`): search; filter Active, With an account, Guests, Businesses, Disabled, All; paging.
- Create and edit a customer: kind (Person, Business), status (Active, Disabled), display, first and last name, company name, tax id, tax exempt and reason, email (only for guests; an account holder's email comes from the user), phone, accepts marketing. There is no delete: a customer is disabled or erased.
- Customer page: address book (add, edit, delete addresses, with country validation and default billing/shipping), recent orders, merge history, privacy log, plus cards other features add.
- Erase personal data (`Erase`, `ManageCustomers`): anonymizes the customer and the personal details of their orders. Export (`Export`): downloads everything the store holds on the customer as JSON. Both are written to the privacy log.
- Store credit card (StoreCredit feature, `StoreCreditAdminController.Change`, `ManageCustomers`): add or take away an amount in a currency with a reason; shows balances and activity.
- Loyalty card (Loyalty feature, `LoyaltyAdminController.Change`, `ManageCustomers`): add or take away points with a reason.
- Settings → Commerce → Customers, Rewards, Referrals (see settings groups).
- Checkout details feature: fields are added to the `CheckoutDetails` content type (content definition).
- Customers API feature: `GET customers` and `GET customers/{customerId}` for staff (`ViewCustomers`); the rest is for the signed-in buyer.
- Storefront "My Account" pages (profile, addresses, store credit, rewards, referrals) are the buyer's own, not admin work.

**Already covered:** recipe steps `Customers`, `StoreCreditAccounts`, `LoyaltyAccounts`, `ReferralProfiles` through `importOrchardCoreRecipe` (seed and migration, not daily work); deployment steps for the same; report data sets (customers, store credit accounts and activity, loyalty accounts and activity, referrals and rewards) through the Reports tools; `CheckoutDetails` fields through `applyContentTypeDefinitionFromRecipe`.

**Settings groups** (all permission `ManageCustomers`, no secrets):

- `CustomerPrivacySettings` (`InactiveGuestMonths`, `GuestOrderMonths`) / group `customers`.
- `CustomerCheckoutSettings` (`BillingAddress` requirement) / group `customers` (only when Orders is on).
- `LoyaltySettings` (`ProgramName`, `PointsPerUnit`, `PointValue`, `MinimumPointsToRedeem`) / group `loyalty`.
- `ReferralSettings` (`FriendCouponId`, `RewardKind`, `RewardAmount`, `RewardPoints`, `MinimumOrderAmount`) / group `referrals`.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listCustomers` | Read | no | `ViewCustomers` | Finds customers by name, company, email or phone, with filters for status, guests or account holders and businesses, and paging. Returns id, name, kind, status, email and last activity. | CustomerAgent |
| `getCustomer` | Read | no | `ViewCustomers` | Returns one customer's profile, addresses, recent orders, merge history, privacy log and, when those features are on, store credit balances and loyalty points. | CustomerAgent |
| `saveCustomer` | Write | no | `ManageCustomers` | Creates a customer or updates one: kind, status (Active or Disabled), names, company, tax id and exemption, phone, marketing consent, and email for guests only. A business needs a company name; an anonymized customer cannot be edited. | CustomerAgent |
| `saveCustomerAddress` | Write | no | `ManageCustomers` | Adds an address to a customer's address book or updates one by id, after checking it against the country's rules; can mark it as the default billing or shipping address. | CustomerAgent |
| `deleteCustomerAddress` | Destructive | yes | `ManageCustomers` | Deletes one address from a customer's address book. | CustomerAgent |
| `exportCustomerData` | External | yes | `ManageCustomers` | Produces everything the store holds on a customer for a data access request and records the export in the customer's privacy log. The result is personal data: hand it to the requester, do not summarize it elsewhere. | CustomerAgent |
| `eraseCustomerData` | Destructive | yes | `ManageCustomers` | Erases a customer's personal data and the personal details on their orders for an erasure request. Orders stay without them; this cannot be undone. | CustomerAgent |
| `adjustCustomerStoreCredit` | External | yes | `ManageCustomers` | Adds or removes store credit for a customer in a currency, with a reason shown in the credit history. A positive amount is money the store gives away. | CustomerAgent |
| `adjustCustomerLoyaltyPoints` | External | yes | `ManageCustomers` | Adds or removes loyalty points for a customer, with a reason shown in the points history. | CustomerAgent |

**Implementation notes:**

- Services: `ICustomerStore` (`PageAsync(CustomerQuery, skip, take)`, `FindByIdAsync`, `CreateAsync`, `UpdateAsync`; update can throw `ConcurrencyException`), `ICustomerPrivacyService` (`EraseAsync`, `ExportAsync`), `CustomerAddressBook` (module class: `SummarizeAsync`, `ValidateAsync`, `Apply`), `StoreCreditService.ChangeAsync(customerId, currency, amount, reason, actor)`, `LoyaltyService.ChangeAsync(customerId, points, LoyaltyActivityKind.Adjusted, reason, actor)`, `IOrderStore.PageAsync` for recent orders (optional service).
- Controller-only logic to extract: `Validate` and `Apply` of the customer form, the filter-to-query mapping, the history assembly (`AddHistoryAsync`), and the guards (`FindEditableAsync` refuses Anonymized and Merged customers).
- Kind choices: store credit and points are marked External because they cost the store money; the export is External because personal data leaves the site through the model provider. Consider returning the export as a download link instead of putting the JSON in the conversation.
- `exportCustomerData` and `eraseCustomerData` go through `IPersonalDataService` contributors of other modules; erasure also touches orders.
- Referrals have no admin action beyond settings and reports. Background task "Customer data retention" (daily 03:30) applies `CustomerPrivacySettings`; no tool.

**Test prompts:**

- "Find the customer with the email dana@example.com and show her recent orders."
- "Give Dana a $25 store credit for the late delivery."
- "Dana asked us to delete her data. Erase it."
- "Add 500 loyalty points to Ana's account for the survey."
- "Mark Contoso Ltd as tax exempt, reason 'reseller certificate'."

## Teams — `CrestApps.OrchardCore.Teams`

**What an admin can do in the UI:** Content → Teams (`TeamsController`, permission `ManageTeams`): the team tree with search and paging; create (optionally under a parent), edit and delete a team. Fields: name, kind (Organization, Region, Site, Department, Team, Crew), parent, description, linked role, time zone, enabled. Delete is refused while `ITeamManager.GetDeletionBlockersAsync` (from `ITeamDeletionGuard` implementations) reports blockers. Other modules add their own cards to the team editor through `DisplayDriver<Team>`. With Staff on, the staff editor gets a "Teams" card with dated memberships and roles (Member, Lead, Scheduler, Manager), limited to the teams the editor manages (`StaffTeamsDisplayDriver`).

**Already covered:** recipe step `Teams` (`Teams` array, `TeamRecipeImport`) through `importOrchardCoreRecipe`; deployment step. Members of a team: existing `staffFind` with `teamName`.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listTeams` | Read | no | `ManageTeams` | Lists the team hierarchy (organization, regions, sites, departments, teams, crews) with id, kind, parent, linked role, time zone and whether each is enabled; filter by name. Use it to get a team id before assigning people or editing a team. | StaffAgent |
| `saveTeam` | Write | no | `ManageTeams` | Creates or updates a team: name, kind, parent, description, linked role, time zone and enabled. A team cannot be moved under itself or one of its sub-teams. | StaffAgent |
| `deleteTeam` | Destructive | yes | `ManageTeams` | Deletes a team. It is refused, with the reasons, while the team still has sub-teams or another module still depends on it. | StaffAgent |

**Implementation notes:**

- `ITeamManager` (`ICatalogManager<Team>` plus `ListTreeAsync`, `GetSubtreeIdsAsync`, `GetAncestorsAsync`, `GetDeletionBlockersAsync`); validation through `CatalogEntryValidation.ValidateAsync(_teamManager, team, …)`.
- `saveTeam` covers the core fields only. Cards other modules add to the team editor (Workforce, Contact Center) are saved by their display drivers; those groups must expose their own team settings or the tool must run `IDisplayManager<Team>`.
- `listTeams` requires `ManageTeams` because the UI has no read-only team permission; the existing `staffFind` already reads team names with `ViewStaff` through `StaffTeamReader`. Decide whether `listTeams` should also accept `ViewStaff`.
- Deletion blockers: `TeamManager` refuses a team with sub-teams and then asks every registered `ITeamDeletionGuard` (Workforce registers `WorkforceTeamDeletionGuard`).
- Team memberships are edited on the staff member (`saveStaffMember`), not on the team.

**Test prompts:**

- "Create a 'West Region' under the organization and a 'Phoenix Service Crew' under it, Arizona time."
- "Show me our team structure."
- "Delete the 'Pilot Team' team."

## Skills — `CrestApps.OrchardCore.Skills`

**What an admin can do in the UI:**

- Content → Skills (`SkillsController`, `ManageSkills`): list with search; create, edit, delete a skill (`Name`, `Category`, `Description`, `RequiredCertificationId`, `Enabled`).
- Content → Certification Types (`CertificationsController`, `ManageSkills`): list; create, edit, delete (`Name`, `Issuer`, `Description`, `ValidityMonths`, `RequiresDocument`, `ReminderDays`, `Enabled`).
- Content → Certificates (`CertificationRecordsController`; list needs `ViewSkills`, changes need `AssignSkills`): list with search, certification type, status and "expiring soon" filters; add and edit a certificate for a person (certification type, number, issued and expiry dates, document, notes); Verify; Revoke; Delete.
- Skills card on the user editor (`UserSkillsDisplayDriver`) and on the staff editor (`StaffSkillsDisplayDriver`), both from `SkillSubjectSkillsDisplayDriverBase` with `AssignSkills`: the person's skills with proficiency 1–5 and effective dates. The staff profile also has "add certificate" (`StaffCertificatesController.Add`).
- Workflows: task `AssignSkillTask`; events `CertificationExpiringEvent`, `CertificationExpiredEvent`, `SkillsChangedEvent`.

**Already covered:** recipe step `Skills` (sections `Certifications`, `Skills`, `Assignments`, `CertificationRecords`) through `importOrchardCoreRecipe`; deployment step; the workflow activities through `listWorkflowActivities`/`createOrUpdateWorkflow`.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listSkills` | Read | no | `ViewSkills` | Lists the skill catalog with category, required certification and whether each skill is enabled; filter by name. | SkillsAgent |
| `saveSkill` | Write | no | `ManageSkills` | Creates or updates a skill in the catalog: name, category, description, the certification it requires and enabled. | SkillsAgent |
| `deleteSkill` | Destructive | yes | `ManageSkills` | Deletes a skill from the catalog. Disable it instead when people still hold it. | SkillsAgent |
| `listCertificationTypes` | Read | no | `ViewSkills` | Lists the certification types people can hold, with issuer, validity in months, reminder days and whether a document is required. | SkillsAgent |
| `saveCertificationType` | Write | no | `ManageSkills` | Creates or updates a certification type: name, issuer, validity in months, reminder days before expiry, document required and enabled. | SkillsAgent |
| `deleteCertificationType` | Destructive | yes | `ManageSkills` | Deletes a certification type. Disable it instead when certificates of this type exist. | SkillsAgent |
| `getSubjectSkills` | Read | no | `ViewSkills` | Returns the skills a person holds (proficiency and effective dates) and their certificates, for a user or a staff member. | SkillsAgent |
| `saveSkillAssignment` | Write | no | `AssignSkills` | Gives a user or staff member a skill, or changes its proficiency (1 to 5) and effective dates. It is refused when the skill requires a certification the person does not hold. | SkillsAgent |
| `removeSkillAssignment` | Write | no | `AssignSkills` | Takes a skill away from a user or staff member. | SkillsAgent |
| `findSkillHolders` | Read | no | `ViewSkills` | Finds the people who hold a skill at or above a proficiency on a date. Use it to answer "who can do X". | SkillsAgent |
| `listCertificates` | Read | no | `ViewSkills` | Lists the certificates people hold, filtered by person, certification type, status or expiring soon, with paging. | SkillsAgent |
| `saveCertificate` | Write | no | `AssignSkills` | Records a certificate for a user or staff member, or updates one: certification type, number, issued and expiry dates, document path and notes. | SkillsAgent |
| `setCertificateStatus` | Write | no | `AssignSkills` | Verifies a pending certificate or revokes one with a reason. A revoked certificate no longer satisfies the skills that require it. | SkillsAgent |
| `deleteCertificate` | Destructive | yes | `AssignSkills` | Deletes a certificate record. Use revoke instead to keep the history. | SkillsAgent |

**Implementation notes:**

- Services (Skills.Abstractions): `ISkillManager` (`SearchAsync`, `FindByNameAsync`, `GetEnabledAsync`, catalog CRUD), `ICertificationManager` (`SearchAsync`, `GetEnabledAsync`, catalog CRUD), `ISkillAssignmentService` (`GetAssignmentsAsync`, `GetEffectiveAsync`, `AssignAsync`, `RemoveAsync`, `SetAsync`, `FindHoldersAsync`, `HoldsAsync`), `ICertificationRecordService` (`PageAsync(CertificationRecordQuery)`, `SaveAsync`, `VerifyAsync`, `RevokeAsync(id, actor, reason)`, `DeleteAsync`, `GetForSubjectAsync`).
- Subjects are `SkillSubject(kind, id)` with open kinds `User` and `Staff` (`SkillSubjectKind`); `ISkillSubjectLinkProvider` makes a staff member and its user share skills. Tools should accept a staff member id, a user id or a name and resolve it.
- The certification rule is in `DefaultSkillAssignmentService` (an assignment is refused when the skill's `RequiredCertificationId` has no valid certificate for the person on that date). Deleting a skill or a certification type has no blocker check in the controllers (`SkillsController.Delete`, `CertificationsController.Delete` call the manager directly); what happens to existing assignments and certificates afterwards is unverified, hence the "disable instead" advice in the descriptions.
- The staff UI additionally limits who you may edit by `IStaffScopeService.CanManageAsync`; apply the same check when the subject is a staff member.
- 14 tools is the largest set in the group. If the agent budget is tight, fold `listCertificationTypes` into `listSkills` (one "skill catalog" read) and `removeSkillAssignment` into `saveSkillAssignment` with a `remove` flag.
- Background task "Skills and certificates daily check" raises reminders and expires certificates; no tool.

**Test prompts:**

- "Add a 'Forklift operation' skill that requires the 'OSHA Forklift' certification, valid for 36 months with reminders 60 and 30 days before."
- "Who is certified for forklift operation at level 3 or higher?"
- "Record Luis's OSHA Forklift certificate, issued 2026-03-01, number FL-22871, and mark it verified."
- "Which certificates expire in the next month?"

## Staff — `CrestApps.OrchardCore.Staff`, `CrestApps.OrchardCore.Staff.Compensation`, `CrestApps.OrchardCore.Staff.Badges`, `CrestApps.OrchardCore.Staff.OrgChart`

**What an admin can do in the UI:**

- Staff → All Staff (`StaffController.Index`): search; filter by team, status (Active, OnLeave, Suspended, Terminated) and capability. People with `ViewStaff` see everyone; others see the staff in their scope (`IStaffScopeService`: the teams they manage, else their reports).
- Create and edit a staff member (`Edit`; allowed when the scope covers the person, `IStaffScopeService.CanManageAsync`): display name and email (taken from the user when linked), user link (can be set once, never changed), phone, employee number, external id, job title, employment type, status, hire, seniority and termination dates, date of birth (only with `ViewSensitiveStaffData`), manager, time zone, work location, home base address, contracted hours per week, capabilities; team memberships card (Teams); skills card (Skills); pay card (Compensation feature, `ManageStaffPay`: dated hourly rates, optionally per activity code, annual salary, hourly cost); badge card (Badges feature: set or clear the PIN).
- Profile page (`Profile`): header, details, reporting line, teams, skills and certificates, pay, history. "My Profile" (`Me`, `ViewOwnStaffProfile`).
- Delete (`ManageStaff`): refused while the person has direct reports.
- Staff → Capabilities (`StaffCapabilitiesController`, `ManageStaff`): list built-in and custom capabilities; create, edit (display name, description, position, enabled); delete a custom one or reset a built-in one to its defaults.
- Staff → Organization Chart (OrgChart feature, `ViewStaff`): chart by reporting line, filter by team.
- Users → edit a user: "Staff" card with "make this user a staff member" (`UserStaffDisplayDriver`, `IStaffAccountService`).
- Settings → Staff.
- `GET Admin/api/staff/search` (`ViewStaff` or `SearchUsers`) feeds the staff picker. Workflow events `StaffMemberHiredEvent`, `StaffMemberLeftEvent`, `StaffManagerChangedEvent`. Report data source "Staff" (members, history).

**Already covered (existing tools, `AIToolsStartup.cs`, category "Staff", both check `ViewStaff`, neither needs approval):**

- `staffFind` — Read. Finds up to 25 staff by text (name, email, job title, employee number), team name (with sub-teams) and capability; returns name, job title, main team, manager, status, capabilities, email, and the total.
- `staffReportingLine` — Read. For a name, an employee number, or the signed-in person: the manager chain and the direct reports.
- Recipe step `Staff` (sections `Capabilities` and `Staff`; members matched by id, employee number or user) through `importOrchardCoreRecipe`; deployment step; workflow events through the workflow tools; reports through the Reports tools.

**Settings groups:** `StaffSettings` (`LimitPickersToStaff`) / group `staff` / permission `ManageStaff` / secrets: none.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `getStaffMember` | Read | no | `ViewStaff` or manager scope; `ViewSensitiveStaffData` for date of birth; `ManageStaffPay` for pay | Returns one staff member's full record: employment details, manager, team memberships with dates and roles, capabilities, linked user, employment history and, when you may see them, date of birth and pay. | StaffAgent |
| `saveStaffMember` | Write | no | `ManageStaff`, or `ManageOwnStaff`/manager scope for people you manage | Creates a staff member or updates one: job title, employment type, status, dates, manager, time zone, locations, contracted hours, capabilities and team memberships; can link a user account once. Setting a termination date or status Terminated starts offboarding in other modules. | StaffAgent |
| `deleteStaffMember` | Destructive | yes | `ManageStaff` | Deletes a staff record. It is refused while people still report to them; set the status to Terminated instead to keep history. | StaffAgent |
| `setStaffPay` | Write | no | `ManageStaffPay` | Sets a staff member's dated hourly pay rates (for all work or one activity code), annual salary and loaded hourly cost. The rates you send replace the stored list. | StaffAgent |
| `setStaffBadgePin` | Security | yes | `ManageStaff` or manager scope | Sets or clears the PIN a staff member uses with their employee number at time clocks and registers. The PIN is stored hashed and never returned. | StaffAgent |
| `listStaffCapabilities` | Read | no | `ViewStaff` | Lists the kinds of work staff can be given (such as Technician, Agent, Cashier), built-in and custom, with whether each is enabled. | StaffAgent |
| `saveStaffCapability` | Write | no | `ManageStaff` | Creates a custom capability or changes the display name, description, position and enabled flag of any capability. | StaffAgent |
| `deleteStaffCapability` | Destructive | yes | `ManageStaff` | Deletes a custom capability, or resets a built-in one to its defaults. Staff who had a deleted capability keep it until they are next saved. | StaffAgent |

**Implementation notes:**

- Services: `IStaffMemberManager` (`SearchAsync(StaffQuery)`, `FindByIdAsync`, `FindByUserIdAsync`, `FindByEmployeeNumberAsync`, `ListDirectReportsAsync`, catalog create/update/delete with `CatalogEntryValidation`), `IStaffHierarchy`, `IStaffDirectory`, `IStaffScopeService` (`GetScopeAsync`, `CanManageAsync`), `IStaffCapabilityService` (`ListAsync`, `FindAsync`, `SaveAsync(definition, isNew)`, `DeleteAsync`), `IStaffBadgeService` (`SetPin` returns an error text, `ClearPin`, `HasPin`), `IStaffAccountService`/`StaffAccounts.SaveAsync(userId, displayName, StaffAccountInput)` for "make this user staff".
- The staff editor is a set of display drivers on `StaffMember` (`StaffMemberDisplayDriver`, `StaffTeamsDisplayDriver` in Teams, `StaffSkillsDisplayDriver`, `StaffPayDisplayDriver`, `StaffBadgeDisplayDriver`, plus cards from Workforce and others). `saveStaffMember` should set the core fields and team memberships and leave pay, badge and skills to their own tools. Rules now in drivers to extract: user link set only once; name and email come from the linked user; date of birth only with `ViewSensitiveStaffData`; a new home base address drops old coordinates; team memberships can only be added for teams in the caller's scope and `PrimaryTeamId` becomes the first membership.
- Proposed changes to the existing tools: rename `staffFind` → `listStaffMembers` and `staffReportingLine` → `getStaffReportingLine` (verb-first, like every other tool); add the staff member `id`, `employeeNumber` and `userId` to the `staffFind` result (today it returns no id, so no follow-up tool can address the person it found); add `status` filter and paging (fixed at 25); honour the manager scope like the UI does instead of refusing everyone without `ViewStaff`.
- Pay is sensitive personal data: `getStaffMember` must omit it without `ManageStaffPay`, and nothing should log it.
- Offboarding effects (`IStaffLifecycleObserver`, workflow event "left") happen in other modules when status or termination date changes; consider approval for that transition only (see Open questions).

**Test prompts:**

- "Add Priya Shah as a full-time Field Technician starting 2026-12-01, reporting to Marco, in the Phoenix Service Crew."
- "Who reports to Marco, and who does Marco report to?"
- "Set Priya's hourly rate to $31.50 from January 1st."
- "Make the user jsmith a staff member with the Cashier capability."
- "Add a 'Dispatcher' capability."

## No tools needed

| Module | Reason | Settings group (if any) |
| --- | --- | --- |
| `CrestApps.OrchardCore.Roles` (Enhanced Roles) | Only adds the reusable `RolePickerPart` and its content-type settings (hint, required, multiple, excluded roles). Content definition and content tools cover it; the two access-control tools above are listed under Content Access Control. | none |
| `CrestApps.OrchardCore.ContentFields` (features `CrestApps.OrchardCore.ContentFields`, `CrestApps.OrchardCore.ContentFields.Address`) | Adds `PhoneField` and `AddressField` with field settings and an address index. Fields are added with `applyContentTypeDefinitionFromRecipe` (schemas `PhoneFieldSchemaDefinition`, `AddressFieldSchemaDefinition`) and filled with the content tools; `validateAddress` and `normalizePhoneNumber` help the model write valid values. | none |
| `CrestApps.OrchardCore.Recipes` | Recipe steps, JSON schemas and the schema-aware JSON import editor. No records of its own. The existing recipe tools (`getOrchardCoreRecipeJsonSchema`, `listOrchardCoreRecipeStepsAndSchemas`, `importOrchardCoreRecipe`) already sit on its services (`RecipeSchemaService`, `RecipeExecutionService`). Its `ISiteSettingsSchemaDefinition` registrations are the natural source of the JSON schema for the planned `getSiteSettings`. | none |
| `CrestApps.OrchardCore.Resources` | Scripts, styles, the `PageSections` shape and two view components. Pure rendering. | none |
| `CrestApps.OrchardCore.SignalR` (features `CrestApps.OrchardCore.SignalR`, `.SignalR.Redis`, `.SignalR.Azure`) | All three features are marked deprecated in the manifest and only depend on Orchard Core's SignalR features. No UI. | none |
| `CrestApps.OrchardCore.SignalR.Azure`, `CrestApps.OrchardCore.SignalR.Redis` (folders) | Empty: only `bin/` and `obj/`. The feature ids are declared by `CrestApps.OrchardCore.SignalR`. | none |
| `CrestApps.OrchardCore.WebSockets` | Adds the WebSocket middleware and a connection registry; enabled by dependency only; configured in appsettings (`CrestApps:WebSockets`). No UI. | none |
| `CrestApps.OrchardCore.TenantHierarchy.Child` | Always on in a child tenant: linked users, session validation, the "back to parent" navbar item. Nothing to administer from inside the child. | none |
| `CrestApps.OrchardCore.Users` (core feature) and `CrestApps.OrchardCore.Users.EmailCodeSignIn` | The core feature is dependency only (user search endpoint, display-name provider). Email-code sign-in is a login form card and a public controller with no settings; it is turned on with `enableSiteFeature`. | none |
| `CrestApps.OrchardCore.Geo.AzureMaps` | Provider with a settings card only. | `AzureMapsSettings` on group `geo`, `ManageGeoSettings`, secret `Key` |
| `CrestApps.OrchardCore.Geo.GoogleMaps` | Provider with a settings card only. | `GoogleMapsSettings` on group `geo`, `ManageGeoSettings`, secret `Key` |
| `CrestApps.OrchardCore.Geo.Osrm` | Provider with a settings card only. | `OsrmSettings` on group `geo`, `ManageGeoSettings`, no secrets |
| `CrestApps.OrchardCore.PhoneNumbers.Verifications.AbstractApi`, `.Veriphone`, `.Twilio` | Providers with a settings card only; the queue tools are listed under Phone Number Verifications. | group `phoneNumberVerifications` (see that section for the secret fields) |
| `CrestApps.OrchardCore.Addresses.GooglePlaces` | Provider with a settings card only. | `GooglePlacesSettings` on group `address-suggestions`, `ManageAddressSuggestions`, secret `ApiKey` |
| `CrestApps.OrchardCore.OrTools` (features `CrestApps.OrchardCore.OrTools.ShiftScheduling`, `CrestApps.OrchardCore.OrTools.Dispatch`) | Registers `IShiftScheduleSolver` and `IDispatchOptimizer` implementations. No UI, no settings; turned on with `enableSiteFeature` and used by the Workforce and Dispatch tools. | none |
| `CrestApps.OrchardCore.Companies` (folder) | Empty: only `bin/` and `obj/`. The concept is owned by Accounts. | none |
| `CrestApps.OrchardCore.Customers.Referrals`, `.Customers.CheckoutDetails`, `.Customers.Api` | Referrals: settings and storefront pages only. Checkout details: fields on the `CheckoutDetails` content type. API: endpoints that mirror the admin pages. | `ReferralSettings` on group `referrals`, `ManageCustomers` |
| `CrestApps.OrchardCore.Staff.OrgChart` | A visual chart of data the staff tools already return. | none |

## Proposed agents for this group

| Agent | Purpose (one sentence) | Tools | Could merge with |
| --- | --- | --- | --- |
| TenantHierarchyAgent | Creates and manages child tenants, their features and who may enter them on a parent tenant, and manages parents and policies on the Default tenant. | Parent side (12): `listChildTenants`, `saveChildTenant`, `changeChildTenantState`, `removeChildTenant`, `restoreChildTenant`, `retryChildTenantSetup`, `discardChildTenant`, `listChildTenantFeatures`, `setChildTenantFeatures`, `listChildTenantAccessGrants`, `grantChildTenantAccess`, `revokeChildTenantAccess`. Platform side (7): `getTenantHierarchyOverview`, `saveParentTenant`, `changeParentTenantState`, `unmakeParentTenant`, `removeParentTenant`, `moveChildTenant`, `detachChildTenant`. | A TenantAgent holding the eight existing tenant tools: on the Default tenant that gives 8 + 7 = 15 tools, on a parent tenant 12, because the two halves never load on the same tenant. |
| StaffAgent | Manages the people who work for the business, their reporting line, pay, badges and capabilities, and the team hierarchy. | Existing (2): `staffFind`, `staffReportingLine`. New (11): `getStaffMember`, `saveStaffMember`, `deleteStaffMember`, `setStaffPay`, `setStaffBadgePin`, `listStaffCapabilities`, `saveStaffCapability`, `deleteStaffCapability`, `listTeams`, `saveTeam`, `deleteTeam`. | The Workforce tools (6 existing: shifts, time off) into one workforce/people agent; SkillsAgent if skills are trimmed to stay under 25. |
| SkillsAgent | Maintains the skill and certification catalog, who holds which skill, and people's certificates. | 14: `listSkills`, `saveSkill`, `deleteSkill`, `listCertificationTypes`, `saveCertificationType`, `deleteCertificationType`, `getSubjectSkills`, `saveSkillAssignment`, `removeSkillAssignment`, `findSkillHolders`, `listCertificates`, `saveCertificate`, `setCertificateStatus`, `deleteCertificate`. | StaffAgent (27 tools together, so fold two reads and one write first), or the Workforce / Contact Center routing agents that read skills. |
| CustomerAgent | Looks up and maintains store customers, their addresses, store credit and loyalty points, and handles data access and erasure requests. | 9: `listCustomers`, `getCustomer`, `saveCustomer`, `saveCustomerAddress`, `deleteCustomerAddress`, `exportCustomerData`, `eraseCustomerData`, `adjustCustomerStoreCredit`, `adjustCustomerLoyaltyPoints`; plus the direct `validateAddress` and `normalizePhoneNumber`. | AccountAgent (20 tools together), or the commerce orders agent of the commerce group. |
| AccountAgent | Manages accounts (businesses and households): members, locations, account team, B2B terms, punchout and price lists. | 11: `listAccounts`, `setAccountMembers`, `saveAccountLocation`, `deleteAccountLocation`, `setAccountTeam`, `getB2BAccountSummary`, `saveB2BAccountSettings`, `configureB2BPunchout`, `listPriceLists`, `savePriceList`, `deletePriceList`; plus the content tools for the account item itself. | CustomerAgent, or the CRM agent of Omnichannel.Managements (contacts, leads and opportunities live on the account page). |
| PlacesAgent | Handles places and contact data: service territories, geocoding, friendly time zone names and the phone verification queue. | 11: `listServiceTerritories`, `saveServiceTerritory`, `deleteServiceTerritory`, `findServiceTerritory`, `geocodeAddress`, `getGeocodingStatus`, `listTimeZoneMaps`, `saveTimeZoneMap`, `deleteTimeZoneMap`, `listPhoneNumberVerifications`, `retryPhoneNumberVerifications`; plus `validateAddress`, `normalizePhoneNumber`, `listTimeZones`. | Territory and geocoding tools into the Dispatch agent; phone verification into the CRM agent; time zone maps into a general site-administration agent. It is the weakest grouping here and the first candidate to dissolve. |
| SeoAgent | Configures what search engines and AI agents read about the site. | From this group (4): `getSchemaOrgOptions`, `configureContentTypeSchemaOrg`, `configureSiteStructuredData` (existing), `getContentItemStructuredData` (new). | Must be completed by the Orchard group with SEO meta, robots, sitemaps and URL rewriting tools; with only these four it should stay direct or sit in ContentAgent. |
| ContentAgent (existing) | Content items and content access. | Adds `getContentItemAccessRoles`, `setContentItemAccessRoles`. | — |
| UserAccessAgent | Users and roles. | Adds `setUserDisplayName`, `setUserAvatar`. | — |
| direct | Broadly useful helpers on the main assistant. | `validateAddress`, `normalizePhoneNumber`. | — |

Totals for this group: 82 new tools, 5 existing tools kept (3 SEO, 2 Staff, with two renames proposed), 28 module folders covered (25 with source, 3 empty).

## Open questions

- Settings trio and display drivers: several settings drivers here do more than copy fields (cache-tag eviction and Liquid validation in `DisplayNameSettingsDisplayDriver`; options notifier in `UserAvatarOptionsDisplayDriver`; range checks and `IGeocodeQueue.WakeAsync()` in `GeoSettingsDisplayDriver`; `ApplySecret` with a "remove" flag in the Azure Maps, Google Maps and Google Places drivers; the `Protected*` fields of the three phone verification providers). Will `updateSiteSettings` run the site display drivers, or does each module register an "apply" hook? Without one of these the trio silently skips validation and side effects.
- Child tenant setup from a tool: `ChildTenantManager.ScheduleSetup` relies on `HttpBackgroundJob.ExecuteAfterEndOfRequestAsync`. Does that fire when the tool runs inside the chat hub or a streaming response? If not, `saveChildTenant` and `retryChildTenantSetup` need another way to start the setup.
- Typed-name confirmations (`removeChildTenant`, `removeParentTenant`): keep the confirmation argument in addition to tool approval, or is approval enough? The UI requires the typed name.
- Kind taxonomy edge cases I decided and you may want to overrule: store credit and loyalty adjustments are External (cost money) rather than Write; `exportCustomerData` is External because personal data leaves the site through the model; `changeChildTenantState` and `saveChildTenant` are Security because their permissions are `isSecurityCritical`, although they do not change access.
- `saveStaffMember` and offboarding: changing status to Terminated or setting a termination date triggers `IStaffLifecycleObserver`s in other modules (shifts, queues, access). Should that transition require approval while ordinary edits do not? One tool cannot have two approval levels today.
- Not UI actions, proposed anyway because the model needs them to do the UI's job: `validateAddress`, `normalizePhoneNumber`, `geocodeAddress`, `findSkillHolders`, `getContentItemStructuredData`. Not proposed, although the services exist: verify one phone number now (`IPhoneNumberVerificationManager.VerifyAsync`, billable), address autocomplete (Google Places, billable), travel time and routes (`IGeoService`), listing a user's role grants by source (`IRoleGrantService.ListAsync`). Say if you want any of them.
- Read permissions that do not exist: Teams has only `ManageTeams` and Geo only `ManageGeoTerritories`, so `listTeams` and `listServiceTerritories` require a manage permission although other modules read teams and territories freely through services. Accept that, or let these reads fall back to `ViewStaff` / any authenticated admin?
- Accounts overlap with the CRM group: contacts, leads and opportunities are child content items of an account and Omnichannel.Managements already has an AI tool. Who owns "create a contact under this account"?
- `staffFind` and `staffReportingLine` renames change tool names that existing AI profiles may already list in `ToolNames`; a migration or alias is needed.
- Statements marked unverified in the sections above (what happens to existing assignments and certificates when a skill or certification type is deleted, and whether orders keep a copy of a deleted account location) need a quick code check before the descriptions are final.
