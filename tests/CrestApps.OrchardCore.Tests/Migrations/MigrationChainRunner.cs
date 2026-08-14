using System.Reflection;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Tests.Migrations;

/// <summary>
/// Walks a data migration's upgrade chain the way the host walks it, so a test observes the same sequence of
/// steps a real tenant would run rather than a sequence the test author chose.
/// </summary>
/// <remarks>
/// The step a tenant actually runs is decided by the version the previous step returned, not by the order the
/// steps appear in the file. A test that calls the steps in source order can therefore pass while a real tenant
/// skips a step entirely, which is precisely the failure this helper exists to make visible.
/// </remarks>
internal static class MigrationChainRunner
{
    /// <summary>
    /// Runs every upgrade step reachable from the specified version and returns the version the chain settles on.
    /// </summary>
    /// <param name="migration">The migration whose upgrade chain is walked.</param>
    /// <param name="fromVersion">The version the tenant is currently at.</param>
    /// <returns>The version reached once no further step exists.</returns>
    public static async Task<int> RunUpgradeChainAsync(DataMigration migration, int fromVersion)
    {
        ArgumentNullException.ThrowIfNull(migration);

        var current = fromVersion;
        var visited = new HashSet<int>();

        while (true)
        {
            if (!visited.Add(current))
            {
                throw new InvalidOperationException(
                    $"The upgrade chain for '{migration.GetType().Name}' revisits version {current}, so it never terminates.");
            }

            var method = FindStep(migration.GetType(), current);

            if (method is null)
            {
                return current;
            }

            var result = method.Invoke(method.IsStatic ? null : migration, []);

            current = result switch
            {
                Task<int> task => await task,
                int version => version,
                _ => throw new InvalidOperationException(
                    $"The upgrade step '{method.Name}' on '{migration.GetType().Name}' does not return a version."),
            };
        }
    }

    private static MethodInfo FindStep(Type migrationType, int version)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

        return migrationType.GetMethod($"UpdateFrom{version}Async", Flags, [])
            ?? migrationType.GetMethod($"UpdateFrom{version}", Flags, []);
    }
}
