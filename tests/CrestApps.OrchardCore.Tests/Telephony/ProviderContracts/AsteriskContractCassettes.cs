using System.Security.Cryptography;
using System.Text.Json;

namespace CrestApps.OrchardCore.Tests.Telephony.ProviderContracts;

/// <summary>
/// Locates and validates the vendored Asterisk provider contract artifacts, and exposes the parsed specification and
/// the recorded payload cassettes that the provider contract tests replay through production code.
/// </summary>
internal sealed class AsteriskContractCassettes
{
    private const string CassetteRootRelativePath = "tests/CrestApps.OrchardCore.Tests/Telephony/Cassettes/Asterisk";
    private const string DockerfileRelativePath = "src/Startup/CrestApps.Aspire.AppHost/Asterisk/Dockerfile";

    private AsteriskContractCassettes(
        string directoryPath,
        string version,
        JsonDocument manifest,
        AriSpecification specification)
    {
        DirectoryPath = directoryPath;
        Version = version;
        Manifest = manifest;
        Specification = specification;
    }

    /// <summary>
    /// Gets the absolute path of the vendored cassette directory for the pinned Asterisk release.
    /// </summary>
    public string DirectoryPath { get; }

    /// <summary>
    /// Gets the Asterisk release the vendored artifacts were published for.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Gets the parsed provenance manifest.
    /// </summary>
    public JsonDocument Manifest { get; }

    /// <summary>
    /// Gets the parsed Asterisk REST Interface specification.
    /// </summary>
    public AriSpecification Specification { get; }

    /// <summary>
    /// Loads the single vendored cassette set. Exactly one version directory must exist so that an Asterisk upgrade
    /// cannot silently leave stale contract artifacts behind.
    /// </summary>
    /// <returns>The loaded cassette set.</returns>
    public static AsteriskContractCassettes Load()
    {
        var repositoryRoot = FindRepositoryRoot();
        var cassetteRoot = Path.Combine(repositoryRoot, CassetteRootRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var versionDirectories = Directory.GetDirectories(cassetteRoot);

        if (versionDirectories.Length != 1)
        {
            throw new InvalidOperationException(
                $"Exactly one Asterisk contract cassette version directory must exist under '{cassetteRoot}', but {versionDirectories.Length} were found.");
        }

        var directoryPath = versionDirectories[0];
        var version = new DirectoryInfo(directoryPath).Name;
        var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directoryPath, "manifest.json")));
        var specificationFilePaths = new List<string>();

        foreach (var file in manifest.RootElement.GetProperty("specFiles").EnumerateArray())
        {
            specificationFilePaths.Add(Path.Combine(directoryPath, file.GetProperty("name").GetString()));
        }

        return new AsteriskContractCassettes(
            directoryPath,
            version,
            manifest,
            AriSpecification.Load(specificationFilePaths));
    }

    /// <summary>
    /// Reads the Asterisk release that the single-node container image is pinned to.
    /// </summary>
    /// <returns>The pinned Asterisk release, for example <c>22.10.1</c>.</returns>
    public static string ReadPinnedContainerVersion()
    {
        var tag = ReadPinnedContainerImageTag();
        var separatorIndex = tag.IndexOf(':', StringComparison.Ordinal);
        var versionAndVariant = tag.Substring(separatorIndex + 1);
        var variantIndex = versionAndVariant.IndexOf('_', StringComparison.Ordinal);

        return variantIndex < 0
            ? versionAndVariant
            : versionAndVariant.Substring(0, variantIndex);
    }

    /// <summary>
    /// Reads the container image tag comment that documents the digest the single-node image is pinned to.
    /// </summary>
    /// <returns>The pinned container image tag.</returns>
    public static string ReadPinnedContainerImageTag()
    {
        foreach (var line in ReadDockerfileLines())
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("# andrius/asterisk:", StringComparison.Ordinal))
            {
                return trimmed.Substring(2).Trim();
            }
        }

        throw new InvalidOperationException(
            "The Asterisk Dockerfile must document the pinned image tag in a comment so the provider contract cassettes can be bound to it.");
    }

    /// <summary>
    /// Reads the container image digest the single-node image is pinned to.
    /// </summary>
    /// <returns>The pinned container image digest.</returns>
    public static string ReadPinnedContainerImageDigest()
    {
        foreach (var line in ReadDockerfileLines())
        {
            var trimmed = line.Trim();
            var markerIndex = trimmed.IndexOf("@sha256:", StringComparison.Ordinal);

            if (trimmed.StartsWith("FROM ", StringComparison.Ordinal) && markerIndex > 0)
            {
                return trimmed.Substring(markerIndex + 1);
            }
        }

        throw new InvalidOperationException(
            "The Asterisk Dockerfile must pin its base image by digest so the provider contract cassettes can be bound to it.");
    }

    /// <summary>
    /// Computes the lowercase hexadecimal SHA-256 hash of a vendored artifact.
    /// </summary>
    /// <param name="filePath">The absolute path of the artifact.</param>
    /// <returns>The computed hash.</returns>
    public static string ComputeSha256(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(filePath))).ToLowerInvariant();
    }

    /// <summary>
    /// Reads every recorded payload cassette stored in the supplied sub-directory of the cassette set.
    /// </summary>
    /// <param name="relativeDirectory">The cassette sub-directory, for example <c>events</c>.</param>
    /// <returns>The cassette file names mapped to their verbatim contents.</returns>
    public Dictionary<string, string> ReadCassettes(string relativeDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeDirectory);

        var cassettes = new Dictionary<string, string>(StringComparer.Ordinal);
        var directory = Path.Combine(DirectoryPath, relativeDirectory);

        foreach (var file in Directory.GetFiles(directory, "*.json"))
        {
            cassettes[Path.GetFileNameWithoutExtension(file)] = File.ReadAllText(file);
        }

        return cassettes;
    }

    /// <summary>
    /// Reads a single recorded Asterisk REST Interface response by its HTTP method and path template.
    /// </summary>
    /// <param name="httpMethod">The HTTP method, for example <c>GET</c>.</param>
    /// <param name="pathTemplate">The recorded path template, for example <c>channels/{channelId}</c>.</param>
    /// <param name="statusCode">When this method returns, the recorded status code, if a match was found.</param>
    /// <param name="body">When this method returns, the recorded response body, if a match was found.</param>
    /// <returns><see langword="true"/> if a matching recorded response exists; otherwise <see langword="false"/>.</returns>
    public bool TryReadRecordedRestResponse(string httpMethod, string pathTemplate, out int statusCode, out string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(httpMethod);
        ArgumentException.ThrowIfNullOrWhiteSpace(pathTemplate);

        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(DirectoryPath, "rest", "responses.json")));

        foreach (var recorded in document.RootElement.GetProperty("responses").EnumerateArray())
        {
            if (!string.Equals(recorded.GetProperty("httpMethod").GetString(), httpMethod, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(recorded.GetProperty("pathTemplate").GetString(), pathTemplate, StringComparison.Ordinal))
            {
                continue;
            }

            statusCode = recorded.GetProperty("statusCode").GetInt32();

            if (recorded.TryGetProperty("body", out var bodyElement))
            {
                body = bodyElement.GetRawText();
            }
            else if (recorded.TryGetProperty("textBody", out var textElement))
            {
                body = textElement.GetString();
            }
            else
            {
                body = null;
            }

            return true;
        }

        statusCode = 0;
        body = null;

        return false;
    }

    /// <summary>
    /// Reads a repository file relative to the repository root.
    /// </summary>
    /// <param name="relativePath">The forward-slash separated path relative to the repository root.</param>
    /// <returns>The file contents.</returns>
    public static string ReadRepositoryFile(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        return File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string[] ReadDockerfileLines()
    {
        var repositoryRoot = FindRepositoryRoot();

        return File.ReadAllLines(Path.Combine(repositoryRoot, DockerfileRelativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException("The repository root could not be located.");
    }
}
