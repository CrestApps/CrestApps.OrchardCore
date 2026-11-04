using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core.Recipes;
using CrestApps.OrchardCore.AI.FileSources.Recipes;

namespace CrestApps.OrchardCore.AI.FileSources.Deployments;

/// <summary>
/// Writes every file source into a <c>FileSources</c> recipe step, without passwords, keys or other secrets.
/// </summary>
internal sealed class FileSourcesDeploymentSource : IngestionSourceDeploymentSource<FileSourcesDeploymentStep, FileSource>
{
    public FileSourcesDeploymentSource(ISourceCatalogManager<FileSource> manager)
        : base(FileSourcesStep.EntriesName, manager)
    {
    }
}
