using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Moq;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

/// <summary>
/// Verifies that <see cref="SubjectFlowSettingsService"/> composes the AI handoff settings from the subject part
/// definition into the flow settings the runtime reads. A field-name mismatch here would silently disable handoff.
/// </summary>
public sealed class SubjectFlowSettingsHandoffCompositionTests
{
    [Fact]
    public async Task FindConfiguredFlowSettingsAsync_CopiesHandoffSettings()
    {
        var typeDefinition = new ContentTypeDefinitionBuilder()
            .WithName("Lead")
            .WithPart(OmnichannelConstants.ContentParts.OmnichannelSubject, partBuilder => partBuilder
                .WithSettings(new OmnichannelSubjectPartSettings
                {
                    Direction = SubjectDirection.Inbound,
                    Channel = OmnichannelConstants.Channels.Sms,
                    InteractionType = ActivityInteractionType.Automated,
                })
                .WithSettings(new OmnichannelSubjectAISettings
                {
                    ProfileId = "profile-1",
                    EnableAgentHandoff = true,
                    HandoffQueueId = "queue-9",
                    HandoffOnUserRequest = true,
                    HandoffOnQualifiedLead = false,
                    HandoffOnFrustration = true,
                }))
            .Build();

        var contentDefinitionManager = new Mock<IContentDefinitionManager>();
        contentDefinitionManager
            .Setup(manager => manager.GetTypeDefinitionAsync("Lead"))
            .ReturnsAsync(typeDefinition);

        var service = new SubjectFlowSettingsService(contentDefinitionManager.Object);

        var flow = await service.FindConfiguredFlowSettingsAsync("Lead", TestContext.Current.CancellationToken);

        Assert.NotNull(flow);
        Assert.True(flow.EnableAgentHandoff);
        Assert.Equal("queue-9", flow.HandoffQueueId);
        Assert.True(flow.HandoffOnUserRequest);
        Assert.False(flow.HandoffOnQualifiedLead);
        Assert.True(flow.HandoffOnFrustration);
    }
}
