using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Handlers;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;

namespace CrestApps.OrchardCore.Tests.Validation;

/// <summary>
/// Pins the subject flow rules to the handler so a recipe writing a flow is held to the same set as the editor.
/// </summary>
public class SubjectFlowSettingsHandlerValidationTests
{
    [Fact]
    public async Task ValidatingAsync_WhenTheFlowIsWellFormed_Succeeds()
    {
        // Arrange
        var context = new ValidatingContext<SubjectFlowSettings>(CreateValidFlow());

        // Act
        await CreateHandler().ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidatingAsync_WhenTheSubjectIsMissing_Fails(string subjectContentType)
    {
        // Arrange
        var flow = CreateValidFlow();
        flow.SubjectContentType = subjectContentType;

        // Act
        var context = await ValidateAsync(flow);

        // Assert
        AssertFailedFor(context, nameof(SubjectFlowSettings.SubjectContentType));
    }

    [Fact]
    public async Task ValidatingAsync_WhenTheCampaignIsMissing_Fails()
    {
        // Arrange
        var flow = CreateValidFlow();
        flow.CampaignId = null;

        // Act
        var context = await ValidateAsync(flow);

        // Assert
        AssertFailedFor(context, nameof(SubjectFlowSettings.CampaignId));
    }

    [Fact]
    public async Task ValidatingAsync_WhenTheChannelIsMissing_Fails()
    {
        // Arrange
        var flow = CreateValidFlow();
        flow.Channel = null;

        // Act
        var context = await ValidateAsync(flow);

        // Assert
        AssertFailedFor(context, nameof(SubjectFlowSettings.Channel));
    }

    [Fact]
    public async Task ValidatingAsync_WhenAnAutomatedFlowNamesNoChannelEndpoint_Fails()
    {
        // Arrange
        var flow = CreateValidFlow();
        flow.InteractionType = ActivityInteractionType.Automated;
        flow.ChannelEndpointId = null;

        // Act
        var context = await ValidateAsync(flow);

        // Assert
        AssertFailedFor(context, nameof(SubjectFlowSettings.ChannelEndpointId));
    }

    [Fact]
    public async Task ValidatingAsync_WhenAManualFlowNamesNoChannelEndpoint_Succeeds()
    {
        // Arrange
        var flow = CreateValidFlow();
        flow.InteractionType = ActivityInteractionType.Manual;
        flow.ChannelEndpointId = null;

        // Act
        var context = await ValidateAsync(flow);

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    private static async Task<ValidatingContext<SubjectFlowSettings>> ValidateAsync(SubjectFlowSettings flow)
    {
        var context = new ValidatingContext<SubjectFlowSettings>(flow);

        await CreateHandler().ValidatingAsync(context, TestContext.Current.CancellationToken);

        return context;
    }

    private static void AssertFailedFor(ValidatingContext<SubjectFlowSettings> context, string memberName)
    {
        Assert.False(context.Result.Succeeded);
        Assert.Contains(context.Result.Errors, error => error.MemberNames.Contains(memberName));
    }

    private static SubjectFlowSettingsHandler CreateHandler()
        => new(new PassThroughStringLocalizer<SubjectFlowSettingsHandler>());

    private static SubjectFlowSettings CreateValidFlow()
    {
        return new SubjectFlowSettings
        {
            SubjectContentType = "SupportCase",
            CampaignId = "campaign-1",
            Channel = OmnichannelConstants.Channels.Phone,
            InteractionType = ActivityInteractionType.Manual,
        };
    }
}
