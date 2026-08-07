using System.Text.Json;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

public sealed class ContactCenterSupportMatrix
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public required ContactCenterTenantProfile[] TenantProfiles { get; init; }

    public static async Task<ContactCenterSupportMatrix> LoadAsync()
    {
        var path = Path.Combine(GetRepositoryRoot(), ".github", "contact-center", "support-matrix.v1.json");
        await using var stream = File.OpenRead(path);
        var matrix = await JsonSerializer.DeserializeAsync<ContactCenterSupportMatrix>(stream, _serializerOptions);

        return matrix ?? throw new InvalidOperationException($"Unable to load the Contact Center support matrix from '{path}'.");
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Unable to locate the repository root.");
    }
}
