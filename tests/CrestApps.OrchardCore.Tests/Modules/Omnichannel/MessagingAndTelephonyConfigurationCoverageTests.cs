using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Deployments;
using CrestApps.OrchardCore.Telephony.Core.Models;
using CrestApps.OrchardCore.Telephony.Deployments;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

/// <summary>
/// Proves that every entity the messaging workspace and the phone system store is either carried by a deployment plan
/// or declared, in writing, as runtime state. An entity nobody decided about is indistinguishable from an oversight,
/// and an oversight is found during a cutover rather than during a build.
/// </summary>
public sealed class MessagingAndTelephonyConfigurationCoverageTests
{
    /// <summary>
    /// The entities that are runtime state rather than configuration, and why.
    /// </summary>
    private static readonly Dictionary<string, string> _runtimeState = new(StringComparer.Ordinal)
    {
        [nameof(MessagingConversation)] = "Communication history. One thread per contact address, produced by traffic, carrying its messages, ownership and unread state.",
        [nameof(MessagingBroadcast)] = "Operational record. A one-off send to a recipient list with its progress counters; replaying it in another environment would message the recipients again.",
    };

    /// <summary>
    /// The entities a deployment plan carries, mapped to the recipe step that carries them.
    /// </summary>
    private static readonly Dictionary<string, string> _configuration = new(StringComparer.Ordinal)
    {
        [nameof(MessageTemplate)] = MessagingDeploymentSteps.MessageTemplate,
        [nameof(TelephonyExtension)] = TelephonyDeploymentSteps.Extension,
    };

    [Fact]
    public void EveryStoredEntity_IsEitherExportedAsConfigurationOrDeclaredAsRuntimeState()
    {
        var entities = GetEntities();

        Assert.True(entities.Length >= 4, $"Only {entities.Length} stored entities were discovered, so the reflection that finds them has stopped working.");

        var undeclared = entities
            .Select(entity => entity.Name)
            .Where(name => !_configuration.ContainsKey(name) && !_runtimeState.ContainsKey(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            undeclared.Length == 0,
            "These entities are neither exported in a deployment plan nor declared as runtime state: " + string.Join(", ", undeclared) +
            ". Add a deployment source and recipe step for the entity and add it to the configuration map, or record in the " +
            "runtime state map why it must not travel between environments.");

        Assert.Empty(_configuration.Keys.Intersect(_runtimeState.Keys, StringComparer.Ordinal));
    }

    [Fact]
    public void EveryDeclaredEntity_StillExists()
    {
        var names = GetEntities().Select(entity => entity.Name).ToHashSet(StringComparer.Ordinal);

        Assert.All(_configuration.Keys.Concat(_runtimeState.Keys), name => Assert.Contains(name, names));
    }

    private static Type[] GetEntities()
    {
        return new[] { typeof(MessageTemplate).Assembly, typeof(TelephonyExtension).Assembly }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsClass
                && !type.IsAbstract
                && type.IsPublic
                && typeof(CatalogItem).IsAssignableFrom(type))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();
    }
}
