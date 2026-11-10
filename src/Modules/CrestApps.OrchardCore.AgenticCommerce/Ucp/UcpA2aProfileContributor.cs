namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// Offers UCP's shopping service over Agent2Agent (A2A): the profile's <c>a2a</c> service points to the store's agent
/// card.
/// </summary>
public sealed class UcpA2aProfileContributor : IUcpProfileContributor
{
    /// <inheritdoc />
    public Task ContributeAsync(UcpProfileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Services.Add(new UcpService
        {
            Transport = "a2a",
            Endpoint = $"{context.BaseUrl}/{UcpA2a.AgentCardRoute}",
        });

        return Task.CompletedTask;
    }
}
