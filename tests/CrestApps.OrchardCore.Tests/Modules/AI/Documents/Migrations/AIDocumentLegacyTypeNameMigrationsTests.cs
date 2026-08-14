using System.Reflection;
using CrestApps.OrchardCore.AI.Documents;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Documents.Migrations;

public sealed class AIDocumentLegacyTypeNameMigrationsTests
{
    [Theory]
    [InlineData(
        "CrestApps.OrchardCore.AI.Models.AIDocument, CrestApps.OrchardCore.AI.Abstractions",
        "CrestApps.Core.AI.Models.AIDocument, CrestApps.Core.AI.Abstractions")]
    [InlineData(
        "CrestApps.OrchardCore.AI.Models.AIDocumentChunk, CrestApps.OrchardCore.AI.Abstractions",
        "CrestApps.Core.AI.Models.AIDocumentChunk, CrestApps.Core.AI.Abstractions")]
    [InlineData(
        "CrestApps.AI.Models.AIDocument, CrestApps.AI.Abstractions",
        "CrestApps.Core.AI.Models.AIDocument, CrestApps.Core.AI.Abstractions")]
    public void RewriteLegacyTypeName_WhenLegacyTypeIsProvided_ShouldReturnCurrentCoreTypeName(
        string typeName,
        string expected)
    {
        // Act
        var result = InvokeRewriteLegacyTypeName(typeName);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void RewriteLegacyTypeName_WhenTypeNameIsCurrent_ShouldLeaveItUnchanged()
    {
        // Arrange
        const string typeName = "CrestApps.Core.AI.Models.AIDocument, CrestApps.Core.AI.Abstractions";

        // Act
        var result = InvokeRewriteLegacyTypeName(typeName);

        // Assert
        Assert.Equal(typeName, result);
    }

    private static string InvokeRewriteLegacyTypeName(string typeName)
    {
        var method = typeof(Startup).Assembly
            .GetType("CrestApps.OrchardCore.AI.Documents.Migrations.AIDocumentLegacyTypeNameMigrations", throwOnError: true)!
            .GetMethod("RewriteLegacyTypeName", BindingFlags.NonPublic | BindingFlags.Static)!;

        return (string)method.Invoke(null, [typeName])!;
    }
}
