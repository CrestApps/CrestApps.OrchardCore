using CrestApps.OrchardCore.DataConnectors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.DataConnectors;

public sealed class DataConnectorManagerTests
{
    [Fact]
    public void AddDataConnector_RegistersAConnectorFromOutsideTheModule_AndResolvesItByNameIgnoringCase()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddDataConnector<TestDataConnector>();
        using var provider = services.BuildServiceProvider();

        var manager = new DataConnectorManager(provider.GetRequiredService<IOptions<DataConnectorOptions>>(), provider);

        // Act
        var descriptor = manager.GetDescriptor("TEST");
        var connector = manager.GetConnector("test");

        // Assert
        Assert.NotNull(descriptor);
        Assert.Equal(TestDataConnector.Name, descriptor.Name);
        Assert.True(descriptor.Supports(DataConnectorCapabilities.Live | DataConnectorCapabilities.Import));
        Assert.IsType<TestDataConnector>(connector);
        Assert.Single(manager.GetConnectors());
    }

    [Fact]
    public void AddDataConnector_Twice_RegistersTheConnectorOnce()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddDataConnector<TestDataConnector>();
        services.AddDataConnector<TestDataConnector>();
        using var provider = services.BuildServiceProvider();

        // Act
        var connectors = provider.GetRequiredService<IOptions<DataConnectorOptions>>().Value.Connectors;

        // Assert
        Assert.Single(connectors);
    }

    [Fact]
    public void GetConnector_WithAnUnknownName_ReturnsNull()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOptions();
        using var provider = services.BuildServiceProvider();

        var manager = new DataConnectorManager(provider.GetRequiredService<IOptions<DataConnectorOptions>>(), provider);

        // Act & Assert
        Assert.Null(manager.GetConnector("Missing"));
        Assert.Null(manager.GetDescriptor(null));
    }
}
