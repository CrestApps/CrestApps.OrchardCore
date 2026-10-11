using System.Globalization;
using System.Security.Cryptography;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Provisioning;

/// <summary>
/// Builds the safe database, schema, login and table prefix names the provisioners create.
/// </summary>
public static class ProvisioningNames
{
    private const string LowercaseAlphabet = "abcdefghijklmnopqrstuvwxyz";
    private const string PasswordAlphabet = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>
    /// Returns the database, schema or login name of a child tenant: <c>th_</c> followed by the opaque tenant name,
    /// lowercased, with every character other than letters, digits and underscores removed.
    /// </summary>
    /// <param name="tenantName">The opaque tenant name.</param>
    public static string ForTenant(string tenantName)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantName);

        var cleaned = new string(tenantName
            .ToLowerInvariant()
            .Where(character => character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_')
            .ToArray());

        if (cleaned.Length == 0)
        {
            throw new ArgumentException("The tenant name has no character that can be used in a database name.", nameof(tenantName));
        }

        return $"th_{cleaned}";
    }

    /// <summary>
    /// Returns whether a name is safe to quote into a SQL statement: letters, digits and underscores only.
    /// </summary>
    /// <param name="name">The name.</param>
    public static bool IsSafeIdentifier(string name)
    {
        return !string.IsNullOrEmpty(name) &&
            name.Length <= 63 &&
            name.All(character => character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_');
    }

    /// <summary>
    /// Throws when a name is not safe to quote into a SQL statement.
    /// </summary>
    /// <param name="name">The name.</param>
    public static string EnsureSafeIdentifier(string name)
    {
        if (!IsSafeIdentifier(name))
        {
            throw new ArgumentException($"'{name}' is not a safe database identifier.", nameof(name));
        }

        return name;
    }

    /// <summary>
    /// Returns a random table prefix of letters only, for example <c>tkqzmwpe</c>.
    /// </summary>
    public static string CreateTablePrefix()
    {
        return "t" + RandomNumberGenerator.GetString(LowercaseAlphabet, 9);
    }

    /// <summary>
    /// Returns a random password of 32 letters and digits for a child database login. It never holds a quote.
    /// </summary>
    public static string CreatePassword()
    {
        return RandomNumberGenerator.GetString(PasswordAlphabet, 30) + "a1";
    }

    /// <summary>
    /// Returns the name a retained database is renamed to, for example <c>th_u_abc_removed_20261008143000</c>.
    /// </summary>
    /// <param name="name">The database name.</param>
    /// <param name="utcNow">The current time.</param>
    public static string ForRetainedDatabase(string name, DateTime utcNow)
    {
        var suffix = "_removed_" + utcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var maxLength = 63 - suffix.Length;

        return (name.Length > maxLength ? name.Substring(0, maxLength) : name) + suffix;
    }
}
