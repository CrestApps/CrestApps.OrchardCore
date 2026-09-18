using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.DataSources.FileSources;
using OrchardCore.Modules.Manifest;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources.FileSources;

/// <summary>
/// Checks that the File Sources feature declares everything it cannot run without.
/// </summary>
/// <remarks>
/// <para>
/// Ingestion is document processing: the run service takes an <c>IKnowledgeIngestionService</c>, and that
/// and the rest of the pipeline are registered by the AI Documents feature. Without the dependency the
/// feature enables happily and then throws
/// <c>Unable to resolve service for type 'IKnowledgeIngestionService'</c> the moment anything touches a
/// file source.
/// </para>
/// <para>
/// It went unnoticed because every manual check happened on a tenant that already had an AI Documents
/// feature enabled for another reason, which satisfied the dependency by accident. Asserting on the
/// manifest is what makes the requirement hold however the feature is enabled.
/// </para>
/// </remarks>
public sealed class FileSourceModuleDependencyTests
{
    [Theory]
    [InlineData(AIConstants.Feature.DataSources)]
    [InlineData(ChatInteractionsConstants.Feature.ChatDocuments)]
    public void TheFeature_DeclaresItsDependency(string expected)
    {
        var dependencies = typeof(FileSourceConstants).Assembly
            .GetCustomAttributes(typeof(ModuleAttribute), inherit: false)
            .Cast<ModuleAttribute>()
            .SelectMany(module => module.Dependencies)
            .ToArray();

        Assert.Contains(expected, dependencies);
    }
}
