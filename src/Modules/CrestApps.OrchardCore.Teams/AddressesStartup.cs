using CrestApps.OrchardCore.Addresses;
using CrestApps.OrchardCore.Teams.Drivers;
using CrestApps.OrchardCore.Teams.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Teams;

/// <summary>
/// Adds the Address card to the team editor when Addresses is on.
/// </summary>
[RequireFeatures(AddressConstants.Feature.ModuleId)]
public sealed class AddressesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<Team, TeamAddressDisplayDriver>();
    }
}
