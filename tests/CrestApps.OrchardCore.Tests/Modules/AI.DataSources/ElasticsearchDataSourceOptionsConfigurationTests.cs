using System.Reflection;
using CrestApps.Core;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Models;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources;

public sealed class ElasticsearchDataSourceOptionsConfigurationTests
{
    private const string TestUrl = "http://localhost:9200";

    [Fact]
    public void Configure_LeavesTheConnectionUnset_WhenNothingIsConfigured()
    {
        var options = Configure([]);

        Assert.Null(options.Url);
        Assert.False(options.HasConnection);
    }

    [Fact]
    public void Configure_ReadsTheSharedElasticsearchSection()
    {
        var options = Configure(new Dictionary<string, string>
        {
            ["CrestApps:Elasticsearch:Url"] = TestUrl,
            ["CrestApps:Elasticsearch:AuthenticationType"] = "Basic",
            ["CrestApps:Elasticsearch:Username"] = "elastic",
            ["CrestApps:Elasticsearch:Password"] = "elasticsearch",
        });

        Assert.True(options.HasConnection);
        Assert.Equal(TestUrl, options.Url);
        Assert.Equal("elastic", options.Username);
        Assert.Equal("elasticsearch", options.Password);
        Assert.Equal(ElasticsearchSourceMetadata.BasicAuthenticationType, options.GetAuthenticationType());
        Assert.Equal(ElasticsearchSourceMetadata.SelfManagedEnvironmentType, options.GetEnvironmentType());
    }

    [Fact]
    public void Configure_PrefersTheDataSourceSectionOverTheSharedSection()
    {
        var options = Configure(new Dictionary<string, string>
        {
            ["CrestApps:Elasticsearch:Url"] = "http://shared:9200",
            ["CrestApps:AI:DataSources:Elasticsearch:Url"] = "http://datasource:9200",
        });

        Assert.Equal("http://datasource:9200", options.Url);
    }

    [Fact]
    public void Configure_TrimsTheConfiguredValues()
    {
        var options = Configure(new Dictionary<string, string>
        {
            ["CrestApps:Elasticsearch:Url"] = $"  {TestUrl}  ",
            ["CrestApps:Elasticsearch:Username"] = "  elastic  ",
        });

        Assert.Equal(TestUrl, options.Url);
        Assert.Equal("elastic", options.Username);
    }

    [Fact]
    public void GetEnvironmentType_IsCloudHosted_WhenACloudIdIsConfigured()
    {
        var options = Configure(new Dictionary<string, string>
        {
            ["CrestApps:Elasticsearch:CloudId"] = "deployment:abc",
        });

        Assert.True(options.HasConnection);
        Assert.Equal(ElasticsearchSourceMetadata.CloudHostedEnvironmentType, options.GetEnvironmentType());
    }

    [Theory]
    [InlineData("ApiKey", ElasticsearchSourceMetadata.ApiKeyAuthenticationType)]
    [InlineData("apikey", ElasticsearchSourceMetadata.ApiKeyAuthenticationType)]
    [InlineData("Base64ApiKey", ElasticsearchSourceMetadata.Base64ApiKeyAuthenticationType)]
    [InlineData("KeyIdAndKey", ElasticsearchSourceMetadata.KeyIdAndKeyAuthenticationType)]
    [InlineData("nonsense", ElasticsearchSourceMetadata.NoneAuthenticationType)]
    public void GetAuthenticationType_NormalizesTheConfiguredValue(string configured, string expected)
    {
        var options = Configure(new Dictionary<string, string>
        {
            ["CrestApps:Elasticsearch:Url"] = TestUrl,
            ["CrestApps:Elasticsearch:AuthenticationType"] = configured,
        });

        Assert.Equal(expected, options.GetAuthenticationType());
    }

    [Fact]
    public void GetAuthenticationType_InfersBasic_FromTheConfiguredCredentials()
    {
        var options = Configure(new Dictionary<string, string>
        {
            ["CrestApps:Elasticsearch:Url"] = TestUrl,
            ["CrestApps:Elasticsearch:Username"] = "elastic",
            ["CrestApps:Elasticsearch:Password"] = "elasticsearch",
        });

        Assert.Equal(ElasticsearchSourceMetadata.BasicAuthenticationType, options.GetAuthenticationType());
    }

    [Fact]
    public async Task ValidateAsync_Succeeds_WhenOnlyTheGlobalConnectionIsConfigured()
    {
        var dataSource = CreateDataSource();
        var result = new ValidationResultDetails();

        await CreateHandler(TestUrl).ValidateAsync(dataSource, result, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidateAsync_Fails_WhenNoConnectionIsAvailable()
    {
        var dataSource = CreateDataSource();
        var result = new ValidationResultDetails();

        await CreateHandler(null).ValidateAsync(dataSource, result, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.MemberNames.Contains(nameof(ElasticsearchSourceMetadata.Url)));
    }

    [Fact]
    public async Task ValidateAsync_Fails_WhenTheIndexNameIsMissing()
    {
        var dataSource = new AIDataSource
        {
            Source = AIDataSourceSourceTypes.Elasticsearch,
        };

        dataSource.Put(new ElasticsearchSourceMetadata());

        var result = new ValidationResultDetails();

        await CreateHandler(TestUrl).ValidateAsync(dataSource, result, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.MemberNames.Contains(nameof(ElasticsearchSourceMetadata.IndexName)));
    }

    private static AIDataSource CreateDataSource()
    {
        var dataSource = new AIDataSource
        {
            Source = AIDataSourceSourceTypes.Elasticsearch,
        };

        dataSource.Put(new ElasticsearchSourceMetadata
        {
            IndexName = "knowledge-base",
        });

        return dataSource;
    }

    private static IAIDataSourceSourceHandler CreateHandler(string globalUrl)
    {
        var type = typeof(CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Startup).Assembly
            .GetType("CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services.ElasticsearchAIDataSourceSourceHandler", throwOnError: true)!;

        var constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)[0];
        var loggerType = typeof(NullLogger<>).MakeGenericType(type);

        // NullLogger<T>.Instance is a field, unlike the property on the non-generic NullLogger.
        var logger = loggerType.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
            ?? loggerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!.GetValue(null);

        return (IAIDataSourceSourceHandler)constructor.Invoke(
        [
            Mock.Of<IDataProtectionProvider>(),
            Options.Create(new ElasticsearchDataSourceOptions { Url = globalUrl }),
            logger,
        ]);
    }

    private static ElasticsearchDataSourceOptions Configure(Dictionary<string, string> configurationValues)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        var options = new ElasticsearchDataSourceOptions();

        new ElasticsearchDataSourceOptionsConfiguration(new TestShellConfiguration(configuration)).Configure(options);

        return options;
    }

    private sealed class TestShellConfiguration : IShellConfiguration
    {
        private readonly IConfiguration _configuration;

        public TestShellConfiguration(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string this[string key]
        {
            get => _configuration[key];
            set => _configuration[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren()
            => _configuration.GetChildren();

        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken()
            => _configuration.GetReloadToken();

        public IConfigurationSection GetSection(string key)
            => _configuration.GetSection(key);
    }
}
