using CrestApps.Core.AI.Documents;
using CrestApps.OrchardCore.AI.Documents.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.AI.Documents;

public sealed class DocumentFileSystemFileStoreOptionsPostConfigurationTests
{
    [Fact]
    public void PostConfigure_AlwaysUsesTenantWebRootPath()
    {
        var shellSettings = new ShellSettings
        {
            Name = "Contoso",
        };

        var webHostEnvironment = new Mock<IWebHostEnvironment>();
        webHostEnvironment.SetupGet(environment => environment.WebRootPath).Returns(@"C:\Sites\Cms\wwwroot");

        var options = new DocumentFileSystemFileStoreOptions
        {
            BasePath = @"C:\Ignored\Path",
        };

        var configuration = new DocumentFileSystemFileStoreOptionsPostConfiguration(shellSettings, webHostEnvironment.Object);

        configuration.PostConfigure(Options.DefaultName, options);

        Assert.Equal(Path.Combine(@"C:\Sites\Cms\wwwroot", "Contoso", "AIDocuments"), options.BasePath);
    }
}
