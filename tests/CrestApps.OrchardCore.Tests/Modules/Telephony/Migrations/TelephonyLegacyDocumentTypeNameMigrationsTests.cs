using System.Reflection;
using CrestApps.OrchardCore.Telephony;

namespace CrestApps.OrchardCore.Tests.Modules.Telephony.Migrations;

/// <summary>
/// A telephony document records the CLR type it was written as. The extraction changed those names, so every
/// call already in a tenant's history depends on this rewrite being exactly right: a rule that misses leaves
/// the rows unreadable, and a rule that over-matches rewrites them to a type that does not exist.
/// </summary>
public sealed class TelephonyLegacyDocumentTypeNameMigrationsTests
{
    [Theory]
    [InlineData(
        "CrestApps.OrchardCore.Telephony.Models.TelephonyInteraction, CrestApps.OrchardCore.Telephony.Abstractions",
        "CrestApps.Core.Telephony.Models.TelephonyInteraction, CrestApps.Core.Telephony.Abstractions")]
    [InlineData(
        "CrestApps.OrchardCore.Telephony.Core.Models.TelephonyExtension, CrestApps.OrchardCore.Telephony.Core",
        "CrestApps.Core.Telephony.Models.TelephonyExtension, CrestApps.Core.Telephony")]
    public void RewriteLegacyTypeName_WhenLegacyTypeIsProvided_ReturnsTheCurrentTypeName(string typeName, string expected)
    {
        // Act
        var result = InvokeRewriteLegacyTypeName(typeName);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void RewriteLegacyTypeName_MatchesTheAssemblyExactly_NotByPrefix()
    {
        // Arrange
        // The three legacy assembly names are prefixes of one another. A rule that matched the assembly
        // loosely would rewrite an Abstractions document to the wrong assembly and lose it.
        const string typeName = "CrestApps.OrchardCore.Telephony.Models.TelephonyInteraction, CrestApps.OrchardCore.Telephony.Abstractions";

        // Act
        var result = InvokeRewriteLegacyTypeName(typeName);

        // Assert
        Assert.EndsWith(", CrestApps.Core.Telephony.Abstractions", result, StringComparison.Ordinal);
    }

    [Fact]
    public void RewriteLegacyTypeName_WhenTypeNameIsCurrent_LeavesItUnchanged()
    {
        // Arrange
        const string typeName = "CrestApps.Core.Telephony.Models.TelephonyInteraction, CrestApps.Core.Telephony.Abstractions";

        // Act
        var result = InvokeRewriteLegacyTypeName(typeName);

        // Assert
        Assert.Equal(typeName, result);
    }

    private static string InvokeRewriteLegacyTypeName(string typeName)
    {
        var method = typeof(Startup).Assembly
            .GetType("CrestApps.OrchardCore.Telephony.Migrations.TelephonyLegacyDocumentTypeNameMigrations", throwOnError: true)!
            .GetMethod("RewriteLegacyTypeName", BindingFlags.NonPublic | BindingFlags.Static)!;

        return (string)method.Invoke(null, [typeName])!;
    }
}
