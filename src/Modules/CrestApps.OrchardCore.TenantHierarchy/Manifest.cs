using CrestApps.OrchardCore;
using CrestApps.OrchardCore.TenantHierarchy;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Tenant Hierarchy",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Multi-Tenancy",
    Description = "Lets a parent tenant create, manage and enter its own child tenants, with strict isolation and full attribution."
)]

[assembly: Feature(
    Name = "Tenant Hierarchy Platform",
    Id = TenantHierarchyConstants.Features.Platform,
    Category = "Multi-Tenancy",
    Description = "Lets the Default tenant make tenants into parent tenants, set their policies and see the whole hierarchy.",
    DefaultTenantOnly = true,
    Dependencies =
    [
        "OrchardCore.Tenants",
        "CrestApps.OrchardCore.Resources",
    ]
)]

[assembly: Feature(
    Name = "Parent Tenant",
    Id = TenantHierarchyConstants.Features.Parent,
    Category = "Multi-Tenancy",
    Description = "Lets a parent tenant create, manage and enter its own child tenants. The platform turns it on when it makes a tenant a parent.",
    Dependencies =
    [
        "OrchardCore.Users",
        "OrchardCore.Roles",
        "CrestApps.OrchardCore.Resources",

        // The user picker of the access rules.
        "CrestApps.OrchardCore.Users",

        // The activity of the child tenants is recorded in the audit trail.
        "OrchardCore.AuditTrail",
    ]
)]

[assembly: Feature(
    Name = "Child Tenant",
    Id = TenantHierarchyConstants.Features.Child,
    Category = "Multi-Tenancy",
    Description = "Lets the users of the parent tenant enter this tenant. It is always on in a child tenant.",
    Dependencies =
    [
        "OrchardCore.Users",
        "OrchardCore.Roles",
    ]
)]
