using System.Net;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.Http;
using CrestApps.OrchardCore.DataConnectors.SqlServer;

namespace CrestApps.OrchardCore.Tests.DataConnectors;

/// <summary>
/// Review 11 (Mild): with <c>AllowPrivateNetworks</c> or <c>AllowedHosts</c>, the data connectors' network rule allowed
/// the link-local range and the cloud metadata services too; and the SQL Server connector checked the name it resolved,
/// then let SqlClient resolve it again, so a name that answered differently the second time (DNS rebinding) reached the
/// site's own network. The metadata addresses are now refused always, and SqlClient connects to the address that was
/// checked, with the certificate still checked against the name.
/// </summary>
public sealed class DataConnectorNetworkPinningTests
{
    [Theory]
    [InlineData("169.254.169.254")]
    [InlineData("168.63.129.16")]
    [InlineData("fd00:ec2::254")]
    public async Task ResolveAllowedAsync_RefusesTheMetadataService_EvenWhenPrivateNetworksAreAllowed(string address)
    {
        var options = new DataConnectorNetworkOptions { AllowPrivateNetworks = true };

        await Assert.ThrowsAsync<DataConnectorException>(() => DataConnectorNetwork.ResolveAllowedAsync(address, options, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ResolveAllowedAsync_RefusesTheMetadataService_EvenWhenTheConfigurationNamesIt()
    {
        var options = new DataConnectorNetworkOptions { AllowedHosts = ["169.254.169.254"] };

        await Assert.ThrowsAsync<DataConnectorException>(() => DataConnectorNetwork.ResolveAllowedAsync("169.254.169.254", options, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("sql.example.com", "tcp:203.0.113.7")]
    [InlineData("tcp:sql.example.com,1433", "tcp:203.0.113.7,1433")]
    [InlineData("sql.example.com\\SALES", "tcp:203.0.113.7\\SALES")]
    public void PinToAddress_ConnectsToTheCheckedAddress_AndChecksTheCertificateAgainstTheName(string server, string expected)
    {
        var builder = SqlServerDataConnector.BuildConnectionString(new SqlServerConnectionSettings { Server = server, Database = "Sales", UserName = "reader" }, "p", allowHostIdentity: false);

        SqlServerDataConnector.PinToAddress(builder, server, IPAddress.Parse("203.0.113.7"));

        Assert.Equal(expected, builder.DataSource);
        Assert.Equal("sql.example.com", builder.HostNameInCertificate);
    }

    [Fact]
    public void PinToAddress_AnIPv6Address_IsWrittenAsSqlClientReadsIt()
    {
        var builder = SqlServerDataConnector.BuildConnectionString(new SqlServerConnectionSettings { Server = "sql.example.com,1433", Database = "Sales", UserName = "reader" }, "p", allowHostIdentity: false);

        SqlServerDataConnector.PinToAddress(builder, "sql.example.com,1433", IPAddress.Parse("2001:db8::7"));

        Assert.Equal("tcp:2001:db8::7,1433", builder.DataSource);
    }

    [Theory]
    [InlineData("myserver.database.windows.net")]
    [InlineData("tcp:myserver.database.windows.net,1433")]
    [InlineData("10.0.0.4")]
    [InlineData("(localdb)\\MSSQLLocalDB")]
    public void PinToAddress_LeavesAzureSqlAddressesAndLocalServersAsTheyAre(string server)
    {
        // Azure SQL's gateway routes by the server name, and its names are answered by Microsoft's DNS, not the tenant's.
        var builder = SqlServerDataConnector.BuildConnectionString(new SqlServerConnectionSettings { Server = server, Database = "Sales", UserName = "reader" }, "p", allowHostIdentity: false);

        SqlServerDataConnector.PinToAddress(builder, server, IPAddress.Parse("203.0.113.7"));

        Assert.Equal(server, builder.DataSource);
    }

    [Fact]
    public void PinToAddress_LeavesIntegratedSignInOnTheName()
    {
        // Kerberos finds the server's account by its name.
        var builder = SqlServerDataConnector.BuildConnectionString(new SqlServerConnectionSettings { Server = "db01", Database = "Sales", Authentication = SqlServerAuthenticationType.Integrated }, null, allowHostIdentity: true);

        SqlServerDataConnector.PinToAddress(builder, "db01", IPAddress.Parse("203.0.113.7"));

        Assert.Equal("db01", builder.DataSource);
    }
}
