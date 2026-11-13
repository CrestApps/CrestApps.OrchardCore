using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.Assets;
using CrestApps.OrchardCore.BusinessHours.Core;
using CrestApps.OrchardCore.Geo.Services;
using CrestApps.OrchardCore.Geo;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Parties;
using CrestApps.OrchardCore.Scheduling;
using CrestApps.OrchardCore.Skills;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.WorkOrders.BackgroundTasks;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Drivers;
using CrestApps.OrchardCore.WorkOrders.Integrations;
using CrestApps.OrchardCore.WorkOrders.Migrations;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Navigation;
using CrestApps.OrchardCore.WorkOrders.Services;
using CrestApps.OrchardCore.WorkOrders.ViewModels;
using CrestApps.OrchardCore.WorkOrders.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.WorkOrders;

/// <summary>
/// Registers work orders, their settings records, the service level monitor and the pages.
/// </summary>
/// <summary>
/// Picks the technicians a job should or must not go to from staff.
/// </summary>
[RequireFeatures(CrestApps.OrchardCore.Staff.StaffConstants.Features.Staff)]
public sealed class StaffTechniciansStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
    }
}
