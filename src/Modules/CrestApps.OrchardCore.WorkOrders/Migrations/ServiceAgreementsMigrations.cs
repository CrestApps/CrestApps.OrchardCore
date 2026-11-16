using CrestApps.OrchardCore.WorkOrders.Core.Legacy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using YesSql.Sql;

namespace CrestApps.OrchardCore.WorkOrders.Migrations;

/// <summary>
/// Service agreements moved to the Agreements module: a new site keeps nothing here; an older one moves its agreements
/// there, with the same ids and numbers, and gives the roles that managed service agreements the agreement permission.
/// </summary>
internal sealed class ServiceAgreementsMigrations : DataMigration
{
    // The permission Work Orders had for its own agreements.
    private const string LegacyPermission = "ManageServiceAgreements";

    /// <summary>
    /// Nothing to create: agreements are the Agreements module's.
    /// </summary>
    /// <returns>The version.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Orchard Core finds a migration's create step on the instance.")]
    public Task<int> CreateAsync()
        => Task.FromResult(3);

    /// <summary>
    /// Adds the CRM account column, which replaced the Companies module's company.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<ServiceAgreementIndex>(table =>
        {
            table.AddColumn<string>("AccountId", column => column.Nullable().WithLength(64));
            table.CreateIndex("IDX_ServiceAgreementIndex_Account", "AccountId");
        }, collection: WorkOrdersConstants.CollectionName);

        return 2;
    }

    /// <summary>
    /// Moves the service agreements to the Agreements module, once the migration has committed (the agreement store is
    /// read in a scope that sees the Agreements tables).
    /// </summary>
    /// <returns>The version.</returns>
    public static int UpdateFrom2()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var services = scope.ServiceProvider;
            var moved = await services.GetRequiredService<ServiceAgreementUpgrade>().RunAsync();

            await GrantAgreementPermissionAsync(services);

            var logger = services.GetRequiredService<ILogger<ServiceAgreementsMigrations>>();

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Moved {Count} service agreements to the Agreements module.", moved);
            }
        });

        return 3;
    }

    // Roles that managed service agreements manage agreements.
    private static async Task GrantAgreementPermissionAsync(IServiceProvider services)
    {
        if (services.GetService<Microsoft.AspNetCore.Identity.RoleManager<global::OrchardCore.Security.IRole>>() is not { } roleManager)
        {
            return;
        }

        foreach (var role in roleManager.Roles.ToList())
        {
            if (role is not Role orchardRole)
            {
                continue;
            }

            var claims = orchardRole.RoleClaims;

            if (claims.Any(claim => claim.ClaimType == Permission.ClaimType && claim.ClaimValue == LegacyPermission)
                && !claims.Any(claim => claim.ClaimType == Permission.ClaimType && claim.ClaimValue == CrestApps.OrchardCore.Agreements.AgreementsPermissions.ManageAgreements.Name))
            {
                claims.Add(new RoleClaim { ClaimType = Permission.ClaimType, ClaimValue = CrestApps.OrchardCore.Agreements.AgreementsPermissions.ManageAgreements.Name });
                await roleManager.UpdateAsync(orchardRole);
            }
        }
    }
}
