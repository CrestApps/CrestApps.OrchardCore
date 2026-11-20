# Orchard Core: users, security, identity and messaging

Part of the [AI tool coverage plan](README.md). Nothing in this file is built yet. The design rules, the permission and approval model and the test plan that apply to every tool below are in [01-foundation.md](01-foundation.md) and [03-testing.md](03-testing.md); the final agent roster is in [02-agents.md](02-agents.md) and wins over the agent notes in this file.

Rules that apply to every tool in this group (the reasons are in the module sections):

- **Check the Orchard permission inside the tool.** Each tool must authorize the calling user (`IHttpContextAccessor.HttpContext.User` + `IAuthorizationService`) against the permission in its row, with the same resource the UI passes (for user tools: the target `User`; for role assignment: the `IRole`). This is on top of the per-tool `AccessAITool_{toolName}` permission that `OrchardCoreAIToolAccessEvaluator` already checks. The existing `getUserInfo`, `searchForUsers`, `getRoleInfo`, `sendEmail`, `sendSmsMessage` and `sendNotification` contain no `AuthorizeAsync` call at all (no file in `CrestApps.OrchardCore.AI.Agent` calls it).
- **Never return to the model:** `User.PasswordHash`, `User.SecurityStamp`, `User.ResetToken`, `User.UserTokens` (authenticator key, recovery codes), `User.Passkeys` (credential id, public key), `UserLoginInfo.ProviderKey`; the value of any secret, protected setting, OpenID client secret, private key, connection string or access token. Orchard itself bans `PasswordHash`, `ResetToken`, `UserTokens` and `Passkeys` from audit snapshots (`UserEventHandler.BannedProperties`).
- **Never accept from the model:** a password (new or current), a password hash, a secret, API key, auth token, connection string, PEM private key or OpenID client secret, or a reset or confirmation token. Anything the model sends has already been through the AI provider and the chat history. Where a credential is needed, the tool takes the **name** of a secret that a person created on the Secrets page, or sends the user an email link.
- **Refuse on the calling user's own account:** changing own roles, disabling, deleting, switching off own two-factor authentication, removing own passkeys.
- **Run only for a signed-in admin in the admin chat.** Tools that build links (`LinkGenerator.GetUriByAction` with the current `HttpContext`) or rely on "the calling user" must refuse when there is no authenticated `HttpContext` (phone, SMS, scheduled or background runs).
- **Tenant reload:** tools marked "reloads the tenant" call `IShellReleaseManager.RequestRelease()` or `IShellHost.ReleaseShellContextAsync`, as the UI does. The reload happens at the end of the request and drops in-memory state for the whole tenant.

## Users — `OrchardCore.Users`

**What an admin can do in the UI:**

- Access Control → Users (`/Admin/Users/Index`): search with the filter syntax (default term = text contained in the user name, `email:`, `role:`, `status:Enabled` or `status:Disabled`, `sort:Name` or `sort:Email`), filter by role ("Any role", "Authenticated (no roles)", each role the caller may list), page; bulk actions Confirm email, Enable, Disable, Delete.
- Create (`/Admin/Users/Create`): user name, email, phone number, password + confirmation (with a "Generate password" button), roles, plus the sections other features add.
- Edit (`/Admin/Users/Edit/{id}`) and View (`/Admin/Users/Display/{id}`): user name, email and phone are editable only when `LoginSettings.AllowChangingUsername`, `AllowChangingEmail`, `AllowChangingPhoneNumber` allow it (this applies to administrators too); roles.
- Row actions: Change password (`EditPassword`: an admin sets a new password for another user without knowing the current one; for the own account the current password is required), Enable or Disable, Unlock (only when locked out), Confirm email (only when not confirmed), Delete (the link is hidden for the own account).
- Settings → Security → User Login (group `userLogin`).
- Own profile (`/Admin/Users/Edit` without id, permission `ManageOwnUserInformation`), user menu.
- Not UI: Orchard command `createUser`; workflow activities `RegisterUserTask`, `AssignUserRoleTask`, `ValidateUserTask` and the user events (created, updated, deleted, enabled, disabled, confirmed, logged in, logged out).

**Already covered:**

- `getUserInfo` — finds one user by id, user name or email and returns a safe projection (`ProjectionExtensions.AsAIObject(User)`). Two problems: no permission check, and `IsLockedOut` is filled from `User.IsLockoutEnabled`, which is not the lockout state (the state is `LockoutEndUtc` in the future, `UserManager.IsLockedOutAsync`). Replace with `getUser` below.
- `searchForUsers` — must be replaced, it has two defects, confirmed by reading the code: (1) the `term` is written to `UserIndexOptions.SearchText` with an empty `QueryFilterResult<User>`, but `DefaultUsersAdminListQueryService.QueryAsync` only executes `options.FilterResult` and never reads `SearchText`, so the result is not filtered at all and the always-run `role-restriction` and `sort` terms do not run either; (2) it does not use the allow-list projection that `getUserInfo` uses (`ProjectionExtensions.AsAIObject(User)`). `listUsers` returns allow-listed fields only.
- Recipe step `Users` (usable through importOrchardCoreRecipe) — do not rely on it: `UsersStep` writes `PasswordHash`, `SecurityStamp`, `ResetToken`, `RoleNames`, `IsEnabled` straight to the session, with no identity validation, no `AssignRoleToUsers` check, no last-administrator guard and no user events. The matching deployment step (All Users) exports password hashes.
- Workflow activities are reachable through WorkflowAgent (`createOrUpdateWorkflow`, `listWorkflowActivities`).

**Settings groups:**

- `LoginSettings` / group `userLogin` / permission `ManageUsers` / secrets: none. Fields: `UseSiteTheme`, `DisableLocalLogin`, `AllowChangingUsername`, `AllowChangingEmail`, `AllowChangingPhoneNumber`, `AllowRememberMe`, `UsePersistentAuthenticationCookie`. `DisableLocalLogin` locks every local account out, so `updateSiteSettings` should warn in the approval text.
- `IdentitySettings` (`UserSettings.AllowedUserNameCharacters`, `UserSettings.RequireUniqueEmail`) has no display driver and therefore no settings page; it is read by `IdentityOptionsConfigurations` and can only be set by the `Settings` recipe step. Not a group for the generic tools.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listUsers` (replaces `searchForUsers`) | Read | no | `ListUsers` (checked against a blank `User`, as the UI does; results limited to roles the caller may list, `ListUsersInRole_{role}`) | Lists user accounts with optional filters (text in the user name, email, role, enabled or disabled), sorting and paging. Use it to find a user's id before any other user tool; it returns id, user name, email, roles and status only, never password or token data. | UserAccessAgent |
| `getUser` (replaces `getUserInfo`) | Read | no | `View Users` on the user (the permission name contains a space); the caller's own account is always allowed | Gets one user by id, user name or email: profile, roles, enabled, email confirmed, locked out until, failed sign-in count, two-factor state, number of passkeys, external login provider names, time zone, culture and custom user settings. Call it before changing a user. | UserAccessAgent |
| `saveUser` | Security | yes | `EditUsers` on the user (for a new user: on a blank `User`) | Creates a user account or updates a user's user name, email, phone number, time zone and culture. It cannot set a password: a new user is created without one, so follow with `sendUserPasswordResetEmail`; roles are changed with `setUserRoles`. | UserAccessAgent |
| `setUserRoles` | Security | yes | `EditUsers` on the user, plus `AssignRoleToUsers` on each role added or removed (`AssignRoleToUsers_{role}`) | Adds roles to a user or removes roles from a user. It refuses to change the calling user's own roles, to remove the administrator role from the only enabled administrator, and to touch a role the caller may not assign. | UserAccessAgent |
| `enableUser` | Security | yes | `EditUsers` on the user | Enables one or more disabled user accounts so they can sign in again. Use it to approve users waiting for moderation. | UserAccessAgent |
| `disableUser` | Security | yes | `EditUsers` on the user | Disables one or more user accounts so they can no longer sign in. It refuses for the calling user's own account, the site super user and the only enabled administrator. | UserAccessAgent |
| `unlockUser` | Security | yes | `EditUsers` on the user | Clears a user's lockout and failed sign-in count after too many failed attempts. Use it when `getUser` shows the user is locked out. | UserAccessAgent |
| `confirmUserEmail` | Security | yes | `EditUsers` on the user | Marks a user's email address as confirmed without sending an email. Use it only when the admin says the address is already verified; otherwise use `sendUserVerificationEmail`. | UserAccessAgent |
| `deleteUser` | Destructive | yes | `DeleteUsers` on the user (`DeleteUsersInRole_{role}`) | Permanently deletes a user account; this cannot be undone. It refuses for the calling user's own account, the site super user and the only enabled administrator; suggest `disableUser` when the admin only wants to block access. | UserAccessAgent |

**Implementation notes:**

- Services: `UserManager<IUser>`, `IUserService` (`CreateUserAsync`, `EnableAsync`, `DisableAsync`, `GetUserAsync`), `IUsersAdminListQueryService` with `IUsersAdminListFilterParser` (build the filter string from the arguments, for example `john email:acme.com role:Editor status:Enabled sort:Email`, and parse it so the always-run `role-restriction` term applies), `IRoleService`, `IUserRoleStore<IUser>`, `IPhoneFormatValidator`, `ISiteService` (for `LoginSettings`). `UsersPermissions` and `User` are in `OrchardCore.Users.Core`; `CrestApps.OrchardCore.AI.Agent` currently references only `OrchardCore.Users.Abstractions` plus `CrestApps.OrchardCore.Users.Core`.
- Resource-based authorization: `AuthorizeAsync(caller, UsersPermissions.EditUsers, targetUser)` lets `RoleAuthorizationHandler` grant through the per-role variants (`EditUsersInRole_{role}`, `DeleteUsersInRole_{role}`, `ListUsersInRole_{role}`, `AssignRoleToUsers_{role}`, `ManageUsersInRole_{role}`). Orchard does not create `DeleteUsersInRole_` or `AssignRoleToUsers_` variants for the administrator role, so only the general permission reaches administrators.
- Projection: build one allow-list DTO for `listUsers` and `getUser` (`UserId`, `UserName`, `Email`, `EmailConfirmed`, `PhoneNumber`, `PhoneNumberConfirmed`, `IsEnabled`, `RoleNames`, `TwoFactorEnabled`, lockout from `UserManager.IsLockedOutAsync` and `LockoutEndUtc`, `AccessFailedCount`, `Passkeys.Count`, `LoginInfos[].LoginProvider`). Never serialize the `User` object. Emails and phone numbers are personal data sent to the AI provider; keep page size small.
- `saveUser`: on create call `IUserService.CreateUserAsync(user, null, reportError)` (the service allows accounts with no password; the UI driver requires one, so this is a deliberate difference). On update follow `AdminController.EditPost`: `UserManager.UpdateAsync`, honor `LoginSettings.AllowChangingUsername`, `AllowChangingEmail`, `AllowChangingPhoneNumber` exactly as `UserInformationDisplayDriver` does (refuse the field, do not silently skip), validate the phone number, and call `SignInManager.RefreshSignInAsync` when the caller edited their own profile. Return identity errors through `IUserService.ProcessValidationErrors` wording.
- `setUserRoles`: the logic exists only inside `UserRoleDisplayDriver.UpdateAsync` and must be extracted: authorize each role with `AssignRoleToUsers` and the `IRole` as resource, use `IRoleService.GetAssignableRolesAsync()` (excludes `Anonymous` and `Authenticated`), keep the last-enabled-administrator guard, and call `UserManager.UpdateSecurityStampAsync` after any removal so existing sessions are invalidated. The UI lets an admin edit their own roles; the tool must not.
- Guards the UI does not enforce on the server: the single `Delete` action has no own-account or last-administrator check (the view only hides the link; the bulk action skips the own account). `IUserService.DisableAsync` returns false for the only enabled administrator, but both it and the role driver hard-code `OrchardCoreConstants.Roles.Administrator` although the admin role name is configurable (`Roles:AdminRoleName`, `SystemRoleOptions.SystemAdminRoleName`); use `IRoleService.IsAdminRoleAsync`. Also protect the user whose id is `ISite.SuperUser`.
- `unlockUser` = `ResetAccessFailedCountAsync` + `SetLockoutEndDateAsync(user, null)`. `confirmUserEmail` = `GenerateEmailConfirmationTokenAsync` + `ConfirmEmailAsync`, the token stays on the server.
- Password change is intentionally not a tool: `AdminController.EditPassword` takes the new password as form text. See `sendUserPasswordResetEmail` in the next section and the open questions.
- `enableUser`, `disableUser` accept a list of ids (the UI has them as bulk actions) and report per-user results.

**Test prompts:**

- "Show me all disabled users with an @contoso.com email."
- "Create an account for Maria Lopez, maria@contoso.com, and send her a link to set her password."
- "Jamal says he is locked out. Check his account and unlock it."
- "Make Priya an Editor and remove her from Contributor."
- "Delete the user testuser42."

## Users — registration, password reset, change email, external authentication — `OrchardCore.Users.Registration`, `OrchardCore.Users.ResetPassword`, `OrchardCore.Users.ChangeEmail`, `OrchardCore.Users.ExternalAuthentication`

**What an admin can do in the UI:**

- Settings → Security → Registration (group `userRegistration`): users must validate their email, users are moderated (new accounts are created disabled), use site theme; with external authentication: disable new registrations, no password, no user name, no email, script to generate the user name.
- Settings → Security → Reset Password (group `userResetPassword`): allow reset password, use site theme.
- Settings → Security → Change Email (group `userChangeEmail`): allow users to change their email.
- Settings → Security → User Login → External Login tab: use the external provider when it is the only one, script to sync properties.
- Users list row action "Send verification email" (shown when the email is not confirmed and `RegistrationOptions.UsersMustValidateEmail` is on).
- Moderation: there is no separate approve button (`UsersBulkAction.Approve` does nothing in the controller); an admin approves a registration by enabling the user.
- Front-end pages (register, forgot password, reset password, change email, link external logins) are self-service and not admin actions.

**Already covered:** approving a moderated registration = `listUsers` (status Disabled) + `enableUser`. Settings through the generic settings tools. Nothing else.

**Settings groups:**

- `RegistrationSettings` / group `userRegistration` / permission `ManageUsers` / secrets: none.
- `ExternalRegistrationSettings` / group `userRegistration` / permission `ManageUsers` / secrets: none. `GenerateUsernameScript` is JavaScript run on the server by `ScriptExternalLoginEventHandler`.
- `ExternalLoginSettings` / group `userLogin` / permission `ManageUsers` / secrets: none. `SyncPropertiesScript` is JavaScript run on the server at every external login and can assign roles.
- `ResetPasswordSettings` / group `userResetPassword` / permission `ManageUsers` / secrets: none.
- `ChangeEmailSettings` / group `userChangeEmail` / permission `ManageUsers` / secrets: none.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `sendUserVerificationEmail` | External | yes | `ManageUsers` (what `EmailConfirmationController.SendVerificationEmail` checks for another user; the menu item is shown on `EditUsers`) | Emails a user the link to confirm their email address. Use it when `getUser` shows the email is not confirmed; needs the Users Registration feature and a working email provider. | UserAccessAgent |
| `sendUserPasswordResetEmail` | External | yes | `EditUsers` on the user (the permission the UI's "Change password" action checks) | Emails a user a link to choose a new password. This is the only password help the assistant can give: it never sets, reads or generates a password. Needs the Users Reset Password feature with "allow reset password" turned on. | UserAccessAgent |

**Implementation notes:**

- Both use `UserEmailService` (`SendEmailConfirmationAsync`, `SendPasswordResetAsync`), which lives in the `OrchardCore.Users` module assembly (not in `OrchardCore.Users.Core`) and is registered only when `OrchardCore.Email` is enabled (`EmailStartup`). The AI.Agent project would need a reference to the module, or the link building has to be re-implemented. Both build absolute links from the current `HttpContext`.
- `sendUserPasswordResetEmail`: `IUserService.GetForgotPasswordUserAsync(userName)` sets `User.ResetToken`, then `UserEmailService.SendPasswordResetAsync(user)`. The link target (`ResetPasswordController.ResetPassword`) returns 404 unless `ResetPasswordSettings.AllowResetPassword` is true, so check the feature and the setting first and say what is missing. Never put the token or the link in the tool result.
- The script fields are code execution. `updateSiteSettings` must treat `GenerateUsernameScript`, `SyncPropertiesScript`, `UseScriptToGenerateUsername` and `UseScriptToSyncProperties` as Security changes with approval, or refuse them (open question).

**Test prompts:**

- "Send Dana a password reset link."
- "Which users are waiting for approval? Approve the two from contoso.com."
- "Resend the verification email to sam@contoso.com."
- "Turn on email verification for new registrations."

## Users — two-factor authentication, passkeys, security page — `OrchardCore.Users.2FA`, `OrchardCore.Users.2FA.AuthenticatorApp`, `OrchardCore.Users.2FA.Email`, `OrchardCore.Users.2FA.Sms`, `OrchardCore.Users.Passkeys`, `OrchardCore.Users.Security`

**What an admin can do in the UI:**

- Users list row action "Disable two-factor authentication" (`AdminTwoFactorAuthenticationController.Disable`), shown when the user has it enabled.
- Users list row action "Remove passkeys" (`AdminPasskeyController.RemoveAll`), shown when the user has passkeys; it also switches two-factor off when no other method is left.
- Settings → Security → User Login → Two-Factor Authentication tab: require two-factor for everyone or for chosen roles, allow "remember this client", number of recovery codes, authenticator app token length and display name, email subject and body (Liquid), SMS body (Liquid); Security Page tab: use site theme.
- Each user's own Security page (set up an authenticator app, show recovery codes, enable email or SMS codes, add, rename and remove passkeys) is self-service.

**Already covered:** settings through the generic settings tools. Nothing else.

**Settings groups (all on group `userLogin`, permission `ManageUsers`, secrets: none):** `TwoFactorLoginSettings` (`RequireTwoFactorAuthentication`, `AllowRememberClientTwoFactorAuthentication`, `NumberOfRecoveryCodesToGenerate`), `RoleLoginSettings` (`RequireTwoFactorAuthenticationForSpecificRoles`, `Roles`; needs `OrchardCore.Roles`), `AuthenticatorAppLoginSettings` (`UseEmailAsAuthenticatorDisplayName`, `TokenLength`), `EmailAuthenticatorLoginSettings` (`Subject`, `Body`), `SmsAuthenticatorLoginSettings` (`Body`), `UserSecuritySettings` (`UseSiteTheme`).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `disableUserTwoFactor` | Security | yes | `DisableTwoFactorAuthenticationForUsers` | Switches off two-factor authentication for a user who lost their second factor, so they can sign in with their password and set it up again. It refuses for the calling user's own account. | UserAccessAgent |
| `removeUserPasskeys` | Destructive | yes | `EditUsers` on the user | Removes all passkeys of a user, for example after a lost device; the user then has to sign in another way. It refuses for the calling user's own account. | UserAccessAgent |

**Implementation notes:**

- `disableUserTwoFactor`: `UserManager.GetTwoFactorEnabledAsync` then `SetTwoFactorEnabledAsync(user, false)`. The UI checks the permission without a resource; do the same.
- `removeUserPasskeys`: copy `AdminPasskeyController.RemoveAll` (clear `User.Passkeys`, `UpdateAsync`, then disable two-factor when `GetValidTwoFactorProvidersAsync` has no provider left in `TwoFactorOptions.Providers`).
- When `TwoFactorLoginSettings.RequireTwoFactorAuthentication` or the role rule applies, the user is forced to enrol again at next sign-in; say so in the result.
- No tool may enrol, show or reset authenticator keys, recovery codes or passkeys; those need the user's own device and are secrets.
- Turning `RequireTwoFactorAuthentication` off weakens security for everyone: Security change with approval in `updateSiteSettings`.

**Test prompts:**

- "Alex lost his phone and cannot get past the two-factor prompt. Help him get back in."
- "Remove all passkeys for user jdoe."
- "Require two-factor authentication for Administrators and Editors."

## Custom User Settings — `OrchardCore.Users.CustomUserSettings`

**What an admin can do in the UI:**

- A content type with the stereotype `CustomUserSettings` becomes a tab on the user editor; its values are stored inside the user (`User.Properties[TypeName]`), not as content items.
- The values show on the users list (hidden by default placement) and export through the Custom User Settings deployment step.

**Already covered:** the type itself (stereotype, parts, fields) through the content definition tools. The content tools do **not** cover the values, because they are not content items in the store. Recipe step `custom-user-settings` (usable through importOrchardCoreRecipe; it writes without any permission check).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `saveUserCustomSettings` | Write | no | `EditUsers` on the user, plus `ManageOwnCustomUserSettings_{ContentType}` | Sets the values of one custom user settings type for a user, given the settings type name and the parts and fields to change. Read the current values with `getUser` and the type's shape with the content type tools first. | UserAccessAgent |

**Implementation notes:**

- Read: `CustomUserSettingsService.GetAllSettingsTypesAsync()` and `GetSettingsAsync(user, settingsType)` (in `OrchardCore.Users.Core`); `getUser` returns each type the caller holds the dynamic permission for.
- Write: `IContentManager.NewAsync(type)`, merge the stored item, apply the changes, `user.Properties[type] = JObject.FromObject(contentItem)`, `UserManager.UpdateAsync(user)`. The UI validates through the content display drivers; the tool has no equivalent, so reuse the payload preparation used by `createOrUpdateContentItem` (`ContentItemPreparationService`) or validation is skipped (gap).
- Despite its name, `ManageOwnCustomUserSettings_{type}` is what `CustomUserSettingsDisplayDriver` checks for whichever user is being edited; it is implied by `ManageUsers`.

**Test prompts:**

- "Set the Department on Lee's user profile to Finance."
- "What custom settings does user mkhan have?"

## User Time Zone and User Localization — `OrchardCore.Users.TimeZone`, `OrchardCore.Users.Localization`

**What an admin can do in the UI:** on the user editor, pick the user's time zone (`UserTimeZone.TimeZoneId`) and culture (`UserLocalizationSettings.Culture`: a supported culture, "Use site's culture" = `none`, or the invariant culture).

**Already covered:** `listTimeZones` gives valid time zone ids. No dedicated tools: `getUser` returns both values and `saveUser` takes `timeZoneId` and `culture`.

**Implementation notes:** both are entity sections on the user (`SectionDisplayDriver<User, UserTimeZone>`, `SectionDisplayDriver<User, UserLocalizationSettings>`), so the tool reads and writes them with `user.As<T>()` and `user.Put(...)`. Validate the culture against `ILocalizationService.GetSupportedCulturesAsync()`. The sections exist only when the feature is enabled; ignore the argument otherwise and say so.

**Test prompts:**

- "Set Ana's time zone to Central European Time and her language to Spanish."

## Users Audit Trail — `OrchardCore.Users.AuditTrail`

**What an admin can do in the UI:**

- Settings → Security → User Audit Trail (`/Admin/Users/AuditTrail/Index`, its own controller): for each user property and custom user settings type choose a redactor (remove, keep, erase, partial asterisks, HMAC when a key is configured) used when a user snapshot is stored in an audit event.
- User events appear in the Audit Trail list (category `User`: `Registered`, `LoggedIn`, `LogInFailed` (off by default), `LogInIsLockedOut`, `Enabled`, `Disabled`, `Created`, `Updated`, `Deleted`, `Confirmed`, `PasswordReset`, `PasswordRecovered`).

**Already covered:** nothing. Events are read with `listAuditTrailEvents` and `getAuditTrailEvent` (Audit Trail section).

**Settings groups:** `AuditTrailUserEventSettings` (`UserSnapshotRedactors`: property name → redactor name) is stored in the site settings but is **not** a settings group; permission `ManageUserAuditTrailSettings` (security critical); secrets: none.

**Implementation notes:** no dedicated tool proposed. It is a privacy control that is set once; changing it by chat adds risk for little value. If wanted, expose it as a virtual group of the generic settings tools (open question). Viewing a user event needs `ViewUserAuditTrailEvents`, or `ViewOwnUserAuditTrailEvents` for the caller's own events, checked with the event as resource (`ViewUserAuditTrailEventsHandler`); both are security critical.

**Test prompts:** see Audit Trail.

## Roles — `OrchardCore.Roles`, `OrchardCore.Roles.Core`

**What an admin can do in the UI:**

- Access Control → Roles (`/Admin/Roles/Index`): list roles with description and system or administrator markers.
- Create (name, description; "Save and configure" opens the permissions).
- Edit (`/Admin/Roles/Edit/{id}`): description and the permission matrix grouped by feature or category. The page also shows the effective permissions and which ones come from `Anonymous` and `Authenticated`, and flags security-critical permissions. The administrator role's permissions cannot be edited (the controller ignores them).
- View (`Display`), Clone (new name, description, adjusted permissions), Delete (not for system roles).
- Workflow activities `UnassignUserRoleTask`, `GetUsersByRoleTask`.

**Already covered:**

- `getRoleInfo` — finds a role by id or name and serializes the `Role` with its claims; no permission check, no effective permissions. Replace with `getRole`.
- Recipe step `Roles` (usable through importOrchardCoreRecipe): creates or updates roles with `PermissionBehavior` `Replace`, `Add` or `Remove`; it skips permission changes for the administrator role but accepts any permission string and checks no Orchard permission.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listRoles` | Read | no | `ViewRoles` | Lists all roles with their description and whether each is a system role or the administrator role. Use it to get exact role names. | UserAccessAgent |
| `getRole` (replaces `getRoleInfo`) | Read | no | `ViewRoles` | Gets a role's description and permissions: the ones assigned directly and the effective ones, including those inherited from Anonymous and Authenticated. Use it to answer "what can this role do" and before `saveRole`. | UserAccessAgent |
| `listPermissions` | Read | no | `ViewRoles` | Lists the permissions that exist on this site, grouped by feature, with name, description and whether each is security critical; optional text filter. Use it to find exact permission names before `saveRole`. | UserAccessAgent |
| `saveRole` | Security | yes | `ManageRoles` | Creates a role, or updates a role's description and permissions (add, remove or replace; a new role can copy the permissions of another role). It refuses to change the administrator role's permissions and names every security-critical permission it would grant. | UserAccessAgent |
| `deleteRole` | Destructive | yes | `ManageRoles` | Deletes a custom role and removes it from every user and OpenID application that has it; this cannot be undone. System roles cannot be deleted. | UserAccessAgent |

**Implementation notes:**

- Services: `RoleManager<IRole>`, `IRoleService` (`GetRolesAsync`, `IsAdminRoleAsync`, `IsSystemRoleAsync`), `IEnumerable<IPermissionProvider>`, `ITypeFeatureProvider`, `IShellFeaturesManager`, `IPermissionGrantingService`, `RoleClaim.Create(name)`, `Permission.ClaimType`, `Permission.IsSecurityCritical`. `RolesPermissions` is in `OrchardCore.Roles.Core`.
- The grouping, the effective-permission calculation (a fake principal per role) and the name validation (no `/`, unique) are private methods of `OrchardCore.Roles.Controllers.AdminController` (`GetInstalledPermissionsAsync`, `GetEffectivePermissions`, `ValidateRoleNameAsync`); they must be re-implemented or extracted.
- `saveRole` rules beyond the UI: reject permission names that no enabled provider returns (the UI only offers installed ones); refuse to grant a security-critical permission to `Anonymous` or `Authenticated`; refuse to add permissions to a role the calling user holds unless the caller is in the administrator role (no self-escalation); list the security-critical permissions in the approval prompt. `ManageRoles` itself is security critical.
- `deleteRole`: `RoleManager.DeleteAsync` after `IsSystemRoleAsync`; the role-removed handlers (`UserRoleRemovedEventHandler`, `OpenIdApplicationRoleRemovedEventHandler`) clean up users and applications. Report how many users had the role (`UserManager.GetUsersInRoleAsync`) before approval.
- Cloning is `saveRole` with `copyPermissionsFrom`; no separate tool.

**Test prompts:**

- "What can the Moderator role do that Author cannot?"
- "Create a role called Newsletter Manager that can only send emails and edit Newsletter content."
- "Remove the permission to delete content from the Contributor role."
- "Delete the role Temp Reviewer."

## OpenID Connect management — `OrchardCore.OpenId.Management` (with `OrchardCore.OpenId`)

**What an admin can do in the UI:**

- Access Control → OpenID Connect → Applications (`/Admin/OpenId/Application`): search by display name, page; Create and Edit (client id, display name, type public or confidential, client secret, consent type, redirect URIs, post-logout redirect URIs, allowed flows: authorization code, client credentials, hybrid, implicit, password, refresh token; endpoints: logout, introspection, revocation; require PKCE, require pushed authorization requests; roles for the client credentials flow; scopes); Delete.
- Access Control → OpenID Connect → Scopes (`/Admin/OpenId/Scope`): list, Create and Edit (name, display name, description, resources), Delete.
- There is no UI to list or revoke tokens or authorizations; the background task "OpenID Cleaner" prunes them every 30 minutes.

**Already covered:** recipe steps `OpenIdApplication` and `OpenIdScope` (usable through importOrchardCoreRecipe). The application step takes `ClientSecret` as plain text in the recipe JSON, so the model must not use it for confidential clients.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listOpenIdApplications` | Read | no | `ManageApplications` | Lists the OpenID Connect client applications registered on this site with id, client id, display name and type; optional search by display name and paging. | SecurityAgent |
| `getOpenIdApplication` | Read | no | `ManageApplications` | Gets one OpenID application by id or client id: type, consent type, redirect URIs, allowed flows and endpoints, PKCE and PAR requirements, roles and scopes. It never returns the client secret. | SecurityAgent |
| `saveOpenIdApplication` | Security | yes | `ManageApplications` | Creates a public client application or updates an existing application's display name, redirect URIs, flows, endpoints, requirements, roles and scopes. It cannot set or rotate a client secret, so creating a confidential client or turning a public client into a confidential one must be done on the Applications page. | SecurityAgent |
| `deleteOpenIdApplication` | Destructive | yes | `ManageApplications` | Deletes an OpenID application; every client that uses its client id stops working. | SecurityAgent |
| `listOpenIdScopes` | Read | no | `ManageScopes` | Lists the OpenID scopes with name, display name, description and resources. | SecurityAgent |
| `saveOpenIdScope` | Security | yes | `ManageScopes` | Creates or updates an OpenID scope: name, display name, description and the resources (audiences) it grants. | SecurityAgent |
| `deleteOpenIdScope` | Destructive | yes | `ManageScopes` | Deletes an OpenID scope; applications that request it will be refused. | SecurityAgent |

**Implementation notes:**

- Services: `IOpenIdApplicationManager`, `IOpenIdScopeManager` (public, in `OrchardCore.OpenId.Core`, as is `OpenIdPermissions`). The mapping from the form to the descriptor is `OpenIdApplicationExtensions.UpdateDescriptorFromSettings`, an `internal` class in the `OrchardCore.OpenId` module; the tool must re-implement it or run the `OpenIdApplication` recipe step through the recipe executor.
- Client secret: `UpdateDescriptorFromSettings` keeps the stored secret when `ClientSecret` is empty and clears it for public clients, so updates of a confidential client without touching the secret work. OpenIddict stores only a hash; it cannot be read back.
- Roles on an application are a privilege grant (a client-credentials client acts with those roles). Recommended: require `AssignRoleToUsers` on each role added, the same as for a user, and refuse the administrator role; the UI itself only checks `ManageApplications`.
- Keep the UI validations: client id unique; no secret for public clients; scope resources must not contain `oct:{tenant name}` (`OpenIdConstants.Prefixes.Tenant`).
- Warn in the approval text when enabling the implicit or password flow, or when a redirect URI is not HTTPS.

**Test prompts:**

- "Which OpenID applications are registered and which flows does each allow?"
- "Register a public SPA client 'portal-web' with the authorization code flow, PKCE and redirect URI https://portal.contoso.com/callback."
- "Add the scope 'orders.read' with the resource 'orders-api' and allow portal-web to request it."
- "Delete the OpenID application 'old-mobile-app'."

## OpenID Connect settings — `OrchardCore.OpenId.Server`, `OrchardCore.OpenId.Validation`, `OrchardCore.OpenId.Client`, `OrchardCore.OpenId.Secrets`

**What an admin can do in the UI:**

- Settings → OpenID Connect → Authorization Server (`/Admin/OpenId/ServerConfiguration`, its own controller): access token format, authority, token encryption, signing and encryption certificates (store location, store name, thumbprint), endpoint paths, allowed flows, rolling refresh tokens, reference tokens, require PKCE, require PAR, require end-session confirmation; with OpenID Connect Secrets: signing key and encryption key secret names. Saving reloads the tenant.
- Settings → OpenID Connect → Token Validation (`/Admin/OpenId/ValidationConfiguration`, its own controller): audience, authority, metadata address, tenant, disable token type validation. Saving reloads the tenant.
- Settings → OpenID Connect → Authentication Client (settings group): display name, authority, client id, client secret, callback paths, scopes, response type and mode, store external tokens, extra parameters.

**Already covered:** recipe steps `OpenIdServerSettings`, `OpenIdValidationSettings`, `OpenIdClientSettings` (usable through importOrchardCoreRecipe; the client step takes the client secret as plain text).

**Settings groups:**

- `OpenIdClientSettings` / group `OrchardCore.OpenId.Client` / permission `ManageClientSettings` / secrets: `ClientSecret` (data-protected value) or `ClientSecretSecretName` (name of a secret).
- `OpenIdServerSettings` — stored in the site settings document but **not** a settings group (`DisplayDriver<OpenIdServerSettings>`, saved by `IOpenIdServerService.UpdateSettingsAsync` after `ValidateSettingsAsync`) / permission `ManageServerSettings` / secrets: none as values; `SigningKeySecretName` and `EncryptionKeySecretName` are secret names.
- `OpenIdValidationSettings` — same pattern with `IOpenIdValidationService` / permission `ManageValidationSettings` / secrets: none.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `getOpenIdSettings` (only if the generic settings tools cannot host these two pages) | Read | no | `ManageServerSettings` or `ManageValidationSettings`, by area | Gets the OpenID authorization server settings or the token validation settings, with their JSON schema and any validation problems. | SecurityAgent |
| `updateOpenIdSettings` (only if the generic settings tools cannot host these two pages) | Security | yes | `ManageServerSettings` or `ManageValidationSettings`, by area | Updates the OpenID authorization server settings or the token validation settings after validating them. It reloads the tenant, and a wrong value can stop every client from signing in. | SecurityAgent |

**Implementation notes:**

- Preferred: let `listSiteSettingsGroups`, `getSiteSettings` and `updateSiteSettings` register "virtual" groups (suggested ids `openIdServer`, `openIdValidation`) backed by the two services, so validation (`ValidateSettingsAsync`) and the tenant reload are kept. The two tools above are the fallback.
- `IOpenIdServerService` and `IOpenIdValidationService` are declared in the `OrchardCore.OpenId` module assembly, not in `OrchardCore.OpenId.Core`; calling them needs a module reference, otherwise use the recipe steps.
- Writing these settings with the plain `Settings` recipe step or `applySiteSettings` skips validation and the reload.
- For the client group the generic tools must mask `ClientSecret` and accept only `ClientSecretSecretName`.

**Test prompts:**

- "Is the OpenID server configured correctly? Which flows are allowed?"
- "Require PKCE for all OpenID clients."
- "Point token validation at the authority https://login.contoso.com."

## Security headers — `OrchardCore.Security`

**What an admin can do in the UI:** Settings → Security → Security Headers: Content-Security-Policy directives, X-Content-Type-Options, Permissions-Policy, Referrer-Policy. Saving reloads the tenant. When the values come from configuration (`SecuritySettings.FromConfiguration`) the driver handles them differently (not verified in detail).

**Already covered:** generic settings tools. The brief mentions "credential settings" for this module; the module contains only the security headers page.

**Settings groups:** `SecuritySettings` / group `SecurityHeaders` / permission `ManageSecurityHeadersSettings` / secrets: none.

**Implementation notes:** a wrong Content-Security-Policy can break the admin UI and the chat widget itself; `updateSiteSettings` for this group should be a Security change with approval and the description should tell the model to change one directive at a time.

**Test prompts:**

- "Show the current Content-Security-Policy."
- "Allow images from https://cdn.contoso.com in the content security policy."

## CORS — `OrchardCore.Cors`

**What an admin can do in the UI:** Settings → Security → Cross-Origin Resource Sharing (`/Admin/Cors`, its own controller): add, edit and remove CORS policies (name, allow any or listed origins, headers and methods, exposed headers, allow credentials, default policy). The page refuses a policy that allows any origin together with credentials. Saving reloads the tenant.

**Already covered:** the `Settings` recipe step can write the `CorsSettings` property (without the validation or the reload).

**Settings groups:** `CorsSettings` (`Policies[]` of `CorsPolicySetting`) — stored in the site settings but **not** a settings group / permission `ManageCorsSettings` (security critical) / secrets: none.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `getCorsSettings` (only if the generic settings tools cannot host this page) | Read | no | `ManageCorsSettings` | Gets the site's CORS policies. | SecurityAgent |
| `updateCorsSettings` (only if the generic settings tools cannot host this page) | Security | yes | `ManageCorsSettings` | Replaces the site's CORS policies. It refuses a policy that allows any origin together with credentials, and reloads the tenant. | SecurityAgent |

**Implementation notes:** `CorsService.GetSettingsAsync()` is public but `UpdateSettingsAsync` is `internal`; write with `ISiteService.LoadSiteSettingsAsync()`, `Put(nameof(CorsSettings), settings)`, `UpdateSiteSettingsAsync`, then `IShellHost.ReleaseShellContextAsync`. Keep the any-origin-plus-credentials check from `AdminController.IndexPOST`. Preferred: a virtual group `cors` in the generic settings tools.

**Test prompts:**

- "Which origins are allowed to call our API?"
- "Allow https://app.contoso.com to call the site with GET and POST."

## HTTPS — `OrchardCore.Https`

**What an admin can do in the UI:** Settings → Security → HTTPS: require HTTPS, permanent redirect, HSTS mode, SSL port. Saving reloads the tenant.

**Already covered:** generic settings tools.

**Settings groups:** `HttpsSettings` / group `Https` / permission `ManageHttps` / secrets: none. Fields: `RequireHttps`, `RequireHttpsPermanent`, `EnableStrictTransportSecurity`, `StrictTransportSecurityMode`, `SslPort`.

**Implementation notes:** turning on `RequireHttps` or HSTS on a site without working TLS makes the site, including the chat, unreachable; Security change with approval.

**Test prompts:**

- "Is HTTPS enforced on this site?"

## Reverse Proxy — `OrchardCore.ReverseProxy`

**What an admin can do in the UI:** Settings → Reverse Proxy: which forwarded headers to trust (X-Forwarded-For, -Host, -Proto), known networks, known proxies. Saving reloads the tenant.

**Already covered:** generic settings tools.

**Settings groups:** `ReverseProxySettings` / group `ReverseProxy` / permission `ManageReverseProxySettings` / secrets: none.

**Implementation notes:** these values decide which client IP and scheme the site sees, which rate limits, HTTPS redirects and audit trail IP addresses depend on; Security change with approval.

**Test prompts:**

- "Trust the X-Forwarded-For and X-Forwarded-Proto headers from 10.0.0.0/8."

## Rate Limits — `OrchardCore.RateLimits`

**What an admin can do in the UI:**

- Tools → Rate Limits (`/Admin/RateLimits`): list policies with search (name, description, path, group) and status filter; the page also shows the built-in route and group limits that modules registered through `RateLimitsOptions` (read-only).
- Create and Edit a policy: name, description, scope (Global, Endpoint with a request path, Group with a rate-limit group). Add, edit and delete limiters on a policy (`LimiterController`): fixed window, sliding window, concurrency, token bucket. The target and the limiters can only be changed while the policy is disabled.
- Enable, Disable, Clone (the copy starts disabled), Delete; bulk Enable, Disable, Delete. Enabling or disabling reloads the tenant.

**Already covered:** recipe step `CreateOrUpdateRateLimitPolicies` (usable through importOrchardCoreRecipe) creates and updates policies. Listing, enabling, disabling and deleting are not covered.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listRateLimitPolicies` | Read | no | `ManageRateLimits` | Lists the site's rate-limit policies with scope, path or group, status and limiters, plus the built-in route and group limits that modules register. Optional search text and status filter. | SecurityAgent |
| `saveRateLimitPolicy` | Security | yes | `ManageRateLimits` | Creates or updates a rate-limit policy: name, description, scope (global, endpoint path or group) and its limiters (fixed window, sliding window, concurrency, token bucket). It always saves a new policy as disabled (turn it on with `enableRateLimitPolicy`), and the target and limiters of an enabled policy cannot be changed until it is disabled. | SecurityAgent |
| `enableRateLimitPolicy` | Security | yes | `ManageRateLimits` | Enables one or more rate-limit policies. Requests over the limit then get 429 responses, and the tenant reloads. | SecurityAgent |
| `disableRateLimitPolicy` | Security | yes | `ManageRateLimits` | Disables one or more rate-limit policies, which removes that protection, and reloads the tenant. | SecurityAgent |
| `deleteRateLimitPolicy` | Destructive | yes | `ManageRateLimits` | Deletes one or more rate-limit policies. | SecurityAgent |

**Implementation notes:**

- Service: `IRateLimitPolicyStore` (`GetAllAsync(PolicyVersion)`, `FindByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `SetStatusAsync`), models `RateLimitPolicy` and `RateLimitLimiter` (an `Entity` whose source-specific options are edited by display drivers), keyed `IRateLimiterSource` services, `RateLimitsOptions` for the built-in limits; all in `OrchardCore.RateLimits.Core`, as is `RateLimitsPermissions`.
- Validation (`ValidatePolicy`), the "no changes while enabled" rule (`AreLimiterChangesAllowed`), the reload rule (`ShouldReloadPolicy`) and the built-in list (`BuildBuiltInRouteLimits`, `BuildBuiltInGroupLimits`) are private to `AdminController`. The simplest route for `saveRateLimitPolicy` is to build the `CreateOrUpdateRateLimitPolicies` step and reuse its validation.
- The UI can create a policy that is enabled straight away (`CreatePost` reloads the tenant when `IsEnabled` is set). The tool should not: creating disabled and enabling in a second approved step gives the admin a chance to review the limits.
- A too-strict global policy can lock everyone out, including the chat; show the limit numbers in the approval text.

**Test prompts:**

- "What rate limits are active on this site?"
- "Limit /api/contact to 10 requests per minute per IP address."
- "Disable the 'Login throttle' policy for now."

## ReCaptcha — `OrchardCore.ReCaptcha`, `OrchardCore.ReCaptcha.Users`

**What an admin can do in the UI:** Settings → Security → reCaptcha: site key, secret key, script and API URLs. `OrchardCore.ReCaptcha.Users` adds the captcha to the login, registration, forgot and reset password forms (no settings of its own). The ReCaptcha content part for forms and the workflow task `ValidateReCaptchaTask` come with the Forms and Workflows features.

**Already covered:** generic settings tools; the `ReCaptchaPart` through the content definition tools; the workflow task through WorkflowAgent.

**Settings groups:** `ReCaptchaSettings` / group `recaptcha` / permission `ManageReCaptchaSettings` / secrets: `SecretKey`. Note: the driver stores `SecretKey` unprotected in the site settings and sends it back to the edit form, so `getSiteSettings` must mask it explicitly; there is no `*SecretName` alternative, so the secret key cannot be set by the assistant.

**Implementation notes:** no tools. Turning `OrchardCore.ReCaptcha.Users` on without valid keys blocks sign-in forms; that is a feature change (`enableSiteFeature`), mention the risk there.

**Test prompts:**

- "Is reCAPTCHA configured for the login page?"

## Secrets — `OrchardCore.Secrets`

**What an admin can do in the UI:**

- Tools → Security → Secrets (`/Admin/Secrets/Index`): list with search (name, description, store, type), status filter (expired, expires within 30 days, never expires) and type filter; bulk Delete.
- Create: pick a type (text secret, RSA key pair generated on the server with a key size, X.509 certificate from PEM or from a certificate store location, name and thumbprint), name, store, description, expiry date.
- Edit: description and expiry; values are write-only (a text value or PEM private key is never shown, an empty field keeps the current one, RSA keys cannot be changed); the store cannot be changed here (`EditPost` rejects a different store). Delete.
- Tools → Security → Migrate Secrets (`/Admin/Secrets/Migration/Index`): move credentials that modules still keep as protected values in their settings into named secrets; reloads the tenant.
- Tools → Security → Secret Stores (`/Admin/Secrets/Stores/Index`): move one secret or all secrets from one writable store to another, with a confirmation checkbox.
- A warning filter shows expiring secrets in the admin.

**Already covered:** recipe step `Secrets` (usable through importOrchardCoreRecipe). It can import plain values, so the model must not use it to create secrets.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listSecrets` | Read | no | `ViewSecrets` | Lists secrets by name, store, type, description and created, updated and expiry dates, with optional search, type and expiry filters, plus the available secret stores. It never returns a secret value; use it to find a secret name to reference in a settings page or to spot expiring secrets. | SecurityAgent |
| `saveSecret` | Security | yes | `ManageSecrets` | Creates an RSA key secret (the key pair is generated on the server) or an X.509 secret that points to a certificate in the server's certificate store, or updates any secret's description and expiry date. It cannot set a text value, a PEM certificate or a private key; a person must enter those on the Secrets page. | SecurityAgent |
| `deleteSecret` | Destructive | yes | `ManageSecrets` | Deletes a secret from its store; this cannot be undone, and every setting that references the secret by name stops working. | SecurityAgent |
| `moveSecrets` | Security | yes | `ManageSecrets` | Moves one secret, or all secrets, from one writable secret store to another, for example from the database store to Azure Key Vault, and reports the result per secret. | SecurityAgent |

**Implementation notes:**

- Services: `ISecretManager` (`GetSecretInfosAsync`, `GetSecretAsync`, `SaveSecretAsync(name, secret, [store], SecretSaveOptions)`, `RemoveSecretAsync`, `GetStores()` with `ISecretStore.IsReadOnly`), `ISecretStoreOperations` (`MoveSecretAsync`, `MoveAllSecretsAsync`), `ISecretTypeProvider`; in `OrchardCore.Secrets.Abstractions` and `OrchardCore.Secrets.Core`. `SecretsPermissions` is declared in the module assembly (`OrchardCore.Secrets/Permissions.cs`), so the tool needs a module reference or a mirrored permission as in `OrchardCorePermissions`.
- `listSecrets` uses `SecretInfo` only. Never call `GetSecretAsync` to build a result.
- `saveSecret`: to change only the metadata of a text secret, load the secret and save it back unchanged with new `SecretSaveOptions`; the value stays inside the tool. Secret-name validation and the type switch are private helpers of `AdminController` (`CreateSecretFromModel`, `CreateRsaKeySecret`, `CreateX509Secret`).
- Not proposed: Migrate Secrets. The work is done by one `DisplayDriver<SecretMigration>` per module (for example `SmtpSecretMigrationDisplayDriver`, `OpenIdClientSecretMigrationDisplayDriver`) driven by form binding, then `SecretMigration.UpdateSettingsAsync()`; there is no service to call (open question).
- A deleted or moved secret can break email, SMS, external sign-in or OpenID signing; `deleteSecret` should say which settings reference the name when it can find them (no index exists; unverified whether a "where used" lookup is available).

**Test prompts:**

- "Which secrets expire in the next 30 days?"
- "Create an RSA key secret called 'OpenId.SigningKey' that expires in one year."
- "Move all secrets from the database store to Azure Key Vault."
- "Delete the secret 'Old.Smtp.Password'."

## Secrets – Azure Key Vault — `OrchardCore.Secrets.AzureKeyVault`

**What an admin can do in the UI:** nothing of its own; it adds a secret store that shows up as a store choice on the Secrets pages.

**Already covered:** `listSecrets` returns the stores; `moveSecrets` can target it.

**Implementation notes:** configured only in configuration (`Secrets:AzureKeyVault` in the shell configuration: `AzureClient`, `NamePrefix`). No tools.

## Facebook — `OrchardCore.Facebook`, `OrchardCore.Facebook.Login`, `OrchardCore.Facebook.Widgets`, `OrchardCore.Facebook.Pixel`

**What an admin can do in the UI:** Settings → Integrations → Meta App (app id, app secret, SDK init options, SDK file and version); Settings → Security → Authentication → Meta (callback path, save tokens); Settings → Integrations → Meta Pixel (pixel id, Conversions API access token, test event code). The Widgets feature adds Facebook plugin widgets (content). The Pixel feature adds the workflow task `MetaConversionsApiEventTask`.

**Already covered:** generic settings tools; widgets through the content tools; the workflow task through WorkflowAgent; recipe steps `FacebookCoreSettings` and `FacebookLoginSettings` (usable through importOrchardCoreRecipe).

**Settings groups:**

- `FacebookSettings` / group `OrchardCore.Facebook` / permission `ManageFacebookApp` / secrets: `AppSecret` (data-protected) or `AppSecretSecretName`. Reloads the tenant.
- `FacebookLoginSettings` / group `OrchardCore.Facebook.Login` / permission `ManageFacebookApp` / secrets: none. Reloads the tenant.
- `FacebookPixelSettings` / group `facebook-pixel` / permission: the driver checks `ManageFacebookApp`, the menu entry uses `ManageFacebookPixel` / secrets: `ConversionsApiAccessToken` (data-protected; the driver unprotects it and sends the plain token to the edit form, so `getSiteSettings` must mask it explicitly).

**Implementation notes:** no tools.

## GitHub — `OrchardCore.GitHub.Authentication`

**What an admin can do in the UI:** Settings → Security → Authentication → GitHub: client id, client secret, callback path, save tokens.

**Already covered:** generic settings tools; recipe step `GitHubAuthenticationSettings`.

**Settings groups:** `GitHubAuthenticationSettings` / group `OrchardCore.GitHub.Authentication` / permission `ManageGitHubAuthentication` / secrets: `ClientSecret` (data-protected) or `ClientSecretSecretName`. Reloads the tenant.

**Implementation notes:** no tools.

## Google — `OrchardCore.Google.GoogleAuthentication`, `OrchardCore.Google.Analytics`, `OrchardCore.Google.TagManager`

**What an admin can do in the UI:** Settings → Security → Authentication → Google (client id, client secret, callback path, save tokens); Settings → Integrations → Google Analytics (tracking id); Settings → Integrations → Google Tag Manager (container id).

**Already covered:** generic settings tools. No module-specific recipe step found; only the generic `Settings` step.

**Settings groups:**

- `GoogleAuthenticationSettings` / group `OrchardCore.Google.GoogleAuthentication` / permission `ManageGoogleAuthentication` / secrets: `ClientSecret` (data-protected) or `ClientSecretSecretName`. Reloads the tenant.
- `GoogleAnalyticsSettings` / group `OrchardCore.Google.Analytics` / permission `ManageGoogleAnalytics` / secrets: none.
- `GoogleTagManagerSettings` / group `OrchardCore.Google.TagManager` / permission `ManageGoogleTagManager` / secrets: none.

**Implementation notes:** no tools.

## Microsoft Authentication — `OrchardCore.Microsoft.Authentication.MicrosoftAccount`, `OrchardCore.Microsoft.Authentication.AzureAD`

**What an admin can do in the UI:** Settings → Security → Authentication → Microsoft (app id, app secret, callback path, save tokens) and Settings → Security → Authentication → Microsoft Entra ID (display name, app id, tenant id, callback path, save tokens).

**Already covered:** generic settings tools; recipe steps `MicrosoftAccountSettings` and `AzureADSettings`.

**Settings groups:**

- `MicrosoftAccountSettings` / group `OrchardCore.Microsoft.Authentication.MicrosoftAccount` / permission `ManageMicrosoftAuthentication` / secrets: `AppSecret` (data-protected) or `AppSecretSecretName`. Reloads the tenant.
- `AzureADSettings` / group `OrchardCore.Microsoft.Authentication.AzureAD` / permission `ManageMicrosoftAuthentication` / secrets: none. Reloads the tenant.

**Implementation notes:** no tools.

## Twitter (X) — `OrchardCore.Twitter`, `OrchardCore.Twitter.Signin`

**What an admin can do in the UI:** Settings → Integrations → X (Twitter) (consumer key, consumer secret, access token, access token secret) and Settings → Security → Authentication → Sign In with X (Twitter) (callback path, save tokens). Workflow task `UpdateTwitterStatusTask` (posts a tweet).

**Already covered:** generic settings tools; recipe step `TwitterSettings`; the workflow task through WorkflowAgent.

**Settings groups:**

- `TwitterSettings` / group `OrchardCore.Twitter` / permission `ManageTwitter` / secrets: `ConsumerSecret` or `ConsumerSecretSecretName`, `AccessTokenSecret` or `AccessTokenSecretSecretName`; also mask `AccessToken` (the driver returns it as plain text). Reloads the tenant.
- `TwitterSigninSettings` / group `OrchardCore.Twitter.Signin` / permission `ManageTwitterSignin` / secrets: none. Reloads the tenant.

**Implementation notes:** no tools. For all five sign-in providers: a wrong client id or secret breaks that provider's sign-in for everyone, and with `LoginSettings.DisableLocalLogin` on there is no other way in; Security change with approval. The assistant can wire a provider to an existing named secret (`*SecretName`) but can never supply the secret itself.

**Test prompts (external sign-in providers together):**

- "Which external sign-in providers are set up on this site?"
- "Set up Google sign-in with client id 1234.apps.googleusercontent.com and use the secret named 'Google.ClientSecret'."
- "Set the Google Analytics tracking id to G-ABC123."

## Audit Trail — `OrchardCore.AuditTrail`

**What an admin can do in the UI:**

- Tools → Audit Trail (`/Admin/AuditTrail/{correlationId?}`): list events with the filter syntax (default term = user name, `id:`, `category:`, `event:`, `date:`, `userid:`, `sort:`), follow a correlation id, page; open one event (`/Admin/AuditTrail/Display/{auditTrailEventId}`) to see its details (user snapshot, content version and so on, depending on the event provider).
- Settings → Audit Trail (group `AuditTrail`): which events of which category are recorded, whether client IP addresses are stored, and trimming (retention days, disable).
- Background task "Audit Trail Events Purger" trims events once a day.

**Already covered:** settings through the generic settings tools. Nothing reads events today.

**Settings groups:**

- `AuditTrailSettings` / group `AuditTrail` / permission `ManageAuditTrailSettings` / secrets: none (`Categories[].Events[].IsEnabled`, `ClientIpAddressAllowed`).
- `AuditTrailTrimmingSettings` / group `AuditTrail` / permission `ManageAuditTrailSettings` / secrets: none (`RetentionDays`, `Disabled`; `LastRunUtc` is read-only).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listAuditTrailEvents` | Read | no | `ViewAuditTrail` | Lists audit trail events, newest first, with optional filters for category, event name, user, date range and correlation id, and paging. Use it to answer "who did what and when". | direct |
| `getAuditTrailEvent` | Read | no | `ViewAuditTrail`, plus the event type's own permission (user events: `ViewUserAuditTrailEvents` or `ViewOwnUserAuditTrailEvents`) | Gets one audit trail event by id with its details, such as the user snapshot or the content item version it recorded. | direct |

**Implementation notes:**

- Services: `IAuditTrailAdminListQueryService.QueryAsync(page, pageSize, AuditTrailIndexOptions)` with the audit trail filter parser (build the filter string from the arguments), `IAuditTrailManager.GetEventAsync(eventId)`; `AuditTrailPermissions` is in `OrchardCore.AuditTrail.Abstractions`.
- `AuditTrailEvent` fields: `EventId`, `Name`, `Category`, `CorrelationId`, `UserId`, `UserName`, `ClientIpAddress`, `CreatedUtc`, plus event data in `Properties`. Return `ClientIpAddress` only in `getAuditTrailEvent`.
- Event details are authorized per event type by display drivers (`AuditTrailUserEventDisplayDriver` checks `ViewUserAuditTrailEvents` with the event as resource). The tool has no display pipeline, so it must apply the same check per event type or return only the header fields for types it does not know (gap).
- User snapshots are already redacted when recorded, and the banned properties are never stored.
- Not proposed: a "trim now" tool. The UI has none; `IAuditTrailManager.TrimEventsAsync` is only called by the background task.
- Content audit events (restore a version, and so on) live in `OrchardCore.Contents/AuditTrail` and belong to the content group.

**Test prompts:**

- "Who changed the About page last week?"
- "Show failed and locked-out sign-in events for user jdoe in the last 3 days."
- "What happened under correlation id 4f2a…?"
- "Keep audit trail events for 90 days."

## Notifications — `OrchardCore.Notifications`, `OrchardCore.Notifications.Email`

**What an admin can do in the UI:**

- Bell in the admin navbar and `/Admin/notifications`: the signed-in user's own notifications with filters (status read or unread, type, text, sort), bulk Mark as read, Mark as unread, Remove; Mark all as read; toggle one; delete one; open one (`/Admin/notifications/{id}`).
- On the user editor: notification preferences (order of delivery methods, opt-out per method, methods per notification type) through `UserNotificationPreferencesPartDisplayDriver`.
- Workflow tasks `NotifyUserTask`, `NotifyContentOwnerTask`. `OrchardCore.Notifications.Email` adds the email delivery method (the SMS method is the feature `OrchardCore.Notifications.Sms` in the SMS module).

**Already covered:** `sendNotification` (sends to any user id; no Orchard permission check, and Orchard has no "send notification" permission). Workflow tasks through WorkflowAgent.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listNotifications` | Read | no | `ManageNotifications` | Lists the calling user's own notifications, newest first, with optional filters for read or unread, type and text, and paging. | direct |
| `markNotifications` | Write | no | `ManageNotifications` | Marks the calling user's notifications as read or unread, either the given ids or all of them. | direct |
| `deleteNotifications` | Destructive | yes | `ManageNotifications` | Permanently removes the given notifications of the calling user. | direct |

**Implementation notes:**

- Services: `INotificationsAdminListQueryService.QueryAsync(page, pageSize, ListNotificationOptions, updater)`, `INotificationManager` (`FindByIdAsync`, `ListAsync(NotificationFilter)`, `MarkAsReadAsync`, `MarkAsUnreadAsync`, `DeleteAsync`), `NotificationFilter` (`UserId`, `Type`, `IsRead`, `Skip`, `Take`). `CrestApps.OrchardCore.AI.Agent` already references `OrchardCore.Notifications.Core`.
- Every call must force `UserId` = the calling user and verify `notification.UserId` before changing or deleting by id, as `AdminController` does. `ManageNotifications` is granted to `Authenticated` by default and is only about the user's own notifications; no tool may read another user's notifications.
- Notification preferences of a user are not proposed (low value); if wanted, add them as fields of `saveUser`.
- `sendNotification` should gain a permission check (open question which one) because it can message any user.

**Test prompts:**

- "Do I have any unread notifications?"
- "Mark all my notifications as read."
- "Delete my notifications about the failed import."

## Email — `OrchardCore.Email`

**What an admin can do in the UI:**

- Settings → Communication → Email (group `email`): choose the default provider among the enabled ones; the providers add their own sections to the same page.
- Tools → Testing → Email Test (`/Admin/Email/Test`): pick a provider and send a test message (to, from, cc, bcc, reply-to, subject, body); provider errors are shown.
- Workflow task `EmailTask`.

**Already covered:** `sendEmail` (default provider only, approval required, no Orchard permission check). Settings through the generic settings tools. The workflow task through WorkflowAgent.

**Settings groups:** `EmailSettings` / group `email` / permission `ManageEmailSettings` / secrets: none (`DefaultProviderName`).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `sendTestEmail` | External | yes | `ManageEmailSettings` | Sends a test email through a chosen email provider (or the default one) and returns the provider's result or error. Use it to check email settings after changing them; it also lists the available providers when none is given. | SiteConfigurationAgent |

**Implementation notes:** `IEmailService.SendAsync(message, providerName)`, `IEmailProviderResolver.GetAsync(name)`, `IOptions<EmailProviderOptions>.Providers`, `EmailOptions.DefaultProviderName`; handle `InvalidEmailProviderException` like `AdminController.Test`. Default the recipient to the calling user's own email and keep subject and body fixed or short, so the tool cannot be used as a second, less restricted `sendEmail`. Never include provider settings in the result.

**Test prompts:**

- "Send a test email to me through SMTP."
- "Email is not going out. Check the email settings and test them."

## SMTP Email Provider — `OrchardCore.Email.Smtp`

**What an admin can do in the UI:** the SMTP section of Settings → Communication → Email: enable, default sender, delivery method (network or pickup directory), host, port, encryption, credentials (user name, password), proxy, ignore invalid SSL certificate.

**Already covered:** generic settings tools; `sendTestEmail`.

**Settings groups:** `SmtpSettings` / group `email` / permission `ManageEmailSettings` / secrets: `Password` (data-protected) or `PasswordSecretName`. Also sensitive: `IgnoreInvalidSslCertificate`, `PickupDirectoryLocation` (a server file path).

**Implementation notes:** no tools.

## Azure Communication Services Email — `OrchardCore.Email.Azure`

**What an admin can do in the UI:** the Azure section of Settings → Communication → Email: enable, default sender, connection string.

**Already covered:** generic settings tools; `sendTestEmail`.

**Settings groups:** `AzureEmailSettings` / group `email` / permission `ManageEmailSettings` / secrets: `ConnectionString` (data-protected) or `ConnectionStringSecretName`.

**Implementation notes:** no tools. The module also registers a "default Azure" provider configured in configuration (`DefaultAzureEmailOptions`); nothing to do there.

## SMS — `OrchardCore.Sms`, `OrchardCore.Notifications.Sms`

**What an admin can do in the UI:**

- Settings → Communication → SMS (group `sms`): choose the default provider; the Twilio section (enable, phone number, account SID, auth token).
- Tools → Testing → SMS Test (`/Admin/sms/test`): pick a provider and a phone number; a fixed test message is sent.
- Workflow task `SmsTask`; an SMS health check when Health Checks is enabled; `OrchardCore.Notifications.Sms` adds SMS as a notification method.

**Already covered:** `sendSmsMessage` (default provider, approval required, no Orchard permission check). Settings through the generic settings tools. The workflow task through WorkflowAgent.

**Settings groups:**

- `SmsSettings` / group `sms` / permission `ManageSmsSettings` / secrets: none (`DefaultProviderName`).
- `TwilioSettings` / group `sms` / permission `ManageSmsSettings` / secrets: `AuthToken` (data-protected) or `AuthTokenSecretName`.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `sendTestSms` | External | yes | `ManageSmsSettings` | Sends the fixed test text message to a phone number through a chosen SMS provider and returns the provider's result or error. Each message costs money; use it only to check SMS settings. | SiteConfigurationAgent |

**Implementation notes:** `ISmsProviderResolver.GetAsync(providerName)`, `ISmsProvider.SendAsync(new SmsMessage { To, Body })`, `IPhoneFormatValidator`, `IOptions<SmsProviderOptions>.Providers` (only enabled ones), as in `AdminController.Test`. Keep the fixed body ("This is a test SMS message.") so it is not a second `sendSmsMessage`.

**Test prompts:**

- "Send a test SMS to +15551234567 using Twilio."
- "Which SMS provider is the default?"

## Azure Communication Services SMS — `OrchardCore.Sms.Azure`

**What an admin can do in the UI:** the Azure section of Settings → Communication → SMS: enable, phone number, connection string.

**Already covered:** generic settings tools; `sendTestSms`.

**Settings groups:** `AzureSmsSettings` / group `sms` / permission `ManageSmsSettings` / secrets: `ConnectionString` (data-protected) or `ConnectionStringSecretName`.

**Implementation notes:** no tools.

## No tools needed

| Module | Reason | Settings group (if any) |
| --- | --- | --- |
| OrchardCore.Users — `OrchardCore.Users.Authentication.CacheTicketStore` | Server-side ticket store, no UI | none |
| OrchardCore.Users — `OrchardCore.Users.TimeZone`, `OrchardCore.Users.Localization` | Two fields on the user editor; covered by `getUser` and `saveUser` | none |
| OrchardCore.Users — `OrchardCore.Users.AuditTrail` | One redaction settings page (own controller); events are read through the audit trail tools | `AuditTrailUserEventSettings`, not a group (`ManageUserAuditTrailSettings`) |
| OrchardCore.Users — self-service pages (login, register, forgot or reset password, change email, Security page, external logins) | Done by the user for their own account; involve passwords, codes and devices | `userLogin`, `userRegistration`, `userResetPassword`, `userChangeEmail` |
| OrchardCore.OpenId — `OrchardCore.OpenId.Client`, `OrchardCore.OpenId.Secrets` | Settings only | `OrchardCore.OpenId.Client`; the secrets feature adds two secret-name fields to the server page |
| OrchardCore.Security | Settings page only | `SecurityHeaders` |
| OrchardCore.Https | Settings page only | `Https` |
| OrchardCore.ReverseProxy | Settings page only | `ReverseProxy` |
| OrchardCore.ReCaptcha | Settings page only; part and workflow task covered elsewhere | `recaptcha` |
| OrchardCore.Secrets.AzureKeyVault | A secret store configured in configuration | none |
| OrchardCore.Facebook | Settings pages; widgets are content; workflow task | `OrchardCore.Facebook`, `OrchardCore.Facebook.Login`, `facebook-pixel` |
| OrchardCore.GitHub | Settings page only | `OrchardCore.GitHub.Authentication` |
| OrchardCore.Google | Settings pages only | `OrchardCore.Google.GoogleAuthentication`, `OrchardCore.Google.Analytics`, `OrchardCore.Google.TagManager` |
| OrchardCore.Microsoft.Authentication | Settings pages only | `OrchardCore.Microsoft.Authentication.MicrosoftAccount`, `OrchardCore.Microsoft.Authentication.AzureAD` |
| OrchardCore.Twitter | Settings pages; workflow task | `OrchardCore.Twitter`, `OrchardCore.Twitter.Signin` |
| OrchardCore.DataProtection.Azure | Configured only in configuration (`DataProtection:Azure`); no UI, no permissions | none |
| OrchardCore.Antivirus (`OrchardCore.Antivirus.ClamAV`) | Configured only in configuration (`ClamAvOptions.ConfigSection`); scans uploads through a file event handler; no UI | none |
| OrchardCore.Email.Smtp | Provider with a settings section; tested with `sendTestEmail` | `email` |
| OrchardCore.Email.Azure | Provider with a settings section; tested with `sendTestEmail` | `email` |
| OrchardCore.Sms.Azure | Provider with a settings section; tested with `sendTestSms` | `sms` |
| OrchardCore.Cors (if hosted by the generic settings tools) | One page of policies | `CorsSettings`, not a group (`ManageCorsSettings`) |

## Proposed agents for this group

| Agent | Purpose (one sentence) | Tools | Could merge with |
| --- | --- | --- | --- |
| UserAccessAgent | Manages who can sign in and what they may do: user accounts, their status and sign-in recovery, roles and permissions. | `listUsers`, `getUser`, `saveUser`, `setUserRoles`, `enableUser`, `disableUser`, `unlockUser`, `confirmUserEmail`, `deleteUser`, `sendUserVerificationEmail`, `sendUserPasswordResetEmail`, `disableUserTwoFactor`, `removeUserPasskeys`, `saveUserCustomSettings`, `listRoles`, `getRole`, `listPermissions`, `saveRole`, `deleteRole`, plus existing `listTimeZones` (20 tools) | The CrestApps user and role modules (`CrestApps.OrchardCore.Users`, `CrestApps.OrchardCore.Roles`, `CrestApps.OrchardCore.ContentAccessControl`) if they get tools; staff or team membership agents |
| SecurityAgent | Manages the site's protection and identity plumbing: OpenID applications and scopes, secrets (never their values), rate limits, and the security, sign-in provider, HTTPS, CORS and reverse proxy settings. | `listOpenIdApplications`, `getOpenIdApplication`, `saveOpenIdApplication`, `deleteOpenIdApplication`, `listOpenIdScopes`, `saveOpenIdScope`, `deleteOpenIdScope`, `listSecrets`, `saveSecret`, `deleteSecret`, `moveSecrets`, `listRateLimitPolicies`, `saveRateLimitPolicy`, `enableRateLimitPolicy`, `disableRateLimitPolicy`, `deleteRateLimitPolicy`, plus the generic `listSiteSettingsGroups`, `getSiteSettings`, `updateSiteSettings` (19 tools; 23 with the conditional `getOpenIdSettings`, `updateOpenIdSettings`, `getCorsSettings`, `updateCorsSettings`) | A general settings or site configuration agent (it would then exceed 25 tools, so split OpenID out if merged); the audit trail tools if the roster prefers them inside an agent |
| direct | Few, broadly useful tools ticked on the main assistant. | `listAuditTrailEvents`, `getAuditTrailEvent`, `listNotifications`, `markNotifications`, `deleteNotifications`, `sendTestEmail`, `sendTestSms` (next to existing `sendEmail`, `sendSmsMessage`, `sendNotification`) | Audit trail tools → a site operations or monitoring agent (background tasks, health checks, logs); test email and SMS → whichever agent owns the generic settings tools; notification tools stay direct |

Suggested instructions for both agents: never ask the user to type a password, secret or token into the chat, and say that such values must be entered on the admin page; always read before writing (`getUser`, `getRole`, `listSecrets`); state exactly which account, role or permission will change before a tool that needs approval; treat a tool refusal as final and never look for another route (in particular not `importOrchardCoreRecipe`).

## Open questions

- **Passwords.** The plan above never lets the model set a password (reset link only). If admins must be able to set one from the chat, it needs an input that goes from the approval card straight to the tool without passing through the model or the transcript. Is that wanted?
- **Recipe bypass.** `importOrchardCoreRecipe` and `applySiteSettings` can already do most of what this group restricts, with no Orchard permission check in the tool: the `Users` step (password hash, roles, enabled flag, no validation), `Roles` (any permission string), `Settings` (sets `SuperUser`, `SiteSalt` and any settings property raw, including unprotected secret values), `Secrets` (plain values), `OpenIdApplication` and `OpenIdClientSettings` (plain client secrets), `custom-user-settings`. Should the recipe tools block these steps, or require `ManageRecipes` plus a stronger approval, so the refusals in the dedicated tools mean something?
- **Existing tools to fix with this work:** `searchForUsers` (filter ignored, no allow-list projection), `getUserInfo` (wrong lockout value), and the missing permission checks in `getUserInfo`, `searchForUsers`, `getRoleInfo`, `sendEmail`, `sendSmsMessage`, `sendNotification`. Rename or keep the old names as aliases?
- **Secret fields in the generic settings tools.** Proposed contract: for every pair `X` / `XSecretName` (`SecretInputViewModel`) return only `hasValue` and `XSecretName`, accept only `XSecretName` (the name of an existing secret), never a value. Fields without a pair must be masked by an explicit list: `ReCaptchaSettings.SecretKey`, `FacebookPixelSettings.ConversionsApiAccessToken`, `TwitterSettings.AccessToken`. Who owns that list?
- **Settings pages that are not settings groups:** OpenID server, OpenID validation, CORS, user audit trail redactors. Can the generic trio register virtual groups backed by a service (validation and tenant reload included), or do we add the four conditional tools?
- **Script fields** (`ExternalLoginSettings.SyncPropertiesScript`, `ExternalRegistrationSettings.GenerateUsernameScript`) run on the server. Refuse them in `updateSiteSettings`, or allow with approval?
- **Confidential OpenID clients.** The tool cannot create one because the secret is required at creation and only its hash is stored. Acceptable, or should the tool generate the secret on the server and show it once to the admin outside the model's context?
- **Administrator role.** Should `setUserRoles` be allowed to grant the administrator role at all (with approval), or always refuse and send the admin to the Users page? Same question for roles on OpenID applications.
- **Self-escalation rule for `saveRole`:** is "no adding permissions to a role the caller holds, unless the caller is an administrator" the right rule, or should approval alone be enough?
- **Super user and admin role name.** Orchard's own last-administrator checks hard-code the `Administrator` role name although it is configurable, and do not protect the `SuperUser`. The tools should use `IsAdminRoleAsync` and protect the super user; confirm.
- **Migrate Secrets page** has no service API (logic in per-module display drivers). Leave it to the UI, or extract a service first?
- **`sendNotification`:** which permission should gate sending a notification to another user (Orchard defines none)?
- **Audit trail event details** are rendered and authorized by display drivers per event type. Is header-only output acceptable for unknown event types?
- **Permission mismatches found in Orchard** (pick the stricter one in tools): `FacebookPixelSettingsDisplayDriver` checks `ManageFacebookApp` while the menu uses `ManageFacebookPixel`; "Send verification email" is shown on `EditUsers` but the action requires `ManageUsers`.
- **Module references.** `UserEmailService`, `SecretsPermissions`, `IOpenIdServerService`, `IOpenIdValidationService` and `OpenIdApplicationExtensions` live in module assemblies, while `CrestApps.OrchardCore.AI.Agent` references only abstractions and core packages and mirrors permissions in `OrchardCorePermissions`. Reference the modules, mirror, or re-implement?
- **Personal data.** `listUsers`, `getUser` and the audit trail tools send names, emails, phone numbers and IP addresses to the AI provider. Is a redaction option needed (for example mask emails unless asked)?
