using System.Reflection;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Migrations;

/// <summary>
/// Covers the rule that credits a legacy chat or utility purpose with the capabilities it stood for.
/// </summary>
public sealed class LegacyAIDeploymentCapabilitiesTests
{
    [Theory]
    [InlineData("Chat")]
    [InlineData("Utility")]
    [InlineData("Chat, Utility")]
    [InlineData("3")]
    public void GetImpliedFeatures_ForAnInteractivePurpose_ShouldCreditTextGenerationToolCallingAndStreaming(string storedPurpose)
    {
        // Act
        var features = LegacyAIDeploymentCapabilities.GetImpliedFeatures([storedPurpose]);

        // Assert
        Assert.Equal(
            [
                AIDeploymentFeatureNames.TextGeneration,
                AIDeploymentFeatureNames.ToolCalling,
                AIDeploymentFeatureNames.Streaming,
            ],
            features.OrderBy(NameOrder).ToArray());
    }

    [Theory]
    [InlineData("Embedding", AIDeploymentFeatureNames.TextEmbedding)]
    [InlineData("Image", AIDeploymentFeatureNames.ImageOutput)]
    [InlineData("Vision", AIDeploymentFeatureNames.ImageInput)]
    [InlineData("SpeechToText", AIDeploymentFeatureNames.SpeechToText)]
    [InlineData("TextToSpeech", AIDeploymentFeatureNames.TextToSpeech)]
    public void GetImpliedFeatures_ForANonInteractivePurpose_ShouldCreditOnlyThatCapability(string storedPurpose, string expectedFeature)
    {
        // Act
        var features = LegacyAIDeploymentCapabilities.GetImpliedFeatures([storedPurpose]);

        // Assert
        Assert.Equal([expectedFeature], features);
    }

    [Fact]
    public void GetImpliedFeatures_WhenTextGenerationIsAlreadyDeclared_ShouldStillAddTheTwoTheFrameworkCannotInfer()
    {
        // Arrange
        // The framework returns nothing here, because the one capability it maps chat onto is already there.
        var declared = new[] { AIDeploymentFeatureNames.TextGeneration };

        // Act
        var features = LegacyAIDeploymentCapabilities.GetImpliedFeatures(["Chat"], declared);

        // Assert
        Assert.Equal(
            [AIDeploymentFeatureNames.ToolCalling, AIDeploymentFeatureNames.Streaming],
            features.OrderBy(NameOrder).ToArray());
    }

    [Fact]
    public void GetImpliedFeatures_ForARealtimeDeployment_ShouldCreditNothing()
    {
        // Arrange
        // A speech-to-speech model was stored under the chat purpose. It answers a text completion with an
        // HTTP 400, which is why the framework withholds text generation; the rest is withheld with it.
        var declared = new[] { AIDeploymentFeatureNames.Realtime };

        // Act
        var features = LegacyAIDeploymentCapabilities.GetImpliedFeatures(["Chat"], declared);

        // Assert
        Assert.Empty(features);
    }

    [Fact]
    public void GetImpliedFeatures_WhenNoPurposeIsNamed_ShouldCreditNothing()
    {
        Assert.Empty(LegacyAIDeploymentCapabilities.GetImpliedFeatures([]));
        Assert.Empty(LegacyAIDeploymentCapabilities.GetImpliedFeatures(["None"]));
        Assert.Empty(LegacyAIDeploymentCapabilities.GetImpliedFeatures(["Bogus"]));
    }

    [Fact]
    public void Normalize_ForAStoredChatDeployment_ShouldDeclareTheFullSetAndBeIdempotent()
    {
        // Arrange
        // The shape a deployment document written before capabilities existed still has on disk. Reading it
        // runs the framework's projection, which credits text generation and clears the legacy purpose.
        var storedJson = """
        {
            "ItemId": "dep-1",
            "Name": "legacy-chat",
            "ModelName": "gpt-4.1-mini",
            "ClientName": "OpenAI",
            "ConnectionName": "default"
        }
        """;

        var deployment = JsonSerializer.Deserialize<AIDeployment>(storedJson);

        // Act
        var changed = LegacyAIDeploymentCapabilities.Normalize(deployment, ["Chat"]);

        // Assert
        Assert.True(changed);
        Assert.True(deployment.TryGet<AIDeploymentMetadata>(out var metadata));
        Assert.True(metadata.SupportsFeature(AIDeploymentFeatureNames.TextGeneration));
        Assert.True(metadata.SupportsFeature(AIDeploymentFeatureNames.ToolCalling));
        Assert.True(metadata.SupportsFeature(AIDeploymentFeatureNames.Streaming));

        // Running again must not duplicate what is already declared.
        LegacyAIDeploymentCapabilities.Normalize(deployment, ["Chat"]);

        Assert.True(deployment.TryGet<AIDeploymentMetadata>(out var reread));
        Assert.Equal(3, reread.Features.Length);
        Assert.Equal(3, reread.Features.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Normalize_ShouldNotGrantInteractiveCapabilitiesToAnEmbeddingDeployment()
    {
        // Arrange
        var deployment = new AIDeployment
        {
            ItemId = "dep-1",
            Name = "legacy-embedding",
        };

        // Act
        LegacyAIDeploymentCapabilities.Normalize(deployment, ["Embedding"]);

        // Assert
        Assert.True(deployment.TryGet<AIDeploymentMetadata>(out var metadata));
        Assert.True(metadata.SupportsFeature(AIDeploymentFeatureNames.TextEmbedding));
        Assert.False(metadata.SupportsFeature(AIDeploymentFeatureNames.TextGeneration));
        Assert.False(metadata.SupportsFeature(AIDeploymentFeatureNames.ToolCalling));
        Assert.False(metadata.SupportsFeature(AIDeploymentFeatureNames.Streaming));
    }

    [Fact]
    public void ApplyTo_ForAChatPurpose_ShouldWriteTheFullSetOntoTheDeployment()
    {
        // Arrange
        // This is what the store migrations call for every record whose stored JSON named a purpose.
        var deployment = new AIDeployment
        {
            ItemId = "dep-1",
            Name = "legacy-chat",
        };

        // Act
        var changed = InvokeApplyTo(LegacyDeploymentPurposes.Chat, deployment);

        // Assert
        Assert.True(changed);
        Assert.True(deployment.TryGet<AIDeploymentMetadata>(out var metadata));
        Assert.Equal(
            [
                AIDeploymentFeatureNames.Streaming,
                AIDeploymentFeatureNames.TextGeneration,
                AIDeploymentFeatureNames.ToolCalling,
            ],
            metadata.Features.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void ApplyTo_ForAChatDeploymentThatAlreadyDeclaresTextGeneration_ShouldAddTheOtherTwoWithoutDuplicating()
    {
        // Arrange
        // The state every record is in by the time the store migration sees it: the framework's read-time
        // projection has already run and credited text generation.
        var deployment = new AIDeployment
        {
            ItemId = "dep-1",
            Name = "legacy-chat",
        }.Declaring(AIDeploymentFeatureNames.TextGeneration);

        // Act
        var changed = InvokeApplyTo(LegacyDeploymentPurposes.Chat | LegacyDeploymentPurposes.Utility, deployment);

        // Assert
        Assert.True(changed);
        Assert.True(deployment.TryGet<AIDeploymentMetadata>(out var metadata));
        Assert.Equal(
            [
                AIDeploymentFeatureNames.Streaming,
                AIDeploymentFeatureNames.TextGeneration,
                AIDeploymentFeatureNames.ToolCalling,
            ],
            metadata.Features.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void IsSupportedBy_ShouldStillMatchADeploymentThatDeclaresOnlyTextGeneration()
    {
        // Arrange
        // Widening what a purpose is written down as must not widen what it takes to satisfy one: the
        // default deployment backfill looks a chat deployment up this way, and every deployment already in
        // the store declares text generation alone.
        var deployment = new AIDeployment
        {
            ItemId = "dep-1",
            Name = "chat",
        }.Declaring(AIDeploymentFeatureNames.TextGeneration);

        // Act & Assert
        Assert.True(InvokeIsSupportedBy(LegacyDeploymentPurposes.Chat, deployment));
        Assert.True(InvokeIsSupportedBy(LegacyDeploymentPurposes.Utility, deployment));
        Assert.False(InvokeIsSupportedBy(LegacyDeploymentPurposes.Embedding, deployment));
    }

    [Fact]
    public void GetDeploymentDocumentTypePrefix_ShouldMatchTheTypeNameTheStoreWrites()
    {
        // Arrange
        // The migration finds the deployment document by matching this prefix against the document table's
        // Type column. A mismatch would not fail anything -- it would just find no purposes and quietly
        // migrate every chat deployment with text generation alone, so the shape is pinned here.
        // The observed column value is the type's full name followed by its own assembly's simple name,
        // with the generic argument still carrying its version, e.g.
        // "CrestApps.OrchardCore.Models.DictionaryDocument`1[[...AIDeployment, ...Abstractions,
        //  Version=3.0.0.0, Culture=neutral, PublicKeyToken=null]], CrestApps.OrchardCore.Abstractions".
        var documentType = typeof(DictionaryDocument<AIDeployment>);
        var storedTypeName = $"{documentType.FullName}, {documentType.Assembly.GetName().Name}";

        // Act
        var prefix = InvokeGetDeploymentDocumentTypePrefix();

        // Assert
        Assert.StartsWith(prefix, storedTypeName, StringComparison.Ordinal);
        Assert.DoesNotContain(", Version=", prefix, StringComparison.Ordinal);
        Assert.Contains(typeof(AIDeployment).FullName, prefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Orders the feature names the way the projection emits them, so a comparison reads in that order.
    /// </summary>
    private static int NameOrder(string feature)
        => Array.IndexOf(LegacyAIDeploymentCapabilities.InteractiveFeatures, feature);

    private static string InvokeGetDeploymentDocumentTypePrefix()
    {
        var method = typeof(Startup).Assembly
            .GetType("CrestApps.OrchardCore.AI.Migrations.AIDeploymentTypeMigrations", throwOnError: true)!
            .GetMethod("GetDeploymentDocumentTypePrefix", BindingFlags.NonPublic | BindingFlags.Static)!;

        return (string)method.Invoke(null, null)!;
    }

    private static Type GetExtensionsType()
        => typeof(Startup).Assembly.GetType(
            "CrestApps.OrchardCore.AI.Migrations.LegacyAIDeploymentPurposeExtensions",
            throwOnError: true)!;

    private static bool InvokeApplyTo(int purposeFlags, AIDeployment deployment)
    {
        var method = GetExtensionsType().GetMethod("ApplyTo", BindingFlags.Public | BindingFlags.Static)!;

        return (bool)method.Invoke(null, [LegacyDeploymentPurposes.Box(purposeFlags), deployment])!;
    }

    private static bool InvokeIsSupportedBy(int purposeFlags, AIDeployment deployment)
    {
        var method = GetExtensionsType().GetMethod("IsSupportedBy", BindingFlags.Public | BindingFlags.Static)!;

        return (bool)method.Invoke(null, [LegacyDeploymentPurposes.Box(purposeFlags), deployment])!;
    }
}
