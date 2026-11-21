using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Computes the content hash of a definition: the SHA-256 of its JSON, written in lowercase hexadecimal.
/// </summary>
public static class SurveyContentHash
{
    /// <summary>
    /// Computes the hash.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <returns>The hash.</returns>
    public static string Compute(SurveyDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var json = JsonSerializer.Serialize(definition);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}
