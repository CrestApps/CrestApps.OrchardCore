using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Defines a factory for creating AI clients, such as chat clients and embedding generators,
/// based on the specified provider, connection, and deployment names.
/// </summary>
public interface IAIClientFactory
{
    /// <summary>
    /// Asynchronously creates an <see cref="IChatClient"/> instance for the given provider, connection, and deployment.
    /// </summary>
    /// <param name="providerName">The name of the AI provider (e.g., "OpenAI", "AzureOpenAI").</param>
    /// <param name="connectionName">The name of the connection configuration to use.</param>
    /// <param name="deploymentName">The name of the deployment or model to use.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation, with the created <see cref="IChatClient"/>.
    /// </returns>
    ValueTask<IChatClient> CreateChatClientAsync(string providerName, string connectionName, string deploymentName);

    /// <summary>
    /// Asynchronously creates an <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> instance for the given provider, connection, and deployment.
    /// </summary>
    /// <param name="providerName">The name of the AI provider (e.g., "OpenAI", "AzureOpenAI").</param>
    /// <param name="connectionName">The name of the connection configuration to use.</param>
    /// <param name="deploymentName">The name of the deployment or model to use.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation, with the created <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/>.
    /// </returns>
    ValueTask<IEmbeddingGenerator<string, Embedding<float>>> CreateEmbeddingGeneratorAsync(string providerName, string connectionName, string deploymentName);

    /// <summary>
    /// Asynchronously creates an <see cref="IImageGenerator"/> instance for the given provider, connection, and deployment.
    /// </summary>
    /// <param name="providerName">The name of the AI provider (e.g., "OpenAI", "AzureOpenAI").</param>
    /// <param name="connectionName">The name of the connection configuration to use.</param>
    /// <param name="deploymentName">The name of the deployment or model to use. If not provided, the default images deployment from the connection will be used.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation, with the created <see cref="IImageGenerator"/>.
    /// </returns>
#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    ValueTask<IImageGenerator> CreateImageGeneratorAsync(string providerName, string connectionName, string deploymentName = null);
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

    /// <summary>
    /// Asynchronously creates an <see cref="ISpeechToTextClient"/> instance for the given provider, connection, and deployment.
    /// </summary>
    /// <param name="providerName">The name of the AI provider (e.g., "OpenAI", "AzureOpenAI").</param>
    /// <param name="connectionName">The name of the connection configuration to use.</param>
    /// <param name="deploymentName">The name of the deployment or model to use. If not provided, the default speech-to-text deployment from the connection will be used.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation, with the created <see cref="ISpeechToTextClient"/>.
    /// </returns>
#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    ValueTask<ISpeechToTextClient> CreateSpeechToTextClientAsync(string providerName, string connectionName, string deploymentName = null);
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

    /// <summary>
    /// Asynchronously creates an <see cref="ISpeechToTextClient"/> instance from a deployment that may use
    /// either a connection reference or contained connection parameters.
    /// </summary>
    /// <param name="deployment">The AI deployment containing provider, connection, and model information.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation, with the created <see cref="ISpeechToTextClient"/>.
    /// </returns>
#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    ValueTask<ISpeechToTextClient> CreateSpeechToTextClientAsync(AIDeployment deployment);
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

    /// <summary>
    /// Asynchronously creates an <see cref="ITextToSpeechClient"/> instance for the given provider, connection, and deployment.
    /// </summary>
    /// <param name="providerName">The name of the AI provider (e.g., "AzureSpeech").</param>
    /// <param name="connectionName">The name of the connection configuration to use.</param>
    /// <param name="deploymentName">The name of the deployment or model to use. If not provided, the default text-to-speech deployment from the connection will be used.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation, with the created <see cref="ITextToSpeechClient"/>.
    /// </returns>
#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    ValueTask<ITextToSpeechClient> CreateTextToSpeechClientAsync(string providerName, string connectionName, string deploymentName = null);
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

    /// <summary>
    /// Asynchronously creates an <see cref="ITextToSpeechClient"/> instance from a deployment that may use
    /// either a connection reference or contained connection parameters.
    /// </summary>
    /// <param name="deployment">The AI deployment containing provider, connection, and model information.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation, with the created <see cref="ITextToSpeechClient"/>.
    /// </returns>
#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    ValueTask<ITextToSpeechClient> CreateTextToSpeechClientAsync(AIDeployment deployment);
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

}
