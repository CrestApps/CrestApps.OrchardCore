namespace CrestApps.OrchardCore.TenantHierarchy.Core.Provisioning;

/// <summary>
/// Builds the PostgreSQL statements that create and remove the database or schema of a child tenant. Every name is
/// checked by <see cref="ProvisioningNames.EnsureSafeIdentifier(string)"/>, and the password holds letters and digits only.
/// </summary>
public static class PostgreSqlProvisioningScripts
{
    /// <summary>
    /// Returns the statements that create a role and a database the role owns, run on the pool's server.
    /// </summary>
    /// <param name="database">The database name.</param>
    /// <param name="role">The role name.</param>
    /// <param name="password">The password of the role.</param>
    public static string[] CreateDatabase(string database, string role, string password)
    {
        ProvisioningNames.EnsureSafeIdentifier(database);
        ProvisioningNames.EnsureSafeIdentifier(role);
        EnsureSafePassword(password);

        return
        [
            $"CREATE ROLE \"{role}\" LOGIN PASSWORD '{password}' NOSUPERUSER NOCREATEDB NOCREATEROLE;",
            $"CREATE DATABASE \"{database}\" OWNER \"{role}\";",
            $"REVOKE ALL ON DATABASE \"{database}\" FROM PUBLIC;",
            $"GRANT CONNECT ON DATABASE \"{database}\" TO \"{role}\";",
        ];
    }

    /// <summary>
    /// Returns the statements that remove a child database and its role. A retained database is renamed and handed to
    /// the pool's login instead of dropped.
    /// </summary>
    /// <param name="database">The database name.</param>
    /// <param name="role">The role name.</param>
    /// <param name="retainedName">The name to rename the database to, or <see langword="null"/> to drop it.</param>
    public static string[] RemoveDatabase(string database, string role, string retainedName)
    {
        ProvisioningNames.EnsureSafeIdentifier(database);
        ProvisioningNames.EnsureSafeIdentifier(role);

        var statements = new List<string>
        {
            $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{database}' AND pid <> pg_backend_pid();",
        };

        if (retainedName is null)
        {
            statements.Add($"DROP DATABASE IF EXISTS \"{database}\";");
        }
        else
        {
            ProvisioningNames.EnsureSafeIdentifier(retainedName);
            statements.Add($"ALTER DATABASE \"{database}\" RENAME TO \"{retainedName}\";");
            statements.Add($"ALTER DATABASE \"{retainedName}\" OWNER TO CURRENT_USER;");
        }

        statements.Add($"DROP ROLE IF EXISTS \"{role}\";");

        return statements.ToArray();
    }

    /// <summary>
    /// Returns the statements that create a role and a schema the role owns, run inside the pool's database.
    /// </summary>
    /// <param name="schema">The schema name.</param>
    /// <param name="role">The role name.</param>
    /// <param name="password">The password of the role.</param>
    public static string[] CreateSchema(string schema, string role, string password)
    {
        ProvisioningNames.EnsureSafeIdentifier(schema);
        ProvisioningNames.EnsureSafeIdentifier(role);
        EnsureSafePassword(password);

        return
        [
            $"CREATE ROLE \"{role}\" LOGIN PASSWORD '{password}' NOSUPERUSER NOCREATEDB NOCREATEROLE;",
            $"CREATE SCHEMA \"{schema}\" AUTHORIZATION \"{role}\";",
            $"ALTER ROLE \"{role}\" SET search_path = \"{schema}\";",
            "DO $$ BEGIN EXECUTE format('GRANT CONNECT ON DATABASE %I TO " + $"\"{role}\"" + "', current_database()); END $$;",
        ];
    }

    /// <summary>
    /// Returns the statements that remove a schema and its role.
    /// </summary>
    /// <param name="schema">The schema name.</param>
    /// <param name="role">The role name.</param>
    public static string[] RemoveSchema(string schema, string role)
    {
        ProvisioningNames.EnsureSafeIdentifier(schema);
        ProvisioningNames.EnsureSafeIdentifier(role);

        return
        [
            $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE;",
            $"DO $$ BEGIN IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{role}') THEN EXECUTE format('REVOKE ALL ON DATABASE %I FROM \"{role}\"', current_database()); END IF; END $$;",
            $"DROP ROLE IF EXISTS \"{role}\";",
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
