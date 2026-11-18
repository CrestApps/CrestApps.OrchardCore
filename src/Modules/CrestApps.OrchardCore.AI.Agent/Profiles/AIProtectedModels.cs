using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Agent.Profiles;

/// <summary>
/// The deployments and provider connections the assistant running the current AI turn talks to: the chat and utility
/// deployments its profiles name or fall back to, the voice deployments of its personas, and the connections of all of
/// them. None of it may be changed or deleted from inside the turn, or the assistant could cut itself off mid-task.
/// </summary>
internal sealed class AIProtectedModels
{
    private readonly bool _everything;

    private AIProtectedModels(bool everything)
    {
        _everything = everything;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProtectedModels"/> class that protects nothing yet.
    /// </summary>
    public AIProtectedModels()
    {
    }

    /// <summary>
    /// Gets the models outside an AI turn: nothing is protected.
    /// </summary>
    public static AIProtectedModels None { get; } = new(everything: false);

    /// <summary>
    /// Gets the models used when the running assistant cannot be worked out: everything is protected.
    /// </summary>
    public static AIProtectedModels Everything { get; } = new(everything: true);

    /// <summary>
    /// Gets the technical names and IDs of the protected deployments.
    /// </summary>
    public HashSet<string> DeploymentReferences { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the protected connections, as provider and connection name or ID.
    /// </summary>
    public HashSet<(string Provider, string Connection)> ConnectionReferences { get; } = [];

    /// <summary>
    /// Whether a deployment is protected.
    /// </summary>
    /// <param name="deployment">The deployment.</param>
    /// <returns><see langword="true"/> when it may not be changed or deleted from inside the AI turn.</returns>
    public bool IsProtected(AIDeployment deployment)
        => deployment is not null && (_everything ||
            (!string.IsNullOrEmpty(deployment.Name) && DeploymentReferences.Contains(deployment.Name)) ||
            (!string.IsNullOrEmpty(deployment.ItemId) && DeploymentReferences.Contains(deployment.ItemId)));

    /// <summary>
    /// Whether a provider connection is protected.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <returns><see langword="true"/> when it may not be changed or deleted from inside the AI turn.</returns>
    public bool IsProtected(AIProviderConnection connection)
        => connection is not null && (_everything ||
            ConnectionReferences.Any(reference =>
                string.Equals(reference.Provider, connection.Source, StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(reference.Connection, connection.Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(reference.Connection, connection.ItemId, StringComparison.OrdinalIgnoreCase))));

    /// <summary>
    /// Adds a deployment and its connection to the protected set.
    /// </summary>
    /// <param name="deployment">The deployment.</param>
    internal void Add(AIDeployment deployment)
    {
        if (deployment is null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(deployment.Name))
        {
            DeploymentReferences.Add(deployment.Name);
        }

        if (!string.IsNullOrEmpty(deployment.ItemId))
        {
            DeploymentReferences.Add(deployment.ItemId);
        }

        if (!string.IsNullOrWhiteSpace(deployment.ClientName) && !string.IsNullOrWhiteSpace(deployment.ConnectionName))
        {
            ConnectionReferences.Add((deployment.ClientName, deployment.ConnectionName));
        }
    }
}
