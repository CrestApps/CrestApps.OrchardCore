using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Ap2;

/// <summary>
/// The SD-JWT hash functions (<c>_sd_alg</c>) AP2 uses over ASCII compact serializations.
/// </summary>
internal static class Ap2Hashing
{
    public const string Sha256 = "sha-256";

    public static bool IsSupported(string sdAlg)
    {
        return sdAlg is null or Sha256 or "sha-384" or "sha-512";
    }

    public static string Hash(string value, string sdAlg)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        var digest = sdAlg switch
        {
            null or Sha256 => SHA256.HashData(bytes),
            "sha-384" => SHA384.HashData(bytes),
            "sha-512" => SHA512.HashData(bytes),
            _ => throw new NotSupportedException($"The SD-JWT hash algorithm '{sdAlg}' is not supported."),
        };

        return Base64Url.EncodeToString(digest);
    }
}
