---
sidebar_label: Tenant Hierarchy
title: Tenant Hierarchy
description: Parent tenants that create, manage and enter their own child tenants, with strict isolation between tenants and full attribution of every action.
user_manual:
  - user-manual/administration/child-tenants
  - user-manual/administration/tenant-hierarchy
---

The Tenant Hierarchy module lets a tenant that the platform marks as a **parent** create and manage its own **child** tenants, and lets the parent's users open a child tenant without signing in again. The motivating case is a bookkeeping firm (the parent) that runs one site per client business (the children). The Default tenant stays the platform root and still manages every tenant through the standard Tenants admin.

This video walks through the problem the module solves, each screen, and how it keeps tenants isolated. The [setup](#install-the-host-guards) and [security](#isolation-between-parents) parts are also below as short screencasts, and the [User Manual](../user-manual/administration/child-tenants.md) has one for each task.

<video controls preload="metadata" width="100%" poster="/img/docs/tenant-hierarchy.jpg" aria-label="Video overview of the Tenant Hierarchy: making a parent, adding and opening clients, managing them, access rules, activity and security">
  <source src="/img/docs/tenant-hierarchy.mp4" type="video/mp4" />
  <track kind="captions" src="/img/docs/tenant-hierarchy.vtt" srcLang="en" label="English" default />
</video>

| Feature name | Feature ID | Where it runs |
| --- | --- | --- |
| Tenant Hierarchy Platform | `CrestApps.OrchardCore.TenantHierarchy.Platform` | The Default tenant only. Makes tenants parents, edits their policies and shows the whole hierarchy. |
| Parent Tenant | `CrestApps.OrchardCore.TenantHierarchy.Parent` | A parent tenant. Forced on when the platform makes the tenant a parent. |
| Child Tenant | `CrestApps.OrchardCore.TenantHierarchy.Child` | Every child tenant. Forced on after the child is set up. |

The Parent and Child features never depend on the Platform feature or on `OrchardCore.Tenants`: a feature that depends on a Default-only feature is unavailable in every other tenant. The Parent feature depends on `CrestApps.OrchardCore.Users` for the user picker of the access rules.

How people use the screens is described in the User Manual: [Child tenants](../user-manual/administration/child-tenants.md) for a parent's users and [Tenant hierarchy](../user-manual/administration/tenant-hierarchy.md) for platform administrators.

## Install the host guards

<video controls preload="metadata" width="100%" aria-label="Screencast of setting up the tenant hierarchy: AddTenantHierarchy in Program.cs, the TenantHierarchy section of appsettings.json, and a provisioner for another database server">
  <source src="/img/docs/tenant-hierarchy-setup.mp4" type="video/mp4" />
  <track kind="captions" src="/img/docs/tenant-hierarchy-setup.vtt" srcLang="en" label="English" default />
</video>

The module needs host-level services that run in every tenant: the scope guard, the feature guard, the egress guard, the cookie hardening, the parent removal guard, the Fetch Metadata guard and the unavailable-address guard. Register them in `Program.cs`:

```csharp
builder.Services
    .AddOrchardCms(orchardCore => orchardCore
        .AddTenantHierarchy());
```

Each guard reads the tenant's own shell settings and does nothing for a tenant that is not a parent or a child. Until `AddTenantHierarchy()` is called, the platform refuses to make parents and a parent refuses to create children, and both screens show a warning.

## How it works

### Where the facts live

| Fact | Store | Who changes it |
| --- | --- | --- |
| `TenantHierarchy:Role` (`Parent` or `Child`), the parent policy `TenantHierarchy:Policy:*`, a child's `TenantHierarchy:ParentTenantId` | Shell settings | The platform, and the module's own code. Tenant administrators cannot edit shell settings. |
| The registry of child tenants, access grants, one-time codes, delegated access sessions, favorites | The parent's database, collection `TenantHierarchy` | The parent's administrators, through the screens |
| The activity of the child tenants | The parent's Orchard Core audit trail, category `TenantHierarchy` | The module only |
| User links (which local user belongs to which parent user) | The child's database, collection `TenantHierarchy` | The module only |

A child is named by an opaque tenant name such as `u_7k2m9q4x1c`; its display name goes into the tenant `Description`, and the parent's display name into `Category`, so the Default tenant's Tenants admin can filter by parent.

### The broker and the scope guard

`ITenantHierarchyBroker` is the only code path from one tenant of a hierarchy to another. The caller is the tenant whose code is running, identified by its own `ShellSettings`; the other side is resolved from host-controlled settings only, and the link must hold in both directions: the parent's registry names the child, and the child's settings name the parent. Requests carry the identifier of an entry in the parent's own registry, never a tenant name, so an identifier from another parent simply does not exist.

`AddTenantHierarchy()` replaces the host's `IShellHost` with `GuardedShellHost`, a decorator that checks every call against the tenant whose code is running:

| Code running in | May reach |
| --- | --- |
| No tenant, or the Default tenant | Every tenant |
| An ordinary tenant | Any tenant that is not a parent or a child |
| A parent | Itself; reads of its own children; changes and scopes on one of its children only inside a broker call for that child |
| A child | Itself; its own parent only inside a broker call for that parent |

Every tenant may also reach the Default tenant, because Orchard Core itself reads Default's settings and opens its scope for feature profiles and for the removal lock. Calls that change a tenant or open its scope throw `TenantHierarchyAccessDeniedException` and log both tenant names; `GetAllSettings`, `ListShellContexts`, `TryGetSettings` and `TryGetShellContext` hide what is out of reach. The guard covers every installed feature. It does not stop malicious server code: all tenants run in one process, so only install modules you trust.

### Delegated access

Opening a child follows the shape of the OAuth 2.0 authorization code flow with PKCE. The code is redeemed in process through the broker instead of over HTTP, and the child is authenticated by its own shell settings instead of a secret.

```mermaid
sequenceDiagram
    autonumber
    participant B as Browser
    participant P as Parent (firm.platform.com)
    participant C as Child (biz1.firm.platform.com)
    B->>P: GET /delegated-access/open/{entryId}
    P->>P: Check "Enter child tenants" and an access grant
    P-->>B: 302 to C /delegated-access/enter
    C->>C: State and PKCE verifier in an encrypted cookie
    C-->>B: 302 to P /delegated-access/authorize
    P->>P: Check the child, the grant and the MFA policy; store a hashed one-time code (60 s)
    P-->>B: 302 to the callback address from the child's settings
    B->>C: GET /delegated-access/callback
    C->>P: Broker RedeemCodeAsync(code, verifier)
    C->>C: Find or create the linked user, sync its roles, sign in
```

- The code has 256 random bits, is stored as a SHA-256 hash, is valid for 60 seconds and is burned by the first attempt, even a failed one. Redemption saves the code with a concurrency check, so of two racing redemptions only one succeeds.
- The callback address is built from the child's shell settings, never from the request, and `returnUrl` must be local.
- `/delegated-access/open`, `/authorize`, `/enter` and `/callback` answer only navigations: a request whose `Sec-Fetch-Mode` is not `navigate` is refused. Responses send `Cache-Control: no-store` and `Referrer-Policy: no-referrer`.
- Unknown children, children of another parent and missing grants all give the same error page.

### Linked users

On first entry the child creates a local **linked user** for the parent user, named `{parent user name}+{parent slug}` (for example `alice+firma`), with no password and the parent user's email when no local user uses it, otherwise an address under `.invalid`. Links are matched by parent tenant and parent user, never by name or email, and an old link is kept when its local user is deleted. The linked user gets exactly the child roles its grants give; every other role is removed, because roles come from the parent only.

A child administrator cannot act as a linked user:

| Attempt | Block |
| --- | --- |
| Sign in with a password, even one written by the Users recipe step | `ILoginFormEvent` refuses password and external logins for linked users; the check reads the module's own collection |
| Edit, delete or assign roles to a linked user in the admin | An authorization handler fails `ManageUsers`, `EditUsers`, `DeleteUsers`, `AssignRoleToUsers` and their per-role variants when the resource is a linked user. A failure wins over the handler that gives administrators every permission. |
| Give the linked user a password | An `IUserEventHandler` removes any password hash when the user is saved, and every entry removes it again |
| Lock the parent user out by disabling the linked user | The next entry enables it again |

### Sessions

The child validates a delegated session with the parent at the policy's interval, from the application cookie's `OnValidatePrincipal` event. A session ends when it is idle too long, older than its lifetime, its parent user is disabled or changed their security stamp, no grant covers the child any more, the child is no longer ready, the user leaves the child, the parent user signs out of the parent (each parent sign-in carries its own identifier), or the user chooses **Sign out of every child tenant**. A linked user that has no delegated session claims is signed out on its next request.

### The tenant switcher

In a child, users who entered through delegated access see a switcher in the admin navbar with the parent's name, a link back to the parent and a **Switch** link. The list of other children is always served by the parent: the hosted mode links to the parent's picker page, and the embedded mode frames the parent's picker, which is served with `frame-ancestors` set to that child only. The child's page never contains another child's name or address.

### Child tenant life cycle

Creating a child validates the slug (a DNS label of 3 to 40 lowercase letters, digits and inner hyphens, not reserved), checks the quota under a distributed lock, builds the host from the parent policy, places the database through a provisioner, writes the shell settings and the registry entry, and runs the setup after the request with a bootstrap administrator whose password is never stored and which is disabled right after setup. The Child feature is forced on through the tenant's `Features` configuration after setup succeeds.

Suspending disables the tenant, resuming enables it, and removing calls Orchard Core's `IShellRemovalManager` and then the provisioner. A parent's policy can keep a removed child suspended and restorable for a number of days; the **Tenant Hierarchy Maintenance** background task removes it when the period ends and deletes expired codes.

A parent that still has children cannot be removed, not even from the Default tenant's Tenants admin: the guard is registered as a tenant-level removing handler, which runs before the handlers that drop the tables, and as a host-level handler for an uninitialized parent.

### Feature guard

| Feature | Rule |
| --- | --- |
| Parent Tenant | Available only in a parent |
| Child Tenant | Never offered; forced on through configuration |
| Tenant Hierarchy Platform | Default only |
| `OrchardCore.OpenId.Validation` | Blocked in parents and children |
| `OrchardCore.Deployment.Remote` | Blocked in children |
| `OrchardCore.Queries.Sql` | Blocked in children that share a database (schema or table prefix strategies) |
| The policy's blocked features | Blocked in that parent's children |

A validation provider only filters what can be enabled; it does not switch off a feature that is already on. The platform's parent screen lists any blocked feature found enabled in a child. Setup recipes are filtered too: a recipe step that asks for a blocked feature is ignored.

### Egress guard and cookies

In parent and child tenants, every `HttpClient` from `IHttpClientFactory` refuses requests to the hosts of the application's own tenants and to loopback, link-local, private and shared addresses. The host check runs before any DNS lookup; the address check runs on the address the connection actually opens to, so DNS rebinding cannot get around it. Requests through a proxy are checked by destination.

The authentication and antiforgery cookies of parent and child tenants get the `__Host-` prefix, `Path=/`, no domain and `Secure`, so a child's script cannot plant a cookie for its parent or a sibling. A parent also refuses every request a child page makes with a script (`Sec-Fetch-Site: same-site` and a mode other than `navigate`).

### Activity in the audit trail

The Parent feature depends on `OrchardCore.AuditTrail` and records every life-cycle, access and session event in the parent's audit trail, in the `TenantHierarchy` category, with the child's registry entry as the correlation identifier. The **Activity** menu item opens `/Admin/AuditTrail?q=category:TenantHierarchy`, and a child's **Manage > Activity** opens `/Admin/AuditTrail/{entryId}`. Each event stores a `HierarchyAuditEvent` with the child, the user, the session reference, the authentication methods, the client address and details, which `HierarchyAuditTrailEventDisplayDriver` shows in the list and on the event page. Events can be turned off and are trimmed like any other audit trail event, under **Settings > Audit Trail**. Events are only written in the parent: a child's own users never see them.

### Unavailable addresses

When the Default tenant has no host name, Orchard Core sends it every request that no running tenant claims. Without a guard, the address of a suspended or removed child, or any address under a parent's host, would show the platform site and its sign-in page. In the Default tenant, a middleware answers such a request with `404` and a short "This site is not available" page instead. It matches the hosts of every parent and child tenant, with or without a port, and every host under a parent's host. A host of an ordinary tenant is left alone.

## Isolation between parents

One parent never reaches another parent or the other parent's children, and their children never reach each other. Each door between two hierarchies has its own block, and most have two, so one mistake does not open it.

<video controls preload="metadata" width="100%" aria-label="Screencast of how the tenant hierarchy stays secure: the one-time code with PKCE, linked accounts, the guards between tenants, and cookies and addresses">
  <source src="/img/docs/tenant-hierarchy-security.mp4" type="video/mp4" />
  <track kind="captions" src="/img/docs/tenant-hierarchy-security.vtt" srcLang="en" label="English" default />
</video>

| Door | What blocks it |
| --- | --- |
| Code in a parent calls `IShellHost` for another hierarchy | `GuardedShellHost` hides the other parent and its children from `GetAllSettings` and `TryGetSettings`, and `GetScopeAsync`, `UpdateShellSettingsAsync`, `ReloadShellContextAsync` and `RemoveShellSettingsAsync` throw `TenantHierarchyAccessDeniedException`, inside a broker call or not. A settings object crafted to name the caller as parent is checked against the host's own copy. |
| The parent's broker or services get another parent's identifier | The registry, the access grants, the codes and the sessions live in each parent's own database, so another parent's entry, grant or session identifier does not exist there. The broker also requires the child's settings to name the caller as its parent. A tenant name or tenant identifier is never accepted where an entry identifier is expected. |
| The admin screens get another parent's child | Every page and action under `/Admin/children/{id}` answers `404` and changes nothing, whether `{id}` is that child's entry identifier, tenant identifier or tenant name. Bulk actions skip identifiers that are not in the parent's registry, and an access rule cannot name another parent's child or remove another parent's rule. |
| Delegated access to another parent's child | `/delegated-access/open` and `/authorize` issue a code only for a child in the caller's registry that a grant covers. A code is bound to one child, its parent and the browser's PKCE verifier, so another parent's child cannot redeem it. |
| A session of one hierarchy shown to another | A child validates and ends sessions only with its own parent. Another parent's session identifier is unknown there, and a child cannot end a session of a child of another parent. |
| A sign-in carried from one tenant to another | Users, cookies and data protection keys belong to each tenant. Cookies are host-only, and a cookie copied to another tenant, even under that tenant's cookie name, does not sign in. Entering another parent's child sends the browser to that parent's sign-in page. |
| Outbound requests | The egress guard refuses requests from a parent or a child to the host of any tenant of the application, before any DNS lookup, and to internal addresses after it. |
| The platform screens | They run only in the Default tenant. A parent's request for them is refused. |
| The data | `DatabasePerChild` gives each child its own database and login; `SchemaPerChild` its own schema and login; `SqlitePerChild` its own file. `TablePrefixPerChild` shares the pool's login, so `OrchardCore.Queries.Sql` is blocked in such children. |

## Configuration

The module reads the `TenantHierarchy` section of the **application** configuration. It is host configuration on purpose: tenant configuration cannot change it.

| Key | Default | Meaning |
| --- | --- | --- |
| `PlatformDomain` | The host of the request to the Default tenant | Parent hosts are `{slug}.{PlatformDomain}`. May carry a port, for example `localhost:5000`. |
| `Scheme` | `https`; in development, the scheme of the request | The scheme of the tenant addresses the module builds |
| `UseHostPrefixedCookies` | On, except in development | Gives parent and child cookies the `__Host-` prefix. Needs HTTPS. |
| `ReservedSlugs` | Empty | More slugs no tenant may use, in addition to the built-in list (`admin`, `www`, `api`, `mail`, `login` and others) |
| `Egress:Enabled` | `true` | Turns the egress guard on |
| `Egress:BlockPrivateNetworks` | `true` | Refuses loopback, link-local, private and shared addresses |
| `Egress:AllowedHosts` | Empty | Hosts that are always allowed, for example a local model server |
| `DatabasePools:{name}:DatabaseProvider` | | `SqlConnection`, `Postgres`, `MySql` or `Sqlite` |
| `DatabasePools:{name}:ConnectionString` | | A login that can create databases, schemas and logins |
| `DatabasePools:{name}:RetainRemovedDatabases` | `true` | Renames a removed child database to `{name}_removed_{timestamp}` instead of dropping it |

```json
{
  "TenantHierarchy": {
    "PlatformDomain": "platform.com",
    "Egress": {
      "AllowedHosts": [ "ollama.internal" ]
    },
    "DatabasePools": {
      "firms": {
        "DatabaseProvider": "Postgres",
        "ConnectionString": "Host=db;Username=provisioner;Password=..."
      }
    }
  }
}
```

```text
TenantHierarchy__PlatformDomain=platform.com
TenantHierarchy__DatabasePools__firms__DatabaseProvider=Postgres
TenantHierarchy__DatabasePools__firms__ConnectionString=Host=db;Username=provisioner;Password=...
```

### Parent policy

The platform writes the policy into the parent's shell settings from the **Policy** screen. It can also be set in configuration as `OrchardCore:{tenant}:TenantHierarchy:Policy:{key}`.

| Key | Default | Meaning |
| --- | --- | --- |
| `MaxChildren` | `100` | The quota, checked under a distributed lock |
| `ChildHostPattern` | `{business}.{parent host}` | Must start with `{business}.`; no wildcard, list or path |
| `Recipes` | Every setup recipe | The setup recipes a child can start from |
| `DatabaseStrategy` | `SqlitePerChild` | `SqlitePerChild`, `DatabasePerChild`, `SchemaPerChild` or `TablePrefixPerChild` |
| `DatabasePool` | | A pool from `TenantHierarchy:DatabasePools` |
| `BlockedFeatures` | Empty | More features blocked in the children |
| `DeniedLocalPermissions` | Empty | Permissions the children's own users never get, for example `ManageRecipes` |
| `RequireMfa` | `false` | The parent sign-in must have used two-factor authentication |
| `SessionValidationInterval` | `00:02:00` | How often a child checks a session with the parent |
| `SessionIdleTimeout` / `SessionLifetime` | `00:30:00` / `08:00:00` | When a session ends |
| `SwitcherMode` | `Hosted` | `Hosted` or `Embedded` |
| `Labels:Parent`, `Labels:Child`, `Labels:Children` | Parent tenant, Child tenant, Child tenants | The words the screens show |
| `RemovalGraceDays` | `0` | Days a removed child stays suspended and restorable |

## Databases and provisioners

| Strategy | Provisioner | What it creates |
| --- | --- | --- |
| `SqlitePerChild` | Built in | A SQLite file in the child's own folder |
| `DatabasePerChild` | SQL Server, PostgreSQL | A database and a login that owns it. On PostgreSQL, `CONNECT` on the database is revoked from `PUBLIC`. |
| `SchemaPerChild` | SQL Server, PostgreSQL | A schema in the pool's database and a login that owns it |
| `TablePrefixPerChild` | Any provider | A random table prefix in the pool's database |

Names are derived from the opaque tenant name (`th_u_...`) and checked to hold only letters, digits and underscores; passwords hold letters and digits only. Removing a child drops its schema, or renames or drops its database, and drops its login.

To support another server, implement `CrestApps.OrchardCore.TenantHierarchy.Services.IChildTenantDatabaseProvisioner` and register it as a host singleton:

```csharp
builder.Services.AddSingleton<IChildTenantDatabaseProvisioner, MySqlChildDatabaseProvisioner>();
```

`CanProvision` says whether the provisioner handles a strategy and pool, `ProvisionAsync` returns the provider, connection string, table prefix, schema and the name of what it created, and `DeprovisionAsync` removes it after Orchard Core dropped the tenant tables.

A step-by-step build guide for AI agents and developers, with the naming, credential and removal rules and the tests to write, lives in the repository at `.agents/skills/crestapps-tenant-database-provisioner`.

## Permissions

| Permission | Allows |
| --- | --- |
| `ManageTenantHierarchy` | Platform screens (Default tenant) |
| `ViewChildTenants` | The child tenants list. Implied by the next five. The activity needs the audit trail's `ViewAuditTrail`. |
| `CreateChildTenants` | Create, retry and discard child tenants |
| `ManageChildTenants` | Edit, suspend, resume and reload |
| `ManageChildFeatures` | Enable and disable features in a child |
| `RemoveChildTenants` | Remove and restore |
| `ManageChildAccess` | Access grants |
| `EnterChildTenants` | Base gate for opening a child; a grant is still needed |

When a tenant becomes a parent, a parent-wide grant gives the parent's **Administrator** role the child role **Administrator** in every child.

## Deployment

- Give every tenant its own host name, never a path prefix: `{firm}.platform.com` for parents and `{business}.{firm}.platform.com` for children. This needs wildcard DNS, a certificate for `*.platform.com` and one for `*.{firm}.platform.com` per parent.
- Serve every tenant over HTTPS, so the `__Host-` cookies work.
- Give the Default tenant its own host name too, such as `platform.com`. Then no unknown host reaches it at all; the unavailable-address guard covers the case where it has none.
- Register the platform domain in the private section of the Public Suffix List, so one parent's children cannot set cookies for another parent or make same-site requests to it.
- With several nodes, use `OrchardCore.Tenants.Distributed`, a shared shell settings store (the shells database or Azure Blob) and a shared data protection key ring.
- Background tasks run only for tenants that have served a request since the process started.

## Not included

- A federated OpenID Connect mode for children that run in another application.
- Custom domains for children, and inviting the business owner as a local user when a child is created. The child's own Users admin adds local users.
- An idle-release service for inactive children, and metrics.

## Troubleshooting

| Symptom | Cause and fix |
| --- | --- |
| The platform and parent screens say **The host guards are not installed** | `AddTenantHierarchy()` is not called in `Program.cs`. Add it and restart. |
| An outbound request fails with *it is the address of a tenant of this application* | The egress guard refused a request from a parent or a child to one of the application's own tenants. Tenants of a hierarchy do not call each other over HTTP; module code that must reach a parent or child goes through `ITenantHierarchyBroker`. |
| An outbound request to an internal address fails | `Egress:BlockPrivateNetworks` refuses loopback, link-local, private and shared addresses. Add the host to `Egress:AllowedHosts`, for example a local model server. |
| Signing in to a parent or a child loops back to the sign-in page | The `__Host-` cookies need HTTPS. Serve every tenant over HTTPS, or turn `UseHostPrefixedCookies` off only on a test machine. |
| A `TenantHierarchyAccessDeniedException` is logged | Code in one tenant tried to reach another tenant outside the broker. The log names both tenants. It is the guard working; find the module that made the call. |
| A child's setup fails | The application log has the error. A database strategy needs a pool whose login can create databases, schemas and logins. Fix it, then click **Retry**. |
| Users are signed out when a request reaches another node | The nodes do not share a data protection key ring or the shell settings. See [Deployment](#deployment). |
| The address of a suspended child shows the platform's sign-in page | The application runs without `AddTenantHierarchy()`, so the unavailable-address guard is not installed. |
