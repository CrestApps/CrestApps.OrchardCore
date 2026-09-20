using CrestApps.Core.ContactCenter;
using CrestApps.OrchardCore.ContactCenter;
using System.Reflection;
using System.Text;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests.DependencyInjection;

/// <summary>
/// Records what every Contact Center feature registers in a tenant container, and fails when that
/// changes.
/// </summary>
/// <remarks>
/// Phase 0 of the framework extraction moves hundreds of registration statements out of the Orchard
/// startups and into framework registration methods. A registration that is dropped, re-typed, given
/// a different lifetime, or moved within an <c>IEnumerable</c> chain produces a container that still
/// builds and still passes every functional test while behaving differently at run time. This
/// repository has already been burned by exactly that: an inbound call router registered in the
/// wrong order made calls ring without ever being offered.
/// <para>
/// The baselines are therefore captured before the moves begin, and every later change is read as a
/// diff against them. Features are discovered by reflection over the feature-id constants, so a
/// feature added later is covered without anyone remembering to extend this file.
/// </para>
/// </remarks>
public sealed class DependencyInjectionSnapshotTests
{
    [Fact]
    public async Task EveryFeature_RegistersTheApprovedServiceDescriptors()
    {
        // Arrange
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var profiles = await GetProfilesAsync();

        Assert.NotEmpty(profiles);

        var differences = new List<string>();
        var written = new List<string>();
        var captured = 0;

        // Act
        foreach (var profile in profiles)
        {
            var featureId = profile.Id;
            var tenant = await host.CreateTenantAsync(profile);

            var actual = TenantServiceCollectionCapture.Find(tenant.Settings.Name);

            if (actual is null)
            {
                differences.Add($"'{featureId}': nothing was captured for this tenant, so the snapshot proves nothing.");

                continue;
            }

            captured++;

            // The tenant name is a fresh identifier on every run, so it can never appear in a baseline.
            actual = actual.Replace(tenant.Settings.Name, "<tenant>", StringComparison.Ordinal);

            var baselinePath = GetBaselinePath(featureId);

            if (!File.Exists(baselinePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(baselinePath));
                await File.WriteAllTextAsync(baselinePath, actual, TestContext.Current.CancellationToken);
                written.Add(baselinePath);

                continue;
            }

            var approved = await File.ReadAllTextAsync(baselinePath, TestContext.Current.CancellationToken);

            if (!string.Equals(Normalize(approved), Normalize(actual), StringComparison.Ordinal))
            {
                var receivedPath = baselinePath.Replace(".approved.txt", ".received.txt", StringComparison.Ordinal);
                await File.WriteAllTextAsync(receivedPath, actual, TestContext.Current.CancellationToken);
                differences.Add($"'{featureId}': {DescribeFirstDifference(approved, actual)} See '{receivedPath}'.");
            }
        }

        // Assert
        Assert.True(
            captured > 0,
            "No tenant service collection was captured at all, so this gate proves nothing. DiSnapshotStartup " +
            "must run inside every tenant; check that the test assembly is still the Orchard application module.");

        Assert.True(
            written.Count == 0,
            Describe(
                $"No approved dependency-injection baseline existed for {written.Count} feature(s). One has been written for each.",
                "Review each file and commit it, so a later change to what the feature registers is a reviewable diff.",
                written));

        Assert.True(
            differences.Count == 0,
            Describe(
                "The services registered by these features no longer match their approved baselines.",
                "If every change is intended, replace each approved file with its received file and commit it. " +
                "A change in position matters as much as a change in type: resolving IEnumerable yields " +
                "registrations in order, and the last non-TryAdd registration of a service wins.",
                differences));
    }

    /// <summary>
    /// Gets the directory the baselines live in, from the repository rather than the output folder so
    /// a newly written baseline lands somewhere it can be committed.
    /// </summary>
    /// <returns>The baseline directory.</returns>
    private static string GetBaselineDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "The repository root could not be located from the test output directory.");

        return Path.Combine(
            directory.FullName,
            "tests",
            "CrestApps.OrchardCore.ContactCenter.FeatureActivationTests",
            "DependencyInjection",
            "Baselines");
    }

    /// <summary>
    /// Gets the baseline path for a feature.
    /// </summary>
    /// <param name="featureId">The feature id.</param>
    /// <returns>The baseline path.</returns>
    private static string GetBaselinePath(string featureId)
        => Path.Combine(GetBaselineDirectory(), $"{featureId}.approved.txt");

    /// <summary>
    /// Normalizes line endings so a baseline compares equal across platforms.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns>The normalized text.</returns>
    private static string Normalize(string value)
        => value.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');

    /// <summary>
    /// Names the first line that differs, so a failure says what changed rather than that something did.
    /// </summary>
    /// <param name="approved">The approved text.</param>
    /// <param name="actual">The captured text.</param>
    /// <returns>A description of the first difference.</returns>
    private static string DescribeFirstDifference(string approved, string actual)
    {
        var approvedLines = Normalize(approved).Split('\n');
        var actualLines = Normalize(actual).Split('\n');

        for (var index = 0; index < Math.Max(approvedLines.Length, actualLines.Length); index++)
        {
            var approvedLine = index < approvedLines.Length ? approvedLines[index] : "<end of file>";
            var actualLine = index < actualLines.Length ? actualLines[index] : "<end of file>";

            if (!string.Equals(approvedLine, actualLine, StringComparison.Ordinal))
            {
                return $"line {index + 1} was '{approvedLine}' and is now '{actualLine}' " +
                    $"({approvedLines.Length} registrations approved, {actualLines.Length} now).";
            }
        }

        return "the files differ only in trailing whitespace.";
    }

    /// <summary>
    /// Builds an assertion message that names every item and explains how to resolve it.
    /// </summary>
    /// <param name="summary">What the gate found.</param>
    /// <param name="remedy">How to resolve it.</param>
    /// <param name="items">The individual items.</param>
    /// <returns>The assertion message.</returns>
    private static string Describe(string summary, string remedy, IEnumerable<string> items)
    {
        var message = new StringBuilder(summary).AppendLine().AppendLine();

        foreach (var item in items)
        {
            message.Append("  - ").AppendLine(item);
        }

        return message.AppendLine().Append(remedy).ToString();
    }

    /// <summary>
    /// Gets the tenant profiles the snapshot covers: every feature on its own, plus the supported
    /// combinations.
    /// </summary>
    /// <remarks>
    /// Six Contact Center features are declared <c>EnabledByDependencyOnly</c>, so enabling one of
    /// them on its own does nothing and its solo snapshot records none of its registrations. The
    /// supported combinations are included so those features are covered by something that actually
    /// activates them.
    /// </remarks>
    /// <returns>The profiles to snapshot.</returns>
    private static async Task<IReadOnlyList<ContactCenterTenantProfile>> GetProfilesAsync()
    {
        var profiles = GetContactCenterFeatureIds()
            .Select(featureId => new ContactCenterTenantProfile
            {
                Id = featureId,
                ProviderProfile = "none",
                Features = [featureId],
            })
            .ToList();

        var matrix = await ContactCenterSupportMatrix.LoadAsync();

        profiles.AddRange(matrix.TenantProfiles);

        return profiles;
    }

    /// <summary>
    /// Gets every Contact Center feature id, by reflection so a new feature is covered automatically.
    /// </summary>
    /// <returns>The feature ids.</returns>
    private static string[] GetContactCenterFeatureIds()
        => typeof(ContactCenterFeatures)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue())
            .Where(featureId => !string.IsNullOrEmpty(featureId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(featureId => featureId, StringComparer.Ordinal)
            .ToArray();
}
