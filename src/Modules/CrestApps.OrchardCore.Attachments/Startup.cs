using CrestApps.OrchardCore.Attachments.Core.Indexes;
using CrestApps.OrchardCore.Attachments.Endpoints;
using CrestApps.OrchardCore.Attachments.Migrations;
using CrestApps.OrchardCore.Attachments.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.FileStorage;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Attachments;

/// <summary>
/// Registers the services, the index and the download endpoint of record attachments.
/// </summary>
public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The tenant's configuration.</param>
    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(AttachmentsConstants.CollectionName));
        services.AddDataMigration<AttachmentsMigrations>()
            .AddIndexProvider<AttachmentIndexProvider>();

        services.AddAttachmentsCore(_shellConfiguration);
        services.TryAddTransient<FileCreationService>();
        services.AddAttachmentScanner<FileCreationAttachmentScanner>();
    }

    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddAttachmentDownloadEndpoint();
    }
}
