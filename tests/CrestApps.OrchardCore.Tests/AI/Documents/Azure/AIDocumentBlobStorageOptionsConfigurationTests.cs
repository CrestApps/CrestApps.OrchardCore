using CrestApps.OrchardCore.AI.Documents.Azure;
using CrestApps.OrchardCore.AI.Documents.Azure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.Tests.AI.Documents.Azure;

public sealed class AIDocumentBlobStorageOptionsConfigurationTests
{
    [Fact]
    public void Configure_BindsAzureDocumentOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["CrestApps:AI:AzureDocuments:ConnectionString"] = "UseDevelopmentStorage=true",
                ["CrestApps:AI:AzureDocuments:ContainerName"] = "AI-DOCUMENTS",
                ["CrestApps:AI:AzureDocuments:BasePath"] = "TenantA/AIDocuments",
                ["CrestApps:AI:AzureDocuments:CreateContainer"] = "false",
                ["CrestApps:AI:AzureDocuments:RemoveContainer"] = "true",
                ["CrestApps:AI:AzureDocuments:RemoveFilesFromBasePath"] = "true",
            })
            .Build();

        var shellConfiguration = new Mock<IShellConfiguration>();
        shellConfiguration
            .Setup(config => config.GetSection("CrestApps:AI:AzureDocuments"))
            .Returns(configuration.GetSection("CrestApps:AI:AzureDocuments"));

        var shellSettings = new ShellSettings
        {
            Name = "TenantA",
        };

        var options = new AIDocumentBlobStorageOptions();
        var optionsConfiguration = new AIDocumentBlobStorageOptionsConfiguration(
            shellConfiguration.Object,
            shellSettings,
            NullLogger<AIDocumentBlobStorageOptionsConfiguration>.Instance);

        optionsConfiguration.Configure(options);

        Assert.Equal("UseDevelopmentStorage=true", options.ConnectionString);
        Assert.Equal("ai-documents", options.ContainerName);
        Assert.Equal("TenantA/AIDocuments", options.BasePath);
        Assert.False(options.CreateContainer);
        Assert.True(options.RemoveContainer);
        Assert.True(options.RemoveFilesFromBasePath);
    }
}
