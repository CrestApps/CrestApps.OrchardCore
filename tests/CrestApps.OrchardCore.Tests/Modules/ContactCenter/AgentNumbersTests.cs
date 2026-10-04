using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// An agent could be given a number to dial from but not one to text from, and nothing could say which numbers an agent
/// uses, so a follow-up text after a call had no way to pick the number it should come from. Agents can now be given a
/// texting number, the tenant picks a default phone and SMS number, and a workflow task finds an agent's numbers.
/// </summary>
public sealed class AgentNumbersTests
{
    [Fact]
    public async Task Resolver_ReturnsTheAgentsOwnNumbers()
    {
        // Arrange
        var line = Address("line", "+15550000001", OmnichannelConstants.Channels.Phone, OmnichannelConstants.Channels.Sms);
        line.Put(new OutboundLineSettings { UserIds = ["agent-1"] });
        line.Put(new MessagingLineSettings { UserIds = ["agent-1"] });
        var main = Address("main", "+15550000009", OmnichannelConstants.Channels.Phone, OmnichannelConstants.Channels.Sms);
        var resolver = CreateResolver([line, main], new ContactCenterDefaultAddressSettings { DefaultPhoneAddressId = "main", DefaultSmsAddressId = "main" });

        // Act
        var addresses = await resolver.ResolveAsync("agent-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(line, addresses.PhoneAddress);
        Assert.Same(line, addresses.SmsAddress);
        Assert.False(addresses.IsDefaultPhone);
        Assert.False(addresses.IsDefaultSms);
    }

    [Fact]
    public async Task Resolver_FallsBackToTheDefaultNumbers()
    {
        // Arrange
        var calls = Address("calls", "+15550000001", OmnichannelConstants.Channels.Phone);
        var texts = Address("texts", "+15550000002", OmnichannelConstants.Channels.Sms);
        var resolver = CreateResolver([calls, texts], new ContactCenterDefaultAddressSettings { DefaultPhoneAddressId = "calls", DefaultSmsAddressId = "texts" });

        // Act
        var addresses = await resolver.ResolveAsync("agent-without-lines", TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(calls, addresses.PhoneAddress);
        Assert.True(addresses.IsDefaultPhone);
        Assert.Same(texts, addresses.SmsAddress);
        Assert.True(addresses.IsDefaultSms);
    }

    [Fact]
    public async Task Resolver_IgnoresADefaultNoLongerUsedForItsChannel_AndFollowsAMergedDefault()
    {
        // Arrange
        var callsOnly = Address("calls", "+15550000001", OmnichannelConstants.Channels.Phone);
        var merged = Address("survivor", "+15550000002", OmnichannelConstants.Channels.Phone);
        merged.MergedItemIds = ["retired"];
        var resolver = CreateResolver([callsOnly, merged], new ContactCenterDefaultAddressSettings { DefaultPhoneAddressId = "retired", DefaultSmsAddressId = "calls" });

        // Act
        var addresses = await resolver.ResolveAsync(null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(merged, addresses.PhoneAddress);
        Assert.Null(addresses.SmsAddress);
    }

    [Fact]
    public async Task Rule_RefusesAnAgentWhoAlreadyTextsFromAnotherNumber()
    {
        // Arrange
        var existing = Address("existing", "+15550000001", OmnichannelConstants.Channels.Sms);
        existing.DisplayText = "Support texts";
        existing.Put(new MessagingLineSettings { UserIds = ["agent-1"] });
        var saved = Address("saved", "+15550000002", OmnichannelConstants.Channels.Sms);
        saved.Put(new MessagingLineSettings { UserIds = ["agent-1"] });
        var context = new ValidatingContext<OmnichannelChannelEndpoint>(saved);

        // Act
        await CreateRule([existing, saved]).ValidateAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var error = Assert.Single(context.Result.Errors);
        Assert.Contains("Support texts", error.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rule_AcceptsTheSameAgentTextingAndDialingFromDifferentNumbers()
    {
        // Arrange
        var calls = Address("calls", "+15550000001", OmnichannelConstants.Channels.Phone);
        calls.Put(new OutboundLineSettings { UserIds = ["agent-1"] });
        var texts = Address("texts", "+15550000002", OmnichannelConstants.Channels.Sms);
        texts.Put(new MessagingLineSettings { UserIds = ["agent-1"] });
        var context = new ValidatingContext<OmnichannelChannelEndpoint>(texts);

        // Act
        await CreateRule([calls, texts]).ValidateAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    [Fact]
    public async Task Task_WritesTheNumbersOfTheUserItIsGiven()
    {
        // Arrange
        var user = new User { UserId = "agent-1", UserName = "mike" };
        var resolver = new Mock<IAgentAddressResolver>();
        resolver
            .Setup(service => service.ResolveAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentAddresses
            {
                PhoneAddress = Address("calls", "+15550000001", OmnichannelConstants.Channels.Phone),
                SmsAddress = Address("texts", "+15550000002", OmnichannelConstants.Channels.Sms),
            });
        var task = CreateTask(resolver.Object, user);
        var workflow = CreateWorkflowContext();

        // Act
        var result = await task.ExecuteAsync(workflow, null!);

        // Assert
        Assert.Contains("Done", result.Outcomes);
        Assert.Equal("+15550000001", workflow.Output[FindAgentNumbersTask.PhoneNumberOutput]);
        Assert.Equal("+15550000002", workflow.Output[FindAgentNumbersTask.SmsNumberOutput]);
    }

    [Fact]
    public async Task Task_GivesAnUnknownUserTheDefaults_AndEndsInNotFoundWithoutAny()
    {
        // Arrange
        var resolver = new Mock<IAgentAddressResolver>();
        resolver.Setup(service => service.ResolveAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(new AgentAddresses());
        var task = CreateTask(resolver.Object, user: null);
        var workflow = CreateWorkflowContext();

        // Act
        var result = await task.ExecuteAsync(workflow, null!);

        // Assert
        Assert.Contains("NotFound", result.Outcomes);
        Assert.Null(workflow.Output[FindAgentNumbersTask.PhoneNumberOutput]);
        resolver.Verify(service => service.ResolveAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static OmnichannelChannelEndpoint Address(string id, string number, params string[] capabilities)
        => new()
        {
            ItemId = id,
            DisplayText = id,
            Value = number,
            AddressType = OmnichannelAddressTypes.PhoneNumber,
            Capabilities = [.. capabilities],
        };

    private static AgentAddressResolver CreateResolver(OmnichannelChannelEndpoint[] addresses, ContactCenterDefaultAddressSettings defaults)
    {
        var manager = new Mock<IOmnichannelChannelEndpointManager>();
        manager.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(addresses);

        var site = new Mock<ISite>();
        site.Setup(value => value.GetOrCreate<ContactCenterDefaultAddressSettings>()).Returns(defaults);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return new AgentAddressResolver(manager.Object, siteService.Object);
    }

    private static MessagingLineEndpointRule CreateRule(OmnichannelChannelEndpoint[] addresses)
    {
        var store = new Mock<IOmnichannelChannelEndpointStore>();
        store.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(addresses);

        var smsChannel = new Mock<IMessagingChannel>();
        smsChannel.SetupGet(channel => channel.Name).Returns(OmnichannelConstants.Channels.Sms);

        var channels = new Mock<IMessagingChannelResolver>();
        channels.Setup(resolver => resolver.GetAll()).Returns([smsChannel.Object]);

        var users = new Mock<IUserStore<IUser>>();

        return new MessagingLineEndpointRule(
            store.Object,
            channels.Object,
            new UserManager<IUser>(users.Object, null, null, null, null, null, null, null, NullLogger<UserManager<IUser>>.Instance),
            Mock.Of<IDisplayNameProvider>(),
            new PassThroughStringLocalizer<MessagingLineEndpointRule>());
    }

    private static FindAgentNumbersTask CreateTask(IAgentAddressResolver resolver, User user)
    {
        var users = new Mock<IUserStore<IUser>>();
        users.Setup(store => store.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        users.Setup(store => store.GetUserIdAsync(It.IsAny<IUser>(), It.IsAny<CancellationToken>())).ReturnsAsync((IUser value, CancellationToken _) => ((User)value).UserId);

        var evaluator = new Mock<IWorkflowExpressionEvaluator>();
        evaluator
            .Setup(service => service.EvaluateAsync(It.IsAny<WorkflowExpression<string>>(), It.IsAny<WorkflowExecutionContext>(), It.IsAny<System.Text.Encodings.Web.TextEncoder>()))
            .ReturnsAsync(user?.UserName ?? "nobody");

        return new FindAgentNumbersTask(
            resolver,
            new UserManager<IUser>(users.Object, null, null, null, null, null, null, null, NullLogger<UserManager<IUser>>.Instance),
            evaluator.Object,
            NullLogger<FindAgentNumbersTask>.Instance,
            new PassThroughStringLocalizer<FindAgentNumbersTask>())
        {
            UserName = "{{ Workflow.Input.UserName }}",
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
