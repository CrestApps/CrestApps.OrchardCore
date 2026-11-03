using System.Net;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.Http;

namespace CrestApps.OrchardCore.Tests.DataConnectors;

public sealed class DataConnectorNetworkTests
{
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("10.1.2.3", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.32.0.1", false)]
    [InlineData("192.168.1.10", true)]
    [InlineData("169.254.169.254", true)]
    [InlineData("100.64.0.1", true)]
    [InlineData("0.0.0.0", true)]
    [InlineData("::1", true)]
    [InlineData("fd00::1", true)]
    [InlineData("::ffff:10.0.0.1", true)]
    [InlineData("8.8.8.8", false)]
    [InlineData("2606:4700:4700::1111", false)]
    public void IsInternal_TellsTheSiteOwnNetworkFromThePublicOne(string address, bool expected)
    {
        Assert.Equal(expected, DataConnectorNetwork.IsInternal(IPAddress.Parse(address)));
    }

    [Fact]
    public async Task ResolveAllowedAsync_RefusesALoopbackHost_ByDefault()
    {
        // Act
        var exception = await Assert.ThrowsAsync<DataConnectorException>(() =>
            DataConnectorNetwork.ResolveAllowedAsync("127.0.0.1", new DataConnectorNetworkOptions(), TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("AllowedHosts", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveAllowedAsync_AllowsAnInternalHost_WhenTheConfigurationNamesIt()
    {
        // Arrange
        var options = new DataConnectorNetworkOptions { AllowedHosts = ["127.0.0.1"] };

        // Act
        var addresses = await DataConnectorNetwork.ResolveAllowedAsync("127.0.0.1", options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(IPAddress.Loopback, Assert.Single(addresses));
    }

    [Fact]
    public async Task ResolveAllowedAsync_AllowsEveryHost_WhenPrivateNetworksAreAllowed()
    {
        // Arrange
        var options = new DataConnectorNetworkOptions { AllowPrivateNetworks = true };

        // Act
        var addresses = await DataConnectorNetwork.ResolveAllowedAsync("10.0.0.5", options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(addresses);
    }

    [Fact]
    public async Task ResolveAllowedAsync_KeepsPublicAddresses()
    {
        // Act
        var addresses = await DataConnectorNetwork.ResolveAllowedAsync("8.8.8.8", new DataConnectorNetworkOptions(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(addresses);
    }
}
