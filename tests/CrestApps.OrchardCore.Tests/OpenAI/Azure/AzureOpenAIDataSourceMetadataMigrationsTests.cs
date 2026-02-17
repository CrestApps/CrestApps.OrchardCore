
using System.Reflection;

using CrestApps.OrchardCore.AI.Core.Models;

using CrestApps.OrchardCore.AI.Models;

using Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.Logging;

using Microsoft.Extensions.Options;

using Moq;



namespace CrestApps.OrchardCore.Tests.OpenAI.Azure;



public sealed class AzureOpenAIDataSourceMetadataMigrationsTests

{

    [Fact]

    public void FindFirstEmbeddingMetadata_WhenEmbeddingConnectionExists_ShouldReturnMetadata()

    {

        var providerOptions = new AIProviderOptions();

        providerOptions.Providers["AzureOpenAI"] = new AIProvider

        {

            Connections = new Dictionary<string, AIProviderConnectionEntry>

            {

                ["Default"] = new(new Dictionary<string, object>

                {

                    ["DefaultEmbeddingDeploymentName"] = "text-embedding-3-small",

                }),

            },

        };



        var serviceProvider = new ServiceCollection()

            .AddSingleton<IOptions<AIProviderOptions>>(Options.Create(providerOptions))

            .BuildServiceProvider();



        var logger = Mock.Of<ILogger>();



        var metadata = InvokeFindFirstEmbeddingMetadata(serviceProvider, logger);



        Assert.Equal("AzureOpenAI", metadata.EmbeddingProviderName);

        Assert.Equal("Default", metadata.EmbeddingConnectionName);

        Assert.Equal("text-embedding-3-small", metadata.EmbeddingDeploymentName);

    }



    [Fact]

    public void FindFirstEmbeddingMetadata_WhenNoEmbeddingConnectionConfigured_ShouldLogWarningAndReturnEmptyMetadata()

    {

        var providerOptions = new AIProviderOptions();



        var serviceProvider = new ServiceCollection()

            .AddSingleton<IOptions<AIProviderOptions>>(Options.Create(providerOptions))

            .BuildServiceProvider();



        var logger = new Mock<ILogger>();



        var metadata = InvokeFindFirstEmbeddingMetadata(serviceProvider, logger.Object);



        Assert.True(string.IsNullOrEmpty(metadata.EmbeddingProviderName));

        Assert.True(string.IsNullOrEmpty(metadata.EmbeddingConnectionName));

        Assert.True(string.IsNullOrEmpty(metadata.EmbeddingDeploymentName));



        logger.Verify(x => x.Log(

                LogLevel.Warning,

                It.IsAny<EventId>(),

                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("No AI provider connection with an embedding deployment")),

                It.IsAny<Exception>(),

                It.IsAny<Func<It.IsAnyType, Exception, string>>()),

            Times.Once);

    }



    private static DataSourceIndexProfileMetadata InvokeFindFirstEmbeddingMetadata(IServiceProvider serviceProvider, ILogger logger)

    {

        var assembly = Assembly.Load("CrestApps.OrchardCore.OpenAI.Azure");

        var type = assembly.GetType(

            "CrestApps.OrchardCore.OpenAI.Azure.Migrations.AzureOpenAIDataSourceMetadataMigrations",

            throwOnError: true)!;

        var method = type.GetMethod("FindFirstEmbeddingMetadata", BindingFlags.NonPublic | BindingFlags.Static)!;



        return (DataSourceIndexProfileMetadata)method.Invoke(null, [serviceProvider, logger])!;

    }

}

