using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Services;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Sms;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// Builds the channel registry the workspace services resolve channels from, around the real SMS channel, so a test
/// drives the workspace exactly as a tenant with the SMS channel enabled does and asserts on the SMS that left.
/// </summary>
internal static class MessagingTestChannels
{
    /// <summary>
    /// A dispatcher that accepts every message.
    /// </summary>
    public static Mock<ISmsDispatcher> AcceptingDispatcher()
    {
        var dispatcher = new Mock<ISmsDispatcher>();

        dispatcher
            .Setup(d => d.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MessageDispatchResult.Success());

        return dispatcher;
    }

    /// <summary>
    /// Creates the SMS channel over the specified dispatcher.
    /// </summary>
    public static SmsMessagingChannel Sms(ISmsDispatcher dispatcher, ISession session = null)
        => new(new Lazy<ISmsDispatcher>(() => dispatcher), session ?? Mock.Of<ISession>(), new PassThroughStringLocalizer<SmsMessagingChannel>());

    /// <summary>
    /// Creates a registry holding only the SMS channel over the specified dispatcher.
    /// </summary>
    public static IMessagingChannelResolver Resolver(ISmsDispatcher dispatcher, ISession session = null)
        => new MessagingChannelResolver([Sms(dispatcher, session)]);

    /// <summary>
    /// Creates a registry holding the specified channels.
    /// </summary>
    public static IMessagingChannelResolver Resolver(params IMessagingChannel[] channels)
        => new MessagingChannelResolver(channels);

    private sealed class PassThroughStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
