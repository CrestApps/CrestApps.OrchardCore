using System.Text.Json;
using CrestApps.OrchardCore.Tests.Telephony.ProviderContracts;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Proves that the vendored Asterisk REST Interface declarations really are the verbatim artifacts the Asterisk project
/// published for the Asterisk release the single-node Contact Center container image is pinned to. Without this gate the
/// provider contract tests could drift into asserting a convenient fiction instead of the provider's real protocol.
/// </summary>
public sealed class AsteriskAriContractProvenanceTests
{
    [Fact]
    public void Cassettes_AreVendoredForTheAsteriskReleaseTheContainerImageIsPinnedTo()
    {
        // Arrange
        var cassettes = AsteriskContractCassettes.Load();
        var manifest = cassettes.Manifest.RootElement;

        // Act
        var pinnedVersion = AsteriskContractCassettes.ReadPinnedContainerVersion();
        var pinnedTag = AsteriskContractCassettes.ReadPinnedContainerImageTag();
        var pinnedDigest = AsteriskContractCassettes.ReadPinnedContainerImageDigest();

        // Assert
        Assert.Equal(pinnedVersion, cassettes.Version);
        Assert.Equal(pinnedVersion, manifest.GetProperty("asteriskVersion").GetString());
        Assert.Equal(pinnedVersion, manifest.GetProperty("sourceRef").GetString());
        Assert.Equal(pinnedTag, manifest.GetProperty("containerImageTag").GetString());
        Assert.Equal(pinnedDigest, manifest.GetProperty("containerImageDigest").GetString());
    }

    [Fact]
    public void Cassettes_VendorTheUnmodifiedSpecificationFilesRecordedInTheManifest()
    {
        // Arrange
        var cassettes = AsteriskContractCassettes.Load();
        var specFiles = cassettes.Manifest.RootElement.GetProperty("specFiles");

        // Act & Assert
        Assert.NotEqual(0, specFiles.GetArrayLength());

        foreach (var specFile in specFiles.EnumerateArray())
        {
            var name = specFile.GetProperty("name").GetString();
            var expectedHash = specFile.GetProperty("sha256").GetString();
            var url = specFile.GetProperty("url").GetString();
            var path = Path.Combine(cassettes.DirectoryPath, name);

            Assert.True(File.Exists(path), $"The vendored specification file '{name}' is missing.");
            Assert.Equal(expectedHash, AsteriskContractCassettes.ComputeSha256(path));
            Assert.Contains($"/{cassettes.Version}/", url, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Cassettes_ParseIntoAUsableAsteriskRestInterfaceSpecification()
    {
        // Arrange
        var cassettes = AsteriskContractCassettes.Load();

        // Act
        var specification = cassettes.Specification;

        // Assert
        Assert.True(specification.Models.Count > 50, "The vendored declarations must describe the full ARI model set.");
        Assert.True(specification.Operations.Count > 40, "The vendored declarations must describe the full ARI operation set.");
        Assert.True(specification.DeclaresPropertyPath("StasisStart", "channel.caller.number"));
        Assert.False(specification.DeclaresPropertyPath("StasisStart", "channel.caller.telephone_number"));
    }

    [Fact]
    public void Cassettes_DeclareEveryFileTheyShipSoUnvendoredArtifactsCannotAccumulate()
    {
        // Arrange
        var cassettes = AsteriskContractCassettes.Load();
        var declared = new HashSet<string>(StringComparer.Ordinal);

        foreach (var specFile in cassettes.Manifest.RootElement.GetProperty("specFiles").EnumerateArray())
        {
            declared.Add(specFile.GetProperty("name").GetString());
        }

        // Act
        var vendored = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.GetFiles(cassettes.DirectoryPath, "spec-*.json"))
        {
            vendored.Add(Path.GetFileName(file));
        }

        // Assert
        Assert.Equal(declared.OrderBy(name => name, StringComparer.Ordinal), vendored.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void Manifest_RecordsTheUpstreamSourceSoTheArtifactsCanBeReproduced()
    {
        // Arrange
        var cassettes = AsteriskContractCassettes.Load();
        var manifest = cassettes.Manifest.RootElement;

        // Act
        var repository = manifest.GetProperty("sourceRepository").GetString();
        var sourcePath = manifest.GetProperty("sourcePath").GetString();

        // Assert
        Assert.Equal("https://github.com/asterisk/asterisk", repository);
        Assert.Equal("rest-api/api-docs", sourcePath);
        Assert.Equal(JsonValueKind.String, manifest.GetProperty("description").ValueKind);
    }
}
