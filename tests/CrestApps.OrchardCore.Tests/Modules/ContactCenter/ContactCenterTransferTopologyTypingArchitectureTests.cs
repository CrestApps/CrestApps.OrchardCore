using System.Reflection;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Pins the transfer/topology typing invariant behind plan item B5: the live call topology exposes typed transfer
/// targets, the retired <c>MediaTopologyId</c> placeholder stays gone from production code, and the only free-form
/// transfer strings are the deliberately historical audit fields on <see cref="InteractionTransferHistoryEntry"/>.
/// </summary>
public sealed class ContactCenterTransferTopologyTypingArchitectureTests
{
    private static readonly Type[] _liveTopologyTypes =
    [
        typeof(CallSession),
        typeof(ConsultCall),
        typeof(CallLeg),
        typeof(Bridge),
        typeof(CallRelationship),
        typeof(MonitorSession),
    ];

    [Fact]
    public void LiveConsultTopology_ExposesTheTypedTransferTargetType()
    {
        // Arrange
        var property = typeof(ConsultCall).GetProperty(nameof(ConsultCall.TargetType));

        // Assert
        Assert.NotNull(property);
        Assert.Equal(typeof(InteractionTransferTargetType), property.PropertyType);
    }

    [Fact]
    public void HistoricalTransferStrings_AreConfinedToTheHistoryEntry()
    {
        // Assert - the audit snapshot fields are strings by design.
        Assert.Equal(
            typeof(string),
            typeof(InteractionTransferHistoryEntry).GetProperty(nameof(InteractionTransferHistoryEntry.TargetType)).PropertyType);
        Assert.Equal(
            typeof(string),
            typeof(InteractionTransferHistoryEntry).GetProperty(nameof(InteractionTransferHistoryEntry.Result)).PropertyType);

        // No live topology model may reintroduce a string target-type or the retired media-topology placeholder.
        foreach (var type in _liveTopologyTypes)
        {
            var offending = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property =>
                    property.Name.Equals("MediaTopologyId", StringComparison.Ordinal) ||
                    (property.Name.Equals("TargetType", StringComparison.Ordinal) && property.PropertyType == typeof(string)))
                .Select(property => $"{type.Name}.{property.Name}")
                .ToArray();

            Assert.Empty(offending);
        }
    }

    [Fact]
    public void MediaTopologyId_IsAbsentFromProductionSource()
    {
        // Arrange
        var sourceRoot = Path.Combine(FindRepositoryRoot(), "src");

        // Act
        var offendingFiles = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file =>
            {
                var directory = Path.GetDirectoryName(file) ?? string.Empty;

                return !directory.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                    !directory.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
            })
            .Where(file => File.ReadAllText(file).Contains("MediaTopologyId", StringComparison.Ordinal))
            .ToArray();

        // Assert
        Assert.Empty(offendingFiles);
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
