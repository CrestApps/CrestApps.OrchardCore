using CrestApps.OrchardCore.TenantHierarchy.Core;
using CrestApps.OrchardCore.TenantHierarchy.Core.Provisioning;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.TenantHierarchy;

public sealed class ProvisioningTests
{
    [Fact]
    public void ForTenant_BuildsASafeIdentifier()
    {
        // Act
        var name = ProvisioningNames.ForTenant("u_AB-c9'; DROP");

        // Assert
        Assert.Equal("th_u_abc9drop", name);
        Assert.True(ProvisioningNames.IsSafeIdentifier(name));
    }

    [Theory]
    [InlineData("th_u_abc", true)]
    [InlineData("Th_U_1", true)]
    [InlineData("th-u", false)]
    [InlineData("th u", false)]
    [InlineData("th\"u", false)]
    [InlineData("th]u", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSafeIdentifier_AcceptsLettersDigitsAndUnderscoresOnly(string name, bool expected)
    {
        // Assert
        Assert.Equal(expected, ProvisioningNames.IsSafeIdentifier(name));
    }

    [Fact]
    public void IsSafeIdentifier_LongerThanAPostgreSqlName_IsRefused()
        => Assert.False(ProvisioningNames.IsSafeIdentifier(new string('a', 64)));

    [Fact]
    public void CreatePassword_HoldsLettersAndDigitsOnly()
    {
        // Act
        var passwords = Enumerable.Range(0, 50).Select(_ => ProvisioningNames.CreatePassword()).ToList();

        // Assert
        Assert.All(passwords, password => Assert.Matches("^[A-Za-z0-9]{32}$", password));
        Assert.Equal(passwords.Count, passwords.Distinct().Count());
    }

    [Fact]
    public void CreateTablePrefix_IsLowercaseLetters()
    {
        // Act
        var prefix = ProvisioningNames.CreateTablePrefix();

        // Assert
        Assert.Matches("^t[a-z]{9}$", prefix);
    }

    [Fact]
    public void ForRetainedDatabase_StaysWithinTheNameLimit()
    {
        // Act
        var name = ProvisioningNames.ForRetainedDatabase(new string('a', 60), new DateTime(2026, 10, 8, 14, 30, 0, DateTimeKind.Utc));

        // Assert
        Assert.True(name.Length <= 63);
        Assert.EndsWith("_removed_20261008143000", name, StringComparison.Ordinal);
    }

    [Fact]
    public void SqlServerCreateDatabase_QuotesEveryName()
    {
        // Act
        var statements = SqlServerProvisioningScripts.CreateDatabase("th_u_abc", "th_u_abc", "Password1");

        // Assert
        Assert.Equal("CREATE DATABASE [th_u_abc];", statements[0]);
        Assert.Contains("CREATE LOGIN [th_u_abc] WITH PASSWORD = N'Password1'", statements[1], StringComparison.Ordinal);
    }

    [Fact]
    public void SqlServerScripts_UnsafeNameOrPassword_AreRefused()
    {
        // Assert
        Assert.Throws<ArgumentException>(() => SqlServerProvisioningScripts.CreateDatabase("x]; DROP DATABASE master;--", "th", "Password1"));
        Assert.Throws<ArgumentException>(() => SqlServerProvisioningScripts.CreateDatabase("th_a", "th_a", "pass'word"));
        Assert.Throws<ArgumentException>(() => SqlServerProvisioningScripts.CreateSchema("th_a", "th_a", "pass word"));
        Assert.Throws<ArgumentException>(() => SqlServerProvisioningScripts.RemoveDatabase("th_a", "th_a", "x; --"));
    }

    [Fact]
    public void SqlServerRemoveDatabase_RetainsByRenaming()
    {
        // Act
        var retained = SqlServerProvisioningScripts.RemoveDatabase("th_a", "th_a", "th_a_removed_1");
        var dropped = SqlServerProvisioningScripts.RemoveDatabase("th_a", "th_a", null);

        // Assert
        Assert.Contains(retained, statement => statement.Contains("MODIFY NAME = [th_a_removed_1]", StringComparison.Ordinal));
        Assert.DoesNotContain(retained, statement => statement.Contains("DROP DATABASE", StringComparison.Ordinal));
        Assert.Contains(dropped, statement => statement.Contains("DROP DATABASE [th_a]", StringComparison.Ordinal));
        Assert.All([retained, dropped], statements => Assert.Contains(statements, statement => statement.Contains("DROP LOGIN [th_a]", StringComparison.Ordinal)));
    }

    [Fact]
    public void PostgreSqlCreateDatabase_GivesTheDatabaseToItsOwnRole()
    {
        // Act
        var statements = PostgreSqlProvisioningScripts.CreateDatabase("th_u_abc", "th_u_abc", "Password1");

        // Assert
        Assert.Contains("CREATE ROLE \"th_u_abc\" LOGIN PASSWORD 'Password1' NOSUPERUSER NOCREATEDB NOCREATEROLE;", statements);
        Assert.Contains("CREATE DATABASE \"th_u_abc\" OWNER \"th_u_abc\";", statements);
        Assert.Contains("REVOKE ALL ON DATABASE \"th_u_abc\" FROM PUBLIC;", statements);
    }

    [Fact]
    public void PostgreSqlScripts_UnsafeNameOrPassword_AreRefused()
    {
        // Assert
        Assert.Throws<ArgumentException>(() => PostgreSqlProvisioningScripts.CreateDatabase("x\"; DROP", "th", "Password1"));
        Assert.Throws<ArgumentException>(() => PostgreSqlProvisioningScripts.CreateSchema("th_a", "th_a", "pa'ss"));
        Assert.Throws<ArgumentException>(() => PostgreSqlProvisioningScripts.RemoveSchema("th_a", "th a"));
    }

    [Fact]
    public void PostgreSqlRemoveDatabase_RetainsByRenamingAndHandingItOver()
    {
        // Act
        var statements = PostgreSqlProvisioningScripts.RemoveDatabase("th_a", "th_a", "th_a_removed_1");

        // Assert
        Assert.Contains("ALTER DATABASE \"th_a\" RENAME TO \"th_a_removed_1\";", statements);
        Assert.Contains("ALTER DATABASE \"th_a_removed_1\" OWNER TO CURRENT_USER;", statements);
        Assert.Contains("DROP ROLE IF EXISTS \"th_a\";", statements);
    }

    [Fact]
    public async Task SqliteProvisioner_PlacesTheDatabaseInTheTenantFolder()
    {
        // Arrange
        var provisioner = new SqliteChildDatabaseProvisioner();
        var context = new ChildDatabaseProvisioningContext { TenantName = "u_abc", Strategy = ChildDatabaseStrategy.SqlitePerChild };

        // Act
        var result = await provisioner.ProvisionAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(provisioner.CanProvision(context));
        Assert.True(result.Succeeded);
        Assert.Equal(DatabaseProviderNames.Sqlite, result.DatabaseProvider);
        Assert.Equal(SqliteChildDatabaseProvisioner.DatabaseFileName, result.DatabaseName);
    }

    [Fact]
    public async Task TablePrefixProvisioner_UsesThePoolWithARandomPrefix()
    {
        // Arrange
        var provisioner = new TablePrefixChildDatabaseProvisioner();
        var context = new ChildDatabaseProvisioningContext
        {
            TenantName = "u_abc",
            Strategy = ChildDatabaseStrategy.TablePrefixPerChild,
            Pool = new DatabasePoolOptions { DatabaseProvider = "Postgres", ConnectionString = "Host=db" },
        };

        // Act
        var result = await provisioner.ProvisionAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(provisioner.CanProvision(context));
        Assert.Equal("Postgres", result.DatabaseProvider);
        Assert.Equal("Host=db", result.ConnectionString);
        Assert.Equal(result.TablePrefix, result.ProvisionedResource);
        Assert.Matches("^t[a-z]{9}$", result.TablePrefix);
    }

    [Theory]
    [InlineData(ChildDatabaseStrategy.DatabasePerChild, "SqlConnection", true, false)]
    [InlineData(ChildDatabaseStrategy.SchemaPerChild, "SqlConnection", true, false)]
    [InlineData(ChildDatabaseStrategy.DatabasePerChild, "Postgres", false, true)]
    [InlineData(ChildDatabaseStrategy.SchemaPerChild, "Postgres", false, true)]
    [InlineData(ChildDatabaseStrategy.DatabasePerChild, "MySql", false, false)]
    [InlineData(ChildDatabaseStrategy.TablePrefixPerChild, "Postgres", false, false)]
    public void ServerProvisioners_HandleTheirProviderOnly(ChildDatabaseStrategy strategy, string provider, bool sqlServer, bool postgreSql)
    {
        // Arrange
        var clock = Mock.Of<IClock>();
        var context = new ChildDatabaseProvisioningContext
        {
            TenantName = "u_abc",
            Strategy = strategy,
            Pool = new DatabasePoolOptions { DatabaseProvider = provider, ConnectionString = "Server=db" },
        };

        // Assert
        Assert.Equal(sqlServer, new SqlServerChildDatabaseProvisioner(clock).CanProvision(context));
        Assert.Equal(postgreSql, new PostgreSqlChildDatabaseProvisioner(clock).CanProvision(context));
    }

    [Fact]
    public async Task ChildDatabaseProvisioning_UnknownPool_FailsWithoutThrowing()
    {
        // Arrange
        var provisioning = CreateProvisioning(new TenantHierarchyOptions());

        // Act
        var result = await provisioning.ProvisionAsync("u_abc", ChildDatabaseStrategy.DatabasePerChild, "missing", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(provisioning.CanProvision(ChildDatabaseStrategy.DatabasePerChild, "missing"));
        Assert.True(provisioning.CanProvision(ChildDatabaseStrategy.SqlitePerChild, null));
    }

    [Fact]
    public async Task ChildDatabaseProvisioning_ProvisionerThatThrows_FailsWithoutThrowing()
    {
        // Arrange
        var failing = new Mock<IChildTenantDatabaseProvisioner>();
        failing.Setup(provisioner => provisioner.CanProvision(It.IsAny<ChildDatabaseProvisioningContext>())).Returns(true);
        failing.Setup(provisioner => provisioner.ProvisionAsync(It.IsAny<ChildDatabaseProvisioningContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("server down"));

        var provisioning = new ChildDatabaseProvisioning(
            [failing.Object],
            Options.Create(new TenantHierarchyOptions()),
            NullLogger<ChildDatabaseProvisioning>.Instance);

        // Act
        var result = await provisioning.ProvisionAsync("u_abc", ChildDatabaseStrategy.SqlitePerChild, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.DoesNotContain("server down", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChildDatabaseProvisioning_Deprovision_SkipsSqliteAndUnknownResources()
    {
        // Arrange
        var provisioner = new Mock<IChildTenantDatabaseProvisioner>();
        provisioner.Setup(candidate => candidate.CanProvision(It.IsAny<ChildDatabaseProvisioningContext>())).Returns(true);
        var provisioning = new ChildDatabaseProvisioning(
            [provisioner.Object],
            Options.Create(new TenantHierarchyOptions()),
            NullLogger<ChildDatabaseProvisioning>.Instance);

        // Act
        await provisioning.DeprovisionAsync("u_abc", ChildDatabaseStrategy.SqlitePerChild, null, "anything", TestContext.Current.CancellationToken);
        await provisioning.DeprovisionAsync("u_abc", ChildDatabaseStrategy.TablePrefixPerChild, "pool", null, TestContext.Current.CancellationToken);

        // Assert
        provisioner.Verify(candidate => candidate.DeprovisionAsync(It.IsAny<ChildDatabaseProvisioningContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ChildDatabaseProvisioning CreateProvisioning(TenantHierarchyOptions options)
    {
        var clock = Mock.Of<IClock>();

        return new ChildDatabaseProvisioning(
            [
                new SqliteChildDatabaseProvisioner(),
                new TablePrefixChildDatabaseProvisioner(),
                new SqlServerChildDatabaseProvisioner(clock),
                new PostgreSqlChildDatabaseProvisioner(clock),
            ],
            Options.Create(options),
            NullLogger<ChildDatabaseProvisioning>.Instance);
    }
}
