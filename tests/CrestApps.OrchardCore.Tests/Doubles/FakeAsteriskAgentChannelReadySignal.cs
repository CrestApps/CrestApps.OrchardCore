using CrestApps.OrchardCore.Asterisk.Services;

namespace CrestApps.OrchardCore.Tests.Doubles;

internal sealed class FakeAsteriskAgentChannelReadySignal : IAsteriskAgentChannelReadySignal
{
    private readonly bool _ready;

    public FakeAsteriskAgentChannelReadySignal(bool ready = true)
    {
        _ready = ready;
    }

    public string LastRegisteredChannelId { get; private set; }

    public string LastSignaledChannelId { get; private set; }

    public IAsteriskAgentChannelReadyRegistration Register(string channelId)
    {
        LastRegisteredChannelId = channelId;

        return new Registration(_ready);
    }

    public void Signal(string channelId)
    {
        LastSignaledChannelId = channelId;
    }

    private sealed class Registration : IAsteriskAgentChannelReadyRegistration
    {
        private readonly bool _ready;

        public Registration(bool ready)
        {
            _ready = ready;
        }

        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return Task.FromResult(_ready);
        }

        public void Dispose()
        {
        }
    }
}
