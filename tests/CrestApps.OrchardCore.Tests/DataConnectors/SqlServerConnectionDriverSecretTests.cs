using CrestApps.Core;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Services;
using CrestApps.OrchardCore.DataConnectors.SqlServer;
using CrestApps.OrchardCore.DataConnectors.SqlServer.Drivers;
using CrestApps.OrchardCore.DataConnectors.SqlServer.ViewModels;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.DataConnectors;

/// <summary>
/// Low (Data) of the final security review: a SQL Server connection kept its stored password when the server changed, so
/// a user who may edit connections could point it at a server of their own and collect the password from the sign-in.
/// The password is now dropped when the server or the authentication changes, in the editor and in a recipe.
/// </summary>
public sealed class SqlServerConnectionDriverSecretTests
{
    private readonly DataConnectionSecretManager _secrets = new(new EphemeralDataProtectionProvider(), NullLogger<DataConnectionSecretManager>.Instance);

    [Theory]
    [InlineData("attacker.example", SqlServerAuthenticationType.SqlLogin)]
    [InlineData("sql.example.com", SqlServerAuthenticationType.EntraServicePrincipal)]
    public async Task UpdateAsync_ANewServerOrAuthentication_DropsTheStoredPassword(string server, SqlServerAuthenticationType authentication)
    {
        // Arrange
        var connection = StoredConnection();
        var context = CreateContext(new SqlServerConnectionViewModel { Server = server, Database = "crm", Authentication = authentication, UserName = "reader", ConnectTimeoutSeconds = 15 });

        // Act
        await CreateDriver().UpdateAsync(connection, context);

        // Assert
        Assert.False(_secrets.Has(connection, "Password"));
        Assert.False(context.Updater.ModelState.IsValid);
    }

    [Fact]
    public async Task UpdateAsync_TheSameServerInAnotherCase_KeepsThePassword()
    {
        // Arrange
        var connection = StoredConnection();
        var context = CreateContext(new SqlServerConnectionViewModel { Server = " SQL.example.com ", Database = "sales", Authentication = SqlServerAuthenticationType.SqlLogin, UserName = "reader", ConnectTimeoutSeconds = 15 });

        // Act
        await CreateDriver().UpdateAsync(connection, context);

        // Assert
        Assert.True(_secrets.Has(connection, "Password"));
        Assert.True(context.Updater.ModelState.IsValid);
    }

    [Fact]
    public async Task UpdateAsync_ANewServerWithANewPassword_KeepsTheNewOne()
    {
        var connection = StoredConnection();
        var context = CreateContext(new SqlServerConnectionViewModel { Server = "other.example.com", Database = "crm", Authentication = SqlServerAuthenticationType.SqlLogin, UserName = "reader", Password = "new-secret", ConnectTimeoutSeconds = 15 });

        await CreateDriver().UpdateAsync(connection, context);

        Assert.Equal("new-secret", _secrets.CreateReader(connection).Get("Password"));
    }

    private DataConnection StoredConnection()
    {
        var connection = new DataConnection { ItemId = "c1", Name = "crm", Connector = DataConnectorConstants.Connectors.SqlServer };
        connection.Put(new SqlServerConnectionSettings { Server = "sql.example.com", Database = "crm", Authentication = SqlServerAuthenticationType.SqlLogin, UserName = "reader", ConnectTimeoutSeconds = 15 });
        _secrets.Set(connection, "Password", "stored-secret");

        return connection;
    }

    private SqlServerConnectionDisplayDriver CreateDriver()
        => new(_secrets, new ShellSettings { Name = ShellSettings.DefaultShellName }, Options.Create(new SqlServerHostIdentityOptions()), new PassThroughLocalizer());

    private static UpdateEditorContext CreateContext(SqlServerConnectionViewModel posted)
        => new(null, string.Empty, false, string.Empty, null, null, new FakeUpdateModel(posted));

    private sealed class PassThroughLocalizer : IStringLocalizer<SqlServerConnectionDisplayDriver>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
