using CrestApps.OrchardCore.SignalR.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.Tests.SignalR;

public sealed class AzureBackplaneStartupTests
{
    [Fact]
    public void ConfigureServices_WithoutConnectionString_DoesNotRegisterAzureSignalR()
    {
        // Arrange
        var services = new ServiceCollection();
        var startup = new Startup(
            BuildShellConfiguration(connectionString: null),
            NullLogger<Startup>.Instance);

        // Act
        startup.ConfigureServices(services);

        // Assert
        Assert.DoesNotContain(services, IsAzureSignalRService);
    }

    [Fact]
    public void ConfigureServices_WithConnectionString_RegistersAzureSignalR()
    {
        // Arrange
        var services = new ServiceCollection();
        var startup = new Startup(
            BuildShellConfiguration("Endpoint=https://tenant.service.signalr.net;AccessKey=abc123;Version=1.0;"),
            NullLogger<Startup>.Instance);

        // Act
        startup.ConfigureServices(services);

        // Assert
        Assert.Contains(services, IsAzureSignalRService);
    }

    private static bool IsAzureSignalRService(ServiceDescriptor descriptor)
        => descriptor.ServiceType.Namespace?.StartsWith("Microsoft.Azure.SignalR", StringComparison.Ordinal) == true;

    private static IShellConfiguration BuildShellConfiguration(string connectionString)
    {
        var settings = new Dictionary<string, string>();

        if (connectionString is not null)
        {
            settings["CrestApps:SignalR:Azure:ConnectionString"] = connectionString;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var shellConfiguration = new Mock<IShellConfiguration>();
        shellConfiguration
            .Setup(config => config.GetSection("CrestApps:SignalR:Azure"))
            .Returns(configuration.GetSection("CrestApps:SignalR:Azure"));

        return shellConfiguration.Object;
    }
}
