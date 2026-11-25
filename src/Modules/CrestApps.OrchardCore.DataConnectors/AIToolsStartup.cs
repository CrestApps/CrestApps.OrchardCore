using CrestApps.Core.AI;
using CrestApps.OrchardCore.DataConnectors.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.DataConnectors;

/// <summary>
/// Registers the AI tools that manage data connections and connected data sets whenever the AI feature is on. No tool
/// returns or takes a credential. Saving a connection, which decides where the site connects to, and deleting a
/// connection or a data set ask for approval.
/// </summary>
[Feature(DataConnectorConstants.Features.Area)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListDataConnectionsTool>(ListDataConnectionsTool.TheName)
            .WithTitle(S["List Data Connections"])
            .WithDescription(S["Lists the connections to external systems (HTTP APIs, Elasticsearch, SQL Server, media files) with their connector and non-secret settings, with optional name search or one connection by id. Also returns the connectors enabled on this site. Secret values are never returned, only which secrets are stored."])
            .WithCategory(S["Data Connectors"])
            .Selectable();

        services.AddCoreAITool<SaveDataConnectionTool>(SaveDataConnectionTool.TheName)
            .WithTitle(S["Save Data Connection"])
            .WithDescription(S["Creates a connection for a connector (HTTP API, Elasticsearch or media files), or updates one by id: name, description and the connector's settings. A connection's connector cannot be changed. Credentials are never taken: the result links to the page where a person enters them."])
            .WithCategory(S["Data Connectors"])
            .AsExternal()
            .Selectable();

        services.AddCoreAITool<TestDataConnectionTool>(TestDataConnectionTool.TheName)
            .WithTitle(S["Test Data Connection"])
            .WithDescription(S["Checks that a saved connection reaches its system and returns success or the error message. Use it after saving a connection or when a data set stops refreshing."])
            .WithCategory(S["Data Connectors"])
            .Selectable();

        services.AddCoreAITool<DeleteDataConnectionTool>(DeleteDataConnectionTool.TheName)
            .WithTitle(S["Delete Data Connection"])
            .WithDescription(S["Deletes a connection and its stored credentials. It is refused while any data set still reads the connection."])
            .WithCategory(S["Data Connectors"])
            .AsDestructive()
            .Selectable();

        services.AddCoreAITool<ListDataConnectionResourcesTool>(ListDataConnectionResourcesTool.TheName)
            .WithTitle(S["List Data Connection Resources"])
            .WithDescription(S["Lists what a connection offers to read, such as the tables and views of a database or the files of a folder. Use it to pick the resource of a new data set."])
            .WithCategory(S["Data Connectors"])
            .Selectable();

        services.AddCoreAITool<ListConnectedDataSetsTool>(ListConnectedDataSetsTool.TheName)
            .WithTitle(S["List Connected Data Sets"])
            .WithDescription(S["Lists the connected data sets with their connection, resource, access mode (live or imported), enabled state, row count, last refresh status and next refresh, with optional name search."])
            .WithCategory(S["Data Connectors"])
            .Selectable();

        services.AddCoreAITool<GetConnectedDataSetTool>(GetConnectedDataSetTool.TheName)
            .WithTitle(S["Get Connected Data Set"])
            .WithDescription(S["Returns one connected data set in full: fields, refresh settings, import state, connector settings, and optionally its latest refresh runs with rows read and errors. Use it before changing a data set or to explain why a refresh failed."])
            .WithCategory(S["Data Connectors"])
            .Selectable();

        services.AddCoreAITool<SaveConnectedDataSetTool>(SaveConnectedDataSetTool.TheName)
            .WithTitle(S["Save Connected Data Set"])
            .WithDescription(S["Creates a data set on a connection, or updates one by id: resource, live or imported, fields, default date field and refresh schedule. Fields are read from the source when none are given, and a new imported data set starts its first import when the user may refresh data sets."])
            .WithCategory(S["Data Connectors"])
            .Selectable();

        services.AddCoreAITool<RefreshConnectedDataSetTool>(RefreshConnectedDataSetTool.TheName)
            .WithTitle(S["Refresh Connected Data Set"])
            .WithDescription(S["Starts a refresh of an imported data set in the background and returns immediately. Only imported data sets are refreshed; a live one is read from its source every time."])
            .WithCategory(S["Data Connectors"])
            .Selectable();

        services.AddCoreAITool<PreviewConnectedDataSetTool>(PreviewConnectedDataSetTool.TheName)
            .WithTitle(S["Preview Connected Data Set"])
            .WithDescription(S["Returns the first rows of a connected data set with its field names. Use it to check that a data set reads what was intended; a disabled data set cannot be read."])
            .WithCategory(S["Data Connectors"])
            .Selectable();

        services.AddCoreAITool<DeleteConnectedDataSetTool>(DeleteConnectedDataSetTool.TheName)
            .WithTitle(S["Delete Connected Data Set"])
            .WithDescription(S["Deletes a connected data set with its imported rows and its refresh history. Reports and data pipelines that read it will find it missing."])
            .WithCategory(S["Data Connectors"])
            .AsDestructive()
            .Selectable();
    }
}
