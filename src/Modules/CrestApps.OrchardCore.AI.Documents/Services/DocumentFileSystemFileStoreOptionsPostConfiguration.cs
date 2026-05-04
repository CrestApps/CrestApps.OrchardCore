using CrestApps.Core.AI.Documents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Documents.Services;

internal sealed class DocumentFileSystemFileStoreOptionsPostConfiguration : IPostConfigureOptions<DocumentFileSystemFileStoreOptions>
{
    private readonly ShellSettings _shellSettings;
    private readonly IWebHostEnvironment _webHostEnvironment;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentFileSystemFileStoreOptionsPostConfiguration"/> class.
    /// </summary>
    /// <param name="shellSettings">The shell settings.</param>
    /// <param name="webHostEnvironment">The web host environment.</param>
    public DocumentFileSystemFileStoreOptionsPostConfiguration(
        ShellSettings shellSettings,
        IWebHostEnvironment webHostEnvironment)
    {
        _shellSettings = shellSettings;
        _webHostEnvironment = webHostEnvironment;
    }

    /// <summary>
    /// Performs the post configure operation.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="options">The options.</param>
    public void PostConfigure(string name, DocumentFileSystemFileStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.BasePath = Path.Combine(_webHostEnvironment.WebRootPath, _shellSettings.Name, "AIDocuments");
    }
}
