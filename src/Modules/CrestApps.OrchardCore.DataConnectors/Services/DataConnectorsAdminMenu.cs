using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.DataConnectors.Services;

/// <summary>
/// Adds Tools › Data Connectors › Connections and Data Sets to the admin menu.
/// </summary>
public sealed class DataConnectorsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataConnectorsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DataConnectorsAdminMenu(IStringLocalizer<DataConnectorsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Tools"], tools => tools
                .Add(S["Data Connectors"], S["Data Connectors"].PrefixPosition(), connectors => connectors
                    .AddClass("data-connectors")
                    .Id("dataConnectors")
                    .Add(S["Connections"], S["Connections"].PrefixPosition("1"), connections => connections
                        .Action("Index", "DataConnections", DataConnectorConstants.Features.Area)
                        .Permission(DataConnectorPermissions.ManageDataConnections)
                        .LocalNav()
                    )
                    .Add(S["Data Sets"], S["Data Sets"].PrefixPosition("2"), dataSets => dataSets
                        .Action("Index", "ConnectedDataSets", DataConnectorConstants.Features.Area)
                        .Permission(DataConnectorPermissions.ManageConnectedDataSets)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
