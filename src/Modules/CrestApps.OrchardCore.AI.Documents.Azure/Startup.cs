using CrestApps.Core.AI.Ingestion;
using CrestApps.OrchardCore.AI.Documents.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Documents.Azure;

/// <summary>
/// Keeps the uploaded AI documents.
/// </summary>
/// <remarks>
/// The store moves to Azure Blob Storage when the <c>CrestApps:AI:AzureDocuments</c> shell configuration section names a connection
/// string and a container; otherwise the local store of the parent feature stays in use.
/// </remarks>
public sealed class Startup : StartupBase
{
    /// <summary>
    /// The shell configuration section the Azure Blob Storage options are read from.
    /// </summary>
    internal const string ConfigurationSectionName = "CrestApps:AI:AzureDocuments";

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddTenantFileStore<IDocumentFileStore>(
            "AIDocuments",
            ConfigurationSectionName,
            (serviceProvider, fileStore) => new DefaultDocumentFileStore(fileStore),
            removeOnTenantRemoval: true);
}