using CrestApps.Core.Services;
using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.Assets.Core.Indexes;
using CrestApps.OrchardCore.Assets.Drivers;
using CrestApps.OrchardCore.Assets.Fleet;
using CrestApps.OrchardCore.Assets.Migrations;
using CrestApps.OrchardCore.Assets.Navigation;
using CrestApps.OrchardCore.Assets.Owners;
using CrestApps.OrchardCore.Assets.ViewModels;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Parties;
using CrestApps.OrchardCore.Users.Core;
using CrestApps.OrchardCore.Users.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using Asset = CrestApps.OrchardCore.Assets.Models.Asset;

namespace CrestApps.OrchardCore.Assets;

/// <summary>
/// Picks the staff member who drives a fleet vehicle by default.
/// </summary>
[Feature(AssetsConstants.Features.Fleet)]
[RequireFeatures(CrestApps.OrchardCore.Staff.StaffConstants.Features.Staff)]
public sealed class FleetDriverStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<Asset, Drivers.FleetDriverDisplayDriver>();
    }
}
