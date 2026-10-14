---
name: crestapps-tenant-database-provisioner
description: >
  Skill for adding a database provisioner to the CrestApps.OrchardCore Tenant Hierarchy, so child tenants can be
  created on another database server (for example MySQL, MariaDB, Azure SQL with elastic pools, or a managed
  PostgreSQL that needs extra grants). Covers the IChildTenantDatabaseProvisioner contract, safe naming,
  credentials, removal and retention, registration, and the tests to write. Use this skill whenever the request is
  to "provision child tenant databases on <server>", "add a MySQL provisioner", "support another database for
  child tenants", implement IChildTenantDatabaseProvisioner, or change how a child tenant's database, schema,
  login or table prefix is created or removed.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Adding a Child Tenant Database Provisioner

Orchard Core never creates or drops databases, schemas or logins. When a parent creates a child tenant, the Tenant
Hierarchy asks a **provisioner** to create the place the child's tables live and to return the connection settings
the child uses. When the child is removed, Orchard Core drops the child's tables first and the provisioner then
removes what it created.

> The architecture reference lives in `src/CrestApps.Docs/docs/modules/tenant-hierarchy.md` ("Databases and
> provisioners" and "Configuration"). Read it first and keep it in sync with what you add.

## Where things are

| What | Where |
| --- | --- |
| The contract | `src/Abstractions/CrestApps.OrchardCore.TenantHierarchy.Abstractions/Services/IChildTenantDatabaseProvisioner.cs` |
| Context and result | `.../Abstractions/Models/ChildDatabaseProvisioningContext.cs`, `ChildDatabaseProvisioningResult.cs`, `DatabasePoolOptions.cs`, `ChildDatabaseStrategy.cs` |
| Reference provisioners | `src/Core/CrestApps.OrchardCore.TenantHierarchy.Core/Provisioning/` (`PostgreSqlChildDatabaseProvisioner` + `PostgreSqlProvisioningScripts`, `SqlServerChildDatabaseProvisioner` + `SqlServerProvisioningScripts`, `TablePrefixChildDatabaseProvisioner`, `SqliteChildDatabaseProvisioner`) |
| Naming helpers | `ProvisioningNames` (names, passwords, retained names) and `DatabaseProviderNames` (Orchard Core provider names) |
| Dispatcher | `ChildDatabaseProvisioning` picks the first registered provisioner whose `CanProvision` returns true |
| Registration of the built-ins | `TenantHierarchyOrchardCoreBuilderExtensions.AddTenantHierarchy()` |
| Tests to copy | `tests/CrestApps.OrchardCore.Tests/TenantHierarchy/ProvisioningTests.cs` (unit) and `tests/CrestApps.OrchardCore.TenantHierarchy.IntegrationTests/PostgreSqlProvisioningTests.cs` (real server) |

## Golden rules

1. **Never build SQL from anything but safe identifiers.** Derive every name with
   `ProvisioningNames.ForTenant(context.TenantName)` and re-check stored names with
   `ProvisioningNames.EnsureSafeIdentifier(...)` before using them in `DeprovisionAsync`. Identifiers cannot be bound
   as parameters, so this check is the injection barrier. Passwords come from `ProvisioningNames.CreatePassword()`
   (letters and digits only), so they never need escaping.
2. **One child, one login.** For database or schema strategies, create a login that owns only the child's database
   or schema, and return a connection string that uses it. Never hand the pool's own credentials to a tenant. Revoke
   default access where the server grants it (PostgreSQL revokes `CONNECT` from `PUBLIC`).
3. **Return what the child needs, and the name of what you created.** Fill `DatabaseProvider` (an Orchard Core
   provider name from `DatabaseProviderNames`), `ConnectionString`, `TablePrefix` (empty when not used), `Schema`
   when the strategy is `SchemaPerChild`, and `ProvisionedResource`. The parent stores `ProvisionedResource` and
   passes it back to `DeprovisionAsync`.
4. **Removal undoes provisioning, and respects retention.** When the strategy is `DatabasePerChild` and
   `Pool.RetainRemovedDatabases` is true, rename the database to
   `ProvisioningNames.ForRetainedDatabase(name, clock.UtcNow)` instead of dropping it. Always drop the login. Clear
   the client's connection pool before dropping or renaming, or open connections block it.
5. **Throw on failure; do not swallow.** `ChildDatabaseProvisioning` catches non-fatal exceptions, logs them with
   the tenant name and shows the parent a generic error. A provisioner that returns a half-made result is worse than
   one that throws.
6. **Claim only what you handle.** `CanProvision` must check the strategy, `Pool.DatabaseProvider` and that the pool
   has a connection string. The first matching provisioner wins and the built-ins register first, so do not claim a
   strategy and provider that a built-in already handles; replace the built-in registration instead if you must.

## Steps

1. Create `MySqlChildDatabaseProvisioner : IChildTenantDatabaseProvisioner` (in the Core project, or in your own
   assembly) and a small static `MySqlProvisioningScripts` class that returns the statements as a list of strings.
   Keeping the scripts pure lets unit tests assert on them without a server.
2. Implement `CanProvision`, `ProvisionAsync` and `DeprovisionAsync` following the rules above. Run each statement
   on one connection opened from `context.Pool.ConnectionString`.
3. Register it as a **host** singleton, after `AddTenantHierarchy()`:

   ```csharp
   builder.Services
       .AddOrchardCms(orchardCore => orchardCore.AddTenantHierarchy());

   builder.Services.AddSingleton<IChildTenantDatabaseProvisioner, MySqlChildDatabaseProvisioner>();
   ```

4. Configure a pool and point a parent policy at it on the platform's **Policy** screen:

   ```json
   {
     "TenantHierarchy": {
       "DatabasePools": {
         "mysql": {
           "DatabaseProvider": "MySql",
           "ConnectionString": "Server=db;User ID=provisioner;Password=...",
           "RetainRemovedDatabases": true
         }
       }
     }
   }
   ```

5. Document the new server in the "Databases and provisioners" table of `docs/modules/tenant-hierarchy.md`.

## Tests

- **Unit** (`ProvisioningTests.cs`): `CanProvision` accepts exactly the strategies and provider you handle and
  refuses a pool without a connection string; the scripts quote every identifier, contain no input other than safe
  names, and `EnsureSafeIdentifier` rejects a crafted `ProvisionedResource`.
- **Integration** (copy `PostgreSqlProvisioningTests.cs`): read the admin connection string from an environment
  variable and skip with `Assert.Skip` when it is missing, so CI without the server stays green. Cover: two children
  get two databases and the second child's login cannot open the first child's database; removal with retention
  renames the database and drops the login; the schema strategy keeps one child's login out of another's schema;
  and a full parent → create child → set up → remove run with `TenantHierarchyTestHost`.
- Run them against a throwaway container, for example
  `docker run -d --name th-mysql -e MYSQL_ROOT_PASSWORD=... -p 127.0.0.1:3307:3306 mysql:8`, then remove the
  container when you are done.
