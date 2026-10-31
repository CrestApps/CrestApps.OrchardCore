using CrestApps.OrchardCore;
using CrestApps.OrchardCore.DataConnectors;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Data Connectors - SQL Server",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = DataConnectorConstants.Features.SqlServer,
    Name = "SQL Server Data Connector",
    Description = "Reads the tables, views and read-only queries of any SQL Server or Azure SQL database, live or as a refreshed import, for reports and data pipelines.",
    Category = "Data",
    Dependencies =
    [
        DataConnectorConstants.Features.Area,
    ]
)]
