using CrestApps.Core;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.Relational;
using CrestApps.OrchardCore.DataConnectors.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Tests.DataConnectors;

public sealed class SqlServerDataConnectorTests
{
    [Fact]
    public void BuildConnectionString_ForASqlLogin_TakesThePasswordFromTheSecretOnly()
    {
        // Arrange
        var settings = new SqlServerConnectionSettings
        {
            Server = "sql.example.com,1433",
            Database = "Sales",
            UserName = "reader",
            ReadOnlyIntent = true,
        };

        // Act
        var builder = SqlServerDataConnector.BuildConnectionString(settings, "p@ss;word");

        // Assert
        Assert.Equal("sql.example.com,1433", builder.DataSource);
        Assert.Equal("Sales", builder.InitialCatalog);
        Assert.Equal("reader", builder.UserID);
        Assert.Equal("p@ss;word", builder.Password);
        Assert.Equal(ApplicationIntent.ReadOnly, builder.ApplicationIntent);
        Assert.Equal(SqlConnectionEncryptOption.Mandatory, builder.Encrypt);
        Assert.False(builder.PersistSecurityInfo);
    }

    [Fact]
    public void BuildConnectionString_ForTheSiteWindowsAccount_SendsNoPassword()
    {
        // Act
        var builder = SqlServerDataConnector.BuildConnectionString(new SqlServerConnectionSettings { Server = "db", Database = "x", Authentication = SqlServerAuthenticationType.Integrated }, "ignored");

        // Assert
        Assert.True(builder.IntegratedSecurity);
        Assert.Equal(string.Empty, builder.Password);
    }

    [Fact]
    public void BuildConnectionString_ForAManagedIdentity_UsesEntra()
    {
        // Act
        var builder = SqlServerDataConnector.BuildConnectionString(new SqlServerConnectionSettings { Server = "db", Database = "x", Authentication = SqlServerAuthenticationType.EntraManagedIdentity }, null);

        // Assert
        Assert.Equal(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity, builder.Authentication);
    }

    [Fact]
    public void BuildConnectionString_WithoutAServer_Fails()
    {
        Assert.Throws<DataConnectorException>(() => SqlServerDataConnector.BuildConnectionString(new SqlServerConnectionSettings(), null));
    }

    [Theory]
    [InlineData("tcp:sql.example.com,1433", "sql.example.com")]
    [InlineData("db01\\SQLEXPRESS", "db01")]
    [InlineData("(localdb)\\MSSQLLocalDB", "localhost")]
    [InlineData(".", "localhost")]
    [InlineData("10.0.0.4", "10.0.0.4")]
    public void GetHost_ReadsTheHostTheNetworkRuleChecks(string server, string expected)
    {
        Assert.Equal(expected, SqlServerDataConnector.GetHost(server));
    }

    [Fact]
    public async Task OpenConnection_ToLocalDb_IsRefusedUnlessTheConfigurationAllowsTheSiteNetwork()
    {
        // Arrange
        var connector = new SqlServerDataConnector(Options.Create(new DataConnectorNetworkOptions()));
        var connection = DataConnectorTestData.Connection(SqlServerDataConnector.Name);
        connection.Put(new SqlServerConnectionSettings { Server = "(localdb)\\MSSQLLocalDB", Database = "master", Authentication = SqlServerAuthenticationType.Integrated });

        // Act
        var result = await ((IDataConnector)connector).TestAsync(new DataConnectorContext(connection, new FakeSecrets()), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("site's own network", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildRead_QuotesIdentifiers_BindsValues_AndLimitsWithTop()
    {
        // Arrange
        var connector = new SqlServerDataConnector(Options.Create(new DataConnectorNetworkOptions()));
        var dataSet = DataConnectorTestData.DataSet();
        dataSet.Resource = "sales.Order]Lines";

        var context = new DataConnectorReadContext(DataConnectorTestData.Connection(), new FakeSecrets(), dataSet) { MaxRows = 10 };
        context.Fields.Add("id");
        context.Fields.Add("name");
        context.Conditions.Add(new DataCondition { Field = "id", Operator = DataFilterOperator.In, Values = [1L, 2L] });
        context.Conditions.Add(new DataCondition { Field = "name", Operator = DataFilterOperator.Equals, Values = ["x'; DROP TABLE y; --"] });
        context.WatermarkField = "modified";
        context.Watermark = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        using var command = new SqlCommand();

        // Act
        var sql = connector.BuildRead(context, new RelationalDataSetSettings(), command);

        // Assert
        Assert.Equal("SELECT TOP (10) [id], [name] FROM [sales].[Order]]Lines] WHERE [id] IN (@p0, @p1) AND [modified] > @p2", sql);
        Assert.Equal(3, command.Parameters.Count);
        Assert.DoesNotContain("DROP", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildRead_WithACustomQuery_WrapsItAsADerivedTable()
    {
        // Arrange
        var connector = new SqlServerDataConnector(Options.Create(new DataConnectorNetworkOptions()));
        var context = new DataConnectorReadContext(DataConnectorTestData.Connection(), new FakeSecrets(), DataConnectorTestData.DataSet());

        using var command = new SqlCommand();

        // Act
        var sql = connector.BuildRead(context, new RelationalDataSetSettings { Query = "SELECT a FROM b" }, command);

        // Assert
        Assert.Equal("SELECT * FROM (SELECT a FROM b) [q]", sql);
    }
}
