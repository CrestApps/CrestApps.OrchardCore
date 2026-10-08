namespace CrestApps.OrchardCore.TenantHierarchy.Core.Provisioning;

/// <summary>
/// Builds the SQL Server statements that create and remove the database or schema of a child tenant. Every name is
/// checked by <see cref="ProvisioningNames.EnsureSafeIdentifier(string)"/>, and the password holds letters and digits only.
/// </summary>
public static class SqlServerProvisioningScripts
{
    /// <summary>
    /// Returns the statements that create a login and a database the login owns, run on the pool's server.
    /// </summary>
    /// <param name="database">The database name.</param>
    /// <param name="login">The login name.</param>
    /// <param name="password">The password of the login.</param>
    public static string[] CreateDatabase(string database, string login, string password)
    {
        ProvisioningNames.EnsureSafeIdentifier(database);
        ProvisioningNames.EnsureSafeIdentifier(login);
        EnsureSafePassword(password);

        return
        [
            $"CREATE DATABASE [{database}];",
            $"CREATE LOGIN [{login}] WITH PASSWORD = N'{password}', DEFAULT_DATABASE = [{database}], CHECK_EXPIRATION = OFF;",
        ];
    }

    /// <summary>
    /// Returns the statements that map the login into its database as its owner, run inside the new database.
    /// </summary>
    /// <param name="login">The login name.</param>
    public static string[] GrantDatabaseOwner(string login)
    {
        ProvisioningNames.EnsureSafeIdentifier(login);

        return
        [
            $"CREATE USER [{login}] FOR LOGIN [{login}];",
            $"ALTER ROLE [db_owner] ADD MEMBER [{login}];",
        ];
    }

    /// <summary>
    /// Returns the statements that remove a child database and its login. A retained database is renamed instead of dropped.
    /// </summary>
    /// <param name="database">The database name.</param>
    /// <param name="login">The login name.</param>
    /// <param name="retainedName">The name to rename the database to, or <see langword="null"/> to drop it.</param>
    public static string[] RemoveDatabase(string database, string login, string retainedName)
    {
        ProvisioningNames.EnsureSafeIdentifier(database);
        ProvisioningNames.EnsureSafeIdentifier(login);

        var statements = new List<string>
        {
            $"IF DB_ID(N'{database}') IS NOT NULL ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;",
        };

        if (retainedName is null)
        {
            statements.Add($"IF DB_ID(N'{database}') IS NOT NULL DROP DATABASE [{database}];");
        }
        else
        {
            ProvisioningNames.EnsureSafeIdentifier(retainedName);
            statements.Add($"IF DB_ID(N'{database}') IS NOT NULL ALTER DATABASE [{database}] MODIFY NAME = [{retainedName}];");
            statements.Add($"IF DB_ID(N'{retainedName}') IS NOT NULL ALTER DATABASE [{retainedName}] SET MULTI_USER;");
        }

        statements.Add($"IF SUSER_ID(N'{login}') IS NOT NULL DROP LOGIN [{login}];");

        return statements.ToArray();
    }

    /// <summary>
    /// Returns the statements that create a schema and a login that can work only in that schema, run inside the
    /// pool's database.
    /// </summary>
    /// <param name="schema">The schema name.</param>
    /// <param name="login">The login name.</param>
    /// <param name="password">The password of the login.</param>
    public static string[] CreateSchema(string schema, string login, string password)
    {
        ProvisioningNames.EnsureSafeIdentifier(schema);
        ProvisioningNames.EnsureSafeIdentifier(login);
        EnsureSafePassword(password);

        return
        [
            $"CREATE LOGIN [{login}] WITH PASSWORD = N'{password}', CHECK_EXPIRATION = OFF;",
            $"CREATE USER [{login}] FOR LOGIN [{login}] WITH DEFAULT_SCHEMA = [{schema}];",
            $"EXEC (N'CREATE SCHEMA [{schema}] AUTHORIZATION [{login}]');",
            $"GRANT CREATE TABLE TO [{login}];",
        ];
    }

    /// <summary>
    /// Returns the statements that remove a schema and its login. Orchard Core has already dropped the tenant tables.
    /// </summary>
    /// <param name="schema">The schema name.</param>
    /// <param name="login">The login name.</param>
    public static string[] RemoveSchema(string schema, string login)
    {
        ProvisioningNames.EnsureSafeIdentifier(schema);
        ProvisioningNames.EnsureSafeIdentifier(login);

        return
        [
            $"IF SCHEMA_ID(N'{schema}') IS NOT NULL EXEC (N'DROP SCHEMA [{schema}]');",
            $"IF DATABASE_PRINCIPAL_ID(N'{login}') IS NOT NULL DROP USER [{login}];",
            $"IF SUSER_ID(N'{login}') IS NOT NULL DROP LOGIN [{login}];",
        ];
    }

    private static void EnsureSafePassword(string password)
    {
        if (string.IsNullOrEmpty(password) || !password.All(char.IsAsciiLetterOrDigit))
        {
            throw new ArgumentException("The password must hold letters and digits only.", nameof(password));
        }
    }
}
