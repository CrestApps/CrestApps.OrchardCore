using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;

#pragma warning disable MEAI001 // Text-to-speech APIs from Microsoft.Extensions.AI are preview and require explicit opt-in at each usage site.
namespace CrestApps.OrchardCore.Tests.Core.Orchestration;

/// <summary>
/// A test orchestrator used for verifying orchestrator resolution.
/// </summary>
internal sealed class TestOrchestrator : IOrchestrator
{
    public string Name => "custom";

    public async IAsyncEnumerable<ChatResponseUpdate> ExecuteStreamingAsync(
        OrchestrationContext context,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new ChatResponseUpdate
        {
            Contents = [new TextContent("test response")],
        };
        await Task.CompletedTask;
    }
}

/// <summary>
/// A no-op completion service for testing.
/// </summary>
internal sealed class NullCompletionService : IAICompletionService
{
    public Task<ChatResponse> CompleteAsync(
        AIDeployment deployment,
        IEnumerable<ChatMessage> messages,
        AICompletionContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ChatResponse([]));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> CompleteStreamingAsync(
        AIDeployment deployment,
        IEnumerable<ChatMessage> messages,
        AICompletionContext context,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }
}

/// <summary>
/// A no-op tool registry for testing.
/// </summary>
internal sealed class NullToolRegistry : IToolRegistry
{
    public Task<IReadOnlyList<ToolRegistryEntry>> GetAllAsync(
        AICompletionContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ToolRegistryEntry>>([]);
    }

    public Task<IReadOnlyList<ToolRegistryEntry>> SearchAsync(
        string query,
        int topK,
        AICompletionContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ToolRegistryEntry>>([]);
    }
}

/// <summary>
/// A no-op AI client factory for testing.
/// </summary>
internal sealed class NullAIClientFactory : IAIClientFactory
{
    public ValueTask<IChatClient> CreateChatClientAsync(string providerName, string connectionName, string deploymentName)
        => new((IChatClient)null);

    public ValueTask<IEmbeddingGenerator<string, Embedding<float>>> CreateEmbeddingGeneratorAsync(string providerName, string connectionName, string deploymentName)
        => new((IEmbeddingGenerator<string, Embedding<float>>)null);

#pragma warning disable MEAI001
    public ValueTask<IImageGenerator> CreateImageGeneratorAsync(string providerName, string connectionName, string deploymentName = null)
        => new((IImageGenerator)null);

    public ValueTask<ISpeechToTextClient> CreateSpeechToTextClientAsync(string providerName, string connectionName, string deploymentName = null)
        => new((ISpeechToTextClient)null);

    public ValueTask<ISpeechToTextClient> CreateSpeechToTextClientAsync(AIDeployment deployment)
        => new((ISpeechToTextClient)null);
#pragma warning restore MEAI001

#pragma warning disable MEAI001
    public ValueTask<ITextToSpeechClient> CreateTextToSpeechClientAsync(string providerName, string connectionName, string deploymentName = null)
        => new((ITextToSpeechClient)null);

    public ValueTask<ITextToSpeechClient> CreateTextToSpeechClientAsync(AIDeployment deployment)
        => new((ITextToSpeechClient)null);
#pragma warning restore MEAI001
}
