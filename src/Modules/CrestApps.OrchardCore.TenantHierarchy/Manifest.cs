using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Resources;
using CrestApps.OrchardCore.TenantHierarchy;
using CrestApps.OrchardCore.Users;
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
        OrchardCoreFeatureIds.Tenants,
        ResourcesConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Name = "Parent Tenant",
    Id = TenantHierarchyConstants.Features.Parent,
    Category = "Multi-Tenancy",
    Description = "Lets a parent tenant create, manage and enter its own child tenants. The platform turns it on when it makes a tenant a parent.",
    Dependencies =
    [
        OrchardCoreFeatureIds.Users,
        OrchardCoreFeatureIds.Roles,
        ResourcesConstants.Feature.Area,

        // The user picker of the access rules.
        UsersConstants.Feature.Area,

        // The activity of the child tenants is recorded in the audit trail.
        OrchardCoreFeatureIds.AuditTrail,
    ]
)]

[assembly: Feature(
    Name = "Child Tenant",
    Id = TenantHierarchyConstants.Features.Child,
    Category = "Multi-Tenancy",
    Description = "Lets the users of the parent tenant enter this tenant. It is always on in a child tenant.",
    Dependencies =
    [
        OrchardCoreFeatureIds.Users,
        OrchardCoreFeatureIds.Roles,
    ]
)]
