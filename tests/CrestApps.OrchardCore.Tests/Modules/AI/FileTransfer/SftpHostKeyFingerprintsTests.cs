using System.Security.Cryptography;
using CrestApps.Core.AI.Sftp;
using CrestApps.OrchardCore.AI.FileTransfer.Core;

namespace CrestApps.OrchardCore.Tests.Modules.AI.FileTransfer;

/// <summary>
/// MD7 (fix verification, round 6): the SFTP fingerprint field took only a SHA-256 fingerprint, checked by a copy of the
/// parsing in the host, while the SFTP client of CrestApps.Core also trusts an MD5 fingerprint and several fingerprints
/// in one field. The field now accepts exactly the forms the client matches, decided by the client's own check.
/// </summary>
public sealed class SftpHostKeyFingerprintsTests
{
    // A server key's fingerprints, as SSH.NET reports them.
    private static readonly byte[] _sha256 = SHA256.HashData("server host key"u8);
    private static readonly byte[] _md5 = Convert.FromHexString("1627aca576282d36631b564debdfa648");

    private static readonly string _sha256Base64 = Convert.ToBase64String(_sha256).TrimEnd('=');
    private static readonly string _md5Hex = Convert.ToHexString(_md5);

    public static TheoryData<string> AcceptedForms => new()
    {
        "SHA256:" + _sha256Base64,
        "sha256:" + _sha256Base64 + "=",
        _sha256Base64,
        "  SHA256:" + _sha256Base64 + "  ",
        _md5Hex,
        _md5Hex.ToLowerInvariant(),
        "MD5:" + ColonSeparated(_md5Hex),
        ColonSeparated(_md5Hex.ToLowerInvariant()),
        "SHA256:" + _sha256Base64 + ", MD5:" + ColonSeparated(_md5Hex),
        _md5Hex + ";" + "SHA256:" + _sha256Base64,
        "SHA256:" + _sha256Base64 + "\nSHA256:" + Convert.ToBase64String(SHA256.HashData("rotated key"u8)).TrimEnd('='),
    };

    public static TheoryData<string> RefusedForms => new()
    {
        null,
        "",
        "   ",
        "SHA256:",
        "SHA256:not-a-fingerprint",
        "AAAA",
        "SHA256:" + _md5Hex,
        "MD5:" + _sha256Base64,
        "MD5:" + _md5Hex[..30],
        "SHA256:" + _sha256Base64 + ", typo",
        "SHA256: " + _sha256Base64[..20] + " " + _sha256Base64[20..],
    };

    [Theory]
    [MemberData(nameof(AcceptedForms))]
    public void IsValid_AcceptsEveryFormTheClientMatches(string field)
    {
        Assert.True(SftpHostKeyFingerprints.IsValid(field));

        // The client trusts the key the field names.
        Assert.True(SftpHostKeys.Matches(field, _sha256Base64, _md5));
    }

    [Theory]
    [MemberData(nameof(RefusedForms))]
    public void IsValid_RefusesWhatTheClientNeverMatches(string field)
        => Assert.False(SftpHostKeyFingerprints.IsValid(field));

    [Fact]
    public void AnAcceptedField_IsNotTrustedForAnotherKey()
    {
        var field = "SHA256:" + _sha256Base64 + ", MD5:" + ColonSeparated(_md5Hex);
        var other = SHA256.HashData("attacker host key"u8);

        Assert.True(SftpHostKeyFingerprints.IsValid(field));
        Assert.False(SftpHostKeys.Matches(field, Convert.ToBase64String(other).TrimEnd('='), Convert.FromHexString("00112233445566778899aabbccddeeff")));
    }

    [Fact]
    public void Normalize_TrimsTheField_AndStoresABlankOneAsNull()
    {
        Assert.Equal("SHA256:" + _sha256Base64, SftpHostKeyFingerprints.Normalize("  SHA256:" + _sha256Base64 + " "));
        Assert.Null(SftpHostKeyFingerprints.Normalize("  "));
    }

    private static string ColonSeparated(string hex)
        => string.Join(':', Enumerable.Range(0, hex.Length / 2).Select(index => hex.Substring(index * 2, 2)));
}
