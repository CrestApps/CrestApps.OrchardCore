using System.Text.Json;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Tools;

public sealed class DocumentReaderToolSource : IAIToolSource
{
    public const string ToolSource = "DocumentReader";

    private readonly SessionDocumentRetriever _documentRetriever;

    public DocumentReaderToolSource(SessionDocumentRetriever documentRetriever, IStringLocalizer<DocumentReaderToolSource> S)
    {
        _documentRetriever = documentRetriever;
        DisplayName = S["Document Reader"];
        Description = S["Reads private uploaded documents for the current chat session."];
    }

    public string Name => ToolSource;

    public AIToolSourceType Type => AIToolSourceType.Function;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }

    public Task<AITool> CreateAsync(AIToolInstance instance)
    {
        return Task.FromResult<AITool>(new DocumentReaderFunction(instance, _documentRetriever));
    }

    private sealed class DocumentReaderFunction : AIFunction
    {
        private readonly SessionDocumentRetriever _documentRetriever;
        private readonly string _sessionId;

        public DocumentReaderFunction(AIToolInstance instance, SessionDocumentRetriever documentRetriever)
        {
            _documentRetriever = documentRetriever;

            _sessionId = instance.ItemId;

            Name = instance.ItemId;
            Description = instance.DisplayText;

            JsonSchema = JsonSerializer.Deserialize<JsonElement>(
                """
                {
                  "type": "object",
                  "properties": {
                    "question": { "type": "string" }
                  },
                  "required": ["question"]
                }
                """);
        }

        public override string Name { get; }

        public override string Description { get; }

        public override JsonElement JsonSchema { get; }

        protected override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            var question = arguments.TryGetValue("question", out var q) ? q?.ToString() : null;

            if (string.IsNullOrWhiteSpace(question))
            {
                return ValueTask.FromResult<object>(string.Empty);
            }

            var documents = arguments.TryGetValue("documents", out var d) ? d as CustomChatSessionDocuments : null;

            if (documents == null || documents.Items.Count == 0)
            {
                return ValueTask.FromResult<object>(string.Empty);
            }

            var context = _documentRetriever.Retrieve(documents, question);

            return ValueTask.FromResult<object>(string.Join("\n\n---\n\n", context));
        }
    }
}
