using System.Security.Cryptography;
using CrestApps.OrchardCore.Core.Security;
using Microsoft.AspNetCore.DataProtection;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

/// <summary>
/// Protects the MCP server's API key in site settings, which used to hold it in clear text where anyone who could read
/// the settings document or a database backup could use it.
/// </summary>
internal static class McpServerApiKeyProtection
{
    /// <summary>
    /// The data protection purpose of the key.
    /// </summary>
    public const string Purpose = "CrestApps.OrchardCore.AI.Mcp.ServerApiKey";

    private const string ProtectedPayloadPrefix = "CfDJ8";

    /// <summary>
    /// Returns the key to store for what the settings form posted: a new key protected, a blank one keeping the stored
    /// key.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="stored">The stored key.</param>
    /// <param name="posted">The posted key.</param>
    public static string Apply(IDataProtectionProvider dataProtectionProvider, string stored, string posted)
        => dataProtectionProvider.ApplySecret(Purpose, stored, posted);

    /// <summary>
    /// Reads the stored key. A key stored before it was protected is read as it is, and is protected the next time the
    /// settings are saved with a new key. A protected key this site's keys can't read (a lost key ring) gives no key:
    /// its ciphertext must never become the key clients are compared with.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="stored">The stored key.</param>
    public static string Read(IDataProtectionProvider dataProtectionProvider, string stored)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return stored;
        }

        try
        {
            return dataProtectionProvider.UnprotectSetting(Purpose, stored);
        }
        catch (CryptographicException)
        {
            // Data protection payloads start with the magic header 09 F0 C9 F0, which base64url writes as "CfDJ8".
            return stored.StartsWith(ProtectedPayloadPrefix, StringComparison.Ordinal) ? null : stored;
        }
    }
}
