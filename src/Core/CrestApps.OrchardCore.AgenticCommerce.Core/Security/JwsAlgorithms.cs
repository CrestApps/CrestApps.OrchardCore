using System.Security.Cryptography;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Security;

/// <summary>
/// The ECDSA JWS algorithms (RFC 7518) this module signs and verifies with, and the curve each one needs.
/// </summary>
internal static class JwsAlgorithms
{
    public const string ES256 = "ES256";

    public const string ES384 = "ES384";

    public const string ES512 = "ES512";

    public const string CurveP256 = "P-256";

    public const string CurveP384 = "P-384";

    public const string CurveP521 = "P-521";

    public static bool TryGetAlgorithm(string alg, out HashAlgorithmName hashAlgorithm, out int keySize)
    {
        switch (alg)
        {
            case ES256:
                hashAlgorithm = HashAlgorithmName.SHA256;
                keySize = 256;

                return true;

            case ES384:
                hashAlgorithm = HashAlgorithmName.SHA384;
                keySize = 384;

                return true;

            case ES512:
                hashAlgorithm = HashAlgorithmName.SHA512;
                keySize = 521;

                return true;

            default:
                hashAlgorithm = default;
                keySize = 0;

                return false;
        }
    }

    public static string GetAlgorithmForCurve(string curve)
    {
        return curve switch
        {
            CurveP256 => ES256,
            CurveP384 => ES384,
            CurveP521 => ES512,
            _ => null,
        };
    }

    public static string GetCurveForKeySize(int keySize)
    {
        return keySize switch
        {
            256 => CurveP256,
            384 => CurveP384,
            521 => CurveP521,
            _ => null,
        };
    }

    public static int GetCoordinateSize(string curve)
    {
        return curve switch
        {
            CurveP256 => 32,
            CurveP384 => 48,
            CurveP521 => 66,
            _ => 0,
        };
    }

    public static ECCurve GetNamedCurve(string curve)
    {
        return curve switch
        {
            CurveP256 => ECCurve.NamedCurves.nistP256,
            CurveP384 => ECCurve.NamedCurves.nistP384,
            CurveP521 => ECCurve.NamedCurves.nistP521,
            _ => throw new NotSupportedException($"The curve '{curve}' is not supported."),
        };
    }

    public static int GetSignatureSize(int keySize)
    {
        return 2 * ((keySize + 7) / 8);
    }
}
