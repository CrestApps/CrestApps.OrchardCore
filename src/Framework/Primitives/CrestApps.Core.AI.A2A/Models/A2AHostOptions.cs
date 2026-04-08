namespace CrestApps.Core.AI.A2A.Models;

/// <summary>
/// Configuration options for the A2A host authentication and authorization.
/// </summary>
public sealed class A2AHostOptions
{
    /// <summary>
    /// Gets or sets the authentication type to use for the A2A host.
    /// Default is <see cref="A2AHostAuthenticationType.OpenId"/>.
    /// </summary>
    public A2AHostAuthenticationType AuthenticationType { get; set; } = A2AHostAuthenticationType.OpenId;

    /// <summary>
    /// Gets or sets the API key required for authentication when
    /// <see cref="AuthenticationType"/> is set to <see cref="A2AHostAuthenticationType.ApiKey"/>.
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets whether to require the <c>AccessA2AHost</c> permission.
    /// When set to <c>false</c>, any authenticated user can access the A2A host.
    /// Default is <c>true</c>.
    /// </summary>
    /// <remarks>
    /// This setting only applies when <see cref="AuthenticationType"/> is
    /// <see cref="A2AHostAuthenticationType.OpenId"/>.
    /// </remarks>
    public bool RequireAccessPermission { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to expose all agent profiles as skills of a single combined agent card.
    /// When <c>false</c> (default), each agent profile is exposed as its own independent agent card.
    /// When <c>true</c>, a single agent card is created with each agent profile listed as a skill.
    /// </summary>
    public bool ExposeAgentsAsSkill { get; set; }
}
