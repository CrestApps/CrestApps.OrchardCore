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
    /// settings are saved with a new key.
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
            return stored;
        }
    }
}
