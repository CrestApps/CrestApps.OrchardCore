using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Users;
using CrestApps.OrchardCore.Tests.Modules.Reports.Contents;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Moq;
using OrchardCore.Security;
using OrchardCore.Users;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Users;

/// <summary>
/// The users data source reads real user documents from a SQLite store, so the index query, the fields it exposes,
/// and the permission it requires are all exercised end to end.
/// </summary>
public sealed class UsersReportDataSourceTests : IAsyncLifetime
{
    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("u1", "Analyst");

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"user-reports-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new UserIndexProvider()]);

        await _store.InitializeAsync(TestContext.Current.CancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);

            // The index table is built from the index's own properties so the test follows the Orchard Core version.
            await new SchemaBuilder(_store.Configuration, transaction).CreateMapIndexTableAsync<UserIndex>(table =>
            {
                foreach (var property in typeof(UserIndex).GetProperties().Where(property => property.CanWrite && property.Name is not "Id" and not "DocumentId"))
                {
                    var underlying = Nullable.GetUnderlyingType(property.PropertyType);

                    table.Column(property.Name, underlying ?? property.PropertyType, column =>
                    {
                        if (underlying is not null || !property.PropertyType.IsValueType)
                        {
                            column.Nullable();
                        }
                    });
                }
            });

            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            await session.SaveAsync(new User
            {
                UserId = "user-ada",
                UserName = "ada",
                Email = "ada@example.com",
                EmailConfirmed = true,
                IsEnabled = true,
                AccessFailedCount = 2,
                RoleNames = ["Administrator", "Editor"],
                PasswordHash = "secret-hash",
                SecurityStamp = "secret-stamp",
                Properties = new JsonObject
                {
                    ["Profile"] = new JsonObject
                    {
                        ["FirstName"] = "Ada",
                        ["Age"] = 36,
                    },
                    ["Connections"] = new JsonObject
                    {
                        ["Phone"] = new JsonObject
                        {
                            ["ProviderName"] = "Phone",
                            ["AccessToken"] = "secret-access-token",
                            ["RefreshToken"] = "secret-refresh-token",
                            ["ClientSecret"] = "secret-client",
                        },
                    },
                },
            }, false, cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveAsync(new User
            {
                UserId = "user-grace",
                UserName = "grace",
                Email = "grace@example.com",
                IsEnabled = false,
                RoleNames = ["Editor"],
            }, false, cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        // Deleting retries while another test's connection still holds the file under a parallel run.
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task WithoutViewUsers_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, canViewUsers: false);
        var context = new ReportDataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(ReportsConstants.UsersDataSet, context, TestContext.Current.CancellationToken);
        var table = await source.QueryAsync(Query(ReportsConstants.UsersDataSet), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public async Task Schema_ExposesAccountFieldsAndCustomSettings_ButNoSecrets()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var dataSets = await source.GetDataSetsAsync(new ReportDataSourceContext { User = _user }, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(ReportsConstants.UsersDataSet, new ReportDataSourceContext { User = _user }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([ReportsConstants.UsersDataSet, UsersReportDataSource.UserRolesDataSet], dataSets.Select(dataSet => dataSet.Name));
        Assert.True(schema.FindField(ReportsConstants.UserIdField).IsIdentifier);
        Assert.Equal(ReportDataType.Text, schema.FindField("Properties.Profile.FirstName").DataType);
        Assert.Equal(ReportDataType.Integer, schema.FindField("Properties.Profile.Age").DataType);
        Assert.DoesNotContain(schema.Fields, field => field.Name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
            field.Name.Contains("SecurityStamp", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(ReportDataType.Text, schema.FindField("Properties.Connections.Phone.ProviderName").DataType);
        Assert.DoesNotContain(schema.Fields, field => field.Name.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
            field.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Query_NeverReturnsSecretProperties_EvenWhenAsked()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var table = await source.QueryAsync(Query(ReportsConstants.UsersDataSet, ReportsConstants.UserIdField, "Properties.Connections.Phone.AccessToken"), TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain(table.Rows.SelectMany(row => row), value => value is string text && text.StartsWith("secret-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Query_Users_ReturnsOneRowPerUser()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var table = await source.QueryAsync(Query(ReportsConstants.UsersDataSet, ReportsConstants.UserIdField, "IsEnabled", "AccessFailedCount", "Roles", "Properties.Profile.FirstName"), TestContext.Current.CancellationToken);

        // Assert
        var rows = Rows(table);
        Assert.Equal(2, rows.Count);
        Assert.Equal(true, rows[0]["IsEnabled"]);
        Assert.Equal(2L, rows[0]["AccessFailedCount"]);
        Assert.Equal("Administrator, Editor", rows[0]["Roles"]);
        Assert.Equal("Ada", rows[0]["Properties.Profile.FirstName"]);
        Assert.Equal(false, rows[1]["IsEnabled"]);
        Assert.Null(rows[1]["Properties.Profile.FirstName"]);
        Assert.False(table.Truncated);
    }

    [Fact]
    public async Task Query_UserRoles_ReturnsOneRowPerUserAndRole_AndReferencesUsers()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var schema = await source.GetSchemaAsync(UsersReportDataSource.UserRolesDataSet, new ReportDataSourceContext { User = _user }, TestContext.Current.CancellationToken);
        var table = await source.QueryAsync(Query(UsersReportDataSource.UserRolesDataSet), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ["user-ada/Administrator", "user-ada/Editor", "user-grace/Editor"],
            Rows(table).Select(row => row[ReportsConstants.UserIdField] + "/" + row["Role"]));
        Assert.Equal(ReportsConstants.UsersDataSet, Assert.Single(schema.FindField(ReportsConstants.UserIdField).References).DataSet);
        Assert.Equal(ReportsConstants.UsersDataSet, Assert.Single(schema.DataSet.References).DataSet);
    }

    [Fact]
    public async Task Query_WithJoinKeys_ReadsOnlyThoseUsers()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = Query(ReportsConstants.UsersDataSet, ReportsConstants.UserIdField, "UserName");
        query.Conditions.Add(new ReportDataCondition { Field = ReportsConstants.UserIdField, Operator = ReportFilterOperator.In, Values = ["user-grace"], IsJoinKey = true });

        // Act
        var table = await source.QueryAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("grace", Assert.Single(Rows(table))["UserName"]);
    }

    [Fact]
    public async Task Query_StopsAtMaxRows_AndSaysItWasTruncated()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = Query(ReportsConstants.UsersDataSet, ReportsConstants.UserIdField);
        query.MaxRows = 1;

        // Act
        var table = await source.QueryAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(table.Rows);
        Assert.True(table.Truncated);
    }

    private static UsersReportDataSource Source(ISession session, bool canViewUsers = true)
    {
        var authorizationService = new Mock<IAuthorizationService>();

        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                canViewUsers && requirements.OfType<PermissionRequirement>().All(requirement => requirement.Permission.Name == UsersPermissions.ViewUsers.Name)
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        return new UsersReportDataSource(session, authorizationService.Object, new ContentReportTestLocalizer<UsersReportDataSource>());
    }

    private static ReportDataSourceQuery Query(string dataSet, params string[] fields)
    {
        return new ReportDataSourceQuery
        {
            DataSet = dataSet,
            Fields = new HashSet<string>(fields, StringComparer.Ordinal),
            MaxRows = 100,
            Context = new ReportDataSourceContext { User = _user },
        };
    }

    private static List<Dictionary<string, object>> Rows(ReportDataTable table)
    {
        return table.Rows
            .Select(row => table.Fields.Select((field, index) => (field.Name, Value: row[index])).ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal))
            .ToList();
    }
}
