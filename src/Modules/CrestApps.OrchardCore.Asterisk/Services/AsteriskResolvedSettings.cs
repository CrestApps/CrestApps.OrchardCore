namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed class AsteriskResolvedSettings
{
    public bool IsEnabled { get; set; }

    public string ProviderName { get; set; }

    public string BaseUrl { get; set; }

    public string UserName { get; set; }

    public string Password { get; set; }

    public string ApplicationName { get; set; }

    public string EndpointTemplate { get; set; }

    public string OutboundCallerId { get; set; }

    public int TimeoutSeconds { get; set; }
}
