using CrestApps.Core.AI.Security;
using CrestApps.OrchardCore.AI.Chat.Hubs;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

public sealed class AIChatHubSessionStartRateLimitMessageTests
{
    [Fact]
    public void GetSessionStartRateLimitMessage_DoesNotDiscloseLimitCountOrRetryDelay()
    {
        using var hub = new TestAIChatHub();

        var message = hub.BuildSessionStartRateLimitMessage(RateLimitResult.Throttled(retryAfterSeconds: 137, currentCount: 21, maxAllowed: 20));

        Assert.Equal("You've reached the limit for starting new chats. Please wait a few minutes and try again.", message);

        // The throttle numbers let an abuser tune traffic to sit just under the limit, so none of them
        // may reach the caller.
        Assert.DoesNotContain("137", message, StringComparison.Ordinal);
        Assert.DoesNotContain("21", message, StringComparison.Ordinal);
        Assert.DoesNotContain("20", message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSessionStartRateLimitMessage_IsLocalized()
    {
        using var hub = new TestAIChatHub();

        Assert.Contains(
            hub.BuildSessionStartRateLimitMessage(RateLimitResult.Throttled(1, 1, 1)),
            hub.Localizer.RequestedNames);
    }

    private sealed class TestAIChatHub : AIChatHub
    {
        public TestAIChatHub()
            : this(new RecordingStringLocalizer())
        {
        }

        private TestAIChatHub(RecordingStringLocalizer localizer)
            : base(new EmptyServiceProvider(), TimeProvider.System, NullLogger<AIChatHub>.Instance, localizer)
        {
            Localizer = localizer;
        }

        public RecordingStringLocalizer Localizer { get; }

        public string BuildSessionStartRateLimitMessage(RateLimitResult result)
            => GetSessionStartRateLimitMessage(result);
    }

    private sealed class RecordingStringLocalizer : IStringLocalizer<AIChatHub>
    {
        public List<string> RequestedNames { get; } = [];

        public LocalizedString this[string name]
        {
            get
            {
                RequestedNames.Add(name);

                return new LocalizedString(name, name);
            }
        }

        public LocalizedString this[string name, params object[] arguments]
        {
            get
            {
                RequestedNames.Add(name);

                return new LocalizedString(name, string.Format(name, arguments));
            }
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object GetService(Type serviceType)
            => null;
    }
}
