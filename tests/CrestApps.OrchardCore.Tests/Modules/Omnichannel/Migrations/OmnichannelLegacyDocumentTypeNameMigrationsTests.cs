using CrestApps.Core.Data.YesSql.Omnichannel.Indexes;
using System.Reflection;
using CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Migrations;

/// <summary>
/// An omnichannel document records the CLR type it was written as. Eight stored types moved into the
/// framework assemblies, so every activity, batch, campaign, disposition, cadence, subject action and channel
/// endpoint already in a tenant depends on this rewrite being exactly right: a rule that misses leaves the
/// rows unreadable, and a rule that over-matches rewrites them to a type that does not exist.
/// </summary>
public sealed class OmnichannelLegacyDocumentTypeNameMigrationsTests
{
    /// <summary>
    /// Every stored type that moved, in the shape YesSql recorded it.
    /// </summary>
    /// <param name="typeName">The recorded type name.</param>
    /// <param name="expected">The name it must be rewritten to.</param>
    [Theory]
    [InlineData(
        "CrestApps.OrchardCore.Omnichannel.Core.Models.OmnichannelActivity, CrestApps.OrchardCore.Omnichannel.Core",
        "CrestApps.Core.Omnichannel.Models.OmnichannelActivity, CrestApps.Core.Omnichannel.Abstractions")]
    [InlineData(
        "CrestApps.OrchardCore.Omnichannel.Core.Models.OmnichannelActivityBatch, CrestApps.OrchardCore.Omnichannel.Core",
        "CrestApps.Core.Omnichannel.Models.OmnichannelActivityBatch, CrestApps.Core.Omnichannel.Abstractions")]
    [InlineData(
        "CrestApps.OrchardCore.Omnichannel.Core.Models.OmnichannelCampaign, CrestApps.OrchardCore.Omnichannel.Core",
        "CrestApps.Core.Omnichannel.Models.OmnichannelCampaign, CrestApps.Core.Omnichannel.Abstractions")]
    [InlineData(
        "CrestApps.OrchardCore.Omnichannel.Core.Models.OmnichannelCampaignGroup, CrestApps.OrchardCore.Omnichannel.Core",
        "CrestApps.Core.Omnichannel.Models.OmnichannelCampaignGroup, CrestApps.Core.Omnichannel.Abstractions")]
    [InlineData(
        "CrestApps.OrchardCore.Omnichannel.Core.Models.OmnichannelDisposition, CrestApps.OrchardCore.Omnichannel.Core",
        "CrestApps.Core.Omnichannel.Models.OmnichannelDisposition, CrestApps.Core.Omnichannel.Abstractions")]
    [InlineData(
        "CrestApps.OrchardCore.Omnichannel.Core.Models.OmnichannelChannelEndpoint, CrestApps.OrchardCore.Omnichannel.Core",
        "CrestApps.Core.Omnichannel.Models.OmnichannelChannelEndpoint, CrestApps.Core.Omnichannel.Abstractions")]
    [InlineData(
        "CrestApps.OrchardCore.Omnichannel.Core.Models.SubjectAction, CrestApps.OrchardCore.Omnichannel.Core",
        "CrestApps.Core.Omnichannel.Models.SubjectAction, CrestApps.Core.Omnichannel.Abstractions")]
    [InlineData(
        "CrestApps.OrchardCore.Omnichannel.Core.Models.Cadence, CrestApps.OrchardCore.Omnichannel.Core",
        "CrestApps.Core.Omnichannel.Models.Cadence, CrestApps.Core.Omnichannel.Abstractions")]
    public void RewriteLegacyTypeName_WhenLegacyTypeIsProvided_ReturnsTheCurrentTypeName(string typeName, string expected)
    {
        // Act
        var result = InvokeRewriteLegacyTypeName(typeName);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Pins that the assembly is matched exactly rather than by prefix.
    /// </summary>
    /// <remarks>
    /// <c>CrestApps.OrchardCore.Omnichannel</c> is a prefix of <c>CrestApps.OrchardCore.Omnichannel.Core</c>.
    /// A rule matching the assembly loosely would rewrite documents written by the module assembly to a
    /// namespace they were never in.
    /// </remarks>
    /// <param name="typeName">A recorded type name no rule should touch.</param>
    [Theory]
    [InlineData("CrestApps.OrchardCore.Omnichannel.Models.OmnichannelMessage, CrestApps.OrchardCore.Omnichannel")]
    [InlineData("CrestApps.OrchardCore.Omnichannel.Core.Models.OmnichannelActivity, CrestApps.OrchardCore.Omnichannel.Core.Extras")]
    [InlineData("CrestApps.Core.Omnichannel.Models.OmnichannelActivity, CrestApps.Core.Omnichannel.Abstractions")]
    public void RewriteLegacyTypeName_LeavesEverythingElseAlone(string typeName)
    {
        // Act
        var result = InvokeRewriteLegacyTypeName(typeName);

        // Assert
        Assert.Equal(typeName, result);
    }

    /// <summary>
    /// Pins that every stored omnichannel catalog type is covered by a rule.
    /// </summary>
    /// <remarks>
    /// A type that moved and is not in the table above is a tenant losing those records on upgrade, and
    /// nothing else in the suite would report it. Listing them here is what makes adding the ninth one a
    /// decision rather than an omission.
    /// </remarks>
    [Fact]
    public void EveryStoredTypeThatMoved_IsRewritten()
    {
        // Arrange
        string[] movedTypeNames =
        [
            "OmnichannelActivity",
            "OmnichannelActivityBatch",
            "OmnichannelCampaign",
            "OmnichannelCampaignGroup",
            "OmnichannelDisposition",
            "OmnichannelChannelEndpoint",
            "SubjectAction",
            "Cadence",
        ];

        // Act & Assert
        foreach (var name in movedTypeNames)
        {
            var legacy = $"CrestApps.OrchardCore.Omnichannel.Core.Models.{name}, CrestApps.OrchardCore.Omnichannel.Core";

            Assert.Equal(
                $"CrestApps.Core.Omnichannel.Models.{name}, CrestApps.Core.Omnichannel.Abstractions",
                InvokeRewriteLegacyTypeName(legacy));
        }
    }

    private static string InvokeRewriteLegacyTypeName(string typeName)
    {
        var type = typeof(OmnichannelActivityBatchIndexMigrations).Assembly
            .GetType("CrestApps.OrchardCore.Omnichannel.Managements.Migrations.OmnichannelLegacyDocumentTypeNameMigrations", throwOnError: true);

        var method = type.GetMethod(
            "RewriteLegacyTypeName",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        return (string)method.Invoke(null, [typeName]);
    }
}
