using CrestApps.OrchardCore.TenantHierarchy.Models;
using Microsoft.Extensions.Options;
using OrchardCore.AuditTrail.Services.Models;

namespace CrestApps.OrchardCore.TenantHierarchy.Services;

/// <summary>
/// Registers the tenant hierarchy events with the Orchard Core audit trail, so they can be listed, filtered, turned
/// off and trimmed like every other audit trail event.
/// </summary>
internal sealed class TenantHierarchyAuditTrailEventConfiguration : IConfigureOptions<AuditTrailOptions>
{
    /// <inheritdoc/>
    public void Configure(AuditTrailOptions options)
    {
        options.For<TenantHierarchyAuditTrailEventConfiguration>(HierarchyAuditEventNames.Category, S => S["Tenant Hierarchy"])
            .WithEvent(HierarchyAuditEventNames.Created, S => S["Child tenant created"], S => S["A child tenant was added."], enableByDefault: true)
            .WithEvent(HierarchyAuditEventNames.SetupSucceeded, S => S["Child tenant set up"], S => S["A new child tenant finished its setup."], enableByDefault: true)
            .WithEvent(HierarchyAuditEventNames.SetupFailed, S => S["Child tenant setup failed"], S => S["A new child tenant could not be set up."], enableByDefault: true)
            .WithEvent(HierarchyAuditEventNames.Edited, S => S["Child tenant edited"], S => S["The name, address or description of a child tenant changed."], enableByDefault: true)
            .WithEvent(HierarchyAuditEventNames.Suspended, S => S["Child tenant suspended"], S => S["A child tenant was suspended."], enableByDefault: true)
            .WithEvent(HierarchyAuditEventNames.Resumed, S => S["Child tenant resumed"], S => S["A suspended child tenant was resumed."], enableByDefault: true)
            .WithEvent(HierarchyAuditEventNames.Reloaded, S => S["Child tenant reloaded"], S => S["A child tenant was restarted."], enableByDefault: true)
            .WithEvent(HierarchyAuditEventNames.RemovalScheduled, S => S["Child tenant removal scheduled"], S => S["A child tenant was removed and is kept until its waiting period ends."], enableByDefault: true)
            .WithEvent(HierarchyAuditEventNames.Restored, S => S["Child tenant restored"], S => S["A child tenant whose removal was scheduled was restored."], enableByDefault: true)
            .WithEvent(HierarchyAuditEventNames.Removed, S => S["Child tenant removed"], S => S["A child tenant and its data were removed."], enableByDefault: true)
            .WithEvent(HierarchyAuditEventNames.RemovalFailed, S => S["Child tenant removal failed"], S => S["A child tenant could not be removed."], enableByDefault: true)
            .WithEvent(HierarchyAuditEventNames.FeatureEnabled, S => S["Child tenant feature enabled"], S => S["A feature was turned on in a child tenant."], enableByDefault: true)
            .WithEvent(HierarchyAuditEventNames.FeatureDisabled, S => S["Child tenant feature disabled"], S => S["A feature was turned off in a child tenant."], enableByDefault: true)
            .WithEvent(HierarchyAuditEventNames.GrantAdded, S => S["Access rule added"], S => S["A rule now lets people open child tenants."], enableByDefault: true)
            .WithEvent(HierarchyAuditEventNames.GrantRemoved, S => S["Access rule removed"], S => S["A rule that let people open child tenants was removed."], enableByDefault: true)
            .WithEvent(HierarchyAuditEventNames.Entered, S => S["Child tenant opened"], S => S["Someone opened a child tenant without signing in again."], enableByDefault: true)
            .WithEvent(HierarchyAuditEventNames.EntryRefused, S => S["Child tenant open refused"], S => S["Someone tried to open a child tenant they may not open."], enableByDefault: true)
            .WithEvent(HierarchyAuditEventNames.SessionEnded, S => S["Child tenant session ended"], S => S["A session in a child tenant ended."], enableByDefault: true)
            .WithEvent(HierarchyAuditEventNames.Dismissed, S => S["Failed child tenant dismissed"], S => S["A child tenant whose setup failed was taken off the list."], enableByDefault: true);
    }
}
