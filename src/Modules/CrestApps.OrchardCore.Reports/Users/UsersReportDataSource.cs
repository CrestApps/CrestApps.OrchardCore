using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.Users;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Reports.Users;

/// <summary>
/// Exposes the users of the site as report data sets: <c>Users</c> (one row per user, with their account details and
/// the custom user settings stored with them) and <c>UserRoles</c> (one row per user and role). Only principals allowed
/// to view users see them. Password hashes, security stamps, and other secrets are never exposed.
/// </summary>
public sealed class UsersReportDataSource : IReportDataSource
{
    /// <summary>
    /// The technical name of the user roles data set.
    /// </summary>
    public const string UserRolesDataSet = "UserRoles";

    private const string PropertiesPrefix = "Properties.";

    private readonly ISession _session;
    private readonly IAuthorizationService _authorizationService;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="UsersReportDataSource"/> class.
    /// </summary>
    /// <param name="session">The YesSql session users are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public UsersReportDataSource(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<UsersReportDataSource> stringLocalizer)
    {
        _session = session;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Name => ReportsConstants.UsersDataSource;

    /// <inheritdoc/>
    public LocalizedString DisplayName => S["Users"];

    /// <inheritdoc/>
    public LocalizedString Description => S["The user accounts of the site and their roles."];

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ReportDataSetDescriptor>> GetDataSetsAsync(ReportDataSourceContext context, CancellationToken cancellationToken = default)
    {
        if (!await CanViewAsync(context))
        {
            return [];
        }

        return
        [
            new ReportDataSetDescriptor(ReportsConstants.UsersDataSet, S["Users"], S["One row per user account, with its custom user settings."]),
            new ReportDataSetDescriptor(UserRolesDataSet, S["User roles"], S["One row per user and role."])
            {
                References = [new ReportFieldReference(ReportsConstants.UsersDataSource, ReportsConstants.UsersDataSet, ReportsConstants.UserIdField)],
            },
        ];
    }

    /// <inheritdoc/>
    public async Task<ReportDataSetSchema> GetSchemaAsync(string dataSet, ReportDataSourceContext context, CancellationToken cancellationToken = default)
    {
        if (!IsKnown(dataSet) || !await CanViewAsync(context))
        {
            return null;
        }

        var dataSets = await GetDataSetsAsync(context, cancellationToken);
        var fields = dataSet == UserRolesDataSet
            ? RoleFields()
            : UserFields().Concat(QueryResultSchema.InferFields((await LoadAsync(QueryResultSchema.MaxFields, cancellationToken)).Select(PropertyValues)))
                .ToList();

        return new ReportDataSetSchema
        {
            DataSet = dataSets.First(candidate => candidate.Name == dataSet),
            Fields = fields,
        };
    }

    /// <inheritdoc/>
    public async Task<ReportDataTable> QueryAsync(ReportDataSourceQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var schema = await GetSchemaAsync(query.DataSet, query.Context, cancellationToken);

        if (schema is null)
        {
            return new ReportDataTable();
        }

        var fields = schema.Fields
            .Where(field => query.Fields is null || query.Fields.Count == 0 || query.Fields.Contains(field.Name))
            .ToList();
        var maxRows = Math.Max(1, query.MaxRows);
        var userIds = ReportJoinKeys.For(query.Conditions, ReportsConstants.UserIdField);

        if (userIds is { Count: 0 })
        {
            return new ReportDataTable { Fields = fields };
        }

        var users = await LoadAsync(maxRows + 1, cancellationToken, userIds);
        var table = new ReportDataTable
        {
            Fields = fields,
        };

        foreach (var user in users)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (query.DataSet == UserRolesDataSet)
            {
                foreach (var role in user.RoleNames ?? [])
                {
                    table.Rows.Add(fields.Select(field => (object)RoleValue(user, role, field.Name)).ToArray());
                }
            }
            else
            {
                var properties = PropertyValues(user).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

                table.Rows.Add(fields.Select(field => UserValue(user, properties, field)).ToArray());
            }

            if (table.Rows.Count >= maxRows)
            {
                table.Truncated = true;

                break;
            }
        }

        table.Truncated |= users.Count > maxRows;

        return table;
    }

    private static bool IsKnown(string dataSet)
    {
        return dataSet == ReportsConstants.UsersDataSet || dataSet == UserRolesDataSet;
    }

    private async Task<bool> CanViewAsync(ReportDataSourceContext context)
    {
        return context?.User is not null &&
            await _authorizationService.AuthorizeAsync(context.User, UsersPermissions.ViewUsers);
    }

    // Reads users in index order, or only the given users when a join sends their IDs.
    private async Task<List<User>> LoadAsync(int take, CancellationToken cancellationToken, IReadOnlyList<string> userIds = null)
    {
        var users = _session.Query<User, UserIndex>();

        if (userIds is not null)
        {
            var ids = userIds.ToArray();

            users = users.Where(index => index.UserId.IsIn(ids));
        }

        return (await users
            .OrderBy(index => index.Id)
            .Take(take)
            .ListAsync(cancellationToken))
            .ToList();
    }

    private List<ReportFieldDescriptor> UserFields()
    {
        var group = S["Account"].Value;

        return
        [
            new ReportFieldDescriptor(ReportsConstants.UserIdField, S["User ID"], ReportDataType.Text, group) { IsIdentifier = true, IsKeyFilterable = true },
            new ReportFieldDescriptor("UserName", S["User name"], ReportDataType.Text, group) { IsIdentifier = true },
            new ReportFieldDescriptor("Email", S["Email"], ReportDataType.Text, group),
            new ReportFieldDescriptor("EmailConfirmed", S["Email confirmed"], ReportDataType.Boolean, group),
            new ReportFieldDescriptor("PhoneNumber", S["Phone number"], ReportDataType.Text, group),
            new ReportFieldDescriptor("PhoneNumberConfirmed", S["Phone number confirmed"], ReportDataType.Boolean, group),
            new ReportFieldDescriptor("IsEnabled", S["Enabled"], ReportDataType.Boolean, group),
            new ReportFieldDescriptor("TwoFactorEnabled", S["Two-factor authentication"], ReportDataType.Boolean, group),
            new ReportFieldDescriptor("IsLockoutEnabled", S["Lockout enabled"], ReportDataType.Boolean, group),
            new ReportFieldDescriptor("LockoutEndUtc", S["Locked out until"], ReportDataType.DateTime, group),
            new ReportFieldDescriptor("AccessFailedCount", S["Failed sign-ins"], ReportDataType.Integer, group),
            new ReportFieldDescriptor("Roles", S["Roles"], ReportDataType.Text, group),
        ];
    }

    private List<ReportFieldDescriptor> RoleFields()
    {
        var group = S["Account"].Value;
        var userId = new ReportFieldDescriptor(ReportsConstants.UserIdField, S["User ID"], ReportDataType.Text, group) { IsIdentifier = true, IsKeyFilterable = true };

        userId.References.Add(new ReportFieldReference(ReportsConstants.UsersDataSource, ReportsConstants.UsersDataSet, ReportsConstants.UserIdField));

        return
        [
            userId,
            new ReportFieldDescriptor("UserName", S["User name"], ReportDataType.Text, group),
            new ReportFieldDescriptor("Role", S["Role"], ReportDataType.Text, group),
        ];
    }

    // The custom user settings and other data stored with a user, flattened like a query result under "Properties.".
    private static IReadOnlyList<KeyValuePair<string, JsonNode>> PropertyValues(User user)
    {
        if (user.Properties is null)
        {
            return [];
        }

        return QueryResultSchema.Flatten(user.Properties)
            .Where(pair => !ReportSecretNames.IsSecret(pair.Key))
            .Select(pair => new KeyValuePair<string, JsonNode>(PropertiesPrefix + pair.Key, pair.Value))
            .ToArray();
    }

    private static object UserValue(User user, Dictionary<string, JsonNode> properties, ReportFieldDescriptor field)
    {
        return field.Name switch
        {
            ReportsConstants.UserIdField => user.UserId,
            "UserName" => user.UserName,
            "Email" => user.Email,
            "EmailConfirmed" => user.EmailConfirmed,
            "PhoneNumber" => user.PhoneNumber,
            "PhoneNumberConfirmed" => user.PhoneNumberConfirmed,
            "IsEnabled" => user.IsEnabled,
            "TwoFactorEnabled" => user.TwoFactorEnabled,
            "IsLockoutEnabled" => user.IsLockoutEnabled,
            "LockoutEndUtc" => user.LockoutEndUtc.HasValue ? DateTime.SpecifyKind(user.LockoutEndUtc.Value, DateTimeKind.Utc) : null,
            "AccessFailedCount" => (long)user.AccessFailedCount,
            "Roles" => string.Join(", ", user.RoleNames ?? []),
            _ => properties.TryGetValue(field.Name, out var value) ? QueryResultSchema.ToValue(value, field.DataType) : null,
        };
    }

    private static string RoleValue(User user, string role, string fieldName)
    {
        return fieldName switch
        {
            ReportsConstants.UserIdField => user.UserId,
            "UserName" => user.UserName,
            "Role" => role,
            _ => null,
        };
    }
}
