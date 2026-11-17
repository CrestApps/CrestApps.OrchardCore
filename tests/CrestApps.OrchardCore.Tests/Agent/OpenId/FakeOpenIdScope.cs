namespace CrestApps.OrchardCore.Tests.Agent.OpenId;

/// <summary>
/// An OpenID scope kept in memory for a tool test.
/// </summary>
internal sealed class FakeOpenIdScope
{
    public string Id { get; set; }

    public string Name { get; set; }

    public string DisplayName { get; set; }

    public string Description { get; set; }

    public List<string> Resources { get; set; } = [];
}
