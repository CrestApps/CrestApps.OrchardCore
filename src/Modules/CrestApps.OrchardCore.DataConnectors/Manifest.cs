using CrestApps.OrchardCore;
using CrestApps.OrchardCore.DataConnectors;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Data Connectors",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = DataConnectorConstants.Features.Area,
    Name = "Data Connectors",
    Description = "Connects to external systems and exposes their data sets to reports and data pipelines, read live or imported into the site's database on a schedule.",
    Category = "Data",
    Dependencies =
    [
        "OrchardCore.DataPipelines.Core",
        "CrestApps.OrchardCore.Resources",
    ]
)]

[assembly: Feature(
    Id = DataConnectorConstants.Features.Http,
    Name = "HTTP API Data Connector",
    Description = "Reads any HTTP or REST API that answers with JSON, CSV or JSON Lines, with API key, Basic, bearer token, OAuth 2.0 or OpenID Connect authentication.",
    Category = "Data",
    Dependencies =
    [
        DataConnectorConstants.Features.Area,
    ]
)]

[assembly: Feature(
    Id = DataConnectorConstants.Features.Elasticsearch,
    Name = "Elasticsearch Data Connector",
    Description = "Reads the documents of any Elasticsearch or OpenSearch cluster, live or as a refreshed import.",
    Category = "Data",
    Dependencies =
    [
        DataConnectorConstants.Features.Area,
    ]
)]

[assembly: Feature(
    Id = DataConnectorConstants.Features.Files,
    Name = "Media Files Data Connector",
    Description = "Reads CSV, JSON, JSON Lines and Excel files of the media library as data sets.",
    Category = "Data",
    Dependencies =
    [
        DataConnectorConstants.Features.Area,
        "OrchardCore.Media",
    ]
)]
