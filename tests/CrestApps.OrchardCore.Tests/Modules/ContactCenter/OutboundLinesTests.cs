using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Telnyx;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using Moq;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Outbound lines: the tenant's phone numbers, each with the agents who dial out from it.
/// </summary>
/// <remarks>
/// A tenant with several numbers wants one group of agents to call from one number and another group from another,
/// so a customer who calls back reaches the right people. The line is read on the server for every call, so the
/// browser never chooses the number an agent presents.
/// </remarks>
public sealed class OutboundLinesTests
{
    private const string SalesLine = "+17025550101";
    private const string SupportLine = "+17025550202";

    [Fact]
    public async Task Resolver_ReturnsTheNumberWhoseLineTheUserIsOn()
    {
        // Arrange
        var resolver = new ChannelEndpointOutboundLineResolver(Catalog(
            Line("sales", "Sales", SalesLine, "user-1", "user-2"),
            Line("support", "Support", SupportLine, "user-3")));

        // Act
        var line = await resolver.ResolveAsync("user-3", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(line);
        Assert.Equal("support", line.Id);
        Assert.Equal("Support", line.Name);
        Assert.Equal(SupportLine, line.Number);
    }

    [Fact]
    public async Task Resolver_GivesAUserOnNoLineNothing_SoTheProviderDefaultApplies()
    {
        // Arrange
        var resolver = new ChannelEndpointOutboundLineResolver(Catalog(Line("sales", "Sales", SalesLine, "user-1")));

        // Act
        var line = await resolver.ResolveAsync("user-9", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(line);
    }

    [Fact]
    public async Task Resolver_IgnoresAnSmsNumberThatNamesTheUser()
    {
        // Arrange - a texting number cannot place a call, whatever its properties say.
        var sms = Line("sms", "Texting", SalesLine, "user-1");
        sms.Channel = OmnichannelConstants.Channels.Sms;
        var resolver = new ChannelEndpointOutboundLineResolver(Catalog(sms));

        // Act
        var line = await resolver.ResolveAsync("user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(line);
    }

    [Fact]
    public async Task Resolver_WhenTwoLinesNameTheSameUser_AlwaysPicksTheOlderLine()
    {
        // Arrange - saving refuses this, but an import can still produce it, and the answer must not flip per call.
        var newer = Line("newer", "Newer", SupportLine, "user-1");
        newer.CreatedUtc = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc);
        var older = Line("older", "Older", SalesLine, "user-1");
        older.CreatedUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var resolver = new ChannelEndpointOutboundLineResolver(Catalog(newer, older));

        // Act
        var line = await resolver.ResolveAsync("user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("older", line?.Id);
    }

    [Fact]
    public async Task OwnNumbers_IncludeEveryPhoneNumber_SoATransferNeverRingsTheTenantBack()
    {
        // Arrange
        var sms = Line("sms", "Texting", "+17025550303");
        sms.Channel = OmnichannelConstants.Channels.Sms;
        var source = new OutboundLineOwnNumberSource(Catalog(
            Line("sales", "Sales", SalesLine, "user-1"),
            Line("unassigned", "Unassigned", SupportLine),
            sms));

        // Act
        var numbers = await source.GetOwnNumbersAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([SalesLine, SupportLine], numbers.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task TelnyxSoftPhone_PresentsTheAgentsLineOnCallsTheBrowserPlaces()
    {
        // Arrange
        var resolver = new ChannelEndpointOutboundLineResolver(Catalog(Line("sales", "Sales", SalesLine, "user-1")));
        var contributor = CreateTelnyxContributor(resolver);

        // Act
        var config = await contributor.BuildAsync(
            new SoftPhoneRegistrationConfigContext { UserId = "user-1", DisplayName = "Agent One" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SalesLine, config.OutboundCallerId);
    }

    [Fact]
    public async Task TelnyxSoftPhone_PresentsTheTenantCallerIdForAnAgentWithNoLine()
    {
        // Arrange
        var resolver = new ChannelEndpointOutboundLineResolver(Catalog(Line("sales", "Sales", SalesLine, "user-1")));
        var contributor = CreateTelnyxContributor(resolver);

        // Act
        var config = await contributor.BuildAsync(
            new SoftPhoneRegistrationConfigContext { UserId = "user-2", DisplayName = "Agent Two" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("+15550000000", config.OutboundCallerId);
    }

    [Fact]
    public async Task Editor_StoresTheAgentsWhoDialFromTheNumber()
    {
        // Arrange
        var endpoint = Line("sales", "Sales", SalesLine);
        var context = PostedFormUpdateModel.CreateContext(new OutboundLineEndpointViewModel { UserIds = ["user-1", " user-2 ", "user-1", ""] });

        // Act
        await new OutboundLineEndpointDisplayDriver().UpdateAsync(endpoint, context);

        // Assert
        Assert.Equal(["user-1", "user-2"], ChannelEndpointOutboundLineResolver.GetUserIds(endpoint));
    }

    [Fact]
    public void Editor_IsNotShownOnATextingNumber()
    {
        // Arrange
        var sms = Line("sms", "Texting", SalesLine);
        sms.Channel = OmnichannelConstants.Channels.Sms;

        // Act
        var result = new OutboundLineEndpointDisplayDriver().Edit(sms, context: null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Validation_RefusesAnAgentWhoAlreadyDialsFromAnotherNumber()
    {
        // Arrange
        var endpoint = Line("support", "Support", SupportLine, "user-1");
        var handler = CreateHandler(Line("sales", "Sales", SalesLine, "user-1"), endpoint);
        var context = new ValidatingContext<OmnichannelChannelEndpoint>(endpoint);

        // Act
        await handler.ValidateAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(context.Result.Succeeded);
        var error = Assert.Single(context.Result.Errors);
        Assert.Equal($"Agent One already dials out from Sales ({SalesLine}). Remove them from that number's line first.", error.ErrorMessage);
    }

    [Fact]
    public async Task Validation_LetsAnAgentStayOnTheNumberBeingEdited()
    {
        // Arrange - re-saving a line must not count the line's own agents as taken.
        var endpoint = Line("sales", "Sales", SalesLine, "user-1");
        var handler = CreateHandler(endpoint);
        var context = new ValidatingContext<OmnichannelChannelEndpoint>(endpoint);

        // Act
        await handler.ValidateAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    [Fact]
    public async Task Validation_IgnoresATextingNumber()
    {
        // Arrange - only a phone number is a line, so an SMS endpoint's properties are none of this rule's business.
        var sms = Line("sms", "Texting", SupportLine, "user-1");
        sms.Channel = OmnichannelConstants.Channels.Sms;
        var handler = CreateHandler(Line("sales", "Sales", SalesLine, "user-1"), sms);
        var context = new ValidatingContext<OmnichannelChannelEndpoint>(sms);

        // Act
        await handler.ValidateAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    private static OmnichannelChannelEndpoint Line(string id, string name, string number, params string[] userIds)
    {
        var endpoint = new OmnichannelChannelEndpoint
        {
            ItemId = id,
            DisplayText = name,
            Channel = OmnichannelConstants.Channels.Phone,
            Value = number,
            CreatedUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        if (userIds.Length > 0)
        {
            endpoint.Put(new OutboundLineSettings { UserIds = userIds });
        }

        return endpoint;
    }

    private static IOmnichannelChannelEndpointManager Catalog(params OmnichannelChannelEndpoint[] endpoints)
    {
        var manager = new Mock<IOmnichannelChannelEndpointManager>();
        manager
            .Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(endpoints);

        return manager.Object;
    }

    private static OutboundLineEndpointRule CreateHandler(params OmnichannelChannelEndpoint[] endpoints)
    {
        var store = new Mock<IOmnichannelChannelEndpointStore>();
        store
            .Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(endpoints);

        var user = Mock.Of<IUser>();
        var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        userManager.Setup(value => value.FindByIdAsync("user-1")).ReturnsAsync(user);

        var displayNames = new Mock<IDisplayNameProvider>();
        displayNames.Setup(value => value.GetAsync(user, It.IsAny<CancellationToken>())).ReturnsAsync("Agent One");

        return new OutboundLineEndpointRule(
            store.Object,
            userManager.Object,
            displayNames.Object,
            new PassThroughStringLocalizer<OutboundLineEndpointRule>());
    }

    private static TelnyxSoftPhoneRegistrationConfigContributor CreateTelnyxContributor(IOutboundLineResolver resolver)
    {
        var issuer = new Mock<ITelnyxTelephonyCredentialIssuer>();
        issuer
            .Setup(value => value.IssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelnyxTelephonyCredential
            {
                CredentialId = "credential-1",
                SipUsername = "agent-one",
                SipPassword = "secret",
                ExpiresAtUtc = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc),
            });

        var options = new TestOptionsMonitor<TelnyxOptions>(new TelnyxOptions
        {
            IsEnabled = true,
            ApiKey = "KEY",
            ConnectionId = "connection-1",
            SipConnectionId = "sip-connection-1",
            SipWebSocketUrl = TelnyxConstants.DefaultSipWebSocketUrl,
            SipDomain = TelnyxConstants.DefaultSipDomain,
            DefaultOutboundCallerId = "+15550000000",
        });

        return new TelnyxSoftPhoneRegistrationConfigContributor(issuer.Object, resolver, options);
    }
}
