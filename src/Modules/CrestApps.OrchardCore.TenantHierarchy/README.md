# Tenant Hierarchy

Lets a parent tenant create, manage and enter its own child tenants, with strict isolation and full attribution.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Tenant Hierarchy Platform | `CrestApps.OrchardCore.TenantHierarchy.Platform` | Lets the Default tenant make tenants into parent tenants, set their policies and see the whole hierarchy. Default tenant only. |
| Parent Tenant | `CrestApps.OrchardCore.TenantHierarchy.Parent` | Lets a parent tenant create, manage and enter its own child tenants. The platform turns it on when it makes a tenant a parent. |
| Child Tenant | `CrestApps.OrchardCore.TenantHierarchy.Child` | Lets the users of the parent tenant enter this tenant. It is always on in a child tenant. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- `OrchardCore.Tenants`
- [`CrestApps.OrchardCore.Resources`](../CrestApps.OrchardCore.Resources/README.md)
- `OrchardCore.Users`
- `OrchardCore.Roles`
- [`CrestApps.OrchardCore.Users`](../CrestApps.OrchardCore.Users/README.md)
- `OrchardCore.AuditTrail`

## Extension points

- `IChildTenantDatabaseProvisioner` (CrestApps.OrchardCore.TenantHierarchy.Abstractions): Creates and removes the database of a child tenant for one `ChildDatabaseStrategy`.
- `ITenantHierarchyBroker` (CrestApps.OrchardCore.TenantHierarchy.Abstractions): The only code path that reaches another tenant of a hierarchy.

## Documentation

See [Tenant Hierarchy](../../CrestApps.Docs/docs/modules/tenant-hierarchy.md) in the technical manual.
