using System.Reflection;
using CrestApps.OrchardCore.ContactCenter.Migrations;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Migrations;

/// <summary>
/// A Contact Center document records the CLR type it was written as. Twenty-five stored types moved into the
/// framework assemblies, so a tenant's queues, agents, sessions, interactions, outbox and metrics all depend
/// on this rewrite being exactly right.
/// </summary>
public sealed class ContactCenterLegacyDocumentTypeNameMigrationsTests
{
    /// <summary>
    /// Every stored Contact Center type that moved.
    /// </summary>
    /// <remarks>
    /// Named individually rather than tested once through the rule, so a twenty-sixth type moving without a
    /// rule is a failing test rather than a tenant losing those records.
    /// </remarks>
    /// <returns>The type names.</returns>
    public static TheoryData<string> StoredTypesThatMoved()
    {
        var data = new TheoryData<string>();

        foreach (var name in new[]
        {
            "ActivityQueue", "ActivityQueueGroup", "ActivityReservation", "AgentProfile", "AgentSession",
            "AgentStateReasonCode", "BusinessHoursCalendar", "CallSession", "CallbackRequest",
            "ContactCenterEntryPoint", "ContactCenterEventMetric", "ContactCenterEventMetricDelta",
            "ContactCenterOutboxMessage", "ContactCenterProcessedEvent", "ContactCenterProjectionCheckpoint",
            "ContactCenterSkill", "ContactCenterWorkState", "DialerProfile", "Interaction", "InteractionEvent",
            "ProviderCommand", "ProviderWebhookInboxMessage", "QueueItem", "SecureCaptureSession",
            "VoiceMediaItem",
        })
        {
            data.Add(name);
        }

        return data;
    }

    /// <summary>
    /// Pins that every stored type that moved is rewritten.
    /// </summary>
    /// <param name="typeName">The simple type name.</param>
    [Theory]
    [MemberData(nameof(StoredTypesThatMoved))]
    public void RewriteLegacyTypeName_RewritesEveryStoredTypeThatMoved(string typeName)
    {
        // Arrange
        var legacy = $"CrestApps.OrchardCore.ContactCenter.Core.Models.{typeName}, CrestApps.OrchardCore.ContactCenter.Core";

        // Act
        var result = InvokeRewriteLegacyTypeName(legacy);

        // Assert
        Assert.Equal(
            $"CrestApps.Core.ContactCenter.Models.{typeName}, CrestApps.Core.ContactCenter.Abstractions",
            result);
    }

    /// <summary>
    /// Pins that the assembly is matched exactly rather than by prefix.
    /// </summary>
    /// <param name="typeName">A recorded type name no rule should touch.</param>
    [Theory]
    [InlineData("CrestApps.OrchardCore.ContactCenter.Models.Something, CrestApps.OrchardCore.ContactCenter")]
    [InlineData("CrestApps.OrchardCore.ContactCenter.Core.Models.QueueItem, CrestApps.OrchardCore.ContactCenter.Core.Extras")]
    [InlineData("CrestApps.Core.ContactCenter.Models.QueueItem, CrestApps.Core.ContactCenter.Abstractions")]
    public void RewriteLegacyTypeName_LeavesEverythingElseAlone(string typeName)
    {
        // Act
        var result = InvokeRewriteLegacyTypeName(typeName);

        // Assert
        Assert.Equal(typeName, result);
    }

    private static string InvokeRewriteLegacyTypeName(string typeName)
    {
        var type = typeof(ActivityQueueIndexMigrations).Assembly
            .GetType("CrestApps.OrchardCore.ContactCenter.Migrations.ContactCenterLegacyDocumentTypeNameMigrations", throwOnError: true);

        var method = type.GetMethod("RewriteLegacyTypeName", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        return (string)method.Invoke(null, [typeName]);
    }
}
