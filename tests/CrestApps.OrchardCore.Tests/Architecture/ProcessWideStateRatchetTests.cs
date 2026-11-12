using System.Text;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// CQ-1: state in a <c>static readonly Concurrent*</c> field lives in one node's memory, so a webhook that lands on
/// another node doesn't see it (SEC-P2: secure keypad digits reached the model that way). Shared state goes to the
/// distributed cache, the activity record or another store every node reads. Caches of immutable values (formats,
/// patterns, serializer metadata) are fine and recorded here; the list may only shrink.
/// </summary>
public sealed partial class ProcessWideStateRatchetTests
{
    private static readonly Dictionary<string, int> _known = new(StringComparer.OrdinalIgnoreCase)
    {
        // Caches of values that are the same on every node.
        ["src/Core/CrestApps.OrchardCore.AI.Core/Drivers/ConnectionAuthValues.cs"] = 1,
        ["src/Core/CrestApps.OrchardCore.Addresses.Core/Services/AddressPatterns.cs"] = 2,
        ["src/Core/CrestApps.OrchardCore.Core/Deployments/CatalogDeploymentSerializer.cs"] = 1,
        ["src/Core/CrestApps.OrchardCore.Core/Deployments/CatalogRecordSerializer.cs"] = 1,
        ["src/Core/CrestApps.OrchardCore.DataConnectors.Core/DataSources/ImportedDataSource.cs"] = 1,
        ["src/Core/CrestApps.OrchardCore.Products.Core/Services/ProductPriceFormatter.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Telephony/Services/TransientDatabaseErrors.cs"] = 1,

        // Live-call state still per node (CQ-1 step 3): the voicemail claim and the turn-based fillers.
        ["src/Core/CrestApps.OrchardCore.Omnichannel.Voice.Core/Services/TurnBasedToolFillers.cs"] = 1,
        ["src/Core/CrestApps.OrchardCore.Omnichannel.Voice.Core/Services/VoiceAgentConversationLoop.Voicemail.cs"] = 1,

        // Per-node by design: tenant-qualified keys, or a durable job stands behind it.
        ["src/Core/CrestApps.OrchardCore.Telnyx.Core/Services/TelnyxRecordingIngestService.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Asterisk/Services/AsteriskAriApplicationOwnershipRegistry.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Asterisk/Services/AsteriskPendingCallerTerminationRegistry.cs"] = 2,
    };

    [Fact]
    public void NoNewProcessWideConcurrentState()
    {
        var actual = Scan();
        var problems = new List<string>();

        foreach (var (file, count) in actual)
        {
            var allowed = _known.TryGetValue(file, out var known) ? known : 0;

            if (count > allowed)
            {
                problems.Add($"{file} has {count} static readonly Concurrent* field(s), {allowed} allowed. Keep state every node must see in the distributed cache or a store.");
            }
        }

        foreach (var (file, known) in _known)
        {
            var count = actual.TryGetValue(file, out var value) ? value : 0;

            if (count < known)
            {
                problems.Add($"{file} now has {count}; lower or remove its entry so the gain is locked in.");
            }
        }

        Assert.True(problems.Count == 0, new StringBuilder().AppendLine().AppendJoin(Environment.NewLine, problems.Order(StringComparer.Ordinal)).ToString());
    }

    private static Dictionary<string, int> Scan()
    {
        var root = FindRepositoryRoot();
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in SourceTreeFiles.Enumerate(Path.Combine(root, "src"), "*.cs", directory => SourceTreeFiles.IsBuildOutputDirectory(directory) || Path.GetFileName(directory) is "node_modules"))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');

            if (relative.Contains("/Telemetry/", StringComparison.OrdinalIgnoreCase) || relative.Contains("/Diagnostics/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var count = ConcurrentField().Count(File.ReadAllText(file));

            if (count > 0)
            {
                result[relative] = count;
            }
        }

        return result;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests", "CrestApps.OrchardCore.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The repository root could not be found.");
    }

    [GeneratedRegex(@"static\s+readonly\s+Concurrent\w+", RegexOptions.CultureInvariant)]
    private static partial Regex ConcurrentField();
}
