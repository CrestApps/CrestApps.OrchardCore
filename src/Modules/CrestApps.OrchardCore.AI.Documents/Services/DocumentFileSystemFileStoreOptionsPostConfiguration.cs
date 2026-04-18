using CrestApps.Core.AI.Documents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Documents.Services;

internal sealed class DocumentFileSystemFileStoreOptionsPostConfiguration : IPostConfigureOptions<DocumentFileSystemFileStoreOptions>
{
    private readonly ShellSettings _shellSettings;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public DocumentFileSystemFileStoreOptionsPostConfiguration(
        ShellSettings shellSettings,
        IWebHostEnvironment webHostEnvironment)
    {
        _shellSettings = shellSettings;
        _webHostEnvironment = webHostEnvironment;
    }

    public void PostConfigure(string name, DocumentFileSystemFileStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.BasePath = Path.Combine(_webHostEnvironment.WebRootPath, _shellSettings.Name, "AIDocuments");
    }
}
