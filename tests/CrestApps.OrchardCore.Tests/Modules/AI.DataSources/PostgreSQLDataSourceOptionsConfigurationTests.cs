using System.Reflection;
using CrestApps.Core;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.DataSources.PostgreSQL.Models;
using CrestApps.OrchardCore.AI.DataSources.PostgreSQL.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources;

public sealed class PostgreSQLDataSourceOptionsConfigurationTests
{
    private const string TestConnectionString = "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=vectordb";

    [Fact]
    public void Configure_LeavesTheConnectionStringUnset_WhenNothingIsConfigured()
    {
        var options = Configure([]);

        Assert.Null(options.ConnectionString);
    }

    [Fact]
    public void Configure_ReadsTheSharedPostgreSQLSection()
    {
        var options = Configure(new Dictionary<string, string>
        {
            ["CrestApps:PostgreSQL:ConnectionString"] = TestConnectionString,
        });

        Assert.Equal(TestConnectionString, options.ConnectionString);
    }

    [Fact]
    public void Configure_ReadsTheDataSourceSection()
    {
        var options = Configure(new Dictionary<string, string>
        {
            ["CrestApps:AI:DataSources:PostgreSQL:ConnectionString"] = TestConnectionString,
        });

        Assert.Equal(TestConnectionString, options.ConnectionString);
    }

    [Fact]
    public void Configure_PrefersTheDataSourceSectionOverTheSharedSection()
    {
        var options = Configure(new Dictionary<string, string>
        {
            ["CrestApps:PostgreSQL:ConnectionString"] = "Host=shared",
            ["CrestApps:AI:DataSources:PostgreSQL:ConnectionString"] = "Host=datasource",
        });

        Assert.Equal("Host=datasource", options.ConnectionString);
    }

    [Fact]
    public void Configure_TrimsTheConnectionString()
    {
        var options = Configure(new Dictionary<string, string>
        {
            ["CrestApps:PostgreSQL:ConnectionString"] = $"  {TestConnectionString}  ",
        });

        Assert.Equal(TestConnectionString, options.ConnectionString);
    }

    [Fact]
    public async Task ValidateAsync_Succeeds_WhenOnlyTheGlobalConnectionStringIsConfigured()
    {
        var dataSource = CreateDataSource();
        var result = new ValidationResultDetails();

        await CreateHandler(TestConnectionString).ValidateAsync(dataSource, result, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidateAsync_Fails_WhenNoConnectionStringIsAvailable()
    {
        var dataSource = CreateDataSource();
        var result = new ValidationResultDetails();

        await CreateHandler(null).ValidateAsync(dataSource, result, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.MemberNames.Contains(nameof(PostgreSQLSourceMetadata.ConnectionString)));
    }

    private static AIDataSource CreateDataSource()
    {
        var dataSource = new AIDataSource
        {
            Source = AIDataSourceSourceTypes.PostgreSQL,
        };

        dataSource.Put(new PostgreSQLSourceMetadata
        {
            TableName = "public.articles",
        });

        return dataSource;
    }

    private static IAIDataSourceSourceHandler CreateHandler(string globalConnectionString)
    {
        var type = typeof(CrestApps.OrchardCore.AI.DataSources.PostgreSQL.Startup).Assembly
            .GetType("CrestApps.OrchardCore.AI.DataSources.PostgreSQL.Services.PostgreSQLAIDataSourceSourceHandler", throwOnError: true)!;

        var constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)[0];
        var loggerType = typeof(NullLogger<>).MakeGenericType(type);

        // NullLogger<T>.Instance is a field, unlike the property on the non-generic NullLogger.
        var logger = loggerType.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
            ?? loggerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!.GetValue(null);

        return (IAIDataSourceSourceHandler)constructor.Invoke(
        [
            Mock.Of<IDataProtectionProvider>(),
            Options.Create(new PostgreSQLDataSourceOptions { ConnectionString = globalConnectionString }),
            logger,
        ]);
    }

    private static PostgreSQLDataSourceOptions Configure(Dictionary<string, string> configurationValues)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        var options = new PostgreSQLDataSourceOptions();

        new PostgreSQLDataSourceOptionsConfiguration(new TestShellConfiguration(configuration)).Configure(options);

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
