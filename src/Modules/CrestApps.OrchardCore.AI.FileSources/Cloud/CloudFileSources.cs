using System.ComponentModel.DataAnnotations;
using CrestApps.Core;
using CrestApps.Core.AI.FileSources;
using CrestApps.Core.AI.FileSources.FileTransfer;
using CrestApps.Core.AI.Indexing;
using CrestApps.Core.AI.Ingestion;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Integrations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.FileSources.Cloud;

/// <summary>
/// The integration connection a cloud file source reads with, and the document library for SharePoint.
/// </summary>
public sealed class CloudFileSourceMetadata
{
    /// <summary>
    /// Gets or sets the integration connection.
    /// </summary>
    public string ConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the SharePoint document library's drive id; empty reads the connected user's OneDrive.
    /// </summary>
    public string DriveId { get; set; }
}

/// <summary>
/// The cloud file source connectors and the provider each reads through.
/// </summary>
public static class CloudFileSourceConnectors
{
    /// <summary>Google Drive.</summary>
    public const string GoogleDrive = "GoogleDrive";

    /// <summary>SharePoint and OneDrive.</summary>
    public const string SharePoint = "SharePoint";

    /// <summary>Notion.</summary>
    public const string Notion = "Notion";

    /// <summary>
    /// Gets the provider a connector's connection must be to.
    /// </summary>
    /// <param name="connector">The connector.</param>
    /// <returns>The provider, or <see langword="null"/>.</returns>
    public static string ProviderOf(string connector)
        => connector switch
        {
            GoogleDrive => AIIntegrationProviders.Google,
            SharePoint => AIIntegrationProviders.Microsoft365,
            Notion => AIIntegrationProviders.Notion,
            _ => null,
        };
}

/// <summary>
/// Makes a cloud file source's client from its integration connection.
/// </summary>
public sealed class CloudFileClientFactory : IRemoteFileClientFactory
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="CloudFileClientFactory"/> class.
    /// </summary>
    /// <param name="connectorName">The connector.</param>
    /// <param name="serviceProvider">The scope's services.</param>
    public CloudFileClientFactory(string connectorName, IServiceProvider serviceProvider)
    {
        ConnectorName = connectorName;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public string ConnectorName { get; }

    /// <inheritdoc/>
    public async Task<IRemoteFileClient> CreateAsync(IngestionSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var metadata = source.GetOrCreate<CloudFileSourceMetadata>();
        var connection = string.IsNullOrEmpty(metadata.ConnectionId)
            ? null
            : await _serviceProvider.GetRequiredService<IAIIntegrationConnectionStore>().FindByIdAsync(metadata.ConnectionId, cancellationToken);

        if (connection is null || connection.Provider != CloudFileSourceConnectors.ProviderOf(ConnectorName))
        {
            throw new InvalidOperationException($"The file source's {CloudFileSourceConnectors.ProviderOf(ConnectorName)} connection is missing.");
        }

        var http = await _serviceProvider.GetRequiredService<AIIntegrationTokens>().CreateClientAsync(connection, cancellationToken);

        return ConnectorName switch
        {
            CloudFileSourceConnectors.GoogleDrive => new GoogleDriveFileClient(http),
            CloudFileSourceConnectors.SharePoint => new SharePointFileClient(http, metadata.DriveId),
            _ => new NotionFileClient(http),
        };
    }
}

/// <summary>
/// A file source that reads from Google Drive, SharePoint or OneDrive, or Notion through an integration connection.
/// </summary>
public sealed class CloudIngestionConnector : RemoteFileIngestionConnector
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CloudIngestionConnector"/> class.
    /// </summary>
    /// <param name="clientFactory">Makes the client.</param>
    /// <param name="logger">The logger.</param>
    public CloudIngestionConnector(CloudFileClientFactory clientFactory, ILogger<CloudIngestionConnector> logger)
        : base(clientFactory, logger)
    {
    }

    /// <inheritdoc/>
    public override ValueTask ValidateAsync(IngestionSource source, ValidationResultDetails result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(result);

        if (string.IsNullOrEmpty(source.GetOrCreate<CloudFileSourceMetadata>().ConnectionId))
        {
            result.Fail(new ValidationResult("Choose the connection to read with.", [nameof(CloudFileSourceMetadata.ConnectionId)]));
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// The editor of a cloud file source.
/// </summary>
public class CloudFileSourceViewModel
{
    /// <summary>Gets or sets the connector.</summary>
    public string Connector { get; set; }

    /// <summary>Gets or sets the connection.</summary>
    public string ConnectionId { get; set; }

    /// <summary>Gets or sets the folder, page or database to read.</summary>
    public string RootPath { get; set; }

    /// <summary>Gets or sets the SharePoint drive.</summary>
    public string DriveId { get; set; }

    /// <summary>Gets or sets a value indicating whether sub-folders are read.</summary>
    public bool Recursive { get; set; }

    /// <summary>Gets or sets the most files read.</summary>
    public int? MaxItems { get; set; }

    /// <summary>Gets or sets the connections to choose from.</summary>
    public IList<SelectListItem> Connections { get; set; } = [];
}

/// <summary>
/// Edits a cloud file source's connection and folder.
/// </summary>
public sealed class CloudFileSourceDisplayDriver : DisplayDriver<FileSource>
{
    private readonly IAIIntegrationConnectionStore _store;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CloudFileSourceDisplayDriver"/> class.
    /// </summary>
    /// <param name="store">Lists the connections.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CloudFileSourceDisplayDriver(IAIIntegrationConnectionStore store, IStringLocalizer<CloudFileSourceDisplayDriver> stringLocalizer)
    {
        _store = store;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(FileSource fileSource, BuildEditorContext context)
    {
        var provider = CloudFileSourceConnectors.ProviderOf(fileSource.Source);

        if (provider is null)
        {
            return null;
        }

        return Initialize<CloudFileSourceViewModel>("CloudFileSource_Edit", async model =>
        {
            var folder = fileSource.GetOrCreate<RemoteFileSourceMetadata>();
            var metadata = fileSource.GetOrCreate<CloudFileSourceMetadata>();

            model.Connector = fileSource.Source;
            model.ConnectionId = metadata.ConnectionId;
            model.DriveId = metadata.DriveId;
            model.RootPath = folder.RootPath == "/" ? null : folder.RootPath;
            model.Recursive = folder.Recursive;
            model.MaxItems = folder.MaxItems;
            model.Connections = (await _store.ListByProvidersAsync([provider]))
                .Select(connection => new SelectListItem(connection.Name, connection.ItemId, connection.ItemId == metadata.ConnectionId))
                .ToList();
        }).Location("Content:5");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(FileSource fileSource, UpdateEditorContext context)
    {
        if (CloudFileSourceConnectors.ProviderOf(fileSource.Source) is null)
        {
            return null;
        }

        var model = new CloudFileSourceViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.ConnectionId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConnectionId), S["Choose the connection to read with. Add one under Artificial Intelligence, Integrations."]);
        }

        if (fileSource.Source == CloudFileSourceConnectors.Notion && string.IsNullOrWhiteSpace(model.RootPath))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.RootPath), S["Enter the id of the Notion database or page to read."]);
        }

        fileSource.Put(new CloudFileSourceMetadata
        {
            ConnectionId = model.ConnectionId,
            DriveId = string.IsNullOrWhiteSpace(model.DriveId) ? null : model.DriveId.Trim(),
        });

        fileSource.Put(new RemoteFileSourceMetadata
        {
            RootPath = string.IsNullOrWhiteSpace(model.RootPath) ? "/" : model.RootPath.Trim(),
            Recursive = model.Recursive,
            MaxItems = model.MaxItems is > 0 ? model.MaxItems : null,
        });

        return Edit(fileSource, context);
    }
}

/// <summary>
/// Registers the Google Drive, SharePoint and Notion file sources when AI Integrations is on.
/// </summary>
[RequireFeatures(AIConstants.Feature.Integrations)]
public sealed class CloudFileSourcesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        Add(services, CloudFileSourceConnectors.GoogleDrive, "Google Drive", "Reads a Google Drive folder, with Google Docs, Sheets and Slides as text.");
        Add(services, CloudFileSourceConnectors.SharePoint, "SharePoint or OneDrive", "Reads a SharePoint document library or a OneDrive folder through Microsoft Graph.");
        Add(services, CloudFileSourceConnectors.Notion, "Notion", "Reads the pages of a Notion database, or a page and its sub-pages, as Markdown.");

        services.AddDisplayDriver<FileSource, CloudFileSourceDisplayDriver>();
    }

    private static void Add(IServiceCollection services, string name, string displayName, string description)
    {
        // One connector type serves the three providers, so each is registered under its own key by hand.
        services.AddCoreFileSources();
        services.TryAddKeyedScoped<IIngestionConnector>(name, (serviceProvider, _) => new CloudIngestionConnector(
            new CloudFileClientFactory(name, serviceProvider),
            serviceProvider.GetRequiredService<ILogger<CloudIngestionConnector>>()));
        services.Configure<IngestionConnectorOptions>(options => options.AddOrUpdate(name, new LocalizedString(displayName, displayName), new LocalizedString(description, description)));
    }
}
