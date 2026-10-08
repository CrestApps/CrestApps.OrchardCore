using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Workflows;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// Orchard's Send SMS task sends from the provider's default sender, so a follow-up could not come from the number the
/// agent texts from. Send Text Message sends from a chosen number, into the customer's conversation.
/// </summary>
public sealed class SendTextMessageTaskTests
{
    [Fact]
    public async Task SendsFromTheChosenNumber_IntoAConversationTheAgentOwns()
    {
        // Arrange
        var conversations = new Mock<IMessagingConversationService>();
        conversations
            .Setup(service => service.SendDirectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessagingSendResult { Succeeded = true, Message = new OmnichannelMessage { Id = "message-1" } });
        var task = CreateTask(conversations.Object, from: "+15550000002", to: "+15551112222", body: "Thanks for your time today.", userName: "mike");
        var workflow = CreateWorkflowContext();

        // Act
        var result = await task.ExecuteAsync(workflow, null!);

        // Assert
        Assert.Contains("Done", result.Outcomes);
        Assert.Equal("message-1", workflow.LastResult);
        conversations.Verify(
            service => service.SendDirectAsync(OmnichannelConstants.Channels.Sms, "+15550000002", "+15551112222", "Thanks for your time today.", "profile-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FailsWithoutSending_WhenTheFromNumberResolvesEmpty()
    {
        // Arrange
        var conversations = new Mock<IMessagingConversationService>();
        var task = CreateTask(conversations.Object, from: "", to: "+15551112222", body: "Hello", userName: null);

        // Act
        var result = await task.ExecuteAsync(CreateWorkflowContext(), null!);

        // Assert
        Assert.Contains("Failed", result.Outcomes);
        conversations.Verify(
            service => service.SendDirectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Fails_WhenTheTextIsRefused()
    {
        // Arrange
        var conversations = new Mock<IMessagingConversationService>();
        conversations
            .Setup(service => service.SendDirectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MessagingSendResult.Failed("The contact has opted out."));
        var task = CreateTask(conversations.Object, from: "+15550000002", to: "+15551112222", body: "Hello", userName: null);

        // Act
        var result = await task.ExecuteAsync(CreateWorkflowContext(), null!);

        // Assert
        Assert.Contains("Failed", result.Outcomes);
    }

    // Each field's expression is its value, so the test reads as the values the task resolved.
    private static SendTextMessageTask CreateTask(IMessagingConversationService conversations, string from, string to, string body, string userName)
    {
        var evaluator = new Mock<IWorkflowExpressionEvaluator>();
        evaluator
            .Setup(service => service.EvaluateAsync(It.IsAny<WorkflowExpression<string>>(), It.IsAny<WorkflowExecutionContext>(), It.IsAny<System.Text.Encodings.Web.TextEncoder>()))
            .Returns((WorkflowExpression<string> expression, WorkflowExecutionContext _, System.Text.Encodings.Web.TextEncoder _) => Task.FromResult(expression.Expression));

        var user = new User { UserId = "agent-1", UserName = "mike" };
        var users = new Mock<IUserStore<IUser>>();
        users.Setup(store => store.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        users.Setup(store => store.GetUserIdAsync(It.IsAny<IUser>(), It.IsAny<CancellationToken>())).ReturnsAsync("agent-1");

        var profiles = new Mock<IAgentProfileManager>();
        profiles.Setup(manager => manager.FindByUserIdAsync("agent-1", It.IsAny<CancellationToken>())).ReturnsAsync(new AgentProfile { ItemId = "profile-1" });

        return new SendTextMessageTask(
            conversations,
            profiles.Object,
            new UserManager<IUser>(users.Object, null, null, null, null, null, null, null, NullLogger<UserManager<IUser>>.Instance),
            evaluator.Object,
            NullLogger<SendTextMessageTask>.Instance,
            new PassThroughStringLocalizer<SendTextMessageTask>())
        {
            From = from,
            To = to,
            Body = body,
            UserName = userName,
        };
    }

    private static WorkflowExecutionContext CreateWorkflowContext()
        => new(
            new WorkflowType(),
            new Workflow(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            [],
            null,
            []);
}
